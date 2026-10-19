using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum PlayerState
{
    Normal,
    Sitting,
    InspectingMonitor
}

/// <summary>
/// Controller for the player character:
/// - Movement and gravity
/// - First-person and Third-person camera view
/// - Sitting down on chair / standing up (E key)
/// - Inspecting / zooming into monitor terminal (F key)
/// Supports both the new Unity Input System and Legacy Input.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Movement speed while walking in m/s")]
    [SerializeField] private float walkSpeed = 3.8f;

    [Tooltip("Gravity acceleration in m/s^2")]
    [SerializeField] private float gravity = -18.0f;

    [Header("Look / Camera Settings")]
    [Tooltip("Transform that pitches up and down with the mouse (camera parent)")]
    [SerializeField] private Transform cameraHolder;

    [Tooltip("The camera to control")]
    [SerializeField] private Camera playerCamera;

    [Tooltip("Mouse look sensitivity")]
    [SerializeField] private float mouseSensitivity = 0.15f;

    [Tooltip("Minimum vertical look angle (looking down)")]
    [SerializeField] private float minPitch = -85f;

    [Tooltip("Maximum vertical look angle (looking up)")]
    [SerializeField] private float maxPitch = 85f;

    [Header("View Modes")]
    [Tooltip("True for First-Person, False for Third-Person")]
    [SerializeField] private bool isFirstPerson = true;

    [Tooltip("Camera local offset in First-Person mode")]
    [SerializeField] private Vector3 firstPersonCamPos = new Vector3(0f, 0f, 0.15f);

    [Tooltip("Camera local offset in Third-Person mode")]
    [SerializeField] private Vector3 thirdPersonCamPos = new Vector3(0f, 0.35f, -1.8f);

    [Tooltip("Player visual body model to toggle or adjust")]
    [SerializeField] private GameObject visualBody;

    [Header("Workstation & Interactions")]
    [SerializeField] private Vector3 chairPosition = new Vector3(8.30f, 0.02f, 3.95f);
    [SerializeField] private Vector3 chairLookEuler = new Vector3(0f, 346.34f, 0f);
    [SerializeField] private Vector3 monitorPosition = new Vector3(8.304f, 1.286f, 4.765f);
    [SerializeField] private Vector3 monitorCamPosition = new Vector3(8.417f, 1.286f, 4.299f);
    [SerializeField] private Vector3 monitorCamEuler = new Vector3(0f, 346.34f, 0f);

    [SerializeField] private float standingEyeHeight = 1.62f;
    [SerializeField] private float sittingEyeHeight = 1.05f;
    [SerializeField] private float maxInteractionDistance = 3.5f;
    [SerializeField] private MonitorTerminal monitorTerminal;

    [Header("UI Feedback")]
    [SerializeField] private bool showControlsUI = true;

    private CharacterController characterController;
    private Vector3 verticalVelocity;
    private float cameraPitch = 0f;
    private bool cursorLocked = true;
    private PlayerState currentState = PlayerState.Normal;
    private PlayerState stateBeforeMonitor = PlayerState.Normal;
    private bool isTransitioningCamera = false;
    private PotentiometerKnob currentKnob;
    private PotentiometerKnob activeKnob;
    private bool isDraggingKnob = false;

    private CartridgeDraggable currentCartridge;
    private CartridgeDraggable activeCartridge;
    private bool isDraggingCartridge = false;
    private float stunTimer = 0f;

    public static PlayerController Instance { get; private set; }
    public bool IsRunning { get; private set; }

    public PlayerState CurrentState => currentState;
    public bool IsSitting => currentState == PlayerState.Sitting;
    public bool IsInspectingMonitor => currentState == PlayerState.InspectingMonitor;
    public bool IsFirstPerson => isFirstPerson;
    public bool IsDraggingKnob => isDraggingKnob;
    public bool IsDraggingCartridge => isDraggingCartridge;
    public float StunTimer => stunTimer;

    private Vector3 initialSpawnPosition;
    private Quaternion initialSpawnRotation;

    private void Awake()
    {
        Instance = this;
        Application.runInBackground = true;
        characterController = GetComponent<CharacterController>();
        initialSpawnPosition = transform.position;
        initialSpawnRotation = transform.rotation;

        if (cameraHolder == null)
        {
            var ch = transform.Find("CameraHolder");
            if (ch != null)
            {
                cameraHolder = ch;
            }
            else
            {
                var newHolder = new GameObject("CameraHolder");
                newHolder.transform.SetParent(transform, false);
                newHolder.transform.localPosition = new Vector3(0f, standingEyeHeight, 0f);
                cameraHolder = newHolder.transform;
            }
        }

        if (playerCamera == null && cameraHolder != null)
        {
            playerCamera = cameraHolder.GetComponentInChildren<Camera>();
        }

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (playerCamera != null && cameraHolder != null)
        {
            playerCamera.transform.SetParent(cameraHolder, false);
        }

        if (visualBody == null)
        {
            var vis = transform.Find("Visuals");
            if (vis != null) visualBody = vis.gameObject;
        }

        if (monitorTerminal == null)
        {
            monitorTerminal = FindAnyObjectByType<MonitorTerminal>();
        }

        UpdateCameraMode();
    }

    private void Start()
    {
        SetCursorLock(true);
        UpdateCameraMode();

        // Ensure operator chair has blocking collision so player cannot walk through it
        var chairObj = GameObject.Find("Cube.020") ?? GameObject.Find("comnata/Cube.020");
        if (chairObj != null)
        {
            var chairCol = chairObj.GetComponent<Collider>();
            if (chairCol == null)
            {
                chairCol = chairObj.AddComponent<BoxCollider>();
            }
            chairCol.enabled = true;
            chairCol.isTrigger = false;
        }
    }

    private void Update()
    {
        if (GuardPawn.IsGameOver)
        {
            SetCursorLock(false);
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.rKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
            {
                GuardPawn.RestartGame();
            }
#else
            if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                GuardPawn.RestartGame();
            }
#endif
            return;
        }

        if (GameVictorySystem.IsGameWon && GameVictorySystem.IsVictoryScreenOpen)
        {
            SetCursorLock(false);
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                GameVictorySystem.RestartGame();
            }
