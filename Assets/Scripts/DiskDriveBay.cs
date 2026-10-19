using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controls the Mainframe Removable Disk Drive Bay (Сменные накопители ЕС-5061 / DEC RK05).
/// Simulates thermal expansion, spindle dynamics, safety interlock timing,
/// mouse drag sliding of cartridges, high-voltage electric shock hazard, and head crash failure.
/// </summary>
public class DiskDriveBay : MonoBehaviour
{
    private static DiskDriveBay _instance;
    public static DiskDriveBay Instance
    {
        get
        {
            if (_instance == null) _instance = UnityEngine.Object.FindAnyObjectByType<DiskDriveBay>();
            return _instance;
        }
        private set { _instance = value; }
    }

    public enum DriveBayState
    {
        Off,
        Running,              // Spindle spinning, heating up under computation load
        Braking,              // Dynamic brake engaged, spinning down (1.4s)
        UnlockedReadyToPull,  // Spindle stopped, latch released, safe to pull hot cartridge
        CartridgeExtracted,   // Slot is empty, waiting for cold cartridge to be picked
        InsertingCold,        // Cold cartridge on rails, waiting to be pushed in
        OverheatedCrash       // Thermal limit exceeded (>105°C), data destroyed
    }

    [Header("Thermal Settings")]
    [SerializeField] private float temperature = 32f; // In Celsius
    [SerializeField] private float normalTemp = 35f;
    [SerializeField] private float yellowWarningTemp = 75f;
    [SerializeField] private float redAlarmTemp = 90f;
    [SerializeField] private float headCrashTemp = 105f;
    [SerializeField] private float heatRatePerSec = 1.75f;

    [Header("Drive State")]
    [SerializeField] private DriveBayState state = DriveBayState.Running;
    [SerializeField] private Transform temperatureNeedle;
    [SerializeField] private Transform brakeButton;
    [SerializeField] private Renderer lampRed;
    [SerializeField] private Renderer lampYellow;
    [SerializeField] private Renderer lampGreen;

    [Header("Cartridges")]
    [SerializeField] private CartridgeDraggable hotCartridge;
    [SerializeField] private GameObject coldCartridgeReserve;
    [SerializeField] private CartridgeDraggable coldCartridgeInSlot;

    [Header("Audio")]
    [SerializeField] private AudioSource driveAudio;
    [SerializeField] private AudioSource alarmAudio;
    private AudioClip spindleHumClip;
    private AudioClip brakeWhineClip;
    private AudioClip electricShockClip;
    private AudioClip alarmBeepClip;
    private AudioClip headCrashClip;
    private AudioClip turbineSpinupClip;

    [Header("Materials (Lamps)")]
    private Material ledRedMat;
    private Material ledYellowMat;
    private Material ledGreenMat;
    private Material ledOffMat;

    private bool warnedYellow = false;
    private bool warnedRed = false;
    private Coroutine brakeCoroutine;

    private int currentCartridgeId = 1;
    private int extractedCartridgeId = 0;

    public DriveBayState State => state;
    public float Temperature => temperature;
    public bool IsOverheating => temperature >= yellowWarningTemp;
    public bool IsCriticalOverheat => temperature >= redAlarmTemp;
    public bool IsDrivePowered => MainframeBootQuest.Instance != null && MainframeBootQuest.Instance.CurrentStage == BootStage.SystemOnline;
    public bool IsAlarmBeeping => IsDrivePowered && (temperature >= redAlarmTemp && state == DriveBayState.Running);
    public bool IsCrashNoiseActive { get; private set; }
    public int CurrentCartridgeId => currentCartridgeId;
    public int ExtractedCartridgeId => extractedCartridgeId;

    public void OnServerStandbyOrCut()
    {
        if (alarmAudio != null && alarmAudio.isPlaying)
        {
            alarmAudio.Stop();
        }
        StopSpindleHum();
    }

