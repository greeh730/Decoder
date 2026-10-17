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
    [SerializeField] private KeyCode toggleKey = KeyCode.T;

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

    // Camera relative positions for 1st person view
    private readonly Vector3 raisedLocalPos = new Vector3(0.09f, -0.060f, 0.22f);
    private readonly Vector3 raisedLocalRot = new Vector3(12f, 16f, 1f);

    private readonly Vector3 loweredLocalPos = new Vector3(0.20f, -0.65f, 0.28f);
    private readonly Vector3 loweredLocalRot = new Vector3(55f, -10f, 20f);

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
    private Color32[] staticBackgroundPixels;
    private Color32[] dynamicPixels;
    private float radarUpdateTimer = 0f;
    private const float RadarUpdateInterval = 0.04f; // 25 fps radar update
    private float radarSweepAngle = 0f;
    private float prevSweepAngle = 0f;
    private const float RadarSweepSpeed = 2.4f; // ~2.6s per 360-degree sweep
    private readonly Dictionary<int, TrackedGuardBlip> trackedGuards = new Dictionary<int, TrackedGuardBlip>();

    public bool IsRaised => isRaised;
    public bool IsMapMode => isMapMode;
    public float TimeRemaining => timeRemaining;

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
            audioSource.PlayOneShot(switchClip, 0.55f);
        }

        UpdateModeVisuals();

        if (isMapMode)
        {
            RenderLiveRadarFrame();
        }
    }

    private void UpdateModeVisuals()
    {
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
                countdownText.color = new Color(0.55f, 0.08f, 0.08f);
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
    // TACTICAL RADAR MAP RENDERING (CORRIDORS & MOVING GUARDS)
    // =========================================================================

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

        // Draw Corridor Rectangles into static background
        // World coordinates mapping: Center is (14.5, 3.5), scale: 4.2 pixels per world meter
        // 1. Main Hallway (X: 12..15, Z: -8..15)
        DrawWorldRectOnStatic(12.0f, -8.0f, 3.0f, 23.0f, corridorFillCol, corridorBorderCol, maxRadius);
        // 2. North Wing (X: 15..25, Z: 12..15)
        DrawWorldRectOnStatic(15.0f, 12.0f, 10.0f, 3.0f, corridorFillCol, corridorBorderCol, maxRadius);
        // 3. South Wing (X: 15..25, Z: -8..-5)
        DrawWorldRectOnStatic(15.0f, -8.0f, 10.0f, 3.0f, corridorFillCol, corridorBorderCol, maxRadius);
        // 4. East Perimeter (X: 23..26, Z: -8..15)
        DrawWorldRectOnStatic(23.2f, -8.0f, 2.6f, 23.0f, corridorFillCol, corridorBorderCol, maxRadius);
        // 5. West Service Passage (X: 3..5, Z: -2..9)
        DrawWorldRectOnStatic(3.0f, -2.0f, 2.0f, 11.0f, corridorFillCol, corridorBorderCol, maxRadius);
        // 6. Our Computer Lab Room (X: 6.4..11.6, Z: -1..6)
        DrawWorldRectOnStatic(6.4f, -1.0f, 5.2f, 7.0f, roomCol, corridorBorderCol, maxRadius);

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
        // World Center = (14.5, 3.5), scale = 3.8 px / meter
        float centerPx = RadarTexSize * 0.5f;
        float px = centerPx + (worldX - 14.5f) * 3.8f;
        float py = centerPx + (worldZ - 3.5f) * 3.8f;
        return new Vector2(px, py);
    }

    public void RenderLiveRadarFrame()
    {
        if (radarTex == null) return;
        if (staticBackgroundPixels == null) InitRadarMap();

        // 1. Copy base static corridors
        System.Array.Copy(staticBackgroundPixels, dynamicPixels, staticBackgroundPixels.Length);

        Vector2 center = new Vector2(RadarTexSize * 0.5f, RadarTexSize * 0.5f);

        // Advance sweep angle
        float dt = Time.deltaTime > 0f ? Time.deltaTime : 0.04f;
        prevSweepAngle = radarSweepAngle;
        radarSweepAngle = Mathf.Repeat(radarSweepAngle + RadarSweepSpeed * dt, Mathf.PI * 2f);

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

        if (guards != null && guardCount > 0)
        {
            foreach (var g in guards)
            {
                if (g == null) continue;
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

                if (blip.intensity > 0.88f)
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
                new Vector3(4.0f, 0f, 3.5f)     // West crawlway
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
            radarStatusText.text = $"• ОХРАНА: {guardCount} •";
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

        var toonLit = Shader.Find("Custom/SovietToonLit");
        if (toonLit == null) toonLit = Shader.Find("Universal Render Pipeline/Lit");

        // Materials: antique brushed brass
        var goldMat = new Material(toonLit);
        goldMat.SetColor("_BaseColor", new Color(0.82f, 0.65f, 0.28f));
        goldMat.SetFloat("_OutlineWidth", 1.8f);

        var darkSteelMat = new Material(toonLit);
        darkSteelMat.SetColor("_BaseColor", new Color(0.12f, 0.12f, 0.15f));
        darkSteelMat.SetFloat("_OutlineWidth", 1.5f);

        var redNeedleMat = new Material(toonLit);
        redNeedleMat.SetColor("_BaseColor", new Color(0.85f, 0.12f, 0.12f));

        var coatMat = new Material(toonLit);
        coatMat.SetColor("_BaseColor", new Color(0.12f, 0.14f, 0.16f)); // Dark wool sleeve

        var gloveMat = new Material(toonLit);
        gloveMat.SetColor("_BaseColor", new Color(0.32f, 0.25f, 0.20f)); // Vintage leather glove

        var ivoryMat = new Material(toonLit);
        ivoryMat.SetColor("_BaseColor", new Color(0.96f, 0.94f, 0.88f)); // Ivory enamel dial

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
        palm.transform.localPosition = new Vector3(0.00f, -0.042f, 0.022f);
        palm.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);
        palm.transform.localScale = new Vector3(0.082f, 0.024f, 0.085f);
        palm.GetComponent<MeshRenderer>().sharedMaterial = gloveMat;

        // Thumb curving on the left rim
        var thumb = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        thumb.name = "GloveThumb";
        DestroyCollider(thumb);
        thumb.transform.SetParent(armGroup, false);
        thumb.transform.localPosition = new Vector3(-0.046f, -0.016f, 0.008f);
        thumb.transform.localRotation = Quaternion.Euler(12f, 15f, 25f);
        thumb.transform.localScale = new Vector3(0.018f, 0.034f, 0.018f);
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

        // 4. Dial Face (Base circular disc)
        var dialObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        dialObj.name = "DialFace";
        DestroyCollider(dialObj);
        dialObj.transform.SetParent(watchBody, false);
        dialObj.transform.localPosition = new Vector3(0f, 0f, -0.003f);
        dialObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        dialObj.transform.localScale = new Vector3(0.076f, 0.001f, 0.076f);
        dialObj.GetComponent<MeshRenderer>().sharedMaterial = ivoryMat;

        // ==========================================
        // 5. CLOCK FACE MODE CONTAINER
        // ==========================================
        var clockGroupObj = new GameObject("ClockFaceGroup");
        clockGroupObj.transform.SetParent(watchBody, false);
        clockFaceGroup = clockGroupObj.transform;

        // 12 Hour Ticks
        var ticksGroup = new GameObject("HourTicks").transform;
        ticksGroup.SetParent(clockFaceGroup, false);
        ticksGroup.localPosition = new Vector3(0f, 0f, -0.0042f);

        var tickMat = new Material(toonLit);
        tickMat.SetColor("_BaseColor", new Color(0.15f, 0.10f, 0.05f));

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

        // Dial UI Canvas (Countdown text aperture)
        var canvasObj = new GameObject("DialCanvas");
        canvasObj.transform.SetParent(clockFaceGroup, false);
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

        // Mechanical Hands
        var handsGroup = new GameObject("HandsPivot");
        handsGroup.transform.SetParent(clockFaceGroup, false);
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

        // ==========================================
        // 6. TACTICAL RADAR MAP CONTAINER
        // ==========================================
        var radarGroupObj = new GameObject("TacticalRadarGroup");
        radarGroupObj.transform.SetParent(watchBody, false);
        radarGroupObj.transform.localPosition = new Vector3(0f, 0f, -0.0050f);
        radarGroup = radarGroupObj.transform;

        var radarCanvas = radarGroupObj.AddComponent<Canvas>();
        radarCanvas.renderMode = RenderMode.WorldSpace;
        var rCanvasRt = radarGroupObj.GetComponent<RectTransform>();
        rCanvasRt.sizeDelta = new Vector2(120f, 120f);
        rCanvasRt.localScale = new Vector3(0.00060f, 0.00060f, 1f);

        // Circular Radar Screen (RawImage displaying dynamic texture)
        var displayObj = new GameObject("RadarDisplayScreen");
        displayObj.transform.SetParent(radarGroupObj.transform, false);
        radarDisplay = displayObj.AddComponent<RawImage>();
        var dispRt = displayObj.GetComponent<RectTransform>();
        dispRt.anchoredPosition = Vector2.zero;
        dispRt.sizeDelta = new Vector2(110f, 110f);

        // Top Status Header Text
        var headerObj = new GameObject("RadarHeader");
        headerObj.transform.SetParent(radarGroupObj.transform, false);
        radarStatusText = headerObj.AddComponent<Text>();
        radarStatusText.font = builtinFont;
        radarStatusText.fontSize = 9;
        radarStatusText.fontStyle = FontStyle.Bold;
        radarStatusText.alignment = TextAnchor.MiddleCenter;
        radarStatusText.color = new Color(0.35f, 0.95f, 0.55f);
        radarStatusText.text = "• СЕКТОР СБ •";
        var headerRt = headerObj.GetComponent<RectTransform>();
        headerRt.anchoredPosition = new Vector2(0f, 36f);
        headerRt.sizeDelta = new Vector2(90f, 14f);

        radarGroup = radarGroupObj.transform;
        if (radarTex == null) InitRadarMap();
        if (radarDisplay != null && radarTex != null)
        {
            radarDisplay.texture = radarTex;
        }
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
                "🗺 ТАКТИЧЕСКИЙ РАДАР (СХЕМА НИИ)\n[ЛКМ / ПКМ] — Режим циферблата", badgeStyle);
        }
        else
        {
            badgeStyle.normal.textColor = (timeRemaining <= 60f) ? new Color(1f, 0.3f, 0.3f) : new Color(1f, 0.95f, 0.75f);
            GUI.backgroundColor = new Color(0.12f, 0.09f, 0.06f, 0.92f);
            GUI.Box(new Rect(sw - 380, sh - 90, 360, 42),
                $"⏱ КАРМАННЫЕ ЧАСЫ [{mins:D2}:{secs:D2}]\n[ЛКМ / ПКМ] — Режим карты (Радар)", badgeStyle);
        }

        GUI.backgroundColor = Color.white;
    }
}