#else
            if (Input.GetKeyDown(KeyCode.R))
            {
                GameVictorySystem.RestartGame();
            }
#endif
            return;
        }

        if (isTransitioningCamera) return;
        if (MainframeBootQuest.Instance != null && MainframeBootQuest.Instance.IsReadingManual) return;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && kb.f9Key.wasPressedThisFrame) ResetPlayerToStartingPoint();
#else
        if (Input.GetKeyDown(KeyCode.F9)) ResetPlayerToStartingPoint();
#endif

        HandleCursorLock();
        HandleCheatCodeInput();

        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            return;
        }

        if (isDraggingKnob && activeKnob != null)
        {
            HandleKnobDragging();
            if (currentState == PlayerState.Normal) HandleMovement();
            return;
        }

        if (isDraggingCartridge && activeCartridge != null)
        {
            HandleCartridgeDragging();
            if (currentState == PlayerState.Normal) HandleMovement();
            return;
        }

        switch (currentState)
        {
            case PlayerState.Normal:
                HandleMouseLook();
                HandleMovement();
                CheckInteractionPromptsNormal();
                break;

            case PlayerState.Sitting:
                HandleMouseLookSitting();
                CheckInteractionPromptsSitting();
                break;

            case PlayerState.InspectingMonitor:
                CheckExitMonitorPrompt();
                break;
        }
    }

    // ==========================================
    // INTERACTION CHECKS & STATE TRANSITIONS
    // ==========================================

    private WorldInteractable currentInteractable;

    private void CheckWorldRaycast()
    {
        currentInteractable = null;
        currentKnob = null;
        currentCartridge = null;

        if (playerCamera == null) return;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit[] hits = Physics.RaycastAll(ray, maxInteractionDistance);
        if (hits == null || hits.Length == 0) return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        // 1. Check Draggable cartridges and Potentiometer knobs first
        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            var cartridge = hit.collider.GetComponentInParent<CartridgeDraggable>();
            if (cartridge != null && !cartridge.IsCompleted)
            {
                currentCartridge = cartridge;

                if (WasKeyPressed(KeyCode.E))
                {
                    string hint = cartridge.Mode == CartridgeDraggable.SlideMode.PullOut
                        ? LocalizationManager.T("<color=#FFFF66>[ТАКТИЛЬНЫЙ ЗАХВАТ]</color> Зажмите [ЛКМ] и тяните мышь на себя, чтобы вытащить диск!",
                                                 "<color=#FFFF66>[TACTILE GRAB]</color> Hold [LMB] and pull mouse towards you to pull disk pack out!")
                        : LocalizationManager.T("<color=#FFFF66>[ТАКТИЛЬНЫЙ ЗАХВАТ]</color> Зажмите [ЛКМ] и ведите мышь вперед, чтобы задвинуть диск в слот!",
                                                 "<color=#FFFF66>[TACTILE GRAB]</color> Hold [LMB] and push mouse forward to slide disk pack in!");
                    MainframeBootQuest.Instance?.ShowNotification(hint);
                }

                if (IsMouseButtonPressed())
                {
                    // Premature pull shock check ONLY when pulling hot disk while drive is spinning
                    if (cartridge.Mode == CartridgeDraggable.SlideMode.PullOut && cartridge.IsHot && DiskDriveBay.Instance != null &&
                        (DiskDriveBay.Instance.State == DiskDriveBay.DriveBayState.Running || DiskDriveBay.Instance.State == DiskDriveBay.DriveBayState.Braking))
                    {
                        DiskDriveBay.Instance.AttemptPullPrematurely();
                        return;
                    }

                    activeCartridge = cartridge;
                    activeCartridge.StartDrag();
                    isDraggingCartridge = true;
                    return;
                }
                break;
            }

            var knob = hit.collider.GetComponentInParent<PotentiometerKnob>();
            if (knob != null)
            {
                currentKnob = knob;

                float scroll = GetScrollDelta();
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    knob.ApplyScroll(scroll);
                }

                if (IsMouseButtonPressed())
                {
                    activeKnob = knob;
                    activeKnob.StartDrag();
                    isDraggingKnob = true;
                    return;
                }
                break;
            }
        }

        // 2. Check WorldInteractables along ray (allows penetrating thin outer frames)
        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            var interactable = hit.collider.GetComponentInParent<WorldInteractable>();
            if (interactable != null && hit.distance <= interactable.MaxDistance)
            {
                currentInteractable = interactable;
                if (interactable.IsInteractable && (WasKeyPressed(KeyCode.E) || IsMouseButtonPressed()))
                {
                    interactable.Interact();
                    return;
                }
                break;
            }
        }
    }

    private void CheckInteractionPromptsNormal()
    {
        CheckWorldRaycast();

        float distToChair = Vector3.Distance(transform.position, chairPosition);
        float distToMonitor = Vector3.Distance(transform.position, monitorPosition);

        Vector3 toMonitor = monitorPosition - transform.position;
        toMonitor.y = 0f;
        bool isFacingMonitor = toMonitor.sqrMagnitude > 0.01f && Vector3.Dot(transform.forward, toMonitor.normalized) > 0.45f;

        Vector3 toChair = chairPosition - transform.position;
        toChair.y = 0f;
        bool isFacingChair = toChair.sqrMagnitude > 0.01f && Vector3.Dot(transform.forward, toChair.normalized) > 0.35f;

        // Sit in chair (E) if facing chair and not looking at another interactable, knob, or cartridge
        if (currentInteractable == null && currentKnob == null && currentCartridge == null && distToChair < 1.35f && isFacingChair && WasKeyPressed(KeyCode.E))
        {
            SitDown();
            return;
        }

        // Inspect monitor directly while standing (F) only when facing monitor at the desk
        if (distToMonitor < 1.4f && isFacingMonitor && WasKeyPressed(KeyCode.F))
        {
            InspectMonitor();
            return;
        }

        // Adjust monitor brightness while standing near desk (B)
        if (distToMonitor < 1.6f && isFacingMonitor && WasKeyPressed(KeyCode.B))
        {
            if (monitorTerminal != null) monitorTerminal.CycleBrightness();
            else if (MonitorTerminal.Instance != null) MonitorTerminal.Instance.CycleBrightness();
            return;
        }
    }

    private void CheckInteractionPromptsSitting()
    {
        CheckWorldRaycast();

        // Stand up (E or movement) if not hovering over knob, interactable, or cartridge
        if (currentInteractable == null && currentKnob == null && currentCartridge == null && (WasKeyPressed(KeyCode.E) || HasMovementInput()))
        {
            StandUp();
            return;
        }

        // Zoom into monitor while sitting (F)
        if (WasKeyPressed(KeyCode.F))
        {
            InspectMonitor();
            return;
        }

        // Adjust monitor brightness with potentiometer knob while sitting (B)
        if (WasKeyPressed(KeyCode.B))
        {
            if (monitorTerminal != null) monitorTerminal.CycleBrightness();
            else if (MonitorTerminal.Instance != null) MonitorTerminal.Instance.CycleBrightness();
            return;
        }
    }

    private void CheckExitMonitorPrompt()
    {
        if (WasKeyPressed(KeyCode.F) || WasKeyPressed(KeyCode.E))
        {
            if ((VectorCommander.Instance != null && VectorCommander.Instance.IsAnyModalOpen) ||
                (SpyMiniGames.Instance != null && SpyMiniGames.Instance.IsMiniGameActive))
            {
                // Let the modal or mini-game consume without exiting the monitor
                return;
            }

            ExitMonitor();
            return;
        }

        // Check hover over knob while zoomed in with free cursor
        if (playerCamera != null)
        {
            Vector2 mousePos = GetMousePosition();
            Ray ray = playerCamera.ScreenPointToRay(mousePos);
            if (Physics.Raycast(ray, out RaycastHit hit, 2.8f))
            {
                var knob = hit.collider.GetComponentInParent<PotentiometerKnob>();
                if (knob != null)
                {
                    currentKnob = knob;
                    float scroll = GetScrollDelta();
                    if (Mathf.Abs(scroll) > 0.01f) knob.ApplyScroll(scroll);

                    if (IsMouseButtonPressed())
                    {
                        activeKnob = knob;
                        activeKnob.StartDrag();
                        isDraggingKnob = true;
                        return;
                    }
                    return;
                }
            }
        }
        currentKnob = null;
    }

    private void HandleKnobDragging()
    {
        if (activeKnob == null)
        {
            isDraggingKnob = false;
            return;
        }

        if (IsMouseButtonHeld())
        {
            Vector2 delta = GetLookDelta();
            activeKnob.UpdateDrag(delta);

            float scroll = GetScrollDelta();
            if (Mathf.Abs(scroll) > 0.01f)
            {
                activeKnob.ApplyScroll(scroll);
            }
        }
        else
        {
            activeKnob.EndDrag();
            isDraggingKnob = false;
            activeKnob = null;
        }
    }

    public void ApplyStun(float duration)
    {
        stunTimer = Mathf.Max(stunTimer, duration);
        if (isDraggingCartridge && activeCartridge != null)
        {
            activeCartridge.EndDrag();
            isDraggingCartridge = false;
            activeCartridge = null;
        }
        if (isDraggingKnob && activeKnob != null)
        {
            activeKnob.EndDrag();
            isDraggingKnob = false;
            activeKnob = null;
        }
    }

    public void ResetPlayerToStartingPoint()
    {
        if (currentState == PlayerState.InspectingMonitor)
        {
            if (monitorTerminal != null) monitorTerminal.SetZoomedIn(false);
        }
        if (currentState == PlayerState.Sitting)
        {
            StandUp();
        }
        if (characterController != null) characterController.enabled = false;
        transform.position = initialSpawnPosition;
        transform.rotation = initialSpawnRotation;
        if (characterController != null) characterController.enabled = true;
        currentState = PlayerState.Normal;
        MainframeBootQuest.Instance?.ShowNotification(LocalizationManager.T(
            "<color=#00FFAA>[СБРОС]</color> Оператор возвращён в стартовую точку [F9].",
            "<color=#00FFAA>[RESET]</color> Operator returned to spawn position [F9]."
        ));
    }

    private void HandleCartridgeDragging()
    {
        if (activeCartridge == null || activeCartridge.IsCompleted)
        {
            isDraggingCartridge = false;
            activeCartridge = null;
            return;
        }

        if (IsMouseButtonHeld())
        {
            Vector2 delta = GetLookDelta();
            activeCartridge.UpdateDrag(delta);
        }
        else
        {
            activeCartridge.EndDrag();
            isDraggingCartridge = false;
            activeCartridge = null;
        }
    }

    public void SitDown()
    {
        if (currentState != PlayerState.Normal) return;

        currentState = PlayerState.Sitting;
        if (characterController == null) characterController = GetComponent<CharacterController>();
        if (characterController != null) characterController.enabled = false;

        // Position player on chair
        transform.position = chairPosition;
        transform.rotation = Quaternion.Euler(chairLookEuler);
        cameraPitch = 0f;

        if (cameraHolder != null)
        {
            cameraHolder.localPosition = new Vector3(0f, sittingEyeHeight, 0f);
            cameraHolder.localRotation = Quaternion.identity;
        }

        UpdateCameraMode();
    }

    public void StandUp()
    {
        if (currentState != PlayerState.Sitting) return;

        currentState = PlayerState.Normal;

        // Clean, fixed stand offset relative to chair orientation (never dependent on mouse look yaw)
        Vector3 standOffset = Quaternion.Euler(0f, chairLookEuler.y, 0f) * new Vector3(-0.45f, 0f, -0.55f);
        Vector3 targetPos = chairPosition + standOffset;

        if (characterController != null) characterController.enabled = false;
        transform.position = targetPos;
        transform.rotation = Quaternion.Euler(chairLookEuler);
        if (characterController != null) characterController.enabled = true;

        if (cameraHolder != null)
        {
            cameraHolder.localPosition = new Vector3(0f, standingEyeHeight, 0f);
        }

        verticalVelocity = Vector3.zero;
        UpdateCameraMode();
    }

    public void InspectMonitor()
    {
        if (currentState == PlayerState.InspectingMonitor) return;

        bool powerCut = (MainframeBootQuest.Instance != null && MainframeBootQuest.Instance.IsPowerCutByBreaker);
        bool isServerOnline = (monitorTerminal != null && monitorTerminal.IsMainframeOnline && !powerCut);

        if (!isServerOnline)
        {
            if (MainframeBootQuest.Instance != null)
            {
                MainframeBootQuest.Instance.ShowNotification(
                    LocalizationManager.T("<color=#FF6666>[ОТКАЗ]</color> Компьютер обесточен! Сначала запустите сервер (холодный пуск ЭВМ).",
                                           "<color=#FF6666>[HALT]</color> Computer unpowered! Start server first (cold boot sequence)."));
            }
            return;
        }

        // Require disk pack to be mounted
        if (DiskDriveBay.Instance != null && DiskDriveBay.Instance.State == DiskDriveBay.DriveBayState.CartridgeExtracted)
        {
            if (MainframeBootQuest.Instance != null)
            {
                MainframeBootQuest.Instance.ShowNotification(
                    LocalizationManager.T("<color=#FF6666>[ОШИБКА I/O]</color> Системный пакет дисков извлечен! Установите накопитель в слот сервера.",
                                           "<color=#FF6666>[I/O FAULT]</color> System disk pack extracted! Insert cartridge into server slot."));
            }
            return;
        }

        stateBeforeMonitor = currentState;
        currentState = PlayerState.InspectingMonitor;

        StartCoroutine(TransitionCameraToMonitor(true));
    }

    public void ExitMonitor()
    {
        if (currentState != PlayerState.InspectingMonitor) return;

        StartCoroutine(TransitionCameraToMonitor(false));
    }

    private IEnumerator TransitionCameraToMonitor(bool zoomIn)
    {
        isTransitioningCamera = true;

        if (zoomIn)
        {
            // Detach camera from holder for world-space interpolation
            Vector3 startPos = playerCamera.transform.position;
            Quaternion startRot = playerCamera.transform.rotation;
            playerCamera.transform.SetParent(null, true);

            float duration = 0.45f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                playerCamera.transform.position = Vector3.Lerp(startPos, monitorCamPosition, t);
                playerCamera.transform.rotation = Quaternion.Slerp(startRot, Quaternion.Euler(monitorCamEuler), t);
                yield return null;
            }

            playerCamera.transform.position = monitorCamPosition;
            playerCamera.transform.rotation = Quaternion.Euler(monitorCamEuler);

            SetCursorLock(false);

            if (monitorTerminal != null)
            {
                monitorTerminal.SetZoomedIn(true);
            }
        }
        else
        {
            if (monitorTerminal != null)
            {
                monitorTerminal.SetZoomedIn(false);
            }

            SetCursorLock(true);

            Vector3 startPos = playerCamera.transform.position;
            Quaternion startRot = playerCamera.transform.rotation;

            Vector3 targetLocalPos = firstPersonCamPos;
            Quaternion targetLocalRot = Quaternion.identity;

            Vector3 endPos = cameraHolder.TransformPoint(targetLocalPos);
            Quaternion endRot = cameraHolder.rotation * targetLocalRot;

            float duration = 0.45f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                playerCamera.transform.position = Vector3.Lerp(startPos, endPos, t);
                playerCamera.transform.rotation = Quaternion.Slerp(startRot, endRot, t);
                yield return null;
            }

            playerCamera.transform.SetParent(cameraHolder, false);
            playerCamera.transform.localPosition = targetLocalPos;
            playerCamera.transform.localRotation = targetLocalRot;

            currentState = stateBeforeMonitor;
        }

        isTransitioningCamera = false;
    }

    // ==========================================
    // LOOK & MOVEMENT
    // ==========================================

    private void HandleMouseLook()
    {
        if (!cursorLocked) return;

        Vector2 lookDelta = GetLookDelta();

        transform.Rotate(Vector3.up, lookDelta.x * mouseSensitivity);

        cameraPitch -= lookDelta.y * mouseSensitivity;
        cameraPitch = Mathf.Clamp(cameraPitch, minPitch, maxPitch);

        if (cameraHolder != null)
        {
            cameraHolder.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
        }
    }

    private void HandleMouseLookSitting()
    {
        if (!cursorLocked) return;

        Vector2 lookDelta = GetLookDelta();

        // Horizontal rotation clamped when sitting (+/- 75 degrees from desk)
        transform.Rotate(Vector3.up, lookDelta.x * mouseSensitivity);

        // Clamp sitting body rotation relative to chair
        Vector3 curEuler = transform.eulerAngles;
        float angleDiff = Mathf.DeltaAngle(chairLookEuler.y, curEuler.y);
        if (Mathf.Abs(angleDiff) > 75f)
        {
            curEuler.y = chairLookEuler.y + Mathf.Sign(angleDiff) * 75f;
            transform.eulerAngles = curEuler;
        }

        cameraPitch -= lookDelta.y * mouseSensitivity;
        cameraPitch = Mathf.Clamp(cameraPitch, -50f, 50f);

        if (cameraHolder != null)
        {
            cameraHolder.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
        }
    }

    private Vector2 GetLookDelta()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null) return mouse.delta.ReadValue();
        return Vector2.zero;
