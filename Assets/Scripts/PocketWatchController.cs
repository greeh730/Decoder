using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controls the vintage antique pocket watch held in the player's hand.
/// Features a mechanical backward-ticking countdown timer,
/// alternating mechanical tick-tock audio, solid ivory dial with 12 hour indices,
/// moving hands (counter-clockwise reverse countdown), digital aperture readout,
/// and smooth raise/lower animation on the T key.
/// </summary>
public class PocketWatchController : MonoBehaviour
{
    public static PocketWatchController Instance { get; private set; }

    [Header("Countdown Settings")]
    [Tooltip("Total countdown duration in seconds (e.g. 900 = 15:00)")]
    [SerializeField] private float countdownDuration = 900f; // 15 minutes
    [SerializeField] private float timeRemaining = 900f;
    [SerializeField] private bool isTicking = true;

    [Header("Watch State")]
    [SerializeField] private bool isRaised = false;
    [SerializeField] private KeyCode toggleKey = KeyCode.T;

    [Header("Procedural Hierarchy References")]
    [SerializeField] private Transform watchRig;
    [SerializeField] private Transform watchBody;
    [SerializeField] private Transform hourHand;
    [SerializeField] private Transform minuteHand;
    [SerializeField] private Transform secondHand;
    [SerializeField] private Text countdownText;
    [SerializeField] private AudioSource audioSource;

    // Camera relative positions for 1st person view
    private readonly Vector3 raisedLocalPos = new Vector3(0.12f, -0.08f, 0.28f);
    private readonly Vector3 raisedLocalRot = new Vector3(16f, -24f, 6f);

    private readonly Vector3 loweredLocalPos = new Vector3(0.20f, -0.65f, 0.28f);
    private readonly Vector3 loweredLocalRot = new Vector3(55f, -10f, 20f);

    private Coroutine transitionCoroutine;
    private AudioClip tickClip;
    private AudioClip tockClip;
    private bool isTickAlternate = false;
    private int lastReportedSecond = -1;
    private PlayerController playerController;

    public bool IsRaised => isRaised;
    public float TimeRemaining => timeRemaining;

    private void Awake()
    {
        Instance = this;
        timeRemaining = countdownDuration;
        playerController = GetComponentInParent<PlayerController>();

        GenerateAudioClips();
        EnsureWatchModel();
    }

    private void Start()
    {
        if (watchRig != null)
        {
            watchRig.localPosition = loweredLocalPos;
            watchRig.localRotation = Quaternion.Euler(loweredLocalRot);
            watchRig.gameObject.SetActive(false);
        }

        UpdateWatchHands(timeRemaining);
    }

    private void Update()
    {
        // 1. Handle countdown timer
        if (isTicking && timeRemaining > 0f)
        {
            timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);

            int currentSecond = Mathf.FloorToInt(timeRemaining);
            if (currentSecond != lastReportedSecond)
            {
                lastReportedSecond = currentSecond;
                PlayTickSound();
            }

            UpdateWatchHands(timeRemaining);
        }

        // 2. Hide watch if in 3rd person or inspecting monitor
        if (playerController != null)
        {
            if (!playerController.IsFirstPerson || playerController.CurrentState == PlayerState.InspectingMonitor)
            {
                if (isRaised)
                {
                    ToggleWatch(false);
                }
                if (watchRig != null && watchRig.gameObject.activeSelf)
                {
                    watchRig.gameObject.SetActive(false);
                }
                return;
            }
        }

        // 3. Handle key toggle (T)
        bool keyPressed = false;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && kb.tKey.wasPressedThisFrame) keyPressed = true;
#else
        if (Input.GetKeyDown(toggleKey)) keyPressed = true;
