using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controls the vintage antique pocket watch held in the player's hand.
/// Features:
/// 1. Mechanical countdown timer with reverse-ticking hands and audio.
/// 2. Tactical Radar Map Mode: Click LMB or RMB while holding the watch to toggle
///    between the antique clock face and a live tactical radar map showing facility
///    corridors, guard patrol positions (red blips), and the player (green blip).
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
    [SerializeField] private bool isMapMode = false;
#if !ENABLE_INPUT_SYSTEM
    [SerializeField] private KeyCode toggleKey = KeyCode.T;
#endif

    [Header("Procedural Hierarchy References")]
    [SerializeField] private Transform watchRig;
    [SerializeField] private Transform watchBody;
    [SerializeField] private Transform clockFaceGroup;
    [SerializeField] private Transform radarGroup;
    [SerializeField] private RawImage radarDisplay;
    [SerializeField] private Transform hourHand;
    [SerializeField] private Transform minuteHand;
    [SerializeField] private Transform secondHand;
    [SerializeField] private Text countdownText;
    [SerializeField] private Text radarTimerText;
    [SerializeField] private Text radarStatusText;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private GameObject handsWithWatchPrefab;

    [Header("First-Person Transform & Hands Settings")]
    [Tooltip("Масштаб 3D модели рук в камере")]
    [SerializeField] [Range(0.2f, 1.5f)] private float modelScale = 0.55f;
    [Tooltip("Позиция руки перед глазами (поднята)")]
    [SerializeField] private Vector3 raisedLocalPos = new Vector3(0.1131f, -0.4601f, 0.3684f);
    [Tooltip("Угол поворота руки перед глазами (поднята)")]
    [SerializeField] private Vector3 raisedLocalRot = new Vector3(357.1935f, 89.3630f, 282.7943f);
    [Tooltip("Позиция руки внизу (опущена)")]
    [SerializeField] private Vector3 loweredLocalPos = new Vector3(0.1631f, -1.0101f, 0.4184f);
    [Tooltip("Угол поворота руки внизу (опущена)")]
    [SerializeField] private Vector3 loweredLocalRot = new Vector3(27.1935f, 99.3630f, 297.7943f);
    [Tooltip("Скорость анимации подъема руки (в секундах)")]
    [SerializeField] [Range(0.1f, 1.0f)] private float raiseDuration = 0.32f;

    [Header("Display Text & Colors")]
    [SerializeField] private string clockHeader = "";
    [SerializeField] private string clockSubtext = "";
    [SerializeField] private Color phosphorGreen = new Color(0.15f, 1.0f, 0.5f);

    [Header("Radar Tuning")]
    [Tooltip("Скорость оборота стрелки радара")]
    [SerializeField] [Range(0.5f, 6.0f)] private float radarSweepSpeed = 2.4f;

    [Header("Audio Tuning")]
    [SerializeField] [Range(0f, 1f)] private float tickVolume = 0.40f;
    [SerializeField] [Range(0f, 1f)] private float switchVolume = 0.55f;

    [System.Serializable]
    private class TrackedGuardBlip
    {
        public Vector2 spottedPixel;
        public Vector2 spottedHeading;
        public float lastSpottedTime;
        public float intensity;
        public bool hasBeenSpotted;
    }

    private Coroutine transitionCoroutine;
    private AudioClip tickClip;
    private AudioClip tockClip;
    private AudioClip switchClip;
    private AudioClip radarPingClip;
    private bool isTickAlternate = false;
    private int lastReportedSecond = -1;
    private PlayerController playerController;

    // Tactical Radar Sweep & Tracked Enemies Buffer
    private const int RadarTexSize = 128;
    private Texture2D radarTex;
    private Texture2D clockDialTex;
    private Color32[] staticBackgroundPixels;
    private Color32[] dynamicPixels;
    private float radarUpdateTimer = 0f;
    private const float RadarUpdateInterval = 0.04f; // 25 fps radar update
    private float radarSweepAngle = 0f;
    private float prevSweepAngle = 0f;
    private readonly Dictionary<int, TrackedGuardBlip> trackedGuards = new Dictionary<int, TrackedGuardBlip>();

    public bool IsRaised => isRaised;
    public bool IsMapMode => isMapMode;
    public float TimeRemaining => timeRemaining;

    private bool hasTriggeredTimeout = false;

    public void SetTimeRemainingDebug(float seconds)
    {
        timeRemaining = seconds;
        isTicking = true;
        hasTriggeredTimeout = false;
        UpdateWatchHands(timeRemaining);
        MainframeBootQuest.Instance?.ShowNotification($"[ТАЙМЕР УСТАНОВЛЕН] Осталось: {seconds:F1} сек. При 00:00 охрана выбивает дверь!");
    }

    private void Awake()
    {
        Instance = this;
        timeRemaining = countdownDuration;
        isRaised = false;
        playerController = GetComponentInParent<PlayerController>();

        GenerateAudioClips();
        EnsureWatchModel();
        InitRadarMap();
    }

    private void Start()
    {
        isRaised = false;
        if (watchRig != null)
        {
            watchRig.localPosition = loweredLocalPos;
            watchRig.localRotation = Quaternion.Euler(loweredLocalRot);
            watchRig.gameObject.SetActive(false);
        }

        UpdateWatchHands(timeRemaining);
        UpdateModeVisuals();
    }

    private void OnValidate()
    {
        if (watchRig != null)
        {
            watchRig.localScale = Vector3.one * modelScale;
            if (isRaised)
            {
                watchRig.localPosition = raisedLocalPos;
                watchRig.localRotation = Quaternion.Euler(raisedLocalRot);
            }
            else
            {
                watchRig.localPosition = loweredLocalPos;
                watchRig.localRotation = Quaternion.Euler(loweredLocalRot);
            }
        }

        if (clockFaceGroup != null)
        {
            var h = clockFaceGroup.Find("TimerHeader");
            if (h != null)
            {
                h.gameObject.SetActive(!string.IsNullOrEmpty(clockHeader));
                var ht = h.GetComponent<Text>();
                if (ht != null) ht.text = clockHeader;
            }

            var s = clockFaceGroup.Find("TimerSubtext");
            if (s != null)
            {
                s.gameObject.SetActive(!string.IsNullOrEmpty(clockSubtext));
                var st = s.GetComponent<Text>();
                if (st != null) st.text = clockSubtext;
            }
        }

        if (countdownText != null && timeRemaining > 60f)
        {
            countdownText.color = phosphorGreen;
        }
    }

    [ContextMenu("Preview Raised Watch in Scene")]
    public void PreviewRaisedInScene()
    {
        ResolveReferences();
        if (watchRig != null)
        {
            watchRig.gameObject.SetActive(true);
            watchRig.localScale = Vector3.one * modelScale;
            watchRig.localPosition = raisedLocalPos;
            watchRig.localRotation = Quaternion.Euler(raisedLocalRot);
        }
    }

    [ContextMenu("Preview Lowered Watch in Scene")]
    public void PreviewLoweredInScene()
    {
        ResolveReferences();
        if (watchRig != null)
        {
            watchRig.gameObject.SetActive(false);
            watchRig.localScale = Vector3.one * modelScale;
            watchRig.localPosition = loweredLocalPos;
            watchRig.localRotation = Quaternion.Euler(loweredLocalRot);
        }
    }

    private void Update()
    {
        // 1. Handle countdown timer
        if (isTicking)
        {
            if (timeRemaining > 0f)
            {
                timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);

                int currentSecond = Mathf.FloorToInt(timeRemaining);
                if (currentSecond != lastReportedSecond)
                {
                    lastReportedSecond = currentSecond;
                    PlayTickSound();
                }

                UpdateWatchHands(timeRemaining);

                if (timeRemaining <= 0f)
                {
                    OnCountdownExpired();
                }
            }
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

        // 4. Handle Map Mode switch on LMB or RMB when watch is raised
        if (isRaised)
        {
            bool clickPressed = false;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
            {
                clickPressed = true;
            }
#else
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                clickPressed = true;
            }
#endif

            if (clickPressed)
            {
                ToggleMapMode(!isMapMode);
            }
        }

        // 5. Update live radar screen if in Map Mode
        if (isRaised && isMapMode)
        {
            radarUpdateTimer += Time.deltaTime;
            if (radarUpdateTimer >= RadarUpdateInterval)
            {
                radarUpdateTimer = 0f;
                RenderLiveRadarFrame();
            }
        }

        // 6. Subtle idle breathing sway when held up in view
        if (isRaised && transitionCoroutine == null && watchRig != null)
        {
            float swayX = Mathf.Sin(Time.time * 1.5f) * 0.0012f;
            float swayY = Mathf.Cos(Time.time * 2.1f) * 0.0016f;
            watchRig.localPosition = raisedLocalPos + new Vector3(swayX, swayY, 0f);
        }
    }

    private void OnCountdownExpired()
    {
        if (hasTriggeredTimeout) return;
        hasTriggeredTimeout = true;
        isTicking = false;
        timeRemaining = 0f;

        if (countdownText != null)
        {
            countdownText.text = "00:00";
            countdownText.color = Color.red;
        }

        MainframeBootQuest.Instance?.ShowNotification(
            LocalizationManager.T("<color=#FFCC00>[ВРЕМЯ ИСТЕКЛО!]</color> По графику комендатуры в машинный зал направлен дежурный патруль! Прячьтесь!",
                                   "<color=#FFCC00>[TIME EXPIRED!]</color> Scheduled patrol dispatch incoming to machine hall! Hide!"));

        // Trigger scheduled guard inspection through GuardPatrolSystem!
        GuardPatrolSystem.TriggerScheduledInspection();
    }

    public void RestartCountdown()
    {
        timeRemaining = countdownDuration;
        isTicking = true;
        hasTriggeredTimeout = false;
        lastReportedSecond = Mathf.FloorToInt(timeRemaining);
        UpdateWatchHands(timeRemaining);
        if (countdownText != null)
        {
            countdownText.color = Color.white;
        }
        MainframeBootQuest.Instance?.ShowNotification(
            LocalizationManager.T("<color=#00FFAA>[ТАЙМЕР ПЕРЕЗАПУЩЕН]</color> Плановая проверка завершена. Следующий обход через 15 минут.",
                                   "<color=#00FFAA>[TIMER RESTARTED]</color> Scheduled inspection passed. Next patrol in 15 minutes."));
    }

    public void ToggleWatch(bool raise)
    {
        if (isRaised == raise && watchRig != null && watchRig.gameObject.activeSelf == raise) return;

        isRaised = raise;

        if (raise)
        {
            UpdateModeVisuals();
            if (isMapMode)
            {
                RenderLiveRadarFrame();
            }
        }

        if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
        transitionCoroutine = StartCoroutine(TransitionRoutine(raise));
    }

    public void ToggleMapMode(bool mapMode)
    {
        if (isMapMode == mapMode) return;
        isMapMode = mapMode;

        if (audioSource != null && switchClip != null)
        {
            audioSource.pitch = isMapMode ? 1.15f : 0.95f;
            audioSource.PlayOneShot(switchClip, switchVolume);
        }

        UpdateModeVisuals();

        if (isMapMode)
        {
            RenderLiveRadarFrame();
        }
    }

    public void ResolveReferences()
    {
        var cam = GetComponentInChildren<Camera>();
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        if (watchRig == null)
        {
            var rig = cam.transform.Find("PocketWatchRig");
            if (rig != null) watchRig = rig;
        }

        if (watchRig != null)
        {
            if (watchBody == null)
            {
                var watchChild = watchRig.Find("watch_silver_closed");
                watchBody = watchChild != null ? watchChild : watchRig;
            }

            if (watchBody != null)
            {
                if (clockFaceGroup == null)
                {
                    clockFaceGroup = watchBody.Find("ClockFaceGroup");
                }
                if (radarGroup == null)
                {
                    radarGroup = watchBody.Find("TacticalRadarGroup");
                }
            }

            if (radarGroup != null && radarDisplay == null)
            {
                var d = radarGroup.Find("RadarDisplayScreen");
                if (d != null) radarDisplay = d.GetComponent<RawImage>();
            }

            if (clockFaceGroup != null)
            {
                var bg = clockFaceGroup.Find("ScreenBg");
                if (bg != null)
                {
                    if (clockDialTex == null) clockDialTex = CreateCircularClockDial(RadarTexSize);
                    var img = bg.GetComponent<Image>();
                    if (img != null) DestroyImmediate(img);
                    var raw = bg.GetComponent<RawImage>();
                    if (raw == null) raw = bg.gameObject.AddComponent<RawImage>();
                    if (raw.texture != clockDialTex) raw.texture = clockDialTex;
                    var rt = bg.GetComponent<RectTransform>();
                    if (rt != null) rt.sizeDelta = new Vector2(70f, 70f);
                }

                var h = clockFaceGroup.Find("TimerHeader");
                if (h != null)
                {
                    h.gameObject.SetActive(!string.IsNullOrEmpty(clockHeader));
                    var ht = h.GetComponent<Text>();
                    if (ht != null) ht.text = clockHeader;
                }

                var s = clockFaceGroup.Find("TimerSubtext");
                if (s != null)
                {
                    s.gameObject.SetActive(!string.IsNullOrEmpty(clockSubtext));
                    var st = s.GetComponent<Text>();
                    if (st != null) st.text = clockSubtext;
                }

                if (countdownText == null)
                {
                    var t = clockFaceGroup.Find("CountdownReadout");
                    if (t != null) countdownText = t.GetComponent<Text>();
                }
            }

            if (radarGroup != null && radarTimerText == null)
            {
                var t = radarGroup.Find("RadarTimer");
                if (t != null) radarTimerText = t.GetComponent<Text>();
            }

            if (radarGroup != null && radarStatusText == null)
            {
                var t = radarGroup.Find("RadarHeader");
                if (t != null) radarStatusText = t.GetComponent<Text>();
            }

            if (audioSource == null)
            {
                audioSource = watchRig.GetComponent<AudioSource>();
            }
        }
    }

    private void UpdateModeVisuals()
    {
        ResolveReferences();
        if (clockFaceGroup != null) clockFaceGroup.gameObject.SetActive(!isMapMode);
        if (radarGroup != null) radarGroup.gameObject.SetActive(isMapMode);
    }

    private IEnumerator TransitionRoutine(bool raise)
    {
        if (watchRig == null) yield break;

        watchRig.gameObject.SetActive(true);

        Vector3 startPos = watchRig.localPosition;
        Quaternion startRot = watchRig.localRotation;

        Vector3 targetPos = raise ? raisedLocalPos : loweredLocalPos;
        Quaternion targetRot = Quaternion.Euler(raise ? raisedLocalRot : loweredLocalRot);

        float duration = raiseDuration;
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
            // Mode is preserved across raise/lower
        }
        else
        {
            UpdateModeVisuals();
            if (isMapMode)
            {
                RenderLiveRadarFrame();
            }
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
        string timeStr = $"{minutes:D2}:{seconds:D2}";

        // Dial readout
        if (countdownText != null)
        {
            countdownText.text = timeStr;
            if (time <= 60f)
            {
                float pulse = Mathf.PingPong(Time.time * 4f, 1f);
                countdownText.color = Color.Lerp(new Color(0.9f, 0.15f, 0.15f), Color.white, pulse);
            }
            else
            {
                countdownText.color = new Color(0.15f, 1.0f, 0.5f);
            }
        }

        if (radarTimerText != null)
        {
            radarTimerText.text = timeStr;
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
        if (!isRaised || isMapMode || audioSource == null) return;

        AudioClip clipToPlay = isTickAlternate ? tockClip : tickClip;
        isTickAlternate = !isTickAlternate;

        audioSource.pitch = Random.Range(0.98f, 1.02f);
        audioSource.PlayOneShot(clipToPlay, tickVolume);
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

        // 3. Mode Switch (Mechanical snap & electric chirp)
        int swLen = (int)(sampleRate * 0.075f);
        switchClip = AudioClip.Create("WatchSwitch", swLen, 1, sampleRate, false);
        float[] swData = new float[swLen];
        for (int i = 0; i < swLen; i++)
        {
            float t = (float)i / swLen;
            float decay = Mathf.Exp(-t * 18f);
            float tone = Mathf.Sin(2f * Mathf.PI * 880f * (i / (float)sampleRate)) * 0.6f +
                         Mathf.Sin(2f * Mathf.PI * 1760f * (i / (float)sampleRate)) * 0.4f;
            float click = (Random.value * 2f - 1f) * Mathf.Exp(-t * 80f) * 0.6f;
            swData[i] = (tone * 0.4f + click) * decay;
        }
        switchClip.SetData(swData, 0);

        // 4. Tactical Radar Ping (Sonar chirp when sweep hits guard)
        int pingLen = (int)(sampleRate * 0.075f);
        radarPingClip = AudioClip.Create("RadarPing", pingLen, 1, sampleRate, false);
        float[] pingData = new float[pingLen];
        for (int i = 0; i < pingLen; i++)
        {
            float t = (float)i / pingLen;
            float decay = Mathf.Exp(-t * 22f);
            float tone = Mathf.Sin(2f * Mathf.PI * 1760f * (i / (float)sampleRate)) * 0.7f +
                         Mathf.Sin(2f * Mathf.PI * 2640f * (i / (float)sampleRate)) * 0.3f;
            pingData[i] = tone * decay * 0.28f;
        }
        radarPingClip.SetData(pingData, 0);
    }

    // =========================================================================
    // CIRCULAR CLOCK DIAL & TACTICAL RADAR RENDERING
    // =========================================================================

    private Texture2D CreateCircularClockDial(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float centerPx = size * 0.5f;
        float maxRadius = centerPx - 4f;

        Color32 clearCol = new Color32(0, 0, 0, 0);
        Color32 bgCol = new Color32(8, 22, 16, 255); // Deep Soviet CRT/LCD green (identical to radar!)
        Color32 outerRingCol = new Color32(28, 70, 52, 255);
        Color32 innerRingCol = new Color32(16, 42, 30, 255);
        Color32 tickCol = new Color32(50, 130, 95, 220);

        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(centerPx, centerPx));
                if (dist > maxRadius + 1f)
                {
                    pixels[y * size + x] = clearCol;
                }
                else if (dist > maxRadius)
                {
                    float alpha = 1f - (dist - maxRadius);
                    pixels[y * size + x] = new Color32(outerRingCol.r, outerRingCol.g, outerRingCol.b, (byte)(alpha * 255f));
                }
                else if (dist >= maxRadius - 2f)
                {
                    pixels[y * size + x] = outerRingCol;
                }
                else if (Mathf.Abs(dist - 44f) < 1f)
                {
                    pixels[y * size + x] = innerRingCol;
                }
                else
                {
                    float angle = Mathf.Atan2(y - centerPx, x - centerPx);
                    float deg = (angle * Mathf.Rad2Deg + 360f) % 360f;
                    float mod30 = deg % 30f;
                    bool isTick = (dist >= maxRadius - 6f) && (mod30 < 1.4f || mod30 > 28.6f);

                    pixels[y * size + x] = isTick ? tickCol : bgCol;
                }
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false);
        return tex;
    }

    private void InitRadarMap()
    {
        radarTex = new Texture2D(RadarTexSize, RadarTexSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        staticBackgroundPixels = new Color32[RadarTexSize * RadarTexSize];
        dynamicPixels = new Color32[RadarTexSize * RadarTexSize];

        // 1. Draw Static CRT Radar Grid and Facility Corridors
        Color32 clearCol = new Color32(0, 0, 0, 0);
        Color32 bgCol = new Color32(8, 22, 16, 255); // Deep Soviet CRT green
        Color32 gridRingCol = new Color32(18, 48, 36, 255);
        Color32 corridorFillCol = new Color32(28, 70, 52, 255);
        Color32 corridorBorderCol = new Color32(75, 185, 130, 255);
        Color32 roomCol = new Color32(35, 90, 70, 255);

        float centerPx = RadarTexSize * 0.5f;
        float maxRadius = centerPx - 4f;

        // Base fill & circle mask
        for (int y = 0; y < RadarTexSize; y++)
        {
            for (int x = 0; x < RadarTexSize; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(centerPx, centerPx));
                if (dist <= maxRadius)
                {
                    bool isRing = Mathf.Abs(dist - 20f) < 1f || Mathf.Abs(dist - 40f) < 1f || Mathf.Abs(dist - 58f) < 1.2f;
                    bool isCross = (Mathf.Abs(x - centerPx) < 0.6f || Mathf.Abs(y - centerPx) < 0.6f) && (dist % 6f < 3f);
                    staticBackgroundPixels[y * RadarTexSize + x] = (isRing || isCross) ? gridRingCol : bgCol;
                }
                else
                {
                    staticBackgroundPixels[y * RadarTexSize + x] = clearCol;
                }
            }
        }

        // Draw Corridor Rectangles into static background (Expanded facility network)
        // 1. Main Hallway (X: 12..15, Z: -22..28)
        DrawWorldRectOnStatic(12.0f, -22.0f, 3.0f, 50.0f, corridorFillCol, corridorBorderCol, maxRadius);
        // 2. North Wing (X: 13.5..24.5, Z: 24.6..27.4)
        DrawWorldRectOnStatic(13.5f, 24.6f, 11.0f, 2.8f, corridorFillCol, corridorBorderCol, maxRadius);
        // 3. South Wing (X: 13.5..24.5, Z: -21.4..-18.6)
        DrawWorldRectOnStatic(13.5f, -21.4f, 11.0f, 2.8f, corridorFillCol, corridorBorderCol, maxRadius);
        // 4. Mid North Cross (X: 13.5..24.5, Z: 12.1..14.9)
        DrawWorldRectOnStatic(13.5f, 12.1f, 11.0f, 2.8f, corridorFillCol, corridorBorderCol, maxRadius);
        // 5. Mid South Cross (X: 13.5..24.5, Z: -7.9..-5.1)
        DrawWorldRectOnStatic(13.5f, -7.9f, 11.0f, 2.8f, corridorFillCol, corridorBorderCol, maxRadius);
        // 6. East Perimeter (X: 23.2..25.8, Z: -21..27)
        DrawWorldRectOnStatic(23.2f, -21.0f, 2.6f, 48.0f, corridorFillCol, corridorBorderCol, maxRadius);
        // 7. West Service Passage (X: 2.08..4.08, Z: -2..9)
        DrawWorldRectOnStatic(2.08f, -2.0f, 2.0f, 11.0f, corridorFillCol, corridorBorderCol, maxRadius);
        // 8. Our Computer Lab Room comnata (X: 4.18..12.01, Z: -0.56..5.35)
        DrawWorldRectOnStatic(4.18f, -0.56f, 7.83f, 5.91f, roomCol, corridorBorderCol, maxRadius);
        // 9. Entrance Vestibule (X: 8.9..12.0, Z: -2.75..-0.5)
        DrawWorldRectOnStatic(8.9f, -2.75f, 3.1f, 2.25f, corridorFillCol, corridorBorderCol, maxRadius);

        // Push static background to texture immediately
        System.Array.Copy(staticBackgroundPixels, dynamicPixels, staticBackgroundPixels.Length);
        radarTex.SetPixels32(dynamicPixels);
        radarTex.Apply(false);

        // Assign to RawImage display
        if (radarDisplay != null)
        {
            radarDisplay.texture = radarTex;
        }
    }

    private void DrawWorldRectOnStatic(float worldX, float worldZ, float sizeX, float sizeZ, Color32 fill, Color32 border, float maxR)
    {
        Vector2 min = WorldToRadarPixel(worldX, worldZ);
        Vector2 max = WorldToRadarPixel(worldX + sizeX, worldZ + sizeZ);

        int minX = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(min.x, max.x)), 0, RadarTexSize - 1);
        int maxX = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(min.x, max.x)), 0, RadarTexSize - 1);
        int minY = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(min.y, max.y)), 0, RadarTexSize - 1);
        int maxY = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(min.y, max.y)), 0, RadarTexSize - 1);

        float centerPx = RadarTexSize * 0.5f;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (Vector2.Distance(new Vector2(x, y), new Vector2(centerPx, centerPx)) > maxR) continue;

                bool isEdge = (x == minX || x == maxX || y == minY || y == maxY);
                staticBackgroundPixels[y * RadarTexSize + x] = isEdge ? border : fill;
            }
        }
    }

    private Vector2 WorldToRadarPixel(float worldX, float worldZ)
    {
        // World Center = (14.5, 3.0), scale = 2.05 px / meter to fit expanded ~114m facility
        float centerPx = RadarTexSize * 0.5f;
        float px = centerPx + (worldX - 14.5f) * 2.05f;
        float py = centerPx + (worldZ - 3.0f) * 2.05f;
        return new Vector2(px, py);
    }

    public void RenderLiveRadarFrame()
    {
        if (radarTex == null) InitRadarMap();
        if (staticBackgroundPixels == null) InitRadarMap();
        if (radarDisplay != null && radarDisplay.texture != radarTex)
        {
            radarDisplay.texture = radarTex;
        }

        // 1. Copy base static corridors
        System.Array.Copy(staticBackgroundPixels, dynamicPixels, staticBackgroundPixels.Length);

        Vector2 center = new Vector2(RadarTexSize * 0.5f, RadarTexSize * 0.5f);

        // Advance sweep angle
        float dt = Time.deltaTime > 0f ? Time.deltaTime : 0.04f;
        prevSweepAngle = radarSweepAngle;
        radarSweepAngle = Mathf.Repeat(radarSweepAngle + radarSweepSpeed * dt, Mathf.PI * 2f);

        // 2. Fading phosphor wake behind leading needle
        for (int tail = 12; tail >= 1; tail--)
        {
            float tailAngle = radarSweepAngle - tail * 0.022f;
            Vector2 tailDir = new Vector2(Mathf.Cos(tailAngle), Mathf.Sin(tailAngle));
            float falloff = (float)tail / 12f;
            byte alpha = (byte)Mathf.RoundToInt(Mathf.Lerp(90f, 12f, falloff));
            Color32 tailCol = new Color32(35, 120, 75, alpha);

            for (int r = 6; r < 56; r += 2)
            {
                Vector2 pt = center + tailDir * r;
                int sx = Mathf.RoundToInt(pt.x);
                int sy = Mathf.RoundToInt(pt.y);
                if (sx >= 0 && sx < RadarTexSize && sy >= 0 && sy < RadarTexSize)
                {
                    int idx = sy * RadarTexSize + sx;
                    Color32 bg = dynamicPixels[idx];
                    dynamicPixels[idx] = new Color32(
                        (byte)Mathf.Min(255, bg.r + tailCol.r * alpha / 255),
                        (byte)Mathf.Min(255, bg.g + tailCol.g * alpha / 255),
                        (byte)Mathf.Min(255, bg.b + tailCol.b * alpha / 255),
                        bg.a
                    );
                }
            }
        }

        // Leading sharp needle line
        Vector2 sweepDir = new Vector2(Mathf.Cos(radarSweepAngle), Mathf.Sin(radarSweepAngle));
        Color32 needleCol = new Color32(185, 255, 215, 255);
        for (int r = 4; r < 56; r++)
        {
            Vector2 pt = center + sweepDir * r;
            int sx = Mathf.RoundToInt(pt.x);
            int sy = Mathf.RoundToInt(pt.y);
            if (sx >= 0 && sx < RadarTexSize && sy >= 0 && sy < RadarTexSize)
            {
                dynamicPixels[sy * RadarTexSize + sx] = needleCol;
            }
        }

        // 3. Track and Plot Guards (Positions update ONLY when swept by the needle!)
        bool justSweptAnyGuard = false;
        var guards = FindObjectsByType<GuardPawn>(FindObjectsInactive.Exclude);
        int guardCount = (guards != null) ? guards.Length : 0;
        float maxSuspicion = 0f;

        if (guards != null && guardCount > 0)
        {
            foreach (var g in guards)
            {
                if (g == null) continue;
                if (g.Suspicion > maxSuspicion) maxSuspicion = g.Suspicion;

                int id = g.gameObject.name.GetHashCode();
                Vector3 gPos = g.transform.position;
                Vector2 livePix = WorldToRadarPixel(gPos.x, gPos.z);
                Vector2 delta = livePix - center;
                float targetAngle = Mathf.Atan2(delta.y, delta.x);
                if (targetAngle < 0f) targetAngle += Mathf.PI * 2f;

                if (!trackedGuards.TryGetValue(id, out var blip))
                {
                    blip = new TrackedGuardBlip
                    {
                        spottedPixel = livePix,
                        spottedHeading = new Vector2(g.transform.forward.x, g.transform.forward.z),
                        lastSpottedTime = Time.time,
                        intensity = 0.5f,
                        hasBeenSpotted = true
                    };
                    trackedGuards[id] = blip;
                }

                // Check if needle swept past the guard's position
                if (WasAngleCrossed(prevSweepAngle, radarSweepAngle, targetAngle))
                {
                    blip.spottedPixel = livePix;
                    blip.spottedHeading = new Vector2(g.transform.forward.x, g.transform.forward.z);
                    blip.lastSpottedTime = Time.time;
                    blip.intensity = 1.0f;
                    justSweptAnyGuard = true;
                }
                else
                {
                    float elapsed = Time.time - blip.lastSpottedTime;
                    blip.intensity = Mathf.Clamp01(1.0f - (elapsed / 2.6f) * 0.70f);
                }

                // Draw at last SPOTTED pixel (frozen in place between sweeps!)
                Color32 coreCol = Color32.Lerp(new Color32(130, 25, 25, 160), new Color32(255, 45, 45, 255), blip.intensity);
                Color32 glowCol = Color32.Lerp(new Color32(70, 10, 10, 70), new Color32(255, 120, 100, 200), blip.intensity);

                if (g.Suspicion > 0.35f)
                {
                    float pulse = Mathf.PingPong(Time.time * 6f, 1f);
                    if (g.Suspicion >= 0.80f)
                    {
                        // High Alert: Rapid flashing red-yellow
                        coreCol = Color32.Lerp(new Color32(255, 30, 30, 255), new Color32(255, 230, 30, 255), pulse);
                        glowCol = new Color32(255, 120, 0, 220);
                    }
                    else
                    {
                        // Suspicious: Pulsing amber
                        coreCol = Color32.Lerp(new Color32(220, 140, 20, 200), new Color32(255, 220, 40, 255), pulse);
                        glowCol = new Color32(200, 120, 10, 180);
                    }
                }
                else if (blip.intensity > 0.88f)
                {
                    coreCol = new Color32(255, 230, 210, 255); // bright ping flash!
                }

                DrawDotOnDynamic(Mathf.RoundToInt(blip.spottedPixel.x), Mathf.RoundToInt(blip.spottedPixel.y), 2, coreCol, glowCol);

                // Heading tick
                Vector2 fwdTick = blip.spottedPixel + blip.spottedHeading * 4f;
                int fx = Mathf.Clamp(Mathf.RoundToInt(fwdTick.x), 0, RadarTexSize - 1);
                int fy = Mathf.Clamp(Mathf.RoundToInt(fwdTick.y), 0, RadarTexSize - 1);
                dynamicPixels[fy * RadarTexSize + fx] = coreCol;
            }
        }
        else
        {
            // Default 3 patrol positions along the corridor routes
            Vector3[] simulatedGuards = new Vector3[]
            {
                new Vector3(13.5f, 0f, 4.5f),   // Main lab corridor
                new Vector3(24.5f, 0f, 6.0f),   // East checkpoint perimeter
                new Vector3(3.08f, 0f, 3.5f)    // West crawlway
            };
            guardCount = simulatedGuards.Length;
            for (int i = 0; i < simulatedGuards.Length; i++)
            {
                int id = 1000 + i;
                Vector3 pos = simulatedGuards[i];
                Vector2 livePix = WorldToRadarPixel(pos.x, pos.z);
                Vector2 delta = livePix - center;
                float targetAngle = Mathf.Atan2(delta.y, delta.x);
                if (targetAngle < 0f) targetAngle += Mathf.PI * 2f;

                if (!trackedGuards.TryGetValue(id, out var blip))
                {
                    blip = new TrackedGuardBlip
                    {
                        spottedPixel = livePix,
                        spottedHeading = Vector2.up,
                        lastSpottedTime = Time.time,
                        intensity = 0.5f,
                        hasBeenSpotted = true
                    };
                    trackedGuards[id] = blip;
                }

                if (WasAngleCrossed(prevSweepAngle, radarSweepAngle, targetAngle))
                {
                    blip.spottedPixel = livePix;
                    blip.lastSpottedTime = Time.time;
                    blip.intensity = 1.0f;
                    justSweptAnyGuard = true;
                }
                else
                {
                    float elapsed = Time.time - blip.lastSpottedTime;
                    blip.intensity = Mathf.Clamp01(1.0f - (elapsed / 2.6f) * 0.70f);
                }

                Color32 coreCol = Color32.Lerp(new Color32(130, 25, 25, 160), new Color32(255, 45, 45, 255), blip.intensity);
                Color32 glowCol = Color32.Lerp(new Color32(70, 10, 10, 70), new Color32(255, 120, 100, 200), blip.intensity);
                if (blip.intensity > 0.88f) coreCol = new Color32(255, 230, 210, 255);

                DrawDotOnDynamic(Mathf.RoundToInt(blip.spottedPixel.x), Mathf.RoundToInt(blip.spottedPixel.y), 2, coreCol, glowCol);
            }
        }

        // Play tactical audio chirp on ping
        if (justSweptAnyGuard && isRaised && isMapMode && audioSource != null && radarPingClip != null)
        {
            audioSource.PlayOneShot(radarPingClip, 0.25f);
        }

        // 4. Plot Player (Cyan/Green Blip)
        Vector3 playerPos = playerController != null ? playerController.transform.position : transform.position;
        Vector2 playerPix = WorldToRadarPixel(playerPos.x, playerPos.z);
        Color32 playerCol = new Color32(50, 255, 110, 255);
        Color32 playerGlow = new Color32(140, 255, 180, 200);
        DrawDotOnDynamic(Mathf.RoundToInt(playerPix.x), Mathf.RoundToInt(playerPix.y), 2, playerCol, playerGlow);

        if (playerController != null)
        {
            Vector2 pFwd = playerPix + new Vector2(playerController.transform.forward.x, playerController.transform.forward.z) * 5f;
            int px = Mathf.Clamp(Mathf.RoundToInt(pFwd.x), 0, RadarTexSize - 1);
            int py = Mathf.Clamp(Mathf.RoundToInt(pFwd.y), 0, RadarTexSize - 1);
            dynamicPixels[py * RadarTexSize + px] = playerCol;
        }

        // Apply texture
        radarTex.SetPixels32(dynamicPixels);
        radarTex.Apply(false);

        // Update status text
        if (radarStatusText != null)
        {
            if (maxSuspicion >= 0.80f)
            {
                radarStatusText.color = new Color(1.0f, 0.25f, 0.25f);
                radarStatusText.text = LocalizationManager.T("⚠ ТРЕВОГА У ДВЕРИ! ⚠", "⚠ ALERT AT DOORWAY! ⚠");
            }
            else if (maxSuspicion > 0.35f)
            {
                radarStatusText.color = new Color(1.0f, 0.85f, 0.2f);
                radarStatusText.text = LocalizationManager.T($"• ПОДОЗРЕНИЕ: {Mathf.RoundToInt(maxSuspicion * 100f)}% •", $"• SUSPICION: {Mathf.RoundToInt(maxSuspicion * 100f)}% •");
            }
            else
            {
                radarStatusText.color = new Color(0.35f, 0.95f, 0.55f);
                radarStatusText.text = LocalizationManager.T($"• ОХРАНА: {guardCount} •", $"• GUARDS: {guardCount} •");
            }
        }
    }

    private bool WasAngleCrossed(float prevA, float currA, float targetA)
    {
        if (currA >= prevA)
        {
            return targetA >= prevA && targetA <= currA;
        }
        else
        {
            return targetA >= prevA || targetA <= currA;
        }
    }

    private void DrawDotOnDynamic(int cx, int cy, int radius, Color32 core, Color32 halo)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = cx + dx;
                int y = cy + dy;
                if (x >= 0 && x < RadarTexSize && y >= 0 && y < RadarTexSize)
                {
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d <= radius * 0.65f)
                    {
                        dynamicPixels[y * RadarTexSize + x] = core;
                    }
                    else if (d <= radius)
                    {
                        dynamicPixels[y * RadarTexSize + x] = halo;
                    }
                }
            }
        }
    }

    // =========================================================================
    // PROCEDURAL 3D POCKET WATCH MODEL BUILDER
    // =========================================================================

    [ContextMenu("Rebuild Watch Model")]
    public void RebuildWatchModelContext()
    {
        EnsureWatchModel(true);
    }

    public void EnsureWatchModel(bool forceRebuild = false)
    {
        var cam = GetComponentInChildren<Camera>();
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        var existing = cam.transform.Find("PocketWatchRig");
        bool hasValidRig = existing != null && existing.GetComponentInChildren<Renderer>(true) != null;

        if (hasValidRig && !forceRebuild)
        {
            watchRig = existing;
            watchRig.localScale = Vector3.one * modelScale;
            watchRig.localPosition = loweredLocalPos;
            watchRig.localRotation = Quaternion.Euler(loweredLocalRot);

            audioSource = watchRig.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = watchRig.gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;

            var watchChild = watchRig.Find("watch_silver_closed");
            watchBody = watchChild != null ? watchChild : watchRig;

            ResolveReferences();
            if (radarTex == null) InitRadarMap();
            if (radarDisplay != null && radarTex != null)
            {
                radarDisplay.texture = radarTex;
            }

            if (clockFaceGroup != null) clockFaceGroup.gameObject.SetActive(!isMapMode);
            if (radarGroup != null) radarGroup.gameObject.SetActive(isMapMode);
            UpdateModeVisuals();

            watchRig.localPosition = loweredLocalPos;
            watchRig.localRotation = Quaternion.Euler(loweredLocalRot);
            watchRig.gameObject.SetActive(false);
            isRaised = false;
            return;
        }

        // Clean up previous rig if rebuilding
        if (existing != null)
        {
            DestroyImmediate(existing.gameObject);
        }

        // 1. Instantiate the Hands with Watch FBX model
        GameObject modelPrefab = handsWithWatchPrefab;
        if (modelPrefab == null)
        {
            modelPrefab = Resources.Load<GameObject>("hands_with_watch");
        }
        if (modelPrefab == null)
        {
            modelPrefab = Resources.Load<GameObject>("hands with a watch");
        }
#if UNITY_EDITOR
        if (modelPrefab == null)
        {
            modelPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/hands with a watch.fbx");
        }
#endif

        GameObject rigObj;
        if (modelPrefab != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                rigObj = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(modelPrefab);
            }
            else
            {
                rigObj = Instantiate(modelPrefab);
            }
#else
            rigObj = Instantiate(modelPrefab);
#endif
            rigObj.name = "PocketWatchRig";
        }
        else
        {
            Debug.LogWarning("[PocketWatchController] No hands model prefab found! PocketWatchRig created empty.");
            rigObj = new GameObject("PocketWatchRig");
        }

        rigObj.transform.SetParent(cam.transform, false);
        watchRig = rigObj.transform;
        watchRig.localScale = Vector3.one * modelScale;
        watchRig.localPosition = loweredLocalPos;
        watchRig.localRotation = Quaternion.Euler(loweredLocalRot);

        audioSource = rigObj.GetComponent<AudioSource>();
        if (audioSource == null) audioSource = rigObj.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        // Locate the watch mesh
        var watchMeshChild = watchRig.Find("watch_silver_closed");
        watchBody = watchMeshChild != null ? watchMeshChild : watchRig;

        var builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (builtinFont == null) builtinFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // 2. CLOCK FACE / DIGITAL DISPLAY CONTAINER
        var clockGroupObj = new GameObject("ClockFaceGroup");
        clockGroupObj.transform.SetParent(watchBody, false);
        clockGroupObj.transform.localPosition = new Vector3(0f, 0f, 0.00049f);
        clockGroupObj.transform.localRotation = Quaternion.identity;
        clockFaceGroup = clockGroupObj.transform;

        var clockCanvas = clockGroupObj.AddComponent<Canvas>();
        clockCanvas.renderMode = RenderMode.WorldSpace;
        var clockRt = clockGroupObj.GetComponent<RectTransform>();
        clockRt.sizeDelta = new Vector2(100f, 100f);
        clockRt.localScale = new Vector3(-0.000014f, 0.000014f, 1f); // Invert X to prevent mirroring

        // Circular LCD background screen (circular, matches radar!)
        var bgObj = new GameObject("ScreenBg");
        bgObj.transform.SetParent(clockGroupObj.transform, false);
        var bgImg = bgObj.AddComponent<RawImage>();
        if (clockDialTex == null) clockDialTex = CreateCircularClockDial(RadarTexSize);
        bgImg.texture = clockDialTex;
        var bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.sizeDelta = new Vector2(70f, 70f);

        // Header: "• ЭЛЕКТРОНИКА •"
        var headerObj = new GameObject("TimerHeader");
        headerObj.transform.SetParent(clockGroupObj.transform, false);
        var headerTxt = headerObj.AddComponent<Text>();
        headerTxt.font = builtinFont;
        headerTxt.fontSize = 8;
        headerTxt.fontStyle = FontStyle.Bold;
        headerTxt.alignment = TextAnchor.MiddleCenter;
        headerTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
        headerTxt.verticalOverflow = VerticalWrapMode.Overflow;
        headerTxt.color = phosphorGreen * 0.9f;
        headerTxt.text = clockHeader;
        var headerRt = headerObj.GetComponent<RectTransform>();
        headerRt.anchoredPosition = new Vector2(0f, 18f);
        headerRt.sizeDelta = new Vector2(70f, 12f);
        headerObj.SetActive(!string.IsNullOrEmpty(clockHeader));

        // Main Countdown Display: "14:59"
        var timerObj = new GameObject("CountdownReadout");
        timerObj.transform.SetParent(clockGroupObj.transform, false);
        countdownText = timerObj.AddComponent<Text>();
        countdownText.font = builtinFont;
        countdownText.fontSize = 24;
        countdownText.fontStyle = FontStyle.Bold;
        countdownText.alignment = TextAnchor.MiddleCenter;
        countdownText.horizontalOverflow = HorizontalWrapMode.Overflow;
        countdownText.verticalOverflow = VerticalWrapMode.Overflow;
        countdownText.color = phosphorGreen;
        countdownText.text = "15:00";
        var timerRt = timerObj.GetComponent<RectTransform>();
        timerRt.anchoredPosition = new Vector2(0f, 0f);
        timerRt.sizeDelta = new Vector2(70f, 28f);

        // Subtext: "• КГБ СССР •"
        var subObj = new GameObject("TimerSubtext");
        subObj.transform.SetParent(clockGroupObj.transform, false);
        var subTxt = subObj.AddComponent<Text>();
        subTxt.font = builtinFont;
        subTxt.fontSize = 8;
        subTxt.fontStyle = FontStyle.Bold;
        subTxt.alignment = TextAnchor.MiddleCenter;
        subTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
        subTxt.verticalOverflow = VerticalWrapMode.Overflow;
        subTxt.color = phosphorGreen * 0.8f;
        subTxt.text = clockSubtext;
        var subRt = subObj.GetComponent<RectTransform>();
        subRt.anchoredPosition = new Vector2(0f, -18f);
        subRt.sizeDelta = new Vector2(70f, 12f);
        subObj.SetActive(!string.IsNullOrEmpty(clockSubtext));

        // 3. TACTICAL RADAR CONTAINER
        var radarGroupObj = new GameObject("TacticalRadarGroup");
        radarGroupObj.transform.SetParent(watchBody, false);
        radarGroupObj.transform.localPosition = new Vector3(0f, 0f, 0.00050f);
        radarGroupObj.transform.localRotation = Quaternion.identity;
        radarGroup = radarGroupObj.transform;

        var radarCanvas = radarGroupObj.AddComponent<Canvas>();
        radarCanvas.renderMode = RenderMode.WorldSpace;
        var rCanvasRt = radarGroupObj.GetComponent<RectTransform>();
        rCanvasRt.sizeDelta = new Vector2(100f, 100f);
        rCanvasRt.localScale = new Vector3(-0.000014f, 0.000014f, 1f);

        // Radar RawImage Display
        var displayObj = new GameObject("RadarDisplayScreen");
        displayObj.transform.SetParent(radarGroupObj.transform, false);
        radarDisplay = displayObj.AddComponent<RawImage>();
        var dispRt = displayObj.GetComponent<RectTransform>();
        dispRt.anchoredPosition = Vector2.zero;
        dispRt.sizeDelta = new Vector2(70f, 70f);

        // Top Radar Header
        var rHeaderObj = new GameObject("RadarHeader");
        rHeaderObj.transform.SetParent(radarGroupObj.transform, false);
        radarStatusText = rHeaderObj.AddComponent<Text>();
        radarStatusText.font = builtinFont;
        radarStatusText.fontSize = 9;
        radarStatusText.fontStyle = FontStyle.Bold;
        radarStatusText.alignment = TextAnchor.MiddleCenter;
        radarStatusText.color = new Color(0.35f, 0.95f, 0.55f);
        radarStatusText.text = LocalizationManager.T("• РАДАР СБ •", "• SECURITY RADAR •");
        var rHeaderRt = rHeaderObj.GetComponent<RectTransform>();
        rHeaderRt.anchoredPosition = new Vector2(0f, 24f);
        rHeaderRt.sizeDelta = new Vector2(70f, 12f);

        // Bottom Radar Timer
        var rTimerObj = new GameObject("RadarTimer");
        rTimerObj.transform.SetParent(radarGroupObj.transform, false);
        radarTimerText = rTimerObj.AddComponent<Text>();
        radarTimerText.font = builtinFont;
        radarTimerText.fontSize = 9;
        radarTimerText.fontStyle = FontStyle.Bold;
        radarTimerText.alignment = TextAnchor.MiddleCenter;
        radarTimerText.color = new Color(0.35f, 0.95f, 0.55f);
        radarTimerText.text = "15:00";
        var rTimerRt = rTimerObj.GetComponent<RectTransform>();
        rTimerRt.anchoredPosition = new Vector2(0f, -24f);
        rTimerRt.sizeDelta = new Vector2(70f, 12f);

        if (radarTex == null) InitRadarMap();
        if (radarDisplay != null && radarTex != null)
        {
            radarDisplay.texture = radarTex;
        }

        clockGroupObj.SetActive(!isMapMode);
        radarGroupObj.SetActive(isMapMode);
        UpdateModeVisuals();

        if (watchRig != null)
        {
            watchRig.localPosition = loweredLocalPos;
            watchRig.localRotation = Quaternion.Euler(loweredLocalRot);
            watchRig.gameObject.SetActive(false);
        }
        isRaised = false;
    }

    private void DestroyCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);
    }

    // ==========================================
    // HUD TIMER & TACTICAL RADAR BADGE
    // ==========================================

    private void OnGUI()
    {
        GUIFontHelper.Apply();
        if (!isRaised) return;

        float sw = Screen.width;
        float sh = Screen.height;

        int totalSec = Mathf.FloorToInt(timeRemaining);
        int mins = totalSec / 60;
        int secs = totalSec % 60;

        var badgeStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
            fontStyle = FontStyle.Bold
        };

        if (isMapMode)
        {
            badgeStyle.normal.textColor = new Color(0.35f, 0.95f, 0.55f);
            GUI.backgroundColor = new Color(0.04f, 0.12f, 0.08f, 0.92f);
            GUI.Box(new Rect(sw - 380, sh - 90, 360, 42),
                LocalizationManager.T(
                    "🗺 ТАКТИЧЕСКИЙ РАДАР (СХЕМА НИИ)\n[ЛКМ / ПКМ] — Режим циферблата",
                    "🗺 TACTICAL RADAR (INSTITUTE MAP)\n[LMB / RMB] — Clock Face Mode"
                ), badgeStyle);
        }
        else
        {
            badgeStyle.normal.textColor = (timeRemaining <= 60f) ? new Color(1f, 0.3f, 0.3f) : new Color(1f, 0.95f, 0.75f);
            GUI.backgroundColor = new Color(0.12f, 0.09f, 0.06f, 0.92f);
            GUI.Box(new Rect(sw - 380, sh - 90, 360, 42),
                LocalizationManager.T(
                    $"⏱ НАРУЧНЫЕ ЧАСЫ [{mins:D2}:{secs:D2}]\n[ЛКМ / ПКМ] — Режим карты (Радар)",
                    $"⏱ POCKET WATCH [{mins:D2}:{secs:D2}]\n[LMB / RMB] — Map Mode (Radar)"
                ), badgeStyle);
        }

        GUI.backgroundColor = Color.white;
    }
}