#else
        return new Vector2(Input.GetAxis("Mouse X") * 10f, Input.GetAxis("Mouse Y") * 10f);
#endif
    }

    private void HandleMovement()
    {
        Vector2 input = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
        }
#else
        input.x = Input.GetAxisRaw("Horizontal");
        input.y = Input.GetAxisRaw("Vertical");
#endif

        if (characterController.isGrounded)
        {
            if (verticalVelocity.y < 0f)
            {
                verticalVelocity.y = -2f;
            }
        }

        Vector3 moveDir = (transform.right * input.x + transform.forward * input.y);
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        bool isShift = false;
#if ENABLE_INPUT_SYSTEM
        var kbCurrent = Keyboard.current;
        if (kbCurrent != null) isShift = kbCurrent.leftShiftKey.isPressed || kbCurrent.rightShiftKey.isPressed;
#else
        isShift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
        IsRunning = isShift && moveDir.sqrMagnitude > 0.01f;
        float currentSpeed = walkSpeed * (IsRunning ? 1.55f : 1.0f);

        Vector3 planarMove = moveDir * currentSpeed;

        verticalVelocity.y += gravity * Time.deltaTime;

        Vector3 motion = (planarMove + verticalVelocity) * Time.deltaTime;
        characterController.Move(motion);
    }

    private bool HasMovementInput()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            return kb.wKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame ||
                   kb.sKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame;
        }
        return false;