#endif

        if (keyPressed)
        {
            ToggleWatch(!isRaised);
        }

        // 4. Subtle idle breathing sway when held up in view
        if (isRaised && transitionCoroutine == null && watchRig != null)
        {
            float swayX = Mathf.Sin(Time.time * 1.5f) * 0.0012f;
            float swayY = Mathf.Cos(Time.time * 2.1f) * 0.0016f;
            watchRig.localPosition = raisedLocalPos + new Vector3(swayX, swayY, 0f);
        }
    }

    public void ToggleWatch(bool raise)
    {
        if (isRaised == raise && watchRig != null && watchRig.gameObject.activeSelf == raise) return;

        isRaised = raise;

        if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
        transitionCoroutine = StartCoroutine(TransitionRoutine(raise));
    }

    private IEnumerator TransitionRoutine(bool raise)
    {
        if (watchRig == null) yield break;

        watchRig.gameObject.SetActive(true);

        Vector3 startPos = watchRig.localPosition;
        Quaternion startRot = watchRig.localRotation;

        Vector3 targetPos = raise ? raisedLocalPos : loweredLocalPos;
        Quaternion targetRot = Quaternion.Euler(raise ? raisedLocalRot : loweredLocalRot);

        float duration = 0.32f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            watchRig.localPosition = Vector3.Lerp(startPos, targetPos, t);
            watchRig.localRotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        watchRig.localPosition = targetPos;
        watchRig.localRotation = targetRot;

        if (!raise)
        {
            watchRig.gameObject.SetActive(false);
        }

        transitionCoroutine = null;
    }

    /// <summary>
    /// Updates the reverse-ticking hands and text readout.
    /// Hand rotates counter-clockwise (backward) as time counts down.
    /// </summary>
    private void UpdateWatchHands(float time)
    {
        int totalSeconds = Mathf.FloorToInt(time);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        // Dial readout
        if (countdownText != null)
        {
            countdownText.text = $"{minutes:D2}:{seconds:D2}";
            if (time <= 60f)
            {
                float pulse = Mathf.PingPong(Time.time * 4f, 1f);
                countdownText.color = Color.Lerp(new Color(0.9f, 0.15f, 0.15f), Color.white, pulse);
            }
            else
            {
                countdownText.color = new Color(0.55f, 0.08f, 0.08f);
            }
        }

        // Counter-clockwise (backward) ticking hand rotations:
        float secProgress = (time % 60f); // 0..59.99
        float secondAngle = secProgress * 6f;

        float minProgress = ((time / 60f) % 60f);
        float minuteAngle = minProgress * 6f;

        float hourProgress = ((time / 3600f) % 12f);
        float hourAngle = hourProgress * 30f;

        if (secondHand != null)
        {
            secondHand.localRotation = Quaternion.Euler(0f, 0f, secondAngle);
        }
        if (minuteHand != null)
        {
            minuteHand.localRotation = Quaternion.Euler(0f, 0f, minuteAngle);
        }
        if (hourHand != null)
        {
            hourHand.localRotation = Quaternion.Euler(0f, 0f, hourAngle);
        }
    }

    private void PlayTickSound()
    {
        if (!isRaised || audioSource == null) return;

        AudioClip clipToPlay = isTickAlternate ? tockClip : tickClip;
        isTickAlternate = !isTickAlternate;

        audioSource.pitch = Random.Range(0.98f, 1.02f);
        audioSource.PlayOneShot(clipToPlay, 0.40f);
    }

    private void GenerateAudioClips()
    {
        int sampleRate = 44100;

        // 1. Tick
        int tickLen = (int)(sampleRate * 0.035f);
        tickClip = AudioClip.Create("WatchTick", tickLen, 1, sampleRate, false);
        float[] tickData = new float[tickLen];
        for (int i = 0; i < tickLen; i++)
        {
            float t = (float)i / tickLen;
            float decay = Mathf.Exp(-t * 32f);
            float noise = (Random.value * 2f - 1f) * 0.35f;
            float tone = Mathf.Sin(2f * Mathf.PI * 2300f * (i / (float)sampleRate));
            tickData[i] = (tone * 0.65f + noise) * decay * 0.5f;
        }
        tickClip.SetData(tickData, 0);

        // 2. Tock
        int tockLen = (int)(sampleRate * 0.038f);
        tockClip = AudioClip.Create("WatchTock", tockLen, 1, sampleRate, false);
        float[] tockData = new float[tockLen];
        for (int i = 0; i < tockLen; i++)
        {
            float t = (float)i / tockLen;
            float decay = Mathf.Exp(-t * 26f);
            float noise = (Random.value * 2f - 1f) * 0.30f;
            float tone = Mathf.Sin(2f * Mathf.PI * 1800f * (i / (float)sampleRate));
            tockData[i] = (tone * 0.7f + noise) * decay * 0.5f;
        }
        tockClip.SetData(tockData, 0);
    }

    // ==========================================
    // PROCEDURAL 3D POCKET WATCH MODEL BUILDER
    // ==========================================

    [ContextMenu("Rebuild Watch Model")]
    public void EnsureWatchModel()
    {
        var cam = GetComponentInChildren<Camera>();
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        // Clean up previous rig if rebuilding
        var existing = cam.transform.Find("PocketWatchRig");
        if (existing != null)
        {
            DestroyImmediate(existing.gameObject);
        }

        // 1. Root Rig under Camera
        var rigObj = new GameObject("PocketWatchRig");
        rigObj.transform.SetParent(cam.transform, false);
        watchRig = rigObj.transform;
        watchRig.localPosition = loweredLocalPos;
        watchRig.localRotation = Quaternion.Euler(loweredLocalRot);

        audioSource = rigObj.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        var urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) urpLit = Shader.Find("Standard");

        // Materials: antique brushed brass
        var goldMat = new Material(urpLit);
        goldMat.SetColor("_BaseColor", new Color(0.82f, 0.65f, 0.28f));
        goldMat.SetFloat("_Metallic", 0.85f);
        goldMat.SetFloat("_Smoothness", 0.58f);

        var darkSteelMat = new Material(urpLit);
        darkSteelMat.SetColor("_BaseColor", new Color(0.12f, 0.12f, 0.15f));
        darkSteelMat.SetFloat("_Metallic", 0.85f);
        darkSteelMat.SetFloat("_Smoothness", 0.80f);

        var redNeedleMat = new Material(urpLit);
        redNeedleMat.SetColor("_BaseColor", new Color(0.85f, 0.12f, 0.12f));
        redNeedleMat.SetFloat("_Metallic", 0.3f);
        redNeedleMat.SetFloat("_Smoothness", 0.75f);

        var coatMat = new Material(urpLit);
        coatMat.SetColor("_BaseColor", new Color(0.12f, 0.14f, 0.16f)); // Dark wool sleeve
        coatMat.SetFloat("_Smoothness", 0.08f);

        var gloveMat = new Material(urpLit);
        gloveMat.SetColor("_BaseColor", new Color(0.32f, 0.25f, 0.20f)); // Vintage leather glove
        gloveMat.SetFloat("_Smoothness", 0.28f);

        var ivoryMat = new Material(urpLit);
        ivoryMat.SetColor("_BaseColor", new Color(0.96f, 0.94f, 0.88f)); // Ivory enamel dial
        ivoryMat.SetFloat("_Smoothness", 0.40f);

        // 2. Arm & Hand (Forearm and leather-gloved hand supporting the watch)
        var armGroup = new GameObject("ArmAndHand").transform;
        armGroup.SetParent(watchRig, false);

        // Sleeve
        var sleeve = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        sleeve.name = "Sleeve";
        DestroyCollider(sleeve);
        sleeve.transform.SetParent(armGroup, false);
        sleeve.transform.localPosition = new Vector3(0.04f, -0.16f, -0.12f);
        sleeve.transform.localRotation = Quaternion.Euler(42f, 15f, -18f);
        sleeve.transform.localScale = new Vector3(0.085f, 0.18f, 0.085f);
        sleeve.GetComponent<MeshRenderer>().sharedMaterial = coatMat;

        // Glove Palm (curved underneath the watch)
        var palm = GameObject.CreatePrimitive(PrimitiveType.Cube);
        palm.name = "GlovePalm";
        DestroyCollider(palm);
        palm.transform.SetParent(armGroup, false);
        palm.transform.localPosition = new Vector3(0.01f, -0.048f, 0.015f);
        palm.transform.localRotation = Quaternion.Euler(22f, 5f, -6f);
        palm.transform.localScale = new Vector3(0.082f, 0.024f, 0.085f);
        palm.GetComponent<MeshRenderer>().sharedMaterial = gloveMat;

        // Thumb curving on the left edge
        var thumb = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        thumb.name = "GloveThumb";
        DestroyCollider(thumb);
        thumb.transform.SetParent(armGroup, false);
        thumb.transform.localPosition = new Vector3(-0.048f, -0.015f, -0.008f);
        thumb.transform.localRotation = Quaternion.Euler(15f, 30f, 38f);
        thumb.transform.localScale = new Vector3(0.022f, 0.038f, 0.022f);
        thumb.GetComponent<MeshRenderer>().sharedMaterial = gloveMat;

        // 3. Watch Body
        var bodyObj = new GameObject("WatchBody");
        bodyObj.transform.SetParent(watchRig, false);
        watchBody = bodyObj.transform;
        watchBody.localPosition = Vector3.zero;
        watchBody.localRotation = Quaternion.identity;

        // Outer Brass Back Case (wider to form the golden bezel rim)
        var backCase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        backCase.name = "BackCase";
        DestroyCollider(backCase);
        backCase.transform.SetParent(watchBody, false);
        backCase.transform.localPosition = new Vector3(0f, 0f, 0.008f);
        backCase.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        backCase.transform.localScale = new Vector3(0.086f, 0.010f, 0.086f);
        backCase.GetComponent<MeshRenderer>().sharedMaterial = goldMat;

        // Top Winding Crown (at 12 o'clock)
        var crown = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        crown.name = "WindingCrown";
        DestroyCollider(crown);
        crown.transform.SetParent(watchBody, false);
        crown.transform.localPosition = new Vector3(0f, 0.048f, 0f);
        crown.transform.localRotation = Quaternion.identity;
        crown.transform.localScale = new Vector3(0.013f, 0.009f, 0.013f);
        crown.GetComponent<MeshRenderer>().sharedMaterial = goldMat;

        // Top Loop Ring (for chain)
        var loop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        loop.name = "ChainLoop";
        DestroyCollider(loop);
        loop.transform.SetParent(watchBody, false);
        loop.transform.localPosition = new Vector3(0f, 0.057f, 0f);
        loop.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        loop.transform.localScale = new Vector3(0.018f, 0.003f, 0.018f);
        loop.GetComponent<MeshRenderer>().sharedMaterial = goldMat;

        // 4. Dial Face (Ivory enamel cylinder disc)
        var dialObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        dialObj.name = "DialFace";
        DestroyCollider(dialObj);
        dialObj.transform.SetParent(watchBody, false);
        dialObj.transform.localPosition = new Vector3(0f, 0f, -0.003f);
        dialObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        dialObj.transform.localScale = new Vector3(0.076f, 0.001f, 0.076f);
        dialObj.GetComponent<MeshRenderer>().sharedMaterial = ivoryMat;

        // 5. 12 Hour Ticks
        var ticksGroup = new GameObject("HourTicks").transform;
        ticksGroup.SetParent(watchBody, false);
        ticksGroup.localPosition = new Vector3(0f, 0f, -0.0042f);

        var tickMat = new Material(urpLit);
        tickMat.SetColor("_BaseColor", new Color(0.15f, 0.10f, 0.05f));
        tickMat.SetFloat("_Smoothness", 0.8f);

        for (int i = 0; i < 12; i++)
        {
            float angle = i * 30f * Mathf.Deg2Rad;
            float r = 0.032f;
            var dot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dot.name = "Tick_" + i;
            DestroyCollider(dot);
            dot.transform.SetParent(ticksGroup, false);
            dot.transform.localPosition = new Vector3(Mathf.Sin(angle) * r, Mathf.Cos(angle) * r, 0f);
            dot.transform.localRotation = Quaternion.Euler(0f, 0f, -i * 30f);
            bool isCardinal = (i % 3 == 0);
            dot.transform.localScale = isCardinal ? new Vector3(0.0022f, 0.006f, 0.0005f) : new Vector3(0.0014f, 0.0038f, 0.0005f);
            dot.GetComponent<MeshRenderer>().sharedMaterial = tickMat;
        }

        // 6. Dial UI Canvas (Countdown text aperture)
        var canvasObj = new GameObject("DialCanvas");
        canvasObj.transform.SetParent(watchBody, false);
        canvasObj.transform.localPosition = new Vector3(0f, 0f, -0.0048f);
        canvasObj.transform.localRotation = Quaternion.identity;

        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = canvasObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(100f, 100f);
        rt.localScale = new Vector3(0.00065f, 0.00065f, 1f);

        var builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Small aperture background plate
        var plateObj = new GameObject("ApertureFrame");
        plateObj.transform.SetParent(canvasObj.transform, false);
        var plateImg = plateObj.AddComponent<Image>();
        plateImg.color = new Color(0.92f, 0.88f, 0.78f, 0.95f);
        var plateRt = plateObj.GetComponent<RectTransform>();
        plateRt.anchoredPosition = new Vector2(0f, -22f);
        plateRt.sizeDelta = new Vector2(58f, 22f);

        // Countdown text
        var timerObj = new GameObject("CountdownReadout");
        timerObj.transform.SetParent(canvasObj.transform, false);
        countdownText = timerObj.AddComponent<Text>();
        countdownText.font = builtinFont;
        countdownText.fontSize = 17;
        countdownText.fontStyle = FontStyle.Bold;
        countdownText.alignment = TextAnchor.MiddleCenter;
        countdownText.color = new Color(0.55f, 0.08f, 0.08f);
        countdownText.text = "15:00";
        var timerRt = timerObj.GetComponent<RectTransform>();
        timerRt.anchoredPosition = new Vector2(0f, -22f);
        timerRt.sizeDelta = new Vector2(58f, 22f);

        // 7. Mechanical Hands
        var handsGroup = new GameObject("HandsPivot");
        handsGroup.transform.SetParent(watchBody, false);
        handsGroup.transform.localPosition = new Vector3(0f, 0f, -0.0055f);

        // Center Pin
        var centerPin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        centerPin.name = "CenterPin";
        DestroyCollider(centerPin);
        centerPin.transform.SetParent(handsGroup.transform, false);
        centerPin.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        centerPin.transform.localScale = new Vector3(0.006f, 0.002f, 0.006f);
        centerPin.GetComponent<MeshRenderer>().sharedMaterial = goldMat;

        // Hour Hand
        var hhPivot = new GameObject("HourHandPivot");
        hhPivot.transform.SetParent(handsGroup.transform, false);
        hourHand = hhPivot.transform;
        var hhMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hhMesh.name = "HourMesh";
        DestroyCollider(hhMesh);
        hhMesh.transform.SetParent(hhPivot.transform, false);
        hhMesh.transform.localPosition = new Vector3(0f, 0.010f, 0f);
        hhMesh.transform.localScale = new Vector3(0.0035f, 0.020f, 0.0006f);
        hhMesh.GetComponent<MeshRenderer>().sharedMaterial = darkSteelMat;

        // Minute Hand
        var mhPivot = new GameObject("MinuteHandPivot");
        mhPivot.transform.SetParent(handsGroup.transform, false);
        minuteHand = mhPivot.transform;
        var mhMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mhMesh.name = "MinuteMesh";
        DestroyCollider(mhMesh);
        mhMesh.transform.SetParent(mhPivot.transform, false);
        mhMesh.transform.localPosition = new Vector3(0f, 0.015f, -0.0004f);
        mhMesh.transform.localScale = new Vector3(0.0024f, 0.030f, 0.0006f);
        mhMesh.GetComponent<MeshRenderer>().sharedMaterial = darkSteelMat;

        // Second Hand (Thin red needle)
        var shPivot = new GameObject("SecondHandPivot");
        shPivot.transform.SetParent(handsGroup.transform, false);
        secondHand = shPivot.transform;
        var shMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shMesh.name = "SecondMesh";
        DestroyCollider(shMesh);
        shMesh.transform.SetParent(shPivot.transform, false);
        shMesh.transform.localPosition = new Vector3(0f, 0.016f, -0.0008f);
        shMesh.transform.localScale = new Vector3(0.0013f, 0.034f, 0.0005f);
        shMesh.GetComponent<MeshRenderer>().sharedMaterial = redNeedleMat;

        // Counterweight
        var shCounter = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shCounter.name = "SecondCounterweight";
        DestroyCollider(shCounter);
        shCounter.transform.SetParent(shPivot.transform, false);
        shCounter.transform.localPosition = new Vector3(0f, -0.007f, -0.0008f);
        shCounter.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        shCounter.transform.localScale = new Vector3(0.004f, 0.0006f, 0.004f);
        shCounter.GetComponent<MeshRenderer>().sharedMaterial = redNeedleMat;
    }

    private void DestroyCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);
    }

    // ==========================================
    // HUD TIMER BADGE (While Looking at Watch)
    // ==========================================

    private void OnGUI()
    {
        if (!isRaised) return;

        float sw = Screen.width;
        float sh = Screen.height;

        int totalSec = Mathf.FloorToInt(timeRemaining);
        int mins = totalSec / 60;
        int secs = totalSec % 60;

        var badgeStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 15,
            fontStyle = FontStyle.Bold
        };
        badgeStyle.normal.textColor = (timeRemaining <= 60f) ? new Color(1f, 0.3f, 0.3f) : new Color(1f, 0.95f, 0.75f);

        GUI.backgroundColor = new Color(0.1f, 0.08f, 0.05f, 0.90f);
        GUI.Box(new Rect(sw - 330, sh - 85, 305, 38), $"⏱ КАРМАННЫЕ ЧАСЫ: {mins:D2}:{secs:D2}", badgeStyle);
        GUI.backgroundColor = Color.white;
    }
}