    private void Awake()
    {
        Instance = this;
        CreateMaterials();
        GenerateAudioClips();
        if (driveAudio == null) driveAudio = GetComponent<AudioSource>();
        if (alarmAudio == null) alarmAudio = driveAudio;
        if (driveAudio != null)
        {
            driveAudio.playOnAwake = false;
            driveAudio.spatialBlend = 1.0f; // 100% 3D spatial
            driveAudio.rolloffMode = AudioRolloffMode.Logarithmic;
            driveAudio.minDistance = 0.8f;
            driveAudio.maxDistance = 8.0f;
            driveAudio.Stop();
        }
        if (alarmAudio != null && alarmAudio != driveAudio)
        {
            alarmAudio.playOnAwake = false;
            alarmAudio.spatialBlend = 1.0f;
            alarmAudio.rolloffMode = AudioRolloffMode.Logarithmic;
            alarmAudio.minDistance = 0.8f;
            alarmAudio.maxDistance = 10.0f;
        }
    }

    private void Start()
    {
        temperature = 35f;
        state = DriveBayState.Running;

        if (hotCartridge != null)
        {
            hotCartridge.gameObject.SetActive(true);
            hotCartridge.SetMode(CartridgeDraggable.SlideMode.PullOut, 0f, true);
            hotCartridge.onCompleted.AddListener(OnHotCartridgeExtracted);
        }

        if (coldCartridgeInSlot != null)
        {
            coldCartridgeInSlot.gameObject.SetActive(false);
            coldCartridgeInSlot.onCompleted.AddListener(OnColdCartridgeLockedIn);
        }

        if (coldCartridgeReserve != null)
        {
            coldCartridgeReserve.SetActive(true);
        }

        if (brakeButton != null)
        {
            var wi = brakeButton.GetComponent<WorldInteractable>();
            if (wi != null)
            {
                wi.onInteract.RemoveListener(PressBrakeButton);
                wi.onInteract.AddListener(PressBrakeButton);
            }
        }

        UpdateLamps();
        UpdateThermometerNeedle();
    }

    private void Update()
    {
        // 1. Thermal simulation during server operation
        bool isServerOnline = MainframeBootQuest.Instance != null && MainframeBootQuest.Instance.CurrentStage == BootStage.SystemOnline;

        if (isServerOnline && state == DriveBayState.Running)
        {
            temperature += heatRatePerSec * Time.deltaTime;

            if (temperature >= yellowWarningTemp && !warnedYellow)
            {
                warnedYellow = true;
                if (MonitorTerminal.Instance != null)
                {
                    MonitorTerminal.Instance.AddLog("<color=#FFFF66>[ВНИМАНИЕ]</color> Повышенная температура дисковода ЕС-5061: 75°C.");
                }
            }

            if (temperature >= redAlarmTemp)
            {
                if (!warnedRed)
                {
                    warnedRed = true;
                    if (MonitorTerminal.Instance != null)
                    {
                        MonitorTerminal.Instance.AddLog("<color=#FF3333><b>[⚠ КРИТИЧЕСКИЙ ПЕРЕГРЕВ ДИСКА]</b></color> 90°C! Угроза запила пластин!");
                    }
                    if (MainframeBootQuest.Instance != null)
                    {
                        MainframeBootQuest.Instance.ShowNotification("<color=#FF3333>[⚠ АВАРИЯ НАКОПИТЕЛЯ]</color> Перегрев 90°C! Срочно замените пакет дисков на сервере!");
                    }
                }

                // Alarm beeping
                if (alarmAudio != null && alarmBeepClip != null && !alarmAudio.isPlaying)
                {
                    alarmAudio.PlayOneShot(alarmBeepClip, 0.65f);
                }
            }

            // Head Crash failure threshold!
            if (temperature >= headCrashTemp)
            {
                TriggerHeadCrash();
            }
        }
        else
        {
            // Rapid cooling when server is offline, in standby, or spindle is stopped/empty
            temperature = Mathf.Max(normalTemp, temperature - 2.5f * Time.deltaTime);

            if (alarmAudio != null && alarmAudio.isPlaying)
            {
                alarmAudio.Stop();
            }

            if (temperature < redAlarmTemp) warnedRed = false;
            if (temperature < yellowWarningTemp) warnedYellow = false;
        }

        UpdateThermometerNeedle();
    }