#else
        return Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) ||
               Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D);
#endif
    }

    private bool WasKeyPressed(KeyCode code)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;
        switch (code)
        {
            case KeyCode.E: return kb.eKey.wasPressedThisFrame;
            case KeyCode.F: return kb.fKey.wasPressedThisFrame;
            case KeyCode.Space: return kb.spaceKey.wasPressedThisFrame;
            case KeyCode.Escape: return kb.escapeKey.wasPressedThisFrame;
            case KeyCode.Tab: return kb.tabKey.wasPressedThisFrame;
            case KeyCode.B: return kb.bKey.wasPressedThisFrame;
            default: return false;
        }
#else
        return Input.GetKeyDown(code);
#endif
    }

    // ==========================================
    // CAMERA & CURSOR
    // ==========================================

    private void HandleCursorLock()
    {
        if (currentState == PlayerState.InspectingMonitor) return;
        if (isDraggingKnob) return;

        bool tabPressed = WasKeyPressed(KeyCode.Tab);
        bool clickPressed = false;

#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) clickPressed = true;
#else
        if (Input.GetMouseButtonDown(0)) clickPressed = true;
#endif

        if (tabPressed)
        {
            SetCursorLock(!cursorLocked);
        }
        else if (clickPressed && !cursorLocked)
        {
            SetCursorLock(true);
        }
    }

    private bool IsMouseButtonPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        return mouse != null && mouse.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    private bool IsMouseButtonHeld()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        return mouse != null && mouse.leftButton.isPressed;
