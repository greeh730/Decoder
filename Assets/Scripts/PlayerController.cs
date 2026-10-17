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
/// - Movement, sprinting, jumping, gravity
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

    [Tooltip("Movement speed while sprinting in m/s")]
    [SerializeField] private float sprintSpeed = 6.0f;

    [Tooltip("Jump height in meters")]
    [SerializeField] private float jumpHeight = 1.0f;

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
    [SerializeField] private Vector3 chairPosition = new Vector3(7.83f, 0.02f, 1.95f);
    [SerializeField] private Vector3 chairLookEuler = new Vector3(0f, 85f, 0f);
    [SerializeField] private Vector3 monitorPosition = new Vector3(9.71f, 1.035f, 1.80f);
    [SerializeField] private Vector3 monitorCamPosition = new Vector3(9.18f, 1.035f, 1.80f);
    [SerializeField] private Vector3 monitorCamEuler = new Vector3(0f, 90f, 0f);

    [SerializeField] private float standingEyeHeight = 1.62f;
    [SerializeField] private float sittingEyeHeight = 1.05f;
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

    public PlayerState CurrentState => currentState;
    public bool IsSitting => currentState == PlayerState.Sitting;
    public bool IsInspectingMonitor => currentState == PlayerState.InspectingMonitor;

    private void Awake()
    {
        Application.runInBackground = true;
        characterController = GetComponent<CharacterController>();

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
    }

    private void Update()
    {
        if (isTransitioningCamera) return;
        if (MainframeBootQuest.Instance != null && MainframeBootQuest.Instance.IsReadingManual) return;

        HandleCursorLock();

        switch (currentState)
        {
            case PlayerState.Normal:
                HandleViewToggle();
                HandleMouseLook();
                HandleMovement();
                CheckInteractionPromptsNormal();
                break;

            case PlayerState.Sitting:
                HandleViewToggle();
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

    private void CheckInteractionPromptsNormal()
    {
        // Check central raycast for WorldInteractables
        if (playerCamera != null)
        {
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, 2.8f))
            {
                var interactable = hit.collider.GetComponentInParent<WorldInteractable>();
                if (interactable != null && interactable.IsInteractable && hit.distance <= interactable.MaxDistance)
                {
                    currentInteractable = interactable;
                    if (WasKeyPressed(KeyCode.E))
                    {
                        interactable.Interact();
                        return;
                    }
                }
                else
                {
                    currentInteractable = null;
                }
            }
            else
            {
                currentInteractable = null;
            }
        }

        float distToChair = Vector3.Distance(transform.position, chairPosition);
        float distToMonitor = Vector3.Distance(transform.position, monitorPosition);

        // Sit in chair (E) if not looking at another interactable
        if (currentInteractable == null && distToChair < 1.6f && WasKeyPressed(KeyCode.E))
        {
            SitDown();
            return;
        }

        // Inspect monitor directly while standing (F)
        if (distToMonitor < 2.2f && WasKeyPressed(KeyCode.F))
        {
            InspectMonitor();
            return;
        }
    }

    private void CheckInteractionPromptsSitting()
    {
        // Stand up (E, Space, or movement)
        if (WasKeyPressed(KeyCode.E) || WasKeyPressed(KeyCode.Space) || HasMovementInput())
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
    }

    private void CheckExitMonitorPrompt()
    {
        if (WasKeyPressed(KeyCode.Escape) || WasKeyPressed(KeyCode.F))
        {
            ExitMonitor();
        }
    }

    public void SitDown()
    {
        if (currentState != PlayerState.Normal) return;

        currentState = PlayerState.Sitting;
        characterController.enabled = false;

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

        // Slightly step back or stand at chair
        transform.position = chairPosition + transform.right * -0.5f;
        if (cameraHolder != null)
        {
            cameraHolder.localPosition = new Vector3(0f, standingEyeHeight, 0f);
        }

        characterController.enabled = true;
        verticalVelocity = Vector3.zero;

        UpdateCameraMode();
    }

    public void InspectMonitor()
    {
        if (currentState == PlayerState.InspectingMonitor) return;

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

            // Target camera local position depending on view mode
            Vector3 targetLocalPos = isFirstPerson ? firstPersonCamPos : thirdPersonCamPos;
            Quaternion targetLocalRot = isFirstPerson ? Quaternion.identity : Quaternion.Euler(10f, 0f, 0f);

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
        bool isSprint = false;
        bool jumpTriggered = false;

#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;

            isSprint = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            jumpTriggered = keyboard.spaceKey.wasPressedThisFrame;
        }
#else
        input.x = Input.GetAxisRaw("Horizontal");
        input.y = Input.GetAxisRaw("Vertical");
        isSprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        jumpTriggered = Input.GetKeyDown(KeyCode.Space);
