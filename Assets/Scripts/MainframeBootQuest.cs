using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum BootStage
{
    PowerOff,
    BreakerOn,
    FilamentWarming,
    FilamentReady,
    HighVoltageOn,
    ReadyForIPL,
    SystemBooting,
    SystemOnline,
    HotStandby
}

/// <summary>
/// Master quest manager for booting the retro mainframe.
/// Coordinates breaker switch, tube warmup, front panel address switches,
/// tape drive, and the final monitor terminal bootup.
public class MainframeBootQuest : MonoBehaviour
{
    private static MainframeBootQuest _instance;
    public static MainframeBootQuest Instance
    {
        get
        {
            if (_instance == null) _instance = UnityEngine.Object.FindAnyObjectByType<MainframeBootQuest>();
            return _instance;
        }
        private set { _instance = value; }
    }

    [Header("Quest State")]
    [SerializeField] private BootStage currentStage = BootStage.PowerOff;
    [SerializeField] private bool hasTapeInInventory = false;
    [SerializeField] private bool isTapeInserted = false;
    [SerializeField] private bool[] addressSwitches = new bool[4] { false, false, false, false };
    private readonly bool[] targetCode = new bool[4] { true, false, true, true }; // 1-0-1-1
    private bool isPowerCutByBreaker = false;
    private BootStage stageBeforeBreakerCut = BootStage.PowerOff;

    public bool IsPowerCutByBreaker => isPowerCutByBreaker;

    [Header("Breaker (Cube.009)")]
    [SerializeField] private Transform breakerHandle;
    [SerializeField] private Renderer breakerLed;
    [SerializeField] private WorldInteractable breakerInteractable;

    [Header("Locker & Tape (Cube.010)")]
    [SerializeField] private GameObject lockerTapeProp;
    [SerializeField] private GameObject serverTapeProp;
    [SerializeField] private WorldInteractable tapePickupInteractable;
    [SerializeField] private WorldInteractable tapeInsertInteractable;
    [SerializeField] private WorldInteractable manualInteractable;

    [Header("Servers (Cube.006) - Tubes & Power")]
    [SerializeField] private Light filamentPointLight;
    [SerializeField] private Renderer[] tubeRenderers;
    [SerializeField] private Transform filamentSwitch;
    [SerializeField] private Transform hvSwitch;
    [SerializeField] private Renderer filamentStatusLed;
    [SerializeField] private Renderer hvStatusLed;
    [SerializeField] private WorldInteractable filamentInteractable;
    [SerializeField] private WorldInteractable hvInteractable;

    [Header("Servers - Front Panel")]
    [SerializeField] private Transform[] switchTransforms = new Transform[4];
    [SerializeField] private Renderer[] switchLeds = new Renderer[4];
    [SerializeField] private WorldInteractable[] switchInteractables = new WorldInteractable[4];
    [SerializeField] private Transform iplButton;
    [SerializeField] private WorldInteractable iplInteractable;
    [SerializeField] private Renderer[] registerLeds = new Renderer[8];

    [Header("Terminal & Audio")]
    [SerializeField] private MonitorTerminal monitorTerminal;
    [SerializeField] private AudioSource questAudioSource;

    [Header("UI Overlay - Engineer Manual & Notifications")]
    [SerializeField] private bool isReadingManual = false;
    [Tooltip("Показывать ли всплывающий текстовый баннер сверху экрана")]
    [SerializeField] private bool showTopBannerNotifications = false;

    // Materials cache for LEDs
    private Material ledRedMat;
    private Material ledGreenMat;
    private Material ledOffMat;
    private Material ledYellowMat;
    private Material tubeGlowMat;

    private Coroutine warmupCoroutine;
    private Coroutine registerBlinkCoroutine;
    private string onScreenNotification = "";
    private float notificationTimer = 0f;

    private Vector3 filamentBaseRot = new Vector3(0f, 134.13f, 0f);
    private Vector3 hvBaseRot = new Vector3(0f, 143.29f, 0f);
    private Vector3 filamentBasePos = Vector3.zero;
    private Vector3 hvBasePos = Vector3.zero;
    private bool basesCached = false;

    private void EnsureSwitchBases()
    {
        if (basesCached) return;
        if (filamentSwitch != null)
        {
            filamentBasePos = filamentSwitch.localPosition;
            if (filamentSwitch.localEulerAngles.y > 10f)
            {
                filamentBaseRot = new Vector3(0f, filamentSwitch.localEulerAngles.y, 0f);
            }
        }
        if (hvSwitch != null)
        {
            hvBasePos = hvSwitch.localPosition;
            if (hvSwitch.localEulerAngles.y > 10f)
            {
                hvBaseRot = new Vector3(0f, hvSwitch.localEulerAngles.y, 0f);
            }
        }
        basesCached = true;
    }

    public BootStage CurrentStage => currentStage;
    public bool IsReadingManual => isReadingManual;
    public bool HasTape => hasTapeInInventory;

    public bool IsNoiseActive
    {
        get
        {
            if (isPowerCutByBreaker) return false;
            if (DiskDriveBay.Instance != null && (DiskDriveBay.Instance.IsCrashNoiseActive || DiskDriveBay.Instance.IsAlarmBeeping))
                return true;
            return (currentStage == BootStage.HighVoltageOn || currentStage == BootStage.ReadyForIPL || currentStage == BootStage.SystemBooting || currentStage == BootStage.SystemOnline);
        }
    }