    private int brakePhase = 0;

    /// <summary>
    /// Player presses [ОСТАНОВ ШПИНДЕЛЯ / BRAKE] button (Cube.107).
    /// </summary>
    public void PressBrakeButton()
    {
        if (state == DriveBayState.Braking)
        {
            MainframeBootQuest.Instance?.ShowNotification("<color=#FFFF66>[ТОРМОЗ (ЖЕЛТЫЙ)]</color> Шпиндель замедляется, дождитесь зеленый сигнал!");
            return;
        }

        if (state == DriveBayState.UnlockedReadyToPull)
        {
            // Spin back up!
            state = DriveBayState.Running;
            brakePhase = 0;
            PlayTurbineSpinup();
            StartSpindleHum();
            UpdateLamps();
            MainframeBootQuest.Instance?.ShowNotification("<color=#FF4444>[ПУСК ШПИНДЕЛЯ (КРАСНЫЙ)]</color> Обороты набраны. Накопитель в работе.");
            return;
        }

        if (state == DriveBayState.Running)
        {
            if (brakeCoroutine != null) StopCoroutine(brakeCoroutine);
            brakeCoroutine = StartCoroutine(BrakeRoutine());
        }
    }

    private IEnumerator BrakeRoutine()
    {
        state = DriveBayState.Braking;
        brakePhase = 1;
        StopSpindleHum();
        PlayBrakeSound();
        UpdateLamps();

        // 0.0s to 0.6s: Spindle dynamic braking (Red)
        yield return new WaitForSeconds(0.6f);

        // 0.6s to 1.5s: Deceleration (Yellow)
        brakePhase = 2;
        UpdateLamps();
        yield return new WaitForSeconds(0.9f);

        // 1.5s: Safely stopped & unlocked! (Green)
        brakePhase = 0;
        state = DriveBayState.UnlockedReadyToPull;
        UpdateLamps();

        if (MainframeBootQuest.Instance != null)
        {
            MainframeBootQuest.Instance.ShowNotification("<color=#00FF88>[РАЗБЛОКИРОВАНО (ЗЕЛЕНЫЙ)]</color> Шпиндель остановлен. Безопасно извлекать носитель.");
        }
    }

    /// <summary>
    /// Triggered if the player attempts to pull the hot cartridge prematurely while 380V is active.
    /// </summary>
    public void AttemptPullPrematurely()
    {
        if (state == DriveBayState.Running || state == DriveBayState.Braking)
        {
            TriggerElectricShock();
        }
    }

    private void TriggerElectricShock()
    {
        PlayElectricShock();

        if (MainframeBootQuest.Instance != null)
        {
            MainframeBootQuest.Instance.ShowNotification("<color=#FF2222><b>⚡ [УДАР ТОКОМ 380В!]</b></color> Срыв замка под напряжением! Контузия оператора!");
        }

        // Stun player through PlayerController
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            player.ApplyStun(3.5f);
        }

        // 12.0m acoustic crash noise spike for 5 seconds
        StartCoroutine(CrashNoiseRoutine(5.0f));