#endif

        if (characterController.isGrounded)
        {
            if (verticalVelocity.y < 0f)
            {
                verticalVelocity.y = -2f;
            }

            if (jumpTriggered)
            {
                verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }

        Vector3 moveDir = (transform.right * input.x + transform.forward * input.y);
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        float currentSpeed = isSprint ? sprintSpeed : walkSpeed;
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
            case KeyCode.V: return kb.vKey.wasPressedThisFrame;
            case KeyCode.C: return kb.cKey.wasPressedThisFrame;
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

        bool escPressed = WasKeyPressed(KeyCode.Escape);
        bool clickPressed = false;

#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) clickPressed = true;
#else
        if (Input.GetMouseButtonDown(0)) clickPressed = true;
#endif

        if (escPressed)
        {
            SetCursorLock(false);
        }
        else if (clickPressed && !cursorLocked)
        {
            SetCursorLock(true);
        }
    }

    public void SetCursorLock(bool locked)
    {
        cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void HandleViewToggle()
    {
        if (WasKeyPressed(KeyCode.V) || WasKeyPressed(KeyCode.C))
        {
            isFirstPerson = !isFirstPerson;
            UpdateCameraMode();
        }
    }

    private void UpdateCameraMode()
    {
        if (playerCamera == null || cameraHolder == null) return;

        if (playerCamera.transform.parent != cameraHolder)
        {
            playerCamera.transform.SetParent(cameraHolder, false);
        }

        if (isFirstPerson)
        {
            playerCamera.transform.localPosition = firstPersonCamPos;
            playerCamera.transform.localRotation = Quaternion.identity;
        }
        else
        {
            playerCamera.transform.localPosition = thirdPersonCamPos;
            playerCamera.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
        }

        if (visualBody != null)
        {
            visualBody.SetActive(!isFirstPerson);
        }
    }

    // ==========================================
    // HUD / INTERACTION GUI
    // ==========================================

    private void OnGUI()
    {
        if (!showControlsUI) return;

        GUI.color = Color.white;

        // Top-left status box
        GUILayout.BeginArea(new Rect(15, 15, 420, 150));
        var boxStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.UpperLeft,
            fontSize = 12,
            padding = new RectOffset(10, 10, 8, 8)
        };
        boxStyle.normal.textColor = Color.white;

        string stateName = currentState switch
        {
            PlayerState.Normal => "Идёт (Стоит)",
            PlayerState.Sitting => "Сидит в кресле",
            PlayerState.InspectingMonitor => "Работа за терминалом",
            _ => "Обычный"
        };

        string viewModeText = isFirstPerson ? "1-е лицо" : "3-е лицо";
        string statusText = $"<b>Состояние:</b> <color=#00FFCC>{stateName}</color> | Вид: {viewModeText}\n" +
                           $"• <b>W, A, S, D</b> — Передвижение / Встать\n" +
                           $"• <b>Shift</b> — Бег | <b>Space</b> — Прыжок\n" +
                           $"• <b>Мышь</b> — Обзор (Look)\n" +
                           $"• <b>V / C</b> — Вид камеры (1st/3rd person)\n" +
                           $"• <b>Esc</b> — Освободить мышь (клик — захват)";

        GUILayout.Box(statusText, boxStyle);
        GUILayout.EndArea();

        // Center Interaction Prompts
        DrawCenterPrompts();
    }

    private void DrawCenterPrompts()
    {
        float screenW = Screen.width;
        float screenH = Screen.height;

        // Draw center crosshair dot
        if (isFirstPerson && currentState == PlayerState.Normal)
        {
            GUI.color = currentInteractable != null ? Color.green : new Color(1f, 1f, 1f, 0.45f);
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

        if (currentState == PlayerState.Normal)
        {
            // Highest priority: WorldInteractable in crosshair
            if (currentInteractable != null)
            {
                promptStyle.normal.textColor = Color.yellow;
                GUI.Box(new Rect(screenW / 2 - 180, screenH - 100, 360, 36), currentInteractable.PromptText, promptStyle);
                return;
            }

            float distToChair = Vector3.Distance(transform.position, chairPosition);
            float distToMonitor = Vector3.Distance(transform.position, monitorPosition);

            if (distToChair < 1.6f)
            {
                GUI.Box(new Rect(screenW / 2 - 130, screenH - 100, 260, 36), "Нажмите [E] — Сесть в кресло", promptStyle);
            }
            else if (distToMonitor < 2.2f)
            {
                GUI.Box(new Rect(screenW / 2 - 140, screenH - 100, 280, 36), "Нажмите [F] — Приблизить монитор", promptStyle);
            }
        }
        else if (currentState == PlayerState.Sitting)
        {
            GUI.Box(new Rect(screenW / 2 - 180, screenH - 110, 360, 34), "Нажмите [F] — Приблизить монитор", promptStyle);
            promptStyle.normal.textColor = Color.yellow;
            GUI.Box(new Rect(screenW / 2 - 180, screenH - 70, 360, 34), "Нажмите [E] / [Space] / [W,A,S,D] — Встать", promptStyle);
        }
        else if (currentState == PlayerState.InspectingMonitor)
        {
            promptStyle.normal.textColor = Color.yellow;
            GUI.Box(new Rect(screenW / 2 - 160, screenH - 60, 320, 36), "Нажмите [ESC] или [F] — Отойти от монитора", promptStyle);
        }
    }
}
