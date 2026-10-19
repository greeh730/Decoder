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
    [SerializeField] private Transform filamentSwitch2;
    [SerializeField] private Transform hvSwitch;
    [SerializeField] private Renderer filamentStatusLed;
    [SerializeField] private Renderer hvStatusLed;
    [SerializeField] private WorldInteractable filamentInteractable;
    [SerializeField] private WorldInteractable filamentInteractable2;
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
    private Vector3 filament2BaseRot = new Vector3(0f, 132.44f, 0f);
    private Vector3 hvBaseRot = new Vector3(0f, 143.29f, 0f);
    private Vector3 filamentBasePos = Vector3.zero;
    private Vector3 filament2BasePos = Vector3.zero;
    private Vector3 hvBasePos = Vector3.zero;
    private bool basesCached = false;
    private bool filament1Active = false;
    private bool filament2Active = false;

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
        if (filamentSwitch2 != null)
        {
            filament2BasePos = filamentSwitch2.localPosition;
            if (filamentSwitch2.localEulerAngles.y > 10f)
            {
                filament2BaseRot = new Vector3(0f, filamentSwitch2.localEulerAngles.y, 0f);
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
    private int lastBreakerToggleFrame = -1;
    private float lastBreakerToggleTime = -1f;
    private int lastHvToggleFrame = -1;
    private float lastHvToggleTime = -1f;
    private int[] lastSwitchToggleFrame = new int[4] { -1, -1, -1, -1 };
    private float[] lastSwitchToggleTime = new float[4] { -1f, -1f, -1f, -1f };
    private int lastIplFrame = -1;
    private float lastIplTime = -1f;

    private void Awake()
    {
        Instance = this;

        currentStage = BootStage.PowerOff;
        hasTapeInInventory = false;
        isTapeInserted = false;
        addressSwitches = new bool[4] { false, false, false, false };

        ResolveReferences();
        CreateMaterials();
        SetupAudio();
        EnsureSwitchBases();
    }

    private void ResolveReferences()
    {
        if (breakerInteractable == null)
        {
            var c9 = GameObject.Find("comnata/Cube.009") ?? GameObject.Find("Cube.009");
            if (c9 != null) breakerInteractable = c9.GetComponent<WorldInteractable>();
        }
        if (breakerHandle == null)
        {
            var w2 = GameObject.Find("comnata/Walec.002") ?? GameObject.Find("Walec.002");
            if (w2 != null) breakerHandle = w2.transform;
        }
        if (filamentSwitch2 == null)
        {
            var c100 = GameObject.Find("comnata/Cube.100") ?? GameObject.Find("Cube.100");
            if (c100 != null)
            {
                filamentSwitch2 = c100.transform;
                filamentInteractable2 = c100.GetComponent<WorldInteractable>();
            }
        }

        // Deactivate legacy broad box collider that obstructs switch clicking
        var filCol = GameObject.Find("FilamentCollider");
        if (filCol != null) filCol.SetActive(false);
    }

    private void Start()
    {
        BindInteractables();
        UpdateAllVisuals();
        ShowNotification(LocalizationManager.T("ЭВМ обесточена. Найдите документацию в шкафчике или осмотрите щиток.",
                                               "Mainframe unpowered. Find operating manual in locker or check breaker."));
    }

    private void SafeBind(WorldInteractable wi, UnityEngine.Events.UnityAction action)
    {
        if (wi == null || action == null) return;
        wi.onInteract.RemoveListener(action);
        bool hasPersistent = false;
        for (int i = 0; i < wi.onInteract.GetPersistentEventCount(); i++)
        {
            if (wi.onInteract.GetPersistentMethodName(i) == action.Method.Name)
            {
                hasPersistent = true;
                break;
            }
        }
        if (!hasPersistent)
        {
            wi.onInteract.AddListener(action);
        }
    }

    private void BindInteractables()
    {
        if (breakerInteractable != null)
        {
            SafeBind(breakerInteractable, ToggleBreaker);
        }
        if (breakerHandle != null)
        {
            var handleWi = breakerHandle.GetComponent<WorldInteractable>();
            if (handleWi != null) SafeBind(handleWi, ToggleBreaker);
        }
        if (filamentInteractable != null)
        {
            filamentInteractable.onInteract.RemoveAllListeners();
            filamentInteractable.onInteract.AddListener(ToggleFilament1);
        }
        if (filamentInteractable2 != null)
        {
            filamentInteractable2.onInteract.RemoveAllListeners();
            filamentInteractable2.onInteract.AddListener(ToggleFilament2);
        }
        if (hvInteractable != null)
        {
            SafeBind(hvInteractable, ToggleHighVoltage);
        }
        if (iplInteractable != null)
        {
            SafeBind(iplInteractable, PressIPL);
        }
        if (switchInteractables != null)
        {
            for (int i = 0; i < switchInteractables.Length; i++)
            {
                int idx = i;
                if (switchInteractables[i] != null)
                {
                    bool hasPersistent = false;
                    for (int p = 0; p < switchInteractables[i].onInteract.GetPersistentEventCount(); p++)
                    {
                        string mName = switchInteractables[i].onInteract.GetPersistentMethodName(p);
                        if (mName.Contains("ToggleAddressSwitch") || mName.Contains($"ToggleAddressSwitch{idx}"))
                        {
                            hasPersistent = true;
                            break;
                        }
                    }
                    if (!hasPersistent)
                    {
                        switchInteractables[i].onInteract.RemoveAllListeners();
                        switchInteractables[i].onInteract.AddListener(() => ToggleAddressSwitch(idx));
                    }
                }
            }
        }
        if (tapePickupInteractable != null)
        {
            SafeBind(tapePickupInteractable, TakeTape);
        }
        if (tapeInsertInteractable != null)
        {
            SafeBind(tapeInsertInteractable, InsertTape);
        }
        if (manualInteractable != null)
        {
            SafeBind(manualInteractable, OpenManual);
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
        if (Time.frameCount == lastBreakerToggleFrame || (Time.unscaledTime - lastBreakerToggleTime) < 0.35f)
        {
            return;
        }
        lastBreakerToggleFrame = Time.frameCount;
        lastBreakerToggleTime = Time.unscaledTime;

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

    private int lastFilament1Frame = -1;
    private int lastFilament2Frame = -1;

    public void ToggleFilament1()
    {
        if (Time.frameCount == lastFilament1Frame) return;
        lastFilament1Frame = Time.frameCount;

        filament1Active = !filament1Active;
        PlaySwitchClickSound(0);
        HandleFilamentToggled();
    }

    public void ToggleFilament2()
    {
        if (Time.frameCount == lastFilament2Frame) return;
        lastFilament2Frame = Time.frameCount;

        filament2Active = !filament2Active;
        PlaySwitchClickSound(1);
        HandleFilamentToggled();
    }

    public void ToggleFilament()
    {
        if (!filament1Active) ToggleFilament1();
        else if (!filament2Active) ToggleFilament2();
        else ToggleFilament1();
    }

    private void HandleFilamentToggled()
    {
        if (currentStage < BootStage.BreakerOn || isPowerCutByBreaker)
        {
            ShowNotification(LocalizationManager.T("[ОТКАЗ] Нет питания в электросети! Сначала включите рубильник на щитке.",
                                                   "[HALT] Power grid offline! Turn on breaker panel switch first."));
            UpdateAllVisuals();
            return;
        }

        bool anyActive = filament1Active || filament2Active;
        if (anyActive && currentStage == BootStage.BreakerOn)
        {
            PlayLampPowerSound();
            currentStage = BootStage.FilamentWarming;
            if (warmupCoroutine != null) StopCoroutine(warmupCoroutine);
            warmupCoroutine = StartCoroutine(WarmupFilamentRoutine());
        }
        else if (currentStage >= BootStage.FilamentReady)
        {
            ShowNotification(LocalizationManager.T("Накал ламп прогрет и стабилен.",
                                                   "Vacuum tube filaments are warmed and stable."));
        }

        UpdateAllVisuals();
    }

    private IEnumerator WarmupFilamentRoutine()
    {
        ShowNotification(LocalizationManager.T("Подан ток накала. Прогрев вакуумных ламп...",
                                               "Filament current applied. Heating vacuum tubes..."));
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
        ShowNotification(LocalizationManager.T("★ Лампы прогреты! Можно подавать высокое анодное напряжение (HV).",
                                               "★ Tubes pre-heated! Ready to apply high voltage (HV)."));
        UpdateAllVisuals();
    }

    public void ToggleHighVoltage()
    {
        if (Time.frameCount == lastHvToggleFrame || (Time.unscaledTime - lastHvToggleTime) < 0.35f)
        {
            return;
        }
        lastHvToggleFrame = Time.frameCount;
        lastHvToggleTime = Time.unscaledTime;

        if (isPowerCutByBreaker || currentStage < BootStage.BreakerOn)
        {
            ShowNotification(LocalizationManager.T("[ОТКАЗ] Электросеть обесточена! Сначала включите рубильник на щитке.",
                                                   "[HALT] Power grid offline! Turn on breaker panel switch first."));
            return;
        }

        if (currentStage == BootStage.SystemOnline)
        {
            // Enter Hot Standby: Cut HV, cut hum, keep filaments warm!
            currentStage = BootStage.HotStandby;
            StopTransformerHum();
            PlayRelaySound();
            if (DiskDriveBay.Instance != null) DiskDriveBay.Instance.OnServerStandbyOrCut();
            if (monitorTerminal != null) monitorTerminal.SetStandbyState(true);
            ShowNotification(LocalizationManager.T("<color=#FFCC00>[ГОТОВНОСТЬ]</color> Высокое напряжение снято (Standby). ЭВМ обесшумлена, накал активен.",
                                                   "<color=#FFCC00>[STANDBY]</color> High voltage cut. Mainframe quieted, filaments hot."));
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
            ShowNotification(LocalizationManager.T("<color=#00FF88>[ОНЛАЙН]</color> Анодное напряжение подано. ЭВМ мгновенно в строю.",
                                                   "<color=#00FF88>[ONLINE]</color> Anode HV restored. Mainframe immediately ready."));
            UpdateAllVisuals();
            return;
        }

        if (currentStage < BootStage.FilamentReady)
        {
            ShowNotification(LocalizationManager.T("[ОПАСНОСТЬ] Нельзя подавать анодное напряжение на холодные лампы!",
                                                   "[HAZARD] Cannot apply high voltage to cold vacuum tubes!"));
            return;
        }

        if (currentStage == BootStage.FilamentReady)
        {
            currentStage = BootStage.HighVoltageOn;
            PlayRelaySound();
            ShowNotification(LocalizationManager.T("Высокое напряжение подано. Вычислительный блок активирован.",
                                                   "High voltage applied. Computing core energized."));
            UpdateAllVisuals();
        }
        else
        {
            ShowNotification(LocalizationManager.T("Высокое напряжение уже подано.",
                                                   "High voltage is already engaged."));
        }
    }

    public void TakeTape()
    {
        if (hasTapeInInventory || isTapeInserted)
        {
            ShowNotification(LocalizationManager.T("Бобина магнитной ленты уже у вас.",
                                                   "Magnetic tape reel is already in your inventory."));
            return;
        }

        hasTapeInInventory = true;
        if (lockerTapeProp != null) lockerTapeProp.SetActive(false);
        ShowNotification(LocalizationManager.T("Получена бобина с ядром ОС [DECODER-KERNEL-TAPE]. Вставьте её в дисковод серверов.",
                                               "Acquired OS kernel reel [DECODER-KERNEL-TAPE]. Mount it into server drive."));
    }

    public void InsertTape()
    {
        if (isTapeInserted)
        {
            ShowNotification(LocalizationManager.T("Бобина с ядром ОС уже установлена в накопитель.",
                                                   "OS kernel tape reel is already mounted in drive."));
            return;
        }

        if (!hasTapeInInventory)
        {
            ShowNotification(LocalizationManager.T("[НЕТ НОСИТЕЛЯ] Сначала найдите бобину магнитной ленты на шкафчике.",
                                                   "[NO MEDIA] Find the OS kernel tape reel in locker first."));
            return;
        }

        hasTapeInInventory = false;
        isTapeInserted = true;
        PlayTapeInsertSound();
        if (serverTapeProp != null) serverTapeProp.SetActive(true);
        if (tapeInsertInteractable != null)
        {
            tapeInsertInteractable.PromptText = LocalizationManager.T("Бобина с ядром ОС установлена [DECODER-KERNEL]",
                                                                      "OS kernel tape reel installed [DECODER-KERNEL]");
            tapeInsertInteractable.IsInteractable = false;
        }
        ShowNotification(LocalizationManager.T("Бобина магнитной ленты установлена в накопитель ЭВМ.",
                                               "Magnetic tape reel mounted into mainframe transport."));
    }

    public void ToggleAddressSwitch(int index)
    {
        if (index < 0 || index >= addressSwitches.Length) return;
        if (Time.frameCount == lastSwitchToggleFrame[index] || (Time.unscaledTime - lastSwitchToggleTime[index]) < 0.25f)
        {
            return;
        }
        lastSwitchToggleFrame[index] = Time.frameCount;
        lastSwitchToggleTime[index] = Time.unscaledTime;

        addressSwitches[index] = !addressSwitches[index];
        PlaySwitchClickSound(index);

        string stateStr = addressSwitches[index] 
            ? LocalizationManager.T("ВКЛ [1]", "ON [1]") 
            : LocalizationManager.T("ВЫКЛ [0]", "OFF [0]");
        ShowNotification(LocalizationManager.T($"Тумблер шины #{index + 1}: {stateStr}",
                                               $"Bus switch #{index + 1}: {stateStr}"));

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
            monitorTerminal.AddLog(LocalizationManager.T("<color=#FF6666>[HARDWARE FAULT] Нарушена адресация шины во время исполнения!</color>",
                                                         "<color=#FF6666>[HARDWARE FAULT] Bus address altered during runtime execution!</color>"));
            monitorTerminal.AddLog(LocalizationManager.T("<color=#FFCC00>Восстановите тумблеры шины адреса (1-0-1-1) и повторно нажмите [START IPL].</color>",
                                                         "<color=#FFCC00>Restore bus address switches (1-0-1-1) and press [START IPL] again.</color>"));
        }

        if (PlayerController.Instance != null && PlayerController.Instance.IsInspectingMonitor)
        {
            PlayerController.Instance.ExitMonitor();
        }

        ShowNotification(LocalizationManager.T("<color=#FF3333><b>[АВАРИЯ: СБОЙ ШИНЫ АДРЕСА]</b></color> Сбита адресация ЭВМ на ходу! Терминал отключен. Верните 1-0-1-1 и нажмите [START IPL].",
                                               "<color=#FF3333><b>[FAULT: BUS PARITY ERROR]</b></color> Bus address modified while running! Return to 1-0-1-1 and press [START IPL]."));
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
        if (Time.frameCount == lastIplFrame || (Time.unscaledTime - lastIplTime) < 0.5f)
        {
            return;
        }
        lastIplFrame = Time.frameCount;
        lastIplTime = Time.unscaledTime;

        if (currentStage < BootStage.HighVoltageOn)
        {
            ShowNotification(LocalizationManager.T("[ОТКАЗ] Мейнфрейм не запитан! Пройдите шаги включения питания и накала.",
                                                   "[HALT] Mainframe not powered! Turn on breaker and pre-heat tubes first."));
            return;
        }

        if (!isTapeInserted)
        {
            ShowNotification(LocalizationManager.T("[ОШИБКА IPL] Накопитель пуст! Вставьте бобину магнитной ленты из шкафчика.",
                                                   "[IPL FAULT] Tape drive empty! Mount OS kernel tape reel from locker."));
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
            ShowNotification(LocalizationManager.T("[ОШИБКА АДРЕСА] Неверный код шины! Сверьтесь с инструкцией инженера в шкафчике.",
                                                   "[ADDRESS FAULT] Incorrect bus address code! Check engineer manual in locker."));
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
        ShowNotification(LocalizationManager.T("★ НАЧАЛЬНАЯ ЗАГРУЗКА (IPL)... Чтение ядра с магнитной ленты!",
                                               "★ INITIAL PROGRAM LOAD (IPL)... Loading kernel from magnetic tape!"));

        // Start dancing register LEDs
        if (registerBlinkCoroutine != null) StopCoroutine(registerBlinkCoroutine);
        registerBlinkCoroutine = StartCoroutine(RegisterBlinkRoutine());

        yield return new WaitForSeconds(2.8f);

        currentStage = BootStage.SystemOnline;
        PlayRelaySound();
        StartTransformerHum();
        ShowNotification(LocalizationManager.T("★★ СИСТЕМА УСПЕШНО ЗАПУЩЕНА! Подойдите к монитору на столе.",
                                               "★★ SYSTEM SUCCESSFULLY BOOTED! Approach terminal on desk."));

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
        onScreenNotification = LocalizationManager.TranslatePrompt(msg);
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
            breakerHandle.localRotation = Quaternion.Euler(0f, 0f, isBreakerUp ? 55f : 125f);
        }
        if (breakerLed != null)
        {
            breakerLed.sharedMaterial = isBreakerUp ? ledGreenMat : ledRedMat;
        }
        string breakerPrompt = isBreakerUp 
            ? LocalizationManager.T("Нажмите [E / ЛКМ] — Отключить главный рубильник питания", "Press [E / LMB] — Turn off main power breaker") 
            : LocalizationManager.T("Нажмите [E / ЛКМ] — Включить главный рубильник питания", "Press [E / LMB] — Turn on main power breaker");
        if (breakerInteractable != null)
        {
            breakerInteractable.PromptText = breakerPrompt;
        }
        if (breakerHandle != null)
        {
            var handleWi = breakerHandle.GetComponent<WorldInteractable>();
            if (handleWi != null)
            {
                handleWi.PromptText = breakerPrompt;
            }
        }

        EnsureSwitchBases();

        // 2. Filament switches (Cube.099 & Cube.100)
        bool f1On = (filament1Active || currentStage >= BootStage.FilamentWarming) && !isPowerCutByBreaker;
        if (filamentSwitch != null)
        {
            float pitch = f1On ? -22f : 0f;
            filamentSwitch.localRotation = Quaternion.Euler(pitch, filamentBaseRot.y, filamentBaseRot.z);
            if (filamentBasePos != Vector3.zero)
            {
                var pos = filamentBasePos;
                if (f1On) pos.y += 0.005f;
                filamentSwitch.localPosition = pos;
            }
        }

        bool f2On = (filament2Active || currentStage >= BootStage.FilamentWarming) && !isPowerCutByBreaker;
        if (filamentSwitch2 != null)
        {
            float pitch = f2On ? -22f : 0f;
            filamentSwitch2.localRotation = Quaternion.Euler(pitch, filament2BaseRot.y, filament2BaseRot.z);
            if (filament2BasePos != Vector3.zero)
            {
                var pos = filament2BasePos;
                if (f2On) pos.y += 0.005f;
                filamentSwitch2.localPosition = pos;
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
                hvInteractable.PromptText = LocalizationManager.T("Нажмите [E] — Снять анодное напряжение (Горячий резерв / Standby)", "Press [E] — Cut anode voltage (Hot Standby)");
            else if (currentStage == BootStage.HotStandby)
                hvInteractable.PromptText = LocalizationManager.T("Нажмите [E] — Подать анодное напряжение (Быстрый запуск ЭВМ)", "Press [E] — Apply anode voltage (Quick Boot)");
            else if (currentStage == BootStage.FilamentReady)
                hvInteractable.PromptText = LocalizationManager.T("Нажмите [E] — Подать высокое напряжение (HV)", "Press [E] — Apply high voltage (HV)");
            else
                hvInteractable.PromptText = LocalizationManager.T("Тумблер высокого напряжения (HV)", "High voltage toggle switch (HV)");
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
            tapeInsertInteractable.PromptText = LocalizationManager.T("Бобина с ядром ОС установлена [DECODER-KERNEL]", "OS kernel tape reel installed [DECODER-KERNEL]");
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
        GUILayout.Label(LocalizationManager.T("МИНИСТЕРСТВО РАДИОПРОМЫШЛЕННОСТИ\nВЫЧИСЛИТЕЛЬНЫЙ КОМПЛЕКС «ДЕКОДЕР-70»",
                                               "MINISTRY OF RADIO INDUSTRY\nCOMPUTING COMPLEX \"DECODER-70\""), headerStyle);

        var subStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        subStyle.normal.textColor = new Color(0.5f, 0.1f, 0.05f);
        GUILayout.Label(LocalizationManager.T("СЛУЖЕБНАЯ ИНСТРУКЦИЯ ПО ХОЛОДНОМУ ПУСКУ (1974 г.)",
                                               "OFFICIAL COLD BOOT OPERATING MANUAL (1974)"), subStyle);
        GUILayout.Space(10);

        var bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            wordWrap = true
        };
        bodyStyle.normal.textColor = new Color(0.15f, 0.15f, 0.15f);

        string manualTextRu =
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

        string manualTextEn =
            "<b>1. POWER SUPPLY:</b>\n" +
            "   Energize wall breaker panel on the wall by flipping the knife switch UP.\n\n" +
            "<b>2. VACUUM TUBE PRE-HEATING (SERVER GENERATOR):</b>\n" +
            "   Flip FILAMENT toggle switch. Wait 3 seconds until the green READY lamp lights up.\n" +
            "   <i>WARNING! Applying anode current to cold unheated tubes will destroy them!</i>\n" +
            "   Once tubes are glowing amber, engage the HIGH VOLTAGE switch.\n\n" +
            "<b>3. INITIAL BOOT ADDRESS (FRONT PANEL):</b>\n" +
            "   Set the 4 bus address switches to match the magnetic tape drive address code:\n" +
            "   <color=#990000><b>[ 1 ] - ON   |  [ 2 ] - OFF  |  [ 3 ] - ON   |  [ 4 ] - ON    (1 - 0 - 1 - 1)</b></color>\n\n" +
            "<b>4. MOUNT MEDIA & START IPL:</b>\n" +
            "   Take the OS kernel tape reel from locker, mount it into server tape transport,\n" +
            "   then press the prominent round red button [START IPL].\n\n" +
            "<b>5. OPERATOR TERMINAL:</b>\n" +
            "   Once kernel load finishes, take the operator seat at the workstation terminal.\n\n" +
            "<b>6. CRYPTANALYSIS & STEALTH:</b>\n" +
            "   In terminal, inspect C:\\SECRET\\INSTRUCTION_74.TXT.\n" +
            "   Read tactical desk notes [N] and built-in commander help [H].\n" +
            "   Keep disk drive EC-5061 temperature safe and monitor patrol footsteps outside.";

        GUILayout.Label(LocalizationManager.IsEnglish ? manualTextEn : manualTextRu, bodyStyle);
        GUILayout.Space(15);

        if (GUILayout.Button(LocalizationManager.T("ЗАКРЫТЬ ИНСТРУКЦИЮ [E / Esc]", "CLOSE MANUAL [E / Esc]"), GUILayout.Height(35)))
        {
            ToggleManual(false);
        }

        GUILayout.EndArea();
        GUI.backgroundColor = Color.white;
    }

    private void SetupAudio()
    {
        Vector3 serverFrontPos = new Vector3(5.90f, 2.10f, 1.65f);

        // 1. Transient / Relay contactor AudioSource
        var questAudioObj = GameObject.Find("ServerQuestAudio");
        if (questAudioObj == null)
        {
            questAudioObj = new GameObject("ServerQuestAudio");
        }
        questAudioObj.transform.SetParent(null);
        questAudioObj.transform.position = serverFrontPos;

        questAudioSource = questAudioObj.GetComponent<AudioSource>();
        if (questAudioSource == null) questAudioSource = questAudioObj.AddComponent<AudioSource>();

        questAudioSource.playOnAwake = false;
        questAudioSource.spatialBlend = 0.15f; // Crisp near-stereo presence across room
        questAudioSource.minDistance = 2.0f;
        questAudioSource.maxDistance = 25.0f;
        questAudioSource.spread = 35f;
        questAudioSource.dopplerLevel = 0f;

        // 2. Continuous Transformer Hum AudioSource (50Hz hum)
        var humAudioObj = GameObject.Find("ServerHumAudio");
        if (humAudioObj == null)
        {
            humAudioObj = new GameObject("ServerHumAudio");
        }
        humAudioObj.transform.SetParent(null);
        humAudioObj.transform.position = serverFrontPos;

        humAudioSource = humAudioObj.GetComponent<AudioSource>();
        if (humAudioSource == null) humAudioSource = humAudioObj.AddComponent<AudioSource>();

        humAudioSource.playOnAwake = false;
        humAudioSource.spatialBlend = 0.85f;
        humAudioSource.minDistance = 1.0f;
        humAudioSource.maxDistance = 12.0f;
        humAudioSource.spread = 45f;
        humAudioSource.dopplerLevel = 0f;
        humAudioSource.loop = true;
        humAudioSource.volume = 0.09f;

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

    private AudioSource directAudioSource;

    private void Play2DSound(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;
        if (directAudioSource == null)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                directAudioSource = cam.GetComponent<AudioSource>();
                if (directAudioSource == null) directAudioSource = cam.gameObject.AddComponent<AudioSource>();
            }
            else
            {
                var go = new GameObject("Mainframe2DAudio");
                directAudioSource = go.AddComponent<AudioSource>();
                DontDestroyOnLoad(go);
            }
            directAudioSource.spatialBlend = 0f;
            directAudioSource.playOnAwake = false;
        }
        directAudioSource.pitch = pitch;
        directAudioSource.PlayOneShot(clip, volume);
    }

    private void PlayRelaySound()
    {
        Play2DSound(relayClackClip, 0.85f, 1.0f);
        if (questAudioSource != null && relayClackClip != null)
        {
            questAudioSource.PlayOneShot(relayClackClip, 0.75f);
        }
    }

    private void PlayBreakerSound()
    {
        float p = UnityEngine.Random.Range(0.97f, 1.03f);
        Play2DSound(breakerSwitchClip, 1.0f, p);
        if (breakerAudioSource != null && breakerSwitchClip != null)
        {
            breakerAudioSource.pitch = p;
            breakerAudioSource.PlayOneShot(breakerSwitchClip, 0.95f);
        }
    }

    private void PlayLampPowerSound()
    {
        if (lampPowerClip == null) lampPowerClip = Resources.Load<AudioClip>("Sound/LampPower");
        Play2DSound(lampPowerClip, 1.0f, 1.0f);
        if (questAudioSource != null && lampPowerClip != null)
        {
            questAudioSource.pitch = 1.0f;
            questAudioSource.PlayOneShot(lampPowerClip, 0.90f);
        }
    }

    private void PlayTapeInsertSound()
    {
        float p = UnityEngine.Random.Range(0.95f, 1.05f);
        Play2DSound(tapeInsertClip, 0.9f, p);
        if (questAudioSource != null && tapeInsertClip != null)
        {
            questAudioSource.pitch = p;
            questAudioSource.PlayOneShot(tapeInsertClip, 0.85f);
        }
    }

    private void PlaySwitchClickSound(int index)
    {
        if (switchClick1Clip == null) switchClick1Clip = Resources.Load<AudioClip>("Sound/SwitchClick1") ?? CreateSwitchClickClip(1800f);
        if (switchClick2Clip == null) switchClick2Clip = Resources.Load<AudioClip>("Sound/SwitchClick2") ?? CreateSwitchClickClip(1400f);

        var clip = (index % 2 == 0) ? switchClick1Clip : switchClick2Clip;
        if (clip == null) clip = switchClick1Clip ?? switchClick2Clip;
        if (clip == null) return;

        float p = UnityEngine.Random.Range(0.95f, 1.05f);
        Play2DSound(clip, 1.0f, p);

        if (questAudioSource != null)
        {
            questAudioSource.pitch = p;
            questAudioSource.PlayOneShot(clip, 0.9f);
        }
    }

    private AudioClip CreateSwitchClickClip(float freq)
    {
        int sampleRate = 44100;
        int samples = (int)(sampleRate * 0.045f);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float click = Mathf.Sin(2f * Mathf.PI * freq * (float)i / sampleRate) * 0.7f;
            float noise = (UnityEngine.Random.value * 2f - 1f) * 0.4f;
            float env = (1f - t) * (1f - t);
            data[i] = (click + noise) * env;
        }
        var clip = AudioClip.Create("GeneratedSwitchClick_" + (int)freq, samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
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