#else
        return Input.GetMouseButton(0);
#endif
    }

    private float GetScrollDelta()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null) return mouse.scroll.ReadValue().y;
        return 0f;
#else
        return Input.mouseScrollDelta.y;
#endif
    }

    private Vector2 GetMousePosition()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null) return mouse.position.ReadValue();
        return Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    public bool IsCursorLocked => cursorLocked;
    public bool IsCursorUnlocked => !cursorLocked;

    public void SetCursorLock(bool locked)
    {
        cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void UpdateCameraMode()
    {
        if (playerCamera == null || cameraHolder == null) return;

        if (playerCamera.transform.parent != cameraHolder)
        {
            playerCamera.transform.SetParent(cameraHolder, false);
        }

        isFirstPerson = true;
        playerCamera.transform.localPosition = firstPersonCamPos;
        playerCamera.transform.localRotation = Quaternion.identity;

        if (visualBody != null)
        {
            visualBody.SetActive(false);
        }
    }

    // ==========================================
    // HUD / INTERACTION GUI
    // ==========================================

    private void OnGUI()
    {
        if (!showControlsUI) return;

        if (stunTimer > 0f)
        {
            float alpha = Mathf.Clamp01(stunTimer / 3.5f) * 0.85f;
            Color oldColor = GUI.color;
            GUI.color = new Color(0.7f, 0.9f, 1.0f, alpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = oldColor;

            var shockStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
            shockStyle.normal.textColor = Color.red;
            GUI.Box(new Rect(Screen.width / 2 - 250, Screen.height / 2 - 45, 500, 90),
                $"⚡ <b><color=#FF2222>[ {LocalizationManager.T("УДАР ТОКОМ 380 ВОЛЬТ", "380 VOLT ELECTRIC SHOCK")} ]</color></b> ⚡\n" +
                $"<size=12><color=#FFFFFF>{LocalizationManager.T("Срыв замка под напряжением! Шок и контузия...", "Lock breached under high voltage! Shock and concussion...")}</color></size>\n" +
                $"<size=11><color=#FFFF66>{LocalizationManager.T($"Приход в сознание через: {stunTimer:F1} сек.", $"Regaining consciousness in: {stunTimer:F1} sec.")}</color></size>", shockStyle);
            return;
        }

        GUI.color = Color.white;

        // Draw stealth status indicators (Lamp & Eye) before and during guard raids
        StealthIndicatorUI.DrawHUD();

        // Top-left status box (only visible in room mode, hidden when zoomed into monitor terminal)
        if (currentState != PlayerState.InspectingMonitor)
        {
            GUILayout.BeginArea(new Rect(15, 15, 430, 168));
            var boxStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 12,
                padding = new RectOffset(10, 10, 8, 8)
            };
            boxStyle.normal.textColor = Color.white;

            string stateTitle = LocalizationManager.T("Состояние:", "State:");
            string stateName = currentState switch
            {
                PlayerState.Normal => LocalizationManager.T("Идёт (Стоит)", "Walking / Standing"),
                PlayerState.Sitting => LocalizationManager.T("Сидит в кресле", "Sitting in chair"),
                _ => LocalizationManager.T("Обычный", "Normal")
            };

            // Acoustic stealth status
            string noiseText = LocalizationManager.T("0 dB [ОБЕСТОЧЕНО]", "0 dB [UNPOWERED]");
            string noiseColor = "#888888";
            if (MainframeBootQuest.Instance != null)
            {
                if (MainframeBootQuest.Instance.IsNoiseActive)
                {
                    noiseText = LocalizationManager.T("72 dB [ГУЛ 50Hz // СЛЫШНО 5.5м]", "72 dB [50Hz HUM // AUDIBLE 5.5m]");
                    noiseColor = "#FF5555";
                }
                else if (MainframeBootQuest.Instance.CurrentStage == BootStage.HotStandby)
                {
                    noiseText = LocalizationManager.T("0 dB [ГОТОВНОСТЬ // БЕСШУМНО]", "0 dB [STANDBY // SILENT]");
                    noiseColor = "#00FFAA";
                }
            }

            // Optical stealth status
            string lightText = LocalizationManager.T("0% [ТЕМНОТА]", "0% [DARK]");
            string lightColor = "#00FFAA";
            var term = monitorTerminal != null ? monitorTerminal : MonitorTerminal.Instance;
            if (term != null && term.IsMainframeOnline && !term.IsStandby)
            {
                if (term.BrightnessLevel > 0.6f)
                {
                    lightText = LocalizationManager.T("100% [ДАЛЬНИЙ ЛУЧ // 22 МЕТРА]", "100% [BEAM // 22 METERS]");
                    lightColor = "#FFCC00";
                }
                else if (term.BrightnessLevel > 0.05f)
                {
                    lightText = LocalizationManager.T("35% [ПРИГЛУШЕН]", "35% [DIMMED]");
                    lightColor = "#FFFF66";
                }
                else
                {
                    lightText = LocalizationManager.T("0% [МАСКИРОВКА // ТЕМНО]", "0% [STEALTH // DARK]");
                    lightColor = "#00FFAA";
                }
            }

            string carriedText = "";
            if (DiskDriveBay.Instance != null && DiskDriveBay.Instance.HasCarriedCartridge)
            {
                carriedText = LocalizationManager.T(
                    $"• <b>В РУКАХ:</b> <color=#00FFAA>Пакет дисков №{DiskDriveBay.Instance.CarriedCartridgeId} [ХОЛОДНЫЙ]</color>\n",
                    $"• <b>HELD:</b> <color=#00FFAA>Disk Pack #{DiskDriveBay.Instance.CarriedCartridgeId} [COOLED]</color>\n"
                );
            }

            string noiseLabel = LocalizationManager.T("ШУМ ЭВМ:", "MAINFRAME NOISE:");
            string lightLabel = LocalizationManager.T("СВЕТ ЭЛТ:", "CRT LIGHT:");
            string moveHelp = LocalizationManager.T("• <b>W, A, S, D</b> — Передвижение / Встать", "• <b>W, A, S, D</b> — Move / Stand up");
            string watchHelp = LocalizationManager.T("• <b>T</b> — Карманные часы (таймер / радар)", "• <b>T</b> — Pocket watch (timer / radar)");
            string brightHelp = LocalizationManager.T("• <b>B</b> — Яркость экрана (100% / 35% / 0%)", "• <b>B</b> — Screen brightness (100% / 35% / 0%)");
            string tabHelp = LocalizationManager.T("• <b>Tab</b> — Освободить / Захватить мышь", "• <b>Tab</b> — Toggle mouse cursor");
            string pauseHelp = LocalizationManager.T("• <b>Esc</b> — Меню паузы", "• <b>Esc</b> — Pause menu");

            string statusText = $"<b>{stateTitle}</b> <color=#00FFCC>{stateName}</color>\n" +
                                carriedText +
                                $"• <b>{noiseLabel}</b> <color={noiseColor}>{noiseText}</color>\n" +
                                $"• <b>{lightLabel}</b> <color={lightColor}>{lightText}</color>\n" +
                                moveHelp + "\n" +
                                watchHelp + "\n" +
                                brightHelp + "\n" +
                                tabHelp + "\n" +
                                pauseHelp;

            GUILayout.Box(statusText, boxStyle);
            GUILayout.EndArea();
        }

        // Center Interaction Prompts
        DrawCenterPrompts();
    }

    private void DrawCenterPrompts()
    {
        float screenW = Screen.width;
        float screenH = Screen.height;

        // 1. Tactile Cartridge Sliding Gauge while Dragging Cartridge
        if (isDraggingCartridge && activeCartridge != null)
        {
            var gaugeBoxStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            gaugeBoxStyle.normal.textColor = Color.white;

            float p = activeCartridge.Progress;
            int pct = Mathf.RoundToInt(p * 100f);
            int filled = Mathf.RoundToInt(p * 16f);
            string bar = new string('█', filled) + new string('░', 16 - filled);

            string actionTitle = activeCartridge.Mode == CartridgeDraggable.SlideMode.PullOut 
                ? LocalizationManager.T("ИЗВЛЕЧЕНИЕ ПАКЕТА ДИСКОВ (ТЯНИТЕ НА СЕБЯ)", "EXTRACTING DISK PACK (PULL TOWARDS YOU)") 
                : LocalizationManager.T("УСТАНОВКА ПАКЕТА ДИСКОВ (ТОЛКАЙТЕ ВПЕРЕД)", "INSERTING DISK PACK (PUSH FORWARD)");
            string hint = activeCartridge.Mode == CartridgeDraggable.SlideMode.PullOut
                ? LocalizationManager.T("Двигайте мышь НАЗАД / ВНИЗ • Зажмите ЛКМ", "Move mouse BACK / DOWN • Hold LMB")
                : LocalizationManager.T("Двигайте мышь ВПЕРЕД / ВВЕРХ • Зажмите ЛКМ", "Move mouse FORWARD / UP • Hold LMB");

            string slotLabel = LocalizationManager.T("В СЛОТЕ", "IN BAY");
            string extLabel = LocalizationManager.T("ИЗВЛЕЧЁН", "EXTRACTED");
            string gaugeText = $"<b><color=#00FFAA>[ {actionTitle} ]</color></b>\n" +
                               $"{slotLabel} [<color=#00FF88>{bar}</color>] {extLabel}  <b><color=#FFFF33>{pct}%</color></b>\n" +
                               $"<size=11><color=#88DDCC>{hint}</color></size>";

            GUI.Box(new Rect(screenW / 2 - 250, screenH - 120, 500, 72), gaugeText, gaugeBoxStyle);
            return;
        }

        // 2. Tactile Rotary Gauge while Dragging Knob
        if (isDraggingKnob && activeKnob != null)
        {
            var gaugeBoxStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            gaugeBoxStyle.normal.textColor = Color.white;

            int pct = Mathf.RoundToInt(activeKnob.CurrentValue * 100f);
            int filled = Mathf.RoundToInt(activeKnob.CurrentValue * 16f);
            string bar = new string('█', filled) + new string('░', 16 - filled);

            string knobTitle = LocalizationManager.T("РЕГУЛЯТОР ЯРКОСТИ ЭЛТ-МОНИТОРА", "CRT MONITOR BRIGHTNESS REGULATOR");
            string darkLabel = LocalizationManager.T("ТЕМНОТА", "DARK");
            string brightLabel = LocalizationManager.T("ЯРКО", "BRIGHT");
            string knobHint = LocalizationManager.T("Двигайте мышь влево / вправо или колёсико • Отпустите ЛКМ для фиксации", "Move mouse left / right or scroll wheel • Release LMB to set");

            string gaugeText = $"<b><color=#00FFAA>[ {knobTitle} ]</color></b>\n" +
                               $"◄ 0% ({darkLabel})  [<color=#00FF88>{bar}</color>]  100% ({brightLabel}) ►  <b><color=#FFFF33>{pct}%</color></b>\n" +
                               $"<size=11><color=#88DDCC>{knobHint}</color></size>";

            GUI.Box(new Rect(screenW / 2 - 250, screenH - 120, 500, 72), gaugeText, gaugeBoxStyle);
            return;
        }

        // Draw center crosshair dot
        if (isFirstPerson && currentState == PlayerState.Normal)
        {
            GUI.color = (currentInteractable != null || currentKnob != null || currentCartridge != null) ? Color.green : new Color(1f, 1f, 1f, 0.45f);
            GUI.DrawTexture(new Rect(screenW / 2 - 2, screenH / 2 - 2, 4, 4), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        var promptStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
            fontStyle = FontStyle.Bold
        };
        promptStyle.normal.textColor = Color.cyan;

        // 3. Cartridge in crosshair
        if (currentCartridge != null && !currentCartridge.IsCompleted)
        {
            promptStyle.normal.textColor = Color.yellow;
            string action = currentCartridge.Mode == CartridgeDraggable.SlideMode.PullOut
                ? LocalizationManager.T("Зажмите [ЛКМ] и тяните мышь на себя — Вытянуть пакет дисков", "Hold [LMB] & pull mouse back — Extract disk pack")
                : LocalizationManager.T("Зажмите [ЛКМ] и толкайте вперед — Задвинуть пакет дисков в слот", "Hold [LMB] & push forward — Insert disk pack into bay");
            GUI.Box(new Rect(screenW / 2 - 275, screenH - 100, 550, 36), action, promptStyle);
            return;
        }

        // 4. Potentiometer Knob in crosshair
        if (currentKnob != null)
        {
            promptStyle.normal.textColor = Color.yellow;
            GUI.Box(new Rect(screenW / 2 - 250, screenH - 100, 500, 36), LocalizationManager.T("Зажмите [ЛКМ] и двигайте мышь (или Колёсико) — Яркость", "Hold [LMB] & move mouse (or Wheel) — Brightness"), promptStyle);
            return;
        }

        bool powerCut = (MainframeBootQuest.Instance != null && MainframeBootQuest.Instance.IsPowerCutByBreaker);
        bool isServerOnline = (monitorTerminal != null && monitorTerminal.IsMainframeOnline && !powerCut);
        bool isDiskExtracted = (DiskDriveBay.Instance != null && DiskDriveBay.Instance.State == DiskDriveBay.DriveBayState.CartridgeExtracted);

        if (currentState == PlayerState.Normal)
        {
            // WorldInteractable in crosshair
            if (currentInteractable != null)
            {
                promptStyle.normal.textColor = currentInteractable.IsInteractable ? Color.yellow : new Color(0.85f, 0.85f, 0.85f, 0.9f);
                GUI.Box(new Rect(screenW / 2 - 250, screenH - 100, 500, 36), currentInteractable.PromptText, promptStyle);
                return;
            }

            Vector3 toChair = chairPosition - transform.position;
            toChair.y = 0f;
            bool isFacingChair = toChair.sqrMagnitude > 0.01f && Vector3.Dot(transform.forward, toChair.normalized) > 0.35f;

            Vector3 toMonitor = monitorPosition - transform.position;
            toMonitor.y = 0f;
            bool isFacingMonitor = toMonitor.sqrMagnitude > 0.01f && Vector3.Dot(transform.forward, toMonitor.normalized) > 0.45f;

            float distToChair = Vector3.Distance(transform.position, chairPosition);
            float distToMonitor = Vector3.Distance(transform.position, monitorPosition);

            if (distToChair < 1.35f && isFacingChair)
            {
                GUI.Box(new Rect(screenW / 2 - 160, screenH - 100, 320, 36), LocalizationManager.T("Нажмите [E] — Сесть в кресло", "Press [E] — Sit in chair"), promptStyle);
            }
            else if (distToMonitor < 1.4f && isFacingMonitor)
            {
                if (isDiskExtracted)
                {
                    promptStyle.normal.textColor = new Color(0.9f, 0.45f, 0.45f, 0.95f);
                    GUI.Box(new Rect(screenW / 2 - 250, screenH - 100, 500, 36), LocalizationManager.T("[ Диск извлечен — установите накопитель в сервер ]", "[ Disk extracted — insert disk pack into bay ]"), promptStyle);
                }
                else if (isServerOnline)
                {
                    promptStyle.normal.textColor = Color.cyan;
                    GUI.Box(new Rect(screenW / 2 - 160, screenH - 100, 320, 36), LocalizationManager.T("Нажмите [F] — Терминал", "Press [F] — Terminal"), promptStyle);
                }
                else
                {
                    promptStyle.normal.textColor = new Color(0.9f, 0.45f, 0.45f, 0.95f);
                    GUI.Box(new Rect(screenW / 2 - 220, screenH - 100, 440, 36), LocalizationManager.T("[ Компьютер обесточен — запустите сервер ]", "[ Computer unpowered — start server ]"), promptStyle);
                }
            }
        }
        else if (currentState == PlayerState.Sitting)
        {
            if (currentInteractable != null)
            {
                promptStyle.normal.textColor = Color.yellow;
                GUI.Box(new Rect(screenW / 2 - 200, screenH - 135, 400, 32), currentInteractable.PromptText, promptStyle);
            }
            else
            {
                if (isDiskExtracted)
                {
                    promptStyle.normal.textColor = new Color(0.9f, 0.45f, 0.45f, 0.95f);
                    GUI.Box(new Rect(screenW / 2 - 250, screenH - 135, 500, 32), LocalizationManager.T("[ Диск извлечен — установите накопитель в сервер ]", "[ Disk extracted — insert disk pack into bay ]"), promptStyle);
                }
                else if (isServerOnline)
                {
                    promptStyle.normal.textColor = Color.cyan;
                    GUI.Box(new Rect(screenW / 2 - 180, screenH - 135, 360, 32), LocalizationManager.T("Нажмите [F] — Терминал", "Press [F] — Terminal"), promptStyle);
                }
                else
                {
                    promptStyle.normal.textColor = new Color(0.9f, 0.45f, 0.45f, 0.95f);
                    GUI.Box(new Rect(screenW / 2 - 200, screenH - 135, 400, 32), LocalizationManager.T("[ Монитор обесточен — запустите сервер ]", "[ Monitor unpowered — start server ]"), promptStyle);
                }
            }

            promptStyle.normal.textColor = new Color(0.4f, 1.0f, 0.8f);
            GUI.Box(new Rect(screenW / 2 - 220, screenH - 95, 440, 32), LocalizationManager.T("Нажмите [B] — Быстрая смена яркости (маскировка)", "Press [B] — Quick brightness toggle (stealth)"), promptStyle);

            promptStyle.normal.textColor = Color.yellow;
            GUI.Box(new Rect(screenW / 2 - 180, screenH - 55, 360, 32), LocalizationManager.T("Нажмите [E] / [W,A,S,D] — Встать", "Press [E] / [W,A,S,D] — Stand up"), promptStyle);
        }
        else if (currentState == PlayerState.InspectingMonitor)
        {
            if (currentKnob != null)
            {
                promptStyle.normal.textColor = Color.yellow;
                GUI.Box(new Rect(screenW / 2 - 260, screenH - 105, 520, 34), LocalizationManager.T("Зажмите [ЛКМ] и двигайте мышь — Крутить ручку яркости", "Hold [LMB] and move mouse — Turn brightness knob"), promptStyle);
            }

            bool isCommanderActive = VectorCommander.Instance != null && VectorCommander.Instance.gameObject.activeInHierarchy;
            bool isMiniGameActive = SpyMiniGames.Instance != null && SpyMiniGames.Instance.IsMiniGameActive;
            if (!isCommanderActive && !isMiniGameActive)
            {
                promptStyle.normal.textColor = Color.yellow;
                GUI.Box(new Rect(screenW / 2 - 200, screenH - 60, 400, 36), LocalizationManager.T("Нажмите [F] / [E] — Отойти | [B] — Яркость", "Press [F] / [E] — Step back | [B] — Brightness"), promptStyle);
            }
        }
    }

    private int cheatKeyStep = 0;

    private void HandleCheatCodeInput()
    {
        // Secret developer cheat sequence: G -> R -> E -> E -> H
        bool pressedG = false;
        bool pressedR = false;
        bool pressedE = false;
        bool pressedH = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            pressedG = kb.gKey.wasPressedThisFrame;
            pressedR = kb.rKey.wasPressedThisFrame;
            pressedE = kb.eKey.wasPressedThisFrame;
            pressedH = kb.hKey.wasPressedThisFrame;
        }
#else
        pressedG = Input.GetKeyDown(KeyCode.G);
        pressedR = Input.GetKeyDown(KeyCode.R);
        pressedE = Input.GetKeyDown(KeyCode.E);
        pressedH = Input.GetKeyDown(KeyCode.H);
#endif

        if (pressedG || pressedR || pressedE || pressedH)
        {
            switch (cheatKeyStep)
            {
                case 0:
                    if (pressedG) cheatKeyStep = 1;
                    break;
                case 1:
                    if (pressedR) cheatKeyStep = 2;
                    else cheatKeyStep = pressedG ? 1 : 0;
                    break;
                case 2:
                    if (pressedE) cheatKeyStep = 3;
                    else cheatKeyStep = pressedG ? 1 : 0;
                    break;
                case 3:
                    if (pressedE) cheatKeyStep = 4;
                    else cheatKeyStep = pressedG ? 1 : 0;
                    break;
                case 4:
                    if (pressedH)
                    {
                        cheatKeyStep = 0;
                        GuardPawn.ToggleCheatPeaceMode();
                    }
                    else cheatKeyStep = pressedG ? 1 : 0;
                    break;
            }
        }

        if (GuardPawn.IsCheatPeaceMode)
        {
#if ENABLE_INPUT_SYSTEM
            var kbCurrent = Keyboard.current;
            if (kbCurrent != null)
            {
                if (kbCurrent.digit1Key.wasPressedThisFrame || kbCurrent.numpad1Key.wasPressedThisFrame) GuardPawn.TriggerCheatBreach();
                if (kbCurrent.digit2Key.wasPressedThisFrame || kbCurrent.numpad2Key.wasPressedThisFrame) GuardPawn.TriggerCheatDoorwayInspect();
                if (kbCurrent.digit3Key.wasPressedThisFrame || kbCurrent.numpad3Key.wasPressedThisFrame) GuardPawn.TriggerCheatStealthSuccess();
                if (kbCurrent.digit4Key.wasPressedThisFrame || kbCurrent.numpad4Key.wasPressedThisFrame) GuardPawn.TriggerCheatAlertChase();
                if (kbCurrent.digit5Key.wasPressedThisFrame || kbCurrent.numpad5Key.wasPressedThisFrame) GuardPawn.TriggerTimeExpiredAssault();
                if (kbCurrent.digit6Key.wasPressedThisFrame || kbCurrent.numpad6Key.wasPressedThisFrame) PocketWatchController.Instance?.SetTimeRemainingDebug(3.0f);
                if (kbCurrent.digit0Key.wasPressedThisFrame || kbCurrent.numpad0Key.wasPressedThisFrame) GuardPawn.ResetAllToPatrol();
            }
#else
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) GuardPawn.TriggerCheatBreach();
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) GuardPawn.TriggerCheatDoorwayInspect();
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) GuardPawn.TriggerCheatStealthSuccess();
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) GuardPawn.TriggerCheatAlertChase();
            if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) GuardPawn.TriggerTimeExpiredAssault();
            if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) PocketWatchController.Instance?.SetTimeRemainingDebug(3.0f);
            if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0)) GuardPawn.ResetAllToPatrol();
#endif
        }
    }
}