    public float NoiseRadius
    {
        get
        {
            if (isPowerCutByBreaker) return 0f;
            if (DiskDriveBay.Instance != null && DiskDriveBay.Instance.IsCrashNoiseActive)
                return 12.0f;
            if (DiskDriveBay.Instance != null && DiskDriveBay.Instance.IsAlarmBeeping)
                return 5.8f;
            if (currentStage == BootStage.HighVoltageOn || currentStage == BootStage.ReadyForIPL || currentStage == BootStage.SystemBooting || currentStage == BootStage.SystemOnline)
                return 3.2f;
            return 0f;
        }
    }

    public string CurrentNoiseSource
    {
        get
        {
            if (isPowerCutByBreaker)
                return "ЭВМ обесточена рубильником (Тишина, 0м)";
            if (DiskDriveBay.Instance != null && DiskDriveBay.Instance.IsCrashNoiseActive)
                return "Аварийный запил / электродуга накопителя (12.0м)";
            if (DiskDriveBay.Instance != null && DiskDriveBay.Instance.IsAlarmBeeping)
                return "Зуммер перегрева диска 90°C (5.8м)";
            if (currentStage == BootStage.HighVoltageOn || currentStage == BootStage.ReadyForIPL || currentStage == BootStage.SystemBooting || currentStage == BootStage.SystemOnline)
                return "Штатный гул трансформатора ЭВМ (3.2м)";
            return "Штатный режим (Тишина, 0м)";
        }
    }

    private AudioSource humAudioSource;
    private AudioSource breakerAudioSource;
    private AudioClip humLoopClip;
    private AudioClip relayClackClip;
    private AudioClip breakerSwitchClip;
    private AudioClip lampPowerClip;
    private AudioClip switchClick1Clip;
    private AudioClip switchClick2Clip;
    private AudioClip tapeInsertClip;
    private AudioClip errorAlarmClip;

    private float manualCooldownTimer = 0f;

    private void Awake()
    {
        Instance = this;

        currentStage = BootStage.PowerOff;
        hasTapeInInventory = false;
        isTapeInserted = false;
        addressSwitches = new bool[4] { false, false, false, false };

        CreateMaterials();
        SetupAudio();
        EnsureSwitchBases();
    }

    private void Start()
    {
        BindInteractables();
        UpdateAllVisuals();
        ShowNotification("ЭВМ обесточена. Найдите документацию в шкафчике или осмотрите щиток.");
    }

    private void BindInteractables()
    {
        if (breakerInteractable != null)
        {
            breakerInteractable.onInteract.RemoveListener(ToggleBreaker);
            breakerInteractable.onInteract.AddListener(ToggleBreaker);
        }
        if (filamentInteractable != null)
        {
            filamentInteractable.onInteract.RemoveListener(ToggleFilament);
            filamentInteractable.onInteract.AddListener(ToggleFilament);
        }
        if (hvInteractable != null)
        {
            hvInteractable.onInteract.RemoveListener(ToggleHighVoltage);
            hvInteractable.onInteract.AddListener(ToggleHighVoltage);
        }
        if (iplInteractable != null)
        {
            iplInteractable.onInteract.RemoveListener(PressIPL);
            iplInteractable.onInteract.AddListener(PressIPL);
        }
        if (switchInteractables != null)
        {
            for (int i = 0; i < switchInteractables.Length; i++)
            {
                int idx = i;
                if (switchInteractables[i] != null)
                {
                    switchInteractables[i].onInteract.RemoveAllListeners();
                    switchInteractables[i].onInteract.AddListener(() => ToggleAddressSwitch(idx));
                }
            }
        }
        if (tapePickupInteractable != null)
        {
            tapePickupInteractable.onInteract.RemoveListener(TakeTape);
            tapePickupInteractable.onInteract.AddListener(TakeTape);
        }
        if (tapeInsertInteractable != null)
        {
            tapeInsertInteractable.onInteract.RemoveListener(InsertTape);
            tapeInsertInteractable.onInteract.AddListener(InsertTape);
        }
        if (manualInteractable != null)
        {
            manualInteractable.onInteract.RemoveListener(OpenManual);
            manualInteractable.onInteract.AddListener(OpenManual);
        }
    }