        // Alert guards in corridor with loud electrical arc crack!
        var guards = FindObjectsByType<GuardPawn>(FindObjectsInactive.Exclude);
        if (guards != null)
        {
            foreach (var g in guards)
            {
                if (g != null)
                {
                    g.AddSuspicion(0.45f, "Электрическая дуга / срыв замка накопителя под напряжением 380В");
                }
            }
        }
    }

    private IEnumerator CrashNoiseRoutine(float duration)
    {
        IsCrashNoiseActive = true;
        yield return new WaitForSeconds(duration);
        IsCrashNoiseActive = false;
    }

    private IEnumerator ElectricShockStunRoutine(PlayerController player)
    {
        // Visual flash or brief paralysis
        yield return new WaitForSeconds(3.5f);
    }

    private void OnHotCartridgeExtracted()
    {
        state = DriveBayState.CartridgeExtracted;
        extractedCartridgeId = currentCartridgeId;

        if (hotCartridge != null) hotCartridge.gameObject.SetActive(false);
        if (coldCartridgeInSlot != null) coldCartridgeInSlot.gameObject.SetActive(false);

        UpdateLamps();

        if (MainframeBootQuest.Instance != null)
        {
            MainframeBootQuest.Instance.ShowNotification(
                $"<color=#FFFF66>[КАРТРИДЖ ИЗВЛЕЧЕН]</color> Положите горячий пакет №{extractedCartridgeId} на полку охлаждения.");
        }
    }

    private bool hasCarriedCartridge = false;
    private int carriedCartridgeId = 0;
    private float carriedCartridgeTemp = 30f;

    public bool HasCarriedCartridge => hasCarriedCartridge;
    public int CarriedCartridgeId => carriedCartridgeId;

    /// <summary>
    /// Called when player places the hot cartridge into a cradle on the cooling rack.
    /// </summary>
    public void NotifyCartridgePlacedOnRack()
    {
        if (hasCarriedCartridge)
        {
            MountColdCartridge(carriedCartridgeId, carriedCartridgeTemp);
            hasCarriedCartridge = false;
            carriedCartridgeId = 0;
            MainframeBootQuest.Instance?.ShowNotification(
                $"<color=#00FFAA>[КАРТРИДЖ НА РЕЛЬСАХ]</color> Пакет №{currentCartridgeId} установлен на рельсы слота. Задвиньте его мышью.");
        }
        else
        {
            int otherId = (extractedCartridgeId == 1) ? 2 : 1;
            MainframeBootQuest.Instance?.ShowNotification(
                $"<color=#00FFAA>[ПОЛКА ОХЛАЖДЕНИЯ]</color> Теперь возьмите охлажденный пакет №{otherId} с полки.");
        }
    }

    /// <summary>
    /// Player picks up a cold cartridge from the rack (can be done before or after hot cartridge extraction).
    /// </summary>
    public void PickUpColdCartridge(int cartridgeId, float temp)
    {
        hasCarriedCartridge = true;
        carriedCartridgeId = cartridgeId;
        carriedCartridgeTemp = temp;

        if (state == DriveBayState.CartridgeExtracted)
        {
            MountColdCartridge(carriedCartridgeId, carriedCartridgeTemp);
            hasCarriedCartridge = false;
            carriedCartridgeId = 0;
            MainframeBootQuest.Instance?.ShowNotification(
                $"<color=#00FFAA>[КАРТРИДЖ НА РЕЛЬСАХ]</color> Пакет №{currentCartridgeId} установлен на рельсы слота. Задвиньте его мышью.");
        }
        else
        {
            MainframeBootQuest.Instance?.ShowNotification(
                $"<color=#00FFAA>[КАРТРИДЖ В РУКАХ]</color> Вы взяли охлажденный пакет №{cartridgeId}. Теперь остановите шпиндель [Cube.107] и вытяните горячий диск из сервера.");
        }
    }

    public void ReturnCarriedCartridgeToRack()
    {
        hasCarriedCartridge = false;
        carriedCartridgeId = 0;
    }

    /// <summary>
    /// Called when player takes a cooled cartridge from the cooling rack.
    /// Mounts it onto the server drive bay guide rails ready to be pushed in.
    /// </summary>
    public void MountColdCartridge(int cartridgeId, float temp)
    {
        currentCartridgeId = cartridgeId;
        extractedCartridgeId = 0;
        state = DriveBayState.InsertingCold;

        if (coldCartridgeInSlot != null)
        {
            coldCartridgeInSlot.gameObject.SetActive(true);
            coldCartridgeInSlot.SetMode(CartridgeDraggable.SlideMode.PushIn, 1.0f, false);

            var labelTm = coldCartridgeInSlot.GetComponentInChildren<TextMesh>();
            if (labelTm != null)
            {
                labelTm.text = $"ПАКЕТ ДИСКОВ ЕС-5061 №{cartridgeId}\n[ РЕЗЕРВНЫЙ ХОЛОДНЫЙ ]";
                labelTm.color = (cartridgeId == 1) ? new Color(0.85f, 0.2f, 0.2f) : new Color(0.1f, 0.85f, 0.95f);
            }
        }
    }

    /// <summary>
    /// Backward-compatible trigger for picking cold cartridge from rack.
    /// </summary>
    public void PickColdCartridgeFromRack()
    {
        if (CartridgeCoolingRack.Instance != null)
        {
            CartridgeCoolingRack.Instance.InteractCradle2();
            return;
        }

        if (state != DriveBayState.CartridgeExtracted)
        {
            MainframeBootQuest.Instance?.ShowNotification("Сначала извлеките горячий картридж из слота!");
            return;
        }

        MountColdCartridge(2, 30f);
    }

    private void OnColdCartridgeLockedIn()
    {
        state = DriveBayState.Running;
        temperature = 31f; // Fresh cold platters!
        warnedYellow = false;
        warnedRed = false;
        IsCrashNoiseActive = false;

        // Reset the in-slot cartridge so it becomes the hotCartridge ready for future extractions
        if (hotCartridge != null && coldCartridgeInSlot != null)
        {
            var labelTm = hotCartridge.GetComponentInChildren<TextMesh>();
            if (labelTm != null)
            {
                labelTm.text = $"ПАКЕТ ДИСКОВ ЕС-5061 №{currentCartridgeId}\n[ ОСТОРОЖНО 380В // ГОРЯЧО ]";
                labelTm.color = (currentCartridgeId == 1) ? new Color(0.85f, 0.2f, 0.2f) : new Color(0.1f, 0.85f, 0.95f);
            }
            hotCartridge.gameObject.SetActive(true);
            hotCartridge.SetMode(CartridgeDraggable.SlideMode.PullOut, 0f, true);
            coldCartridgeInSlot.gameObject.SetActive(false);
        }

        UpdateLamps();
        PlayTurbineSpinup();
        StartSpindleHum();

        if (MonitorTerminal.Instance != null)
        {
            MonitorTerminal.Instance.AddLog(
                $"<color=#00FF88>[ДИСКОВОД ЕС-5061]</color> Пакет дисков №{currentCartridgeId} смонтирован. Температура: 31°C. ДАННЫЕ В БЕЗОПАСНОСТИ.");
        }

        if (MainframeBootQuest.Instance != null)
        {
            MainframeBootQuest.Instance.ShowNotification(
                $"★★ <color=#00FF88>ПАКЕТ №{currentCartridgeId} УСПЕШНО ЗАМЕНЕН!</color> Данные спасены от перегрева.");
        }
    }

    private void TriggerHeadCrash()
    {
        state = DriveBayState.OverheatedCrash;
        IsCrashNoiseActive = true;
        StopSpindleHum();
        PlayHeadCrashSound();
        UpdateLamps();

        // Immediately close minigames and modals
        SpyMiniGames.Instance?.CancelMiniGame();
        if (VectorCommander.Instance != null)
        {
            VectorCommander.Instance.CloseHelp();
            VectorCommander.Instance.CloseViewer();
        }

        // Auto-unzoom monitor so player is never trapped
        if (PlayerController.Instance != null && PlayerController.Instance.IsInspectingMonitor)
        {
            PlayerController.Instance.ExitMonitor();
        }

        if (MonitorTerminal.Instance != null)
        {
            MonitorTerminal.Instance.AddLog("<color=#FF0000><b>[КАТАСТРОФА: ЗАПИЛ ДИСКА]</b></color> Физическое разрушение магнитных пластин! Данные дешифровки уничтожены!");
        }

        if (MainframeBootQuest.Instance != null)
        {
            MainframeBootQuest.Instance.ShowNotification("<color=#FF0000><b>[ПРОВАЛ МИССИИ]</b></color> Диск перегрелся и разрушился! Данные стерты.");
        }
    }

    private void UpdateThermometerNeedle()
    {
        if (temperatureNeedle != null)
        {
            // Map 20°C .. 120°C to angle -75° .. +75°
            float t = Mathf.InverseLerp(20f, 120f, temperature);
            float angle = Mathf.Lerp(-75f, 75f, t);

            // Add nervous vibration if critically hot
            if (temperature > yellowWarningTemp)
            {
                angle += UnityEngine.Random.Range(-1.5f, 1.5f);
            }

            temperatureNeedle.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }
    }

    public void UpdateLamps()
    {
        if (lampRed == null || lampYellow == null || lampGreen == null) return;

        bool isRed = (state == DriveBayState.Running) || (brakePhase == 1);
        bool isYellow = (brakePhase == 2);
        bool isGreen = (state == DriveBayState.UnlockedReadyToPull);

        lampRed.sharedMaterial = isRed ? ledRedMat : ledOffMat;
        lampYellow.sharedMaterial = isYellow ? ledYellowMat : ledOffMat;
        lampGreen.sharedMaterial = isGreen ? ledGreenMat : ledOffMat;
    }

    private void CreateMaterials()
    {
        var toonShader = Shader.Find("Custom/SovietToonLit");
        if (toonShader == null) toonShader = Shader.Find("Universal Render Pipeline/Lit");

        ledRedMat = new Material(toonShader);
        ledRedMat.SetColor("_BaseColor", new Color(1.0f, 0.15f, 0.15f));
        ledRedMat.EnableKeyword("_EMISSION");
        ledRedMat.SetColor("_EmissionColor", new Color(1.0f, 0.15f, 0.15f) * 1.5f);

        ledYellowMat = new Material(toonShader);
        ledYellowMat.SetColor("_BaseColor", new Color(1.0f, 0.85f, 0.15f));
        ledYellowMat.EnableKeyword("_EMISSION");
        ledYellowMat.SetColor("_EmissionColor", new Color(1.0f, 0.85f, 0.15f) * 1.5f);

        ledGreenMat = new Material(toonShader);
        ledGreenMat.SetColor("_BaseColor", new Color(0.15f, 1.0f, 0.35f));
        ledGreenMat.EnableKeyword("_EMISSION");
        ledGreenMat.SetColor("_EmissionColor", new Color(0.15f, 1.0f, 0.35f) * 1.5f);

        ledOffMat = new Material(toonShader);
        ledOffMat.SetColor("_BaseColor", new Color(0.15f, 0.18f, 0.16f));
    }

    private void StartSpindleHum()
    {
        // Spindle hum and white noise hiss disabled per user request to keep lab quiet and footsteps audible
        if (driveAudio != null && driveAudio.isPlaying)
        {
            driveAudio.Stop();
        }
    }

    private void StopSpindleHum()
    {
        if (driveAudio != null && driveAudio.isPlaying)
        {
            driveAudio.Stop();
        }
    }

    private void PlayBrakeSound()
    {
        if (driveAudio != null && brakeWhineClip != null)
        {
            driveAudio.PlayOneShot(brakeWhineClip, 0.65f);
        }
    }

    private void PlayTurbineSpinup()
    {
        if (driveAudio != null && turbineSpinupClip != null)
        {
            driveAudio.PlayOneShot(turbineSpinupClip, 0.70f);
        }
    }

    private void PlayElectricShock()
    {
        if (driveAudio != null && electricShockClip != null)
        {
            driveAudio.pitch = UnityEngine.Random.Range(0.92f, 1.08f);
            driveAudio.PlayOneShot(electricShockClip, 0.90f);
        }
    }

    private void PlayHeadCrashSound()
    {
        if (driveAudio != null && headCrashClip != null)
        {
            driveAudio.PlayOneShot(headCrashClip, 0.95f);
        }
    }

    private void GenerateAudioClips()
    {
        int sampleRate = 44100;

        // 1. Spindle hum (high-RPM balanced disk rotation)
        int humSamples = sampleRate * 1;
        float[] humData = new float[humSamples];
        for (int i = 0; i < humSamples; i++)
        {
            float t = (float)i / sampleRate;
            float tone = Mathf.Sin(2f * Mathf.PI * 480f * t) * 0.35f;
            float whirr = Mathf.Sin(2f * Mathf.PI * 960f * t) * 0.15f;
            float air = (UnityEngine.Random.value * 2f - 1f) * 0.05f;
            humData[i] = (tone + whirr + air);
        }
        spindleHumClip = AudioClip.Create("SpindleHumLoop", humSamples, 1, sampleRate, false);
        spindleHumClip.SetData(humData, 0);

        // 2. Brake Whine (descending whine on dynamic brake)
        int brakeSamples = (int)(sampleRate * 1.5f);
        float[] brakeData = new float[brakeSamples];
        for (int i = 0; i < brakeSamples; i++)
        {
            float t = (float)i / sampleRate;
            float freq = Mathf.Lerp(650f, 60f, t / 1.5f);
            float sine = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.5f;
            float env = 1f - (t / 1.5f);
            brakeData[i] = sine * env;
        }
        brakeWhineClip = AudioClip.Create("SpindleBrakeWhine", brakeSamples, 1, sampleRate, false);
        brakeWhineClip.SetData(brakeData, 0);

        // 3. Electric Shock Arc (380V violent electrical discharge crack)
        int shockSamples = (int)(sampleRate * 0.28f);
        float[] shockData = new float[shockSamples];
        for (int i = 0; i < shockSamples; i++)
        {
            float t = (float)i / shockSamples;
            float crack = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-t * 22f);
            float hum = Mathf.Sin(2f * Mathf.PI * 50f * ((float)i / sampleRate)) * 0.6f * Mathf.Exp(-t * 8f);
            shockData[i] = crack + hum;
        }
        electricShockClip = AudioClip.Create("ElectricShock380V", shockSamples, 1, sampleRate, false);
        electricShockClip.SetData(shockData, 0);

        // 4. Alarm Beep (high-pitched urgency beep)
        int beepSamples = (int)(sampleRate * 0.18f);
        float[] beepData = new float[beepSamples];
        for (int i = 0; i < beepSamples; i++)
        {
            float t = (float)i / sampleRate;
            float sine = Mathf.Sin(2f * Mathf.PI * 1850f * t) * 0.6f;
            float env = Mathf.Sin(Mathf.PI * (t / 0.18f));
            beepData[i] = sine * env;
        }
        alarmBeepClip = AudioClip.Create("DiskOverheatAlarmBeep", beepSamples, 1, sampleRate, false);
        alarmBeepClip.SetData(beepData, 0);

        // 5. Head Crash (terrible metallic gouging screech)
        int crashSamples = (int)(sampleRate * 1.2f);
        float[] crashData = new float[crashSamples];
        for (int i = 0; i < crashSamples; i++)
        {
            float t = (float)i / crashSamples;
            float screech = Mathf.Sin(2f * Mathf.PI * UnityEngine.Random.Range(1800f, 3200f) * ((float)i / sampleRate)) * 0.7f;
            float noise = (UnityEngine.Random.value * 2f - 1f) * 0.5f;
            crashData[i] = (screech + noise) * (1f - t);
        }
        headCrashClip = AudioClip.Create("DiskHeadCrash", crashSamples, 1, sampleRate, false);
        headCrashClip.SetData(crashData, 0);

        // 6. Turbine spinup (ascending whine on fresh cartridge lock)
        int spinSamples = (int)(sampleRate * 1.8f);
        float[] spinData = new float[spinSamples];
        for (int i = 0; i < spinSamples; i++)
        {
            float t = (float)i / sampleRate;
            float freq = Mathf.Lerp(120f, 980f, t / 1.8f);
            float sine = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.55f;
            spinData[i] = sine;
        }
        turbineSpinupClip = AudioClip.Create("TurbineSpinup", spinSamples, 1, sampleRate, false);
        turbineSpinupClip.SetData(spinData, 0);
    }
}