    private void Update()
    {
        if (manualCooldownTimer > 0f)
        {
            manualCooldownTimer -= Time.deltaTime;
        }

        if (notificationTimer > 0f)
        {
            notificationTimer -= Time.deltaTime;
            if (notificationTimer <= 0f) onScreenNotification = "";
        }

        // Close manual with E or Esc
        if (isReadingManual)
        {
            bool closeManual = false;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)) closeManual = true;
#else
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E)) closeManual = true;
#endif
            if (closeManual) ToggleManual(false);
        }
    }

    private void CreateMaterials()
    {
        var urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) urpLit = Shader.Find("Standard");

        ledRedMat = new Material(urpLit);
        ledRedMat.SetColor("_BaseColor", new Color(1f, 0.1f, 0.1f));
        ledRedMat.SetColor("_EmissionColor", new Color(1f, 0.1f, 0.1f) * 2.5f);
        ledRedMat.EnableKeyword("_EMISSION");

        ledGreenMat = new Material(urpLit);
        ledGreenMat.SetColor("_BaseColor", new Color(0.1f, 1f, 0.2f));
        ledGreenMat.SetColor("_EmissionColor", new Color(0.1f, 1f, 0.2f) * 2.5f);
        ledGreenMat.EnableKeyword("_EMISSION");

        ledYellowMat = new Material(urpLit);
        ledYellowMat.SetColor("_BaseColor", new Color(1f, 0.9f, 0.1f));
        ledYellowMat.SetColor("_EmissionColor", new Color(1f, 0.9f, 0.1f) * 2.0f);
        ledYellowMat.EnableKeyword("_EMISSION");

        ledOffMat = new Material(urpLit);
        ledOffMat.SetColor("_BaseColor", new Color(0.12f, 0.12f, 0.12f));

        tubeGlowMat = new Material(urpLit);
        tubeGlowMat.SetColor("_BaseColor", new Color(0.35f, 0.35f, 0.38f));
        tubeGlowMat.SetColor("_EmissionColor", Color.black);
        tubeGlowMat.DisableKeyword("_EMISSION");
    }

    // ==========================================
    // INTERACTION ACTIONS
    // ==========================================

    public void ToggleBreaker()
    {
        PlayBreakerSound();

        if (currentStage == BootStage.PowerOff)
        {
            currentStage = BootStage.BreakerOn;
            isPowerCutByBreaker = false;
            ShowNotification("Главный рубильник ВКЛЮЧЕН. Силовая сеть под напряжением.");
        }
        else if (currentStage == BootStage.BreakerOn)
        {
            currentStage = BootStage.PowerOff;
            isPowerCutByBreaker = true;
            ShowNotification("Главный рубильник ВЫКЛЮЧЕН. Питание обесточено.");
        }
        else
        {
            // System is warming up, online, or in standby
            if (!isPowerCutByBreaker)
            {
                // Pulling breaker down cuts power to the room!
                isPowerCutByBreaker = true;
                stageBeforeBreakerCut = currentStage;
                StopTransformerHum();
                ShowNotification("<color=#FFFF00>[СИЛОВАЯ СЕТЬ ОБЕСТОЧЕНА]</color> Рубильник опущен! ЭВМ выключена, терминал погас. Быстро прячьтесь за стойку!");

                if (PlayerController.Instance != null && PlayerController.Instance.IsInspectingMonitor)
                {
                    PlayerController.Instance.ExitMonitor();
                }

                if (MonitorTerminal.Instance != null)
                {
                    MonitorTerminal.Instance.SetStandbyState(true);
                    MonitorTerminal.Instance.SetMainframeOnline(false);
                }
            }
            else
            {
                // Raising breaker restores power!
                isPowerCutByBreaker = false;
                currentStage = (stageBeforeBreakerCut != BootStage.PowerOff) ? stageBeforeBreakerCut : BootStage.BreakerOn;
                if (currentStage >= BootStage.HighVoltageOn && currentStage != BootStage.HotStandby)
                {
                    StartTransformerHum();
                }
                ShowNotification("<color=#00FFAA>[ПИТАНИЕ ВОССТАНОВЛЕНО]</color> Рубильник поднят! Электросеть активна.");

                if (MonitorTerminal.Instance != null)
                {
                    MonitorTerminal.Instance.SetStandbyState(false);
                    MonitorTerminal.Instance.SetMainframeOnline(currentStage == BootStage.SystemOnline);
                }
            }
        }

        UpdateAllVisuals();
    }

    public void ToggleFilament()
    {
        if (currentStage < BootStage.BreakerOn || isPowerCutByBreaker)
        {
            ShowNotification("[ОТКАЗ] Нет питания в электросети! Сначала включите рубильник на щитке.");
            return;
        }

        PlaySwitchClickSound(0);

        if (currentStage == BootStage.BreakerOn)
        {
            PlayLampPowerSound();
            currentStage = BootStage.FilamentWarming;
            if (warmupCoroutine != null) StopCoroutine(warmupCoroutine);
            warmupCoroutine = StartCoroutine(WarmupFilamentRoutine());
        }
        else if (currentStage >= BootStage.FilamentReady)
        {
            ShowNotification("Накал ламп уже прогрет и стабилен.");
        }

        UpdateAllVisuals();
    }

    private IEnumerator WarmupFilamentRoutine()
    {
        ShowNotification("Подан ток накала. Прогрев вакуумных ламп...");
        float duration = 3.2f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            if (filamentPointLight != null)
            {
                filamentPointLight.intensity = Mathf.Lerp(0f, 2.2f, t);
            }

            if (tubeGlowMat != null)
            {
                tubeGlowMat.EnableKeyword("_EMISSION");
                tubeGlowMat.SetColor("_BaseColor", Color.Lerp(new Color(0.35f, 0.35f, 0.38f), new Color(1f, 0.55f, 0.15f), t));
                tubeGlowMat.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.08f) * (t * 2.8f));
            }

            yield return null;
        }

        currentStage = BootStage.FilamentReady;
        ShowNotification("★ Лампы прогреты! Можно подавать высокое анодное напряжение (HV).");
        UpdateAllVisuals();
    }

    public void ToggleHighVoltage()
    {
        PlayBreakerSound();

        if (currentStage == BootStage.SystemOnline)
        {
            // Enter Hot Standby: Cut HV, cut hum, keep filaments warm!
            currentStage = BootStage.HotStandby;
            StopTransformerHum();
            PlayRelaySound();
            if (DiskDriveBay.Instance != null) DiskDriveBay.Instance.OnServerStandbyOrCut();
            if (monitorTerminal != null) monitorTerminal.SetStandbyState(true);
            ShowNotification("<color=#FFCC00>[ГОТОВНОСТЬ]</color> Высокое напряжение снято (Standby). ЭВМ обесшумлена, накал активен.");
            UpdateAllVisuals();
            return;
        }

        if (currentStage == BootStage.HotStandby)
        {
            // Fast resume in 0.4s!
            currentStage = BootStage.SystemOnline;
            PlayRelaySound();
            StartTransformerHum();
            if (monitorTerminal != null) monitorTerminal.SetStandbyState(false);
            if (registerBlinkCoroutine != null) StopCoroutine(registerBlinkCoroutine);
            registerBlinkCoroutine = StartCoroutine(RegisterBlinkRoutine());
            ShowNotification("<color=#00FF88>[ОНЛАЙН]</color> Анодное напряжение подано. ЭВМ мгновенно в строю.");
            UpdateAllVisuals();
            return;
        }

        if (currentStage < BootStage.FilamentReady)
        {
            ShowNotification("[ОПАСНОСТЬ] Нельзя подавать анодное напряжение на холодные лампы!");
            return;
        }

        if (currentStage == BootStage.FilamentReady)
        {
            currentStage = BootStage.HighVoltageOn;
            PlayRelaySound();
            ShowNotification("Высокое напряжение подано. Вычислительный блок активирован.");
            UpdateAllVisuals();
        }
        else
        {
            ShowNotification("Высокое напряжение уже подано.");
        }
    }

    public void TakeTape()
    {
        if (hasTapeInInventory || isTapeInserted)
        {
            ShowNotification("Бобина магнитной ленты уже у вас.");
            return;
        }

        hasTapeInInventory = true;
        if (lockerTapeProp != null) lockerTapeProp.SetActive(false);
        ShowNotification("Получена бобина с ядром ОС [DECODER-KERNEL-TAPE]. Вставьте её в дисковод серверов.");
    }

    public void InsertTape()
    {
        if (isTapeInserted)
        {
            ShowNotification("Бобина с ядром ОС уже установлена в накопитель.");
            return;
        }

        if (!hasTapeInInventory)
        {
            ShowNotification("[НЕТ НОСИТЕЛЯ] Сначала найдите бобину магнитной ленты на шкафчике.");
            return;
        }

        hasTapeInInventory = false;
        isTapeInserted = true;
        PlayTapeInsertSound();
        if (serverTapeProp != null) serverTapeProp.SetActive(true);
        if (tapeInsertInteractable != null)
        {
            tapeInsertInteractable.PromptText = "Бобина с ядром ОС установлена [DECODER-KERNEL]";
            tapeInsertInteractable.IsInteractable = false;
        }
        ShowNotification("Бобина магнитной ленты установлена в накопитель ЭВМ.");
    }

    public void ToggleAddressSwitch(int index)
    {
        if (index < 0 || index >= addressSwitches.Length) return;

        addressSwitches[index] = !addressSwitches[index];
        PlaySwitchClickSound(index);

        string stateStr = addressSwitches[index] ? "ВКЛ [1]" : "ВЫКЛ [0]";
        ShowNotification($"Тумблер шины #{index + 1}: {stateStr}");

        UpdateFrontPanelSwitches();

        // If server & PC are already online, tampering with the bus switches causes a bus parity fault / crash!
        if (currentStage == BootStage.SystemOnline)
        {
            TriggerBusAddressFault();
        }
    }

    public void TriggerBusAddressFault()
    {
        PlayErrorAlarmSound();
        currentStage = BootStage.HighVoltageOn;

        if (registerBlinkCoroutine != null)
        {
            StopCoroutine(registerBlinkCoroutine);
            registerBlinkCoroutine = null;
        }
        if (registerLeds != null)
        {
            foreach (var r in registerLeds)
            {
                if (r != null) r.sharedMaterial = ledOffMat;
            }
        }

        if (monitorTerminal != null)
        {
            monitorTerminal.SetMainframeOnline(false);
            monitorTerminal.AddLog("<color=#FF0000><b>[BUS PARITY ERROR 0x07: KERNEL PANIC]</b></color>");
            monitorTerminal.AddLog("<color=#FF6666>[HARDWARE FAULT] Нарушена адресация шины во время исполнения!</color>");
            monitorTerminal.AddLog("<color=#FFCC00>Восстановите тумблеры шины адреса (1-0-1-1) и повторно нажмите [START IPL].</color>");
        }

        if (PlayerController.Instance != null && PlayerController.Instance.IsInspectingMonitor)
        {
            PlayerController.Instance.ExitMonitor();
        }

        ShowNotification("<color=#FF3333><b>[АВАРИЯ: СБОЙ ШИНЫ АДРЕСА]</b></color> Сбита адресация ЭВМ на ходу! Терминал отключен. Верните 1-0-1-1 и нажмите [START IPL].");
        UpdateAllVisuals();
    }

    public void ToggleAddressSwitch0() => ToggleAddressSwitch(0);
    public void ToggleAddressSwitch1() => ToggleAddressSwitch(1);
    public void ToggleAddressSwitch2() => ToggleAddressSwitch(2);
    public void ToggleAddressSwitch3() => ToggleAddressSwitch(3);
    public void OpenManual()
    {
        if (manualCooldownTimer > 0f) return;
        ToggleManual(true);
    }

    public void PressIPL()
    {
        if (currentStage < BootStage.HighVoltageOn)
        {
            ShowNotification("[ОТКАЗ] Мейнфрейм не запитан! Пройдите шаги включения питания и накала.");
            return;
        }

        if (!isTapeInserted)
        {
            ShowNotification("[ОШИБКА IPL] Накопитель пуст! Вставьте бобину магнитной ленты из шкафчика.");
            return;
        }

        // Validate code 1-0-1-1
        bool codeCorrect = true;
        for (int i = 0; i < 4; i++)
        {
            if (addressSwitches[i] != targetCode[i]) codeCorrect = false;
        }

        if (!codeCorrect)
        {
            ShowNotification("[ОШИБКА АДРЕСА] Неверный код шины! Сверьтесь с инструкцией инженера в шкафчике.");
            return;
        }

        if (currentStage != BootStage.SystemOnline && currentStage != BootStage.SystemBooting)
        {
            StartCoroutine(BootSystemRoutine());
        }
    }

    private IEnumerator BootSystemRoutine()
    {
        currentStage = BootStage.SystemBooting;
        ShowNotification("★ НАЧАЛЬНАЯ ЗАГРУЗКА (IPL)... Чтение ядра с магнитной ленты!");

        // Start dancing register LEDs
        if (registerBlinkCoroutine != null) StopCoroutine(registerBlinkCoroutine);
        registerBlinkCoroutine = StartCoroutine(RegisterBlinkRoutine());

        yield return new WaitForSeconds(2.8f);

        currentStage = BootStage.SystemOnline;
        PlayRelaySound();
        StartTransformerHum();
        ShowNotification("★★ СИСТЕМА УСПЕШНО ЗАПУЩЕНА! Подойдите к монитору на столе.");

        if (monitorTerminal != null)
        {
            monitorTerminal.SetMainframeOnline(true);
            monitorTerminal.AddLog("<color=#00FFFF>[IPL SUCCESS] DECODER MAINFRAME ONLINE</color>");
            monitorTerminal.AddLog("<color=#72D572>[CORE] OS/70 Kernel loaded from magnetic tape drive.</color>");
            monitorTerminal.AddLog("[CORE] Workstation terminal link established.");
        }

        UpdateAllVisuals();
    }

    private IEnumerator RegisterBlinkRoutine()
    {
        while (currentStage == BootStage.SystemBooting || currentStage == BootStage.SystemOnline)
        {
            if (registerLeds != null)
            {
                foreach (var r in registerLeds)
                {
                    if (r != null)
                    {
                        bool on = Random.value > 0.45f;
                        r.sharedMaterial = on ? ledYellowMat : ledOffMat;
                    }
                }
            }
            yield return new WaitForSeconds(Random.Range(0.06f, 0.14f));
        }

        // Extinguish LEDs in standby or off
        if (registerLeds != null)
        {
            foreach (var r in registerLeds)
            {
                if (r != null) r.sharedMaterial = ledOffMat;
            }
        }
    }

    public void ToggleManual(bool show)
    {
        isReadingManual = show;
        if (show)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            manualCooldownTimer = 0.45f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void ShowNotification(string msg)
    {
        if (!showTopBannerNotifications) return;
        onScreenNotification = msg;
        notificationTimer = 4.0f;
    }

    // ==========================================
    // VISUALS UPDATER
    // ==========================================

    public void UpdateAllVisuals()
    {
        // 1. Breaker
        bool isBreakerUp = (currentStage >= BootStage.BreakerOn && !isPowerCutByBreaker);
        if (breakerHandle != null)
        {
            breakerHandle.localRotation = Quaternion.Euler(isBreakerUp ? -35f : 35f, 0f, 0f);
        }
        if (breakerLed != null)
        {
            breakerLed.sharedMaterial = isBreakerUp ? ledGreenMat : ledRedMat;
        }
        if (breakerInteractable != null)
        {
            breakerInteractable.PromptText = isBreakerUp ? "Нажмите [E] — Отключить главный рубильник питания" : "Нажмите [E] — Включить главный рубильник питания";
        }

        EnsureSwitchBases();

        // 2. Filament switch (Cube.099)
        bool filamentOn = currentStage >= BootStage.FilamentWarming && !isPowerCutByBreaker;
        if (filamentSwitch != null)
        {
            float pitch = filamentOn ? -22f : 0f;
            filamentSwitch.localRotation = Quaternion.Euler(pitch, filamentBaseRot.y, filamentBaseRot.z);
            if (filamentBasePos != Vector3.zero)
            {
                var pos = filamentBasePos;
                if (filamentOn) pos.y += 0.005f;
                filamentSwitch.localPosition = pos;
            }
        }
        if (filamentStatusLed != null)
        {
            filamentStatusLed.sharedMaterial = (currentStage >= BootStage.FilamentReady && !isPowerCutByBreaker) ? ledGreenMat :
                                               ((currentStage == BootStage.FilamentWarming && !isPowerCutByBreaker) ? ledYellowMat : ledOffMat);
        }

        // 3. High Voltage switch (Cube.101)
        bool hvActive = (currentStage >= BootStage.HighVoltageOn && currentStage != BootStage.HotStandby && !isPowerCutByBreaker);
        if (hvSwitch != null)
        {
            float pitch = hvActive ? -22f : 0f;
            hvSwitch.localRotation = Quaternion.Euler(pitch, hvBaseRot.y, hvBaseRot.z);
            if (hvBasePos != Vector3.zero)
            {
                var pos = hvBasePos;
                if (hvActive) pos.y += 0.005f;
                hvSwitch.localPosition = pos;
            }
        }
        if (hvInteractable != null)
        {
            if (currentStage == BootStage.SystemOnline)
                hvInteractable.PromptText = "Нажмите [E] — Снять анодное напряжение (Горячий резерв / Standby)";
            else if (currentStage == BootStage.HotStandby)
                hvInteractable.PromptText = "Нажмите [E] — Подать анодное напряжение (Быстрый запуск ЭВМ)";
            else if (currentStage == BootStage.FilamentReady)
                hvInteractable.PromptText = "Нажмите [E] — Подать высокое напряжение (HV)";
            else
                hvInteractable.PromptText = "Тумблер высокого напряжения (HV)";
        }

        // 4. Tube glow
        bool isWarm = currentStage >= BootStage.FilamentReady && !isPowerCutByBreaker;
        bool isWarming = currentStage == BootStage.FilamentWarming && !isPowerCutByBreaker;
        if (filamentPointLight != null)
        {
            filamentPointLight.enabled = isWarm || isWarming;
            if (isWarm) filamentPointLight.intensity = 2.2f;
        }
        if (tubeRenderers != null && tubeGlowMat != null)
        {
            if (isWarm)
            {
                tubeGlowMat.SetColor("_BaseColor", new Color(1f, 0.55f, 0.15f));
                tubeGlowMat.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.08f) * 2.8f);
                tubeGlowMat.EnableKeyword("_EMISSION");
            }
            else if (!isWarming)
            {
                // Cold filament / unpowered: cold tungsten metal, NO emission, NOT orange
                tubeGlowMat.SetColor("_BaseColor", new Color(0.35f, 0.35f, 0.38f));
                tubeGlowMat.SetColor("_EmissionColor", Color.black);
                tubeGlowMat.DisableKeyword("_EMISSION");
            }

            foreach (var tr in tubeRenderers)
            {
                if (tr != null) tr.sharedMaterial = tubeGlowMat;
            }
        }

        // 5. Tape props
        if (lockerTapeProp != null) lockerTapeProp.SetActive(!hasTapeInInventory && !isTapeInserted);
        if (serverTapeProp != null) serverTapeProp.SetActive(isTapeInserted);
        if (tapeInsertInteractable != null && isTapeInserted)
        {
            tapeInsertInteractable.PromptText = "Бобина с ядром ОС установлена [DECODER-KERNEL]";
            tapeInsertInteractable.IsInteractable = false;
        }

        // 6. Front panel switches
        UpdateFrontPanelSwitches();
    }

    private void UpdateFrontPanelSwitches()
    {
        for (int i = 0; i < 4; i++)
        {
            if (switchTransforms != null && i < switchTransforms.Length && switchTransforms[i] != null)
            {
                // Toggle matches user reference: 0° OFF -> 30° ON, lowered on Y from 1.718m to 1.710m
                float xAngle = addressSwitches[i] ? 30f : 0f;
                switchTransforms[i].localRotation = Quaternion.Euler(xAngle, 133f, 329f);
                var pos = switchTransforms[i].localPosition;
                pos.y = addressSwitches[i] ? 1.710f : 1.718f;
                switchTransforms[i].localPosition = pos;
            }
            if (switchLeds != null && i < switchLeds.Length && switchLeds[i] != null)
            {
                switchLeds[i].sharedMaterial = addressSwitches[i] ? ledRedMat : ledOffMat;
            }
        }
    }

    // ==========================================
    // ONGUI OVERLAY (Manual & Notification)
    // ==========================================

    private void OnGUI()
    {
        GUIFontHelper.Apply();
        float sw = Screen.width;
        float sh = Screen.height;

        // Notification banner (only in room view, not when zoomed into monitor)
        bool isZoomedOnMonitor = MonitorTerminal.Instance != null && MonitorTerminal.Instance.IsZoomedIn;
        if (showTopBannerNotifications && !string.IsNullOrEmpty(onScreenNotification) && !isReadingManual && !isZoomedOnMonitor)
        {
            var notifStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            notifStyle.normal.textColor = Color.white;
            GUI.backgroundColor = new Color(0.05f, 0.15f, 0.25f, 0.92f);
            GUI.Box(new Rect(sw / 2 - 280, 20, 560, 42), onScreenNotification, notifStyle);
            GUI.backgroundColor = Color.white;
        }

        // Engineer Manual Overlay
        if (isReadingManual)
        {
            DrawManualOverlay(sw, sh);
        }
    }

    private void DrawManualOverlay(float sw, float sh)
    {
        // Dark translucent background
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float w = 680f;
        float h = 540f;
        Rect manualRect = new Rect(sw / 2 - w / 2, sh / 2 - h / 2, w, h);

        GUI.color = new Color(0.96f, 0.93f, 0.83f, 1f); // Solid aged paper
        GUI.DrawTexture(manualRect, Texture2D.whiteTexture);
        GUI.color = new Color(0.4f, 0.3f, 0.2f, 1f); // Border
        GUI.Box(manualRect, GUIContent.none);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(manualRect.x + 25, manualRect.y + 18, w - 50, h - 36));

        var headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        headerStyle.normal.textColor = new Color(0.15f, 0.1f, 0.05f);
        GUILayout.Label("МИНИСТЕРСТВО РАДИОПРОМЫШЛЕННОСТИ\nВЫЧИСЛИТЕЛЬНЫЙ КОМПЛЕКС «ДЕКОДЕР-70»", headerStyle);

        var subStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        subStyle.normal.textColor = new Color(0.5f, 0.1f, 0.05f);
        GUILayout.Label("СЛУЖЕБНАЯ ИНСТРУКЦИЯ ПО ХОЛОДНОМУ ПУСКУ (1974 г.)", subStyle);
        GUILayout.Space(10);

        var bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            wordWrap = true
        };
        bodyStyle.normal.textColor = new Color(0.15f, 0.15f, 0.15f);

        string manualText =
            "<b>1. ЭЛЕКТРОПИТАНИЕ:</b>\n" +
            "   Подать напряжение на силовом щитке на стене (Cube.009), переведя главный рубильник вверх.\n\n" +
            "<b>2. ПРОГРЕВ ВАКУУМНЫХ ЛАМП ГЕНЕРАТОРА (СЕРВЕРА):</b>\n" +
            "   Включить тумблер НАКАЛА (FILAMENT). Подождать 3 секунды до загорания индикатора READY.\n" +
            "   <i>ВНИМАНИЕ! Категорически запрещается подавать анодный ток на непрогретые лампы!</i>\n" +
            "   После прогрева включить тумблер ВЫСОКОГО НАПРЯЖЕНИЯ (HIGH VOLTAGE).\n\n" +
            "<b>3. НАЧАЛЬНЫЙ АДРЕС ЗАГРУЗКИ (FRONT PANEL):</b>\n" +
            "   Для адресации привода магнитной ленты выставить 4 тумблера шины в код:\n" +
            "   <color=#990000><b>[ 1 ] - ВКЛ  |  [ 2 ] - ВЫКЛ  |  [ 3 ] - ВКЛ  |  [ 4 ] - ВКЛ   (1 - 0 - 1 - 1)</b></color>\n\n" +
            "<b>4. УСТАНОВКА НОСИТЕЛЯ И ПУСК (IPL):</b>\n" +
            "   Извлечь бобину магнитной ленты из шкафчика, зарядить в лентопротяжный механизм серверов\n" +
            "   и нажать большую красную кнопку [START IPL].\n\n" +
            "<b>5. ТЕРМИНАЛ:</b>\n" +
            "   После завершения считывания ядра занять место оператора за столом и начать работу.\n\n" +
            "<b>6. ДЕШИФРАЦИЯ ДАННЫХ (КРИПТОАНАЛИЗ):</b>\n" +
            "   В терминале откройте файл C:\\СЕКРЕТНО\\ИНСТРУКЦИЯ_ЭВМ.TXT.\n" +
            "   Изучайте служебные записки на столе [N] и встроенную справку [H].\n" +
            "   Следите за температурой накопителя ЕС-5061 и шагами патруля охраны за дверью.";

        GUILayout.Label(manualText, bodyStyle);
        GUILayout.Space(15);

        if (GUILayout.Button("ЗАКРЫТЬ ИНСТРУКЦИЮ [E / Esc]", GUILayout.Height(35)))
        {
            ToggleManual(false);
        }

        GUILayout.EndArea();
        GUI.backgroundColor = Color.white;
    }

    private void SetupAudio()
    {
        // Attach audio emitters to physical server cabinet (Cube.006)
        Transform serverTransform = transform;
        var serverObj = GameObject.Find("Cube.006");
        if (serverObj != null)
        {
            serverTransform = serverObj.transform;
        }

        // 1. Transient / Relay contactor AudioSource
        Transform questAudioObj = serverTransform.Find("ServerQuestAudio");
        if (questAudioObj == null)
        {
            var go = new GameObject("ServerQuestAudio");
            go.transform.SetParent(serverTransform, false);
            go.transform.position = new Vector3(5.92f, 1.50f, 1.65f); // physical center of server front panel
            questAudioObj = go.transform;
        }
        questAudioSource = questAudioObj.GetComponent<AudioSource>();
        if (questAudioSource == null) questAudioSource = questAudioObj.gameObject.AddComponent<AudioSource>();

        questAudioSource.playOnAwake = false;
        questAudioSource.spatialBlend = 1.0f; // 100% 3D spatial sound
        questAudioSource.rolloffMode = AudioRolloffMode.Custom;
        questAudioSource.minDistance = 1.0f;
        questAudioSource.maxDistance = 12.0f;
        questAudioSource.spread = 35f;
        questAudioSource.dopplerLevel = 0f;

        var relayCurve = new AnimationCurve(
            new Keyframe(0.00f, 1.00f),
            new Keyframe(0.20f, 0.70f),
            new Keyframe(0.50f, 0.35f),
            new Keyframe(0.80f, 0.10f),
            new Keyframe(1.00f, 0.00f)
        );
        questAudioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, relayCurve);

        // 2. Continuous Transformer Hum AudioSource (50Hz hum)
        Transform humAudioObj = serverTransform.Find("ServerHumAudio");
        if (humAudioObj == null)
        {
            var go = new GameObject("ServerHumAudio");
            go.transform.SetParent(serverTransform, false);
            go.transform.position = new Vector3(5.92f, 1.50f, 1.65f);
            humAudioObj = go.transform;
        }
        humAudioSource = humAudioObj.GetComponent<AudioSource>();
        if (humAudioSource == null) humAudioSource = humAudioObj.gameObject.AddComponent<AudioSource>();

        humAudioSource.playOnAwake = false;
        humAudioSource.spatialBlend = 1.0f; // 100% 3D spatial audio! No flat 2D bleed into headphones!
        humAudioSource.rolloffMode = AudioRolloffMode.Custom;
        humAudioSource.minDistance = 0.8f;  // Full volume only within arms-reach (0.8m)
        humAudioSource.maxDistance = 8.5f;  // Completely fades out before outer corridors
        humAudioSource.spread = 45f;        // Sounds like a wide physical cabinet, not a single point
        humAudioSource.dopplerLevel = 0f;
        humAudioSource.loop = true;
        humAudioSource.volume = 0.09f;      // Gentle, non-fatiguing, authentic ambient background level

        // Smooth distance attenuation curve:
        // Close to server: distinct tactile hum.
        // At the desk (2.5m away): soft, relaxing background presence (drops to ~35%).
        // At the door (4.5m away): very faint (drops to ~12%).
        // In the vestibule / corridors (>7m): drops to 0% so outside guards can be clearly heard!
        var humCurve = new AnimationCurve(
            new Keyframe(0.00f, 1.00f, 0f, -1.8f),     // 0m - 0.8m: 100%
            new Keyframe(0.18f, 0.65f, -2.0f, -2.0f),  // ~1.5m: 65%
            new Keyframe(0.32f, 0.35f, -1.5f, -1.5f),  // ~2.7m (at desk): 35%
            new Keyframe(0.55f, 0.12f, -0.8f, -0.8f),  // ~4.6m (at door): 12%
            new Keyframe(0.80f, 0.03f, -0.3f, -0.3f),  // ~6.8m (vestibule entrance): 3%
            new Keyframe(1.00f, 0.00f, -0.1f, 0f)      // 8.5m: 0% (silence in hallway)
        );
        humAudioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, humCurve);

        humLoopClip = CreateTransformerHumClip();
        relayClackClip = CreateRelayClackClip();

        breakerSwitchClip = Resources.Load<AudioClip>("Sound/BreakerSwitch");
        lampPowerClip = Resources.Load<AudioClip>("Sound/LampPower");
        tapeInsertClip = Resources.Load<AudioClip>("Sound/DiskInsert");
        switchClick1Clip = Resources.Load<AudioClip>("Sound/SwitchClick1");
        switchClick2Clip = Resources.Load<AudioClip>("Sound/SwitchClick2");
        errorAlarmClip = Resources.Load<AudioClip>("Sound/ErrorAlarm");

        // Breaker 3D audio emitter on wall panel (Cube.009)
        var breakerObj = GameObject.Find("Cube.009");
        if (breakerObj != null)
        {
            breakerAudioSource = breakerObj.GetComponent<AudioSource>();
            if (breakerAudioSource == null) breakerAudioSource = breakerObj.AddComponent<AudioSource>();
            breakerAudioSource.spatialBlend = 1.0f;
            breakerAudioSource.rolloffMode = AudioRolloffMode.Custom;
            breakerAudioSource.minDistance = 1.0f;
            breakerAudioSource.maxDistance = 10.0f;
            breakerAudioSource.playOnAwake = false;
        }
    }

    private void StartTransformerHum()
    {
        if (humAudioSource != null && humLoopClip != null && !humAudioSource.isPlaying)
        {
            humAudioSource.clip = humLoopClip;
            humAudioSource.Play();
        }
    }

    private void StopTransformerHum()
    {
        if (humAudioSource != null && humAudioSource.isPlaying)
        {
            humAudioSource.Stop();
        }
    }

    private void PlayRelaySound()
    {
        if (questAudioSource != null && relayClackClip != null)
        {
            questAudioSource.PlayOneShot(relayClackClip, 0.75f);
        }
    }

    private void PlayBreakerSound()
    {
        if (breakerAudioSource != null && breakerSwitchClip != null)
        {
            breakerAudioSource.pitch = UnityEngine.Random.Range(0.97f, 1.03f);
            breakerAudioSource.PlayOneShot(breakerSwitchClip, 0.95f);
        }
        else if (questAudioSource != null && breakerSwitchClip != null)
        {
            questAudioSource.pitch = UnityEngine.Random.Range(0.97f, 1.03f);
            questAudioSource.PlayOneShot(breakerSwitchClip, 0.95f);
        }
    }

    private void PlayLampPowerSound()
    {
        if (questAudioSource != null && lampPowerClip != null)
        {
            questAudioSource.pitch = 1.0f;
            questAudioSource.PlayOneShot(lampPowerClip, 0.90f);
        }
    }

    private void PlayTapeInsertSound()
    {
        if (questAudioSource != null && tapeInsertClip != null)
        {
            questAudioSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
            questAudioSource.PlayOneShot(tapeInsertClip, 0.85f);
        }
    }

    private void PlaySwitchClickSound(int index)
    {
        if (questAudioSource != null)
        {
            var clip = (index % 2 == 0) ? switchClick1Clip : switchClick2Clip;
            if (clip == null) clip = switchClick1Clip ?? switchClick2Clip;
            if (clip != null)
            {
                questAudioSource.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
                questAudioSource.PlayOneShot(clip, 0.90f);
            }
        }
    }

    private void PlayErrorAlarmSound()
    {
        if (questAudioSource != null && errorAlarmClip != null)
        {
            questAudioSource.pitch = 1.0f;
            questAudioSource.PlayOneShot(errorAlarmClip, 0.85f);
        }
    }

    private AudioClip CreateTransformerHumClip()
    {
        int sampleRate = 44100;
        int samples = sampleRate * 1; // 1-second seamless loop
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            // 50Hz fundamental power line hum (deep smooth tone, no harsh harmonics)
            float h50 = Mathf.Sin(2f * Mathf.PI * 50f * t) * 0.45f;
            // 100Hz rectification harmonic (warm analog transformer rumble)
            float h100 = Mathf.Sin(2f * Mathf.PI * 100f * t) * 0.15f;
            // Subtle 150Hz third harmonic
            float h150 = Mathf.Sin(2f * Mathf.PI * 150f * t) * 0.04f;

            data[i] = (h50 + h100 + h150);
        }

        var clip = AudioClip.Create("MainframeHum50Hz", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip CreateRelayClackClip()
    {
        int sampleRate = 44100;
        int samples = (int)(sampleRate * 0.08f);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float thud = Mathf.Sin(2f * Mathf.PI * 120f * (float)i / sampleRate) * 0.7f;
            float metallic = Mathf.Sin(2f * Mathf.PI * 850f * (float)i / sampleRate) * 0.35f;
            float noise = (Random.value * 2f - 1f) * 0.2f;
            float env = 1f - t;
            data[i] = (thud + metallic + noise) * env * env;
        }

        var clip = AudioClip.Create("RelayContactorClack", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
