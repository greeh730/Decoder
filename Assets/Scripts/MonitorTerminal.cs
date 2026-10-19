using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controls the retro workstation terminal on the monitor screen.
/// Replaces generic prototype actions with an authentic Soviet-themed WIP (Work In Progress)
/// development dashboard, roadmap, CRT diagnostics, and system status.
/// Activates strictly only when the mainframe server has been cold-booted into online state.
/// </summary>
public class MonitorTerminal : MonoBehaviour
{
    public static MonitorTerminal Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private Canvas terminalCanvas;
    [SerializeField] private Text terminalLogText;
    [SerializeField] private Text statusText;
    [SerializeField] private Text clockText;
    [SerializeField] private Light roomLight;
    [SerializeField] private GameObject headerPanel;

    [Header("Interactive Panels")]
    [SerializeField] private GameObject startScreenPanel;
    [SerializeField] private GameObject activeScreenPanel;
    [SerializeField] private GameObject idleScreenPanel;
    [SerializeField] private GameObject offScreenPanel;

    [Header("Screen Lighting & Sound")]
    [SerializeField] private Light screenGlowLight;
    [SerializeField] private AudioSource terminalAudio;

    [Header("Brightness & Potentiometer Knob")]
    [SerializeField] private float brightnessLevel = 1.0f; // 1.0f (100%), 0.35f (35%), 0.0f (0%)
    [SerializeField] private Transform brightnessKnob;
    [SerializeField] private CanvasGroup screenCanvasGroup;

    private bool isRoomLightOn = true;
    private bool isZoomedIn = false;
    private bool isMainframeOnline = false;
    private bool isStandby = false;
    private bool hasLoggedIn = false;
    private float cursorBlinkTimer = 0f;
    private bool cursorVisible = true;
    private Coroutine crtTestCoroutine;
    private readonly List<string> logLines = new List<string>();

    public void SetLoggedIn(bool loggedIn) => hasLoggedIn = loggedIn;

    // Procedural sound clips
    private AudioClip crtStartupClip;
    private AudioClip pcShutdownClip;
    private AudioClip keyClickClip;
    private AudioClip alertClip;
    private AudioClip potentiometerClickClip;

    public bool IsZoomedIn => isZoomedIn;
    public bool IsMainframeOnline => isMainframeOnline;
    public bool IsStandby => isStandby;
    public float BrightnessLevel => brightnessLevel;

    public bool IsLightEmitting => isMainframeOnline && !isStandby && !(MainframeBootQuest.Instance != null && MainframeBootQuest.Instance.IsPowerCutByBreaker) && brightnessLevel > 0.05f && (roomLight == null || !roomLight.enabled);
    public float LightEmissionDistance => IsLightEmitting ? 22.0f : 0f;

    private void Awake()
    {
        Instance = this;

        if (terminalCanvas == null) terminalCanvas = GetComponent<Canvas>();

        if (roomLight == null)
        {
            var lightObj = GameObject.Find("Directional Light");
            if (lightObj != null) roomLight = lightObj.GetComponent<Light>();
        }

        if (offScreenPanel == null && transform.Find("OffScreenPanel") != null)
        {
            offScreenPanel = transform.Find("OffScreenPanel").gameObject;
        }

        if (startScreenPanel == null && transform.Find("StartScreenPanel") != null)
        {
            startScreenPanel = transform.Find("StartScreenPanel").gameObject;
        }

        if (headerPanel == null && transform.Find("Header") != null)
        {
            headerPanel = transform.Find("Header").gameObject;
        }

        if (screenGlowLight == null && transform.parent != null)
        {
            var lightTr = transform.parent.Find("ScreenGlowLight");
            if (lightTr != null) screenGlowLight = lightTr.GetComponent<Light>();
        }

        if (terminalAudio == null)
        {
            terminalAudio = GetComponent<AudioSource>();
            if (terminalAudio == null) terminalAudio = gameObject.AddComponent<AudioSource>();
            terminalAudio.spatialBlend = 1.0f; // 100% 3D spatial
            terminalAudio.rolloffMode = AudioRolloffMode.Logarithmic;
            terminalAudio.minDistance = 0.5f;
            terminalAudio.maxDistance = 6.0f;
        }

        if (screenCanvasGroup == null)
        {
            screenCanvasGroup = GetComponent<CanvasGroup>();
            if (screenCanvasGroup == null) screenCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (brightnessKnob == null && transform.parent != null)
        {
            var knobTr = transform.parent.Find("BrightnessKnob");
            if (knobTr != null) brightnessKnob = knobTr;
        }

        GenerateAudioClips();
        pcShutdownClip = Resources.Load<AudioClip>("Sound/PcShutdown");
        SetupWipButtons();
        ApplyBrightnessVisuals();
    }

    private void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += OnLanguageChanged;
    }

    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        SetupWipButtons();
        UpdateIdleScreenText();
        UpdateHeaderTitle();
    }

    private void UpdateHeaderTitle()
    {
        if (headerPanel != null)
        {
            var title = headerPanel.transform.Find("TitleText")?.GetComponent<Text>();
            if (title != null)
            {
                title.text = LocalizationManager.T("ЭВМ «ВЕКТОР-1» // СИСТЕМА ДЕКОДЕР", "MAINFRAME \"VECTOR-1\" // DECODER SYSTEM");
            }
        }
    }

    private void Start()
    {
        // Initial state: computer powers on ONLY when server is online!
        bool serverRunning = (MainframeBootQuest.Instance != null &&
                              MainframeBootQuest.Instance.CurrentStage == BootStage.SystemOnline);

        SetMainframeOnline(serverRunning);
        SetZoomedIn(false);
    }

    private void Update()
    {
        bool powerCut = (MainframeBootQuest.Instance != null && MainframeBootQuest.Instance.IsPowerCutByBreaker);
        bool serverOnline = (MainframeBootQuest.Instance != null && MainframeBootQuest.Instance.CurrentStage == BootStage.SystemOnline && !powerCut);
        bool diskExtracted = (DiskDriveBay.Instance != null && DiskDriveBay.Instance.State == DiskDriveBay.DriveBayState.CartridgeExtracted);

        if (!serverOnline || diskExtracted)
        {
            if (isMainframeOnline)
            {
                SetMainframeOnline(false);
            }
        }

        if (clockText != null)
        {
            clockText.text = isMainframeOnline ? System.DateTime.Now.ToString("HH:mm:ss") : "--:--:--";
        }

        if (isMainframeOnline)
        {
            // Cursor blink animation
            cursorBlinkTimer += Time.deltaTime;
            if (cursorBlinkTimer >= 0.5f)
            {
                cursorBlinkTimer = 0f;
                cursorVisible = !cursorVisible;
                UpdateIdleScreenText();
            }

            bool isVcActive = VectorCommander.Instance != null && VectorCommander.Instance.gameObject.activeInHierarchy;
            bool isMgActive = SpyMiniGames.Instance != null && SpyMiniGames.Instance.IsMiniGameActive;

            if (isZoomedIn && !isVcActive && !isMgActive)
            {
                HandleKeyboardShortcuts();
            }
        }
    }

    private void UpdateIdleScreenText()
    {
        if (idleScreenPanel == null || !idleScreenPanel.activeSelf) return;

        var idleText = idleScreenPanel.GetComponentInChildren<Text>();
        if (idleText == null) return;

        string cursor = cursorVisible ? "_" : " ";
        if (LocalizationManager.IsEnglish)
        {
            idleText.text =
                "<size=36><b><color=#00FFAA>MAINFRAME \"VECTOR-1\"</color></b></size>\n" +
                "<size=17><color=#66CCAA>INSTITUTE OF APPLIED CRYPTOGRAPHY // SECTOR 7</color></size>\n\n" +
                "<size=28><b><color=#FFCC00>/// WORK IN PROGRESS ///</color></b></size>\n" +
                "<size=22><color=#FFFFFF>TERMINAL INTERFACE UNDER DEVELOPMENT</color></size>\n" +
                "<color=#00FFCC>────────────────────────────────────────────</color>\n" +
                "<size=16><color=#99FFCC>• SERVER POWER: <color=#00FF88>[ GRID STABLE // ONLINE ]</color></color></size>\n" +
                "<size=16><color=#99FFCC>• KERNEL MEDIA: <color=#00FF88>[ MAGNETIC TAPE LOADED ]</color></color></size>\n" +
                "<size=16><color=#FFCC66>• DECRYPTOR UNIT: <color=#FFAA33>[ IN DEVELOPMENT (PRE-ALPHA) ]</color></color></size>\n\n" +
                $"<size=23><b><color=#FFFF66>[ Press [F] — Terminal (WIP) {cursor} ]</color></b></size>";
        }
        else
        {
            idleText.text =
                "<size=36><b><color=#00FFAA>ЭВМ «ВЕКТОР-1»</color></b></size>\n" +
                "<size=17><color=#66CCAA>ИНСТИТУТ ПРИКЛАДНОЙ ДЕШИФРАЦИИ // СЕКТОР 7</color></size>\n\n" +
                "<size=28><b><color=#FFCC00>/// WORK IN PROGRESS ///</color></b></size>\n" +
                "<size=22><color=#FFFFFF>ИНТЕРФЕЙС ТЕРМИНАЛА В РАЗРАБОТКЕ</color></size>\n" +
                "<color=#00FFCC>────────────────────────────────────────────</color>\n" +
                "<size=16><color=#99FFCC>• ПИТАНИЕ СЕРВЕРА: <color=#00FF88>[ СЕТЬ СТАБИЛЬНА // ОНЛАЙН ]</color></color></size>\n" +
                "<size=16><color=#99FFCC>• НОСИТЕЛЬ ЯДРА: <color=#00FF88>[ МАГНИТНАЯ ЛЕНТА ЗАГРУЖЕНА ]</color></color></size>\n" +
                "<size=16><color=#FFCC66>• БЛОК ДЕШИФРАТОРА: <color=#FFAA33>[ В РАЗРАБОТКЕ (PRE-ALPHA) ]</color></color></size>\n\n" +
                $"<size=23><b><color=#FFFF66>[ Нажмите [F] — Терминал (WIP) {cursor} ]</color></b></size>";
        }
    }

    private void SetupWipButtons()
    {
        if (activeScreenPanel == null) return;

        var leftButtons = activeScreenPanel.transform.Find("LeftButtons");
        if (leftButtons == null) return;

        // Button 1: Roadmap
        var b1 = leftButtons.Find("BtnLight");
        if (b1 != null)
        {
            var t = b1.GetComponentInChildren<Text>();
            if (t != null) t.text = LocalizationManager.T("📋 Дорожная карта [1]", "📋 Project Roadmap [1]");
            var btn = b1.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(ShowRoadmap);
            }
        }

        // Button 2: Modules status
        var b2 = leftButtons.Find("BtnDecrypt");
        if (b2 != null)
        {
            var t = b2.GetComponentInChildren<Text>();
            if (t != null) t.text = LocalizationManager.T("📊 Статус модулей [2]", "📊 Subsystems Status [2]");
            var btn = b2.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(ShowWipModules);
            }
        }

        // Button 3: CRT Matrix Test
        var b3 = leftButtons.Find("BtnDiag");
        if (b3 != null)
        {
            var t = b3.GetComponentInChildren<Text>();
            if (t != null) t.text = LocalizationManager.T("📺 Тест ЭЛТ-экрана [3]", "📺 CRT Matrix Test [3]");
            var btn = b3.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(RunCrtTest);
            }
        }

        // Button 4: Room Light
        var b4 = leftButtons.Find("BtnFiles");
        if (b4 != null)
        {
            var t = b4.GetComponentInChildren<Text>();
            if (t != null) t.text = LocalizationManager.T("💡 Свет в кабинете [4]", "💡 Office Light [4]");
            var btn = b4.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(ToggleRoomLight);
            }
        }
    }

    public void SetMainframeOnline(bool online)
    {
        bool wasOnline = isMainframeOnline;
        isMainframeOnline = online;
        ApplyBrightnessVisuals();

        if (online)
        {
            if (offScreenPanel != null) offScreenPanel.SetActive(false);
            if (screenGlowLight != null) screenGlowLight.enabled = true;

            if (startScreenPanel != null)
            {
                startScreenPanel.SetActive(true);
                if (headerPanel != null) headerPanel.SetActive(false);
                if (idleScreenPanel != null) idleScreenPanel.SetActive(false);
                if (activeScreenPanel != null) activeScreenPanel.SetActive(false);
            }
            else
            {
                if (headerPanel != null) headerPanel.SetActive(true);
                UpdateHeaderTitle();

                if (isZoomedIn)
                {
                    if (activeScreenPanel != null) activeScreenPanel.SetActive(true);
                    if (idleScreenPanel != null) idleScreenPanel.SetActive(false);
                }
                else
                {
                    if (idleScreenPanel != null) idleScreenPanel.SetActive(true);
                    if (activeScreenPanel != null) activeScreenPanel.SetActive(false);
                }
            }

            UpdateIdleScreenText();

            // Populate initial WIP log
            logLines.Clear();
            AddLog("<color=#00FFAA>===========================================================</color>");
            AddLog(LocalizationManager.T("<color=#FFCC00><b>/// РАБОЧИЙ ТЕРМИНАЛ «ДЕКОДЕР» ///</b></color>",
                                         "<color=#FFCC00><b>/// OPERATOR TERMINAL \"DECODER\" ///</b></color>"));
            AddLog(LocalizationManager.T("<color=#88DDCC>Интерфейс оператора запущен.</color>",
                                         "<color=#88DDCC>Operator console interface started.</color>"));
            AddLog("<color=#00FFAA>===========================================================</color>");
            AddLog(LocalizationManager.T("<color=#72D572>[04:12] Силовое питание от мейнфрейма «Вектор-1» получено.</color>",
                                         "<color=#72D572>[04:12] Power feed from Vector-1 mainframe energized.</color>"));
            AddLog(LocalizationManager.T("<color=#72D572>[04:12] Ядро DECODER-OS загружено с магнитной ленты.</color>",
                                         "<color=#72D572>[04:12] DECODER-OS kernel loaded from magnetic tape.</color>"));

            if (terminalAudio != null && crtStartupClip != null)
            {
                terminalAudio.PlayOneShot(crtStartupClip, 0.65f);
            }
        }
        else
        {
            // Server is OFFLINE: computer screen is completely unpowered
            if (wasOnline && terminalAudio != null && pcShutdownClip != null)
            {
                terminalAudio.PlayOneShot(pcShutdownClip, 0.75f);
            }

            if (PlayerController.Instance != null && PlayerController.Instance.IsInspectingMonitor)
            {
                PlayerController.Instance.ExitMonitor();
            }

            if (offScreenPanel != null) offScreenPanel.SetActive(true);
            if (screenGlowLight != null) screenGlowLight.enabled = false;
            if (headerPanel != null) headerPanel.SetActive(false);
            if (startScreenPanel != null) startScreenPanel.SetActive(false);
            if (idleScreenPanel != null) idleScreenPanel.SetActive(false);
            if (activeScreenPanel != null) activeScreenPanel.SetActive(false);

            if (VectorCommander.Instance != null && VectorCommander.Instance.gameObject.activeSelf)
            {
                VectorCommander.Instance.gameObject.SetActive(false);
            }
            if (SpyMiniGames.Instance != null && SpyMiniGames.Instance.IsMiniGameActive)
            {
                SpyMiniGames.Instance.CancelMiniGame();
            }
            hasLoggedIn = false;
        }
    }

    public void SetZoomedIn(bool zoomed)
    {
        isZoomedIn = zoomed;

        if (!isMainframeOnline)
        {
            if (offScreenPanel != null) offScreenPanel.SetActive(true);
            if (startScreenPanel != null) startScreenPanel.SetActive(false);
            if (activeScreenPanel != null) activeScreenPanel.SetActive(false);
            if (idleScreenPanel != null) idleScreenPanel.SetActive(false);
            return;
        }

        if (offScreenPanel != null) offScreenPanel.SetActive(false);

        bool isVcActive = VectorCommander.Instance != null && VectorCommander.Instance.gameObject.activeSelf;
        bool isMgActive = SpyMiniGames.Instance != null && SpyMiniGames.Instance.IsMiniGameActive;

        if (isVcActive || isMgActive || hasLoggedIn)
        {
            if (startScreenPanel != null) startScreenPanel.SetActive(false);
            if (idleScreenPanel != null) idleScreenPanel.SetActive(false);
            if (activeScreenPanel != null) activeScreenPanel.SetActive(false);
        }
        else if (startScreenPanel != null)
        {
            startScreenPanel.SetActive(true);
            if (activeScreenPanel != null) activeScreenPanel.SetActive(false);
            if (idleScreenPanel != null) idleScreenPanel.SetActive(false);
        }
        else
        {
            if (activeScreenPanel != null) activeScreenPanel.SetActive(zoomed);
            if (idleScreenPanel != null) idleScreenPanel.SetActive(!zoomed);
        }

        if (terminalAudio != null && keyClickClip != null)
        {
            terminalAudio.PlayOneShot(keyClickClip, 0.4f);
        }
    }

    public void CycleBrightness()
    {
        if (brightnessLevel > 0.7f) SetBrightness(0.35f);
        else if (brightnessLevel > 0.2f) SetBrightness(0.0f);
        else SetBrightness(1.0f);
    }

    public void SetBrightness(float level)
    {
        brightnessLevel = Mathf.Clamp01(level);
        ApplyBrightnessVisuals();

        var knobComp = brightnessKnob != null ? brightnessKnob.GetComponentInParent<PotentiometerKnob>() : null;
        if (knobComp != null && Mathf.Abs(knobComp.CurrentValue - brightnessLevel) > 0.01f)
        {
            knobComp.SetValue(brightnessLevel, false);
        }

        PlayPotentiometerClick();

        string pct = Mathf.RoundToInt(brightnessLevel * 100f) + "%";
        string desc = brightnessLevel <= 0.05f ? "0% [МАСКИРОВКА / ТЕМНОТА]" :
                      (brightnessLevel < 0.6f ? $"{pct} [ПРИГЛУШЕННО]" : $"{pct} [СТАНДАРТ / ЯРКО]");

        bool isEffectivelyPowered = isMainframeOnline && !(MainframeBootQuest.Instance != null && MainframeBootQuest.Instance.IsPowerCutByBreaker);

        if (isEffectivelyPowered)
        {
            if (isZoomedIn)
            {
                AddLog(LocalizationManager.T($"<color=#FFFF66>[ЯРКОСТЬ ЭКРАНА]</color> Регулятор потенциометра: {desc}",
                                             $"<color=#FFFF66>[SCREEN BRIGHTNESS]</color> Potentiometer knob: {desc}"));
            }
            else if (MainframeBootQuest.Instance != null)
            {
                MainframeBootQuest.Instance.ShowNotification(LocalizationManager.T($"Яркость экрана ЭЛТ: {desc}",
                                                                                    $"CRT Screen Brightness: {desc}"));
            }
        }
    }

    public void SetBrightnessContinuous(float level)
    {
        brightnessLevel = Mathf.Clamp01(level);
        ApplyBrightnessVisuals();
    }

    public void OnKnobDragFinished()
    {
        bool isEffectivelyPowered = isMainframeOnline && !(MainframeBootQuest.Instance != null && MainframeBootQuest.Instance.IsPowerCutByBreaker);
        if (!isEffectivelyPowered) return;

        string pct = Mathf.RoundToInt(brightnessLevel * 100f) + "%";
        string desc = brightnessLevel <= 0.05f ? LocalizationManager.T("0% [МАСКИРОВКА / ТЕМНОТА]", "0% [STEALTH / DARK]") :
                      (brightnessLevel < 0.6f ? LocalizationManager.T($"{pct} [ПРИГЛУШЕННО]", $"{pct} [DIMMED]") :
                                                LocalizationManager.T($"{pct} [СТАНДАРТ / ЯРКО]", $"{pct} [DEFAULT / BRIGHT]"));

        if (isZoomedIn)
        {
            AddLog(LocalizationManager.T($"<color=#FFFF66>[ЯРКОСТЬ ЭКРАНА]</color> Регулятор установлен: {desc}",
                                         $"<color=#FFFF66>[SCREEN BRIGHTNESS]</color> Knob set: {desc}"));
        }
        else if (MainframeBootQuest.Instance != null)
        {
            MainframeBootQuest.Instance.ShowNotification(LocalizationManager.T($"Яркость экрана: {desc}",
                                                                                $"Screen Brightness: {desc}"));
        }
    }

    public void ApplyBrightnessVisuals()
    {
        bool powerCut = (MainframeBootQuest.Instance != null && MainframeBootQuest.Instance.IsPowerCutByBreaker);
        bool diskExtracted = (DiskDriveBay.Instance != null && DiskDriveBay.Instance.State == DiskDriveBay.DriveBayState.CartridgeExtracted);
        bool effectivelyOnline = isMainframeOnline && !powerCut && !diskExtracted;

        if (screenCanvasGroup != null)
        {
            if (!effectivelyOnline)
            {
                // Unpowered or halted CRT: Completely dark/off!
                screenCanvasGroup.alpha = 0.0f;
                screenCanvasGroup.interactable = false;
                screenCanvasGroup.blocksRaycasts = false;
            }
            else if (isStandby)
            {
                // Standby: dimmed dark screen
                screenCanvasGroup.alpha = 0.05f;
                screenCanvasGroup.interactable = false;
                screenCanvasGroup.blocksRaycasts = false;
            }
            else
            {
                // Online: scaled by user brightness knob (100%, 35%, 0%)
                screenCanvasGroup.alpha = brightnessLevel <= 0.05f ? 0.03f : brightnessLevel;
                screenCanvasGroup.interactable = (brightnessLevel > 0.05f);
                screenCanvasGroup.blocksRaycasts = (brightnessLevel > 0.05f);
            }
        }

        if (screenGlowLight != null)
        {
            bool lightActive = effectivelyOnline && !isStandby && brightnessLevel > 0.05f;
            screenGlowLight.enabled = lightActive;
            screenGlowLight.intensity = 0.85f * brightnessLevel;
        }

        if (brightnessKnob != null)
        {
            float angle = Mathf.Lerp(-65f, 65f, brightnessLevel);
            brightnessKnob.localRotation = Quaternion.Euler(0f, angle, 0f);
        }

        if (!effectivelyOnline)
        {
            if (offScreenPanel != null) offScreenPanel.SetActive(true);
            if (startScreenPanel != null) startScreenPanel.SetActive(false);
            if (idleScreenPanel != null) idleScreenPanel.SetActive(false);
            if (activeScreenPanel != null) activeScreenPanel.SetActive(false);
            if (headerPanel != null) headerPanel.SetActive(false);
        }
        else if (offScreenPanel != null)
        {
            offScreenPanel.SetActive(false);
        }
    }

    public void SetStandbyState(bool standby)
    {
        isStandby = standby;
        ApplyBrightnessVisuals();

        if (standby)
        {
            if (screenGlowLight != null) screenGlowLight.enabled = false;
        }
    }

    private void PlayPotentiometerClick()
    {
        if (terminalAudio != null && potentiometerClickClip != null)
        {
            terminalAudio.pitch = Random.Range(0.9f, 1.1f);
            terminalAudio.PlayOneShot(potentiometerClickClip, 0.45f);
        }
    }

    private void HandleKeyboardShortcuts()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) ShowRoadmap();
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) ShowWipModules();
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) RunCrtTest();
            if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) ToggleRoomLight();
            if (kb.bKey.wasPressedThisFrame) CycleBrightness();
        }
#else
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) ShowRoadmap();
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) ShowWipModules();
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) RunCrtTest();
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) ToggleRoomLight();
        if (Input.GetKeyDown(KeyCode.B)) CycleBrightness();
#endif
    }

    public void AddLog(string msg)
    {
        string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
        logLines.Add($"<color=#72D572>[{timestamp}]</color> {msg}");
        if (logLines.Count > 13) logLines.RemoveAt(0);

        if (terminalLogText != null)
        {
            terminalLogText.text = string.Join("\n", logLines);
        }
    }

    public void ShowRoadmap()
    {
        PlayKeyClick();
        AddLog(LocalizationManager.T("<color=#FFCC00>================ ДОРОЖНАЯ КАРТА ПРОЕКТА (ROADMAP) ================</color>",
                                     "<color=#FFCC00>================ PROJECT ROADMAP (ROADMAP) ================</color>"));
        AddLog(LocalizationManager.T("<color=#00FF88>[✔] Локация НИИ:</color> Архитектура кабинета, мебель, ретро-стиль, коллизии",
                                     "<color=#00FF88>[✔] Facility Lab:</color> Cabinet layout, retro styling, physics collisions"));
        AddLog(LocalizationManager.T("<color=#00FF88>[✔] Карманные часы:</color> Таймер обратного отсчета + круговой радар",
                                     "<color=#00FF88>[✔] Pocket Watch:</color> Countdown timer + circular acoustic radar"));
        AddLog(LocalizationManager.T("<color=#00FF88>[✔] Сканирование:</color> Засечка охранников лучом, затухание люминофора CRT",
                                     "<color=#00FF88>[✔] Scanning Ray:</color> Guard detection beam, phosphor decay"));
        AddLog(LocalizationManager.T("<color=#00FF88>[✔] Мейнфрейм «Вектор-1»:</color> Рубильник, прогрев ламп, магнитная лента",
                                     "<color=#00FF88>[✔] Vector-1 Mainframe:</color> Power knife switch, tube warm-up, magnetic tape"));
        AddLog(LocalizationManager.T("<color=#FFCC00>[▶] ТЕРМИНАЛ ДЕШИФРАТОРА:</color> <b>В РАЗРАБОТКЕ (WORK IN PROGRESS)</b>",
                                     "<color=#FFCC00>[▶] CRYPTANALYSIS TERMINAL:</color> <b>WORK IN PROGRESS (ACTIVE)</b>"));
        AddLog(LocalizationManager.T("    • Мини-игры шифров, телекома и досье",
                                     "    • Cipher, telecom, and personnel dossier mini-games"));
        AddLog(LocalizationManager.T("    • Считывание дампов секретных директив из памяти ЭВМ",
                                     "    • Dumping classified directives from mainframe memory"));
        AddLog("<color=#FFCC00>==================================================================</color>");
    }

    public void ShowWipModules()
    {
        PlayKeyClick();
        AddLog(LocalizationManager.T("<color=#00FFFF>=================== СТАТУС ПОДСИСТЕМ ТЕРМИНАЛА ===================</color>",
                                     "<color=#00FFFF>================ SUBSYSTEM DIAGNOSTICS ================</color>"));
        AddLog(LocalizationManager.T("<color=#00FF88>[ПИТАНИЕ]</color> Трансформатор 1500В, лампы накала: СТАБИЛЬНО ОНЛАЙН",
                                     "<color=#00FF88>[POWER]</color> 1500V Transformer, vacuum tubes: STABLE ONLINE"));
        AddLog(LocalizationManager.T("<color=#00FF88>[НОСИТЕЛЬ]</color> Ядро DECODER-KERNEL-70: СЧИТАНО С ЛЕНТЫ В ПАМЯТЬ",
                                     "<color=#00FF88>[MEDIA]</color> DECODER-KERNEL-70: LOADED FROM TAPE TO MEMORY"));
        AddLog(LocalizationManager.T("<color=#00FF88>[ЛОКАТОР]</color> Синхронизация с часами: 3 ПАТРУЛЬНЫХ СЕКТОРА АКТИВНЫ",
                                     "<color=#00FF88>[RADAR]</color> Watch sync: 3 PATROL SECTORS MONITORED"));
        AddLog(LocalizationManager.T("<color=#FFCC00>[КРИПТО]</color> Модуль дешифровки кодов: <color=#FFAA33>[ В РАЗРАБОТКЕ / WIP ]</color>",
                                     "<color=#FFCC00>[CRYPTO]</color> Decryption core: <color=#FFAA33>[ IN DEVELOPMENT / WIP ]</color>"));
        AddLog(LocalizationManager.T("<color=#FFCC00>[ОКОННЫЙ СТЕК]</color> Графический интерфейс: <color=#FFAA33>[ PRE-ALPHA v0.4 ]</color>",
                                     "<color=#FFCC00>[GUI STACK]</color> Graphical terminal UI: <color=#FFAA33>[ PRE-ALPHA v0.4 ]</color>"));
        AddLog(LocalizationManager.T("<color=#FFCC00>Прогресс готовности системы: [██████████████░░░░░░] 68%</color>",
                                     "<color=#FFCC00>System readiness progress: [██████████████░░░░░░] 68%</color>"));
    }

    public void RunCrtTest()
    {
        PlayKeyClick();
        if (crtTestCoroutine != null) StopCoroutine(crtTestCoroutine);
        crtTestCoroutine = StartCoroutine(CrtTestRoutine());
    }

    private IEnumerator CrtTestRoutine()
    {
        AddLog(LocalizationManager.T("<color=#00FF88>[CRT-DIAG] Инициализация калибровки трубки ЭЛТ-70...</color>",
                                     "<color=#00FF88>[CRT-DIAG] Initializing CRT-70 tube calibration...</color>"));
        yield return new WaitForSeconds(0.35f);
        AddLog(LocalizationManager.T("[CRT-DIAG] Кадровая развертка: 50 Гц | Люминофор: P31 Зеленый | Фокус: 100%",
                                     "[CRT-DIAG] Vertical refresh: 50 Hz | Phosphor: P31 Green | Focus: 100%"));
        yield return new WaitForSeconds(0.35f);
        AddLog("[CRT-DIAG] ░░▒▒▓▓████████████████████████████████████▓▓▒▒░░");
        yield return new WaitForSeconds(0.35f);
        AddLog(LocalizationManager.T("<color=#00FFFF>[CRT-DIAG] Калибровка монитора завершена. Матрица символов исправна.</color>",
                                     "<color=#00FFFF>[CRT-DIAG] CRT calibration completed. Character matrix OK.</color>"));
    }

    public void ToggleRoomLight()
    {
        PlayKeyClick();
        if (roomLight != null)
        {
            isRoomLightOn = !isRoomLightOn;
            roomLight.enabled = isRoomLightOn;
            string stateStr = isRoomLightOn 
                ? LocalizationManager.T("<color=#00FF88>ВКЛЮЧЕНО</color>", "<color=#00FF88>ENABLED</color>") 
                : LocalizationManager.T("<color=#FF4444>ВЫКЛЮЧЕНО</color>", "<color=#FF4444>DISABLED</color>");
            AddLog(LocalizationManager.T($"[СВЕТ] Освещение кабинета: {stateStr}",
                                         $"[LIGHT] Office illumination: {stateStr}"));
        }
        else
        {
            AddLog(LocalizationManager.T("[СВЕТ] Ошибка: Источник освещения не найден.",
                                         "[LIGHT] Error: Room light source not found."));
        }
    }

    // Legacy wrappers so any remaining UnityEvents still work gracefully
    public void StartDecryption() => ShowWipModules();
    public void ShowSystemDiagnostics() => RunCrtTest();
    public void ShowSecretNotes() => ShowRoadmap();

    private void PlayKeyClick()
    {
        if (terminalAudio != null && keyClickClip != null)
        {
            terminalAudio.pitch = Random.Range(0.95f, 1.05f);
            terminalAudio.PlayOneShot(keyClickClip, 0.45f);
        }
    }

    private void GenerateAudioClips()
    {
        // 1. CRT startup rising chirp + thump
        crtStartupClip = CreateCrtStartupSound();

        // 2. Relay / keyboard mechanical click
        keyClickClip = CreateKeyClickSound();

        // 3. Alert / rejection sound
        alertClip = CreateAlertSound();

        // 4. Potentiometer detent click
        potentiometerClickClip = CreatePotentiometerClickSound();
    }

    private AudioClip CreateCrtStartupSound()
    {
        int sampleRate = 44100;
        int samples = (int)(sampleRate * 0.45f);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float freq = Mathf.Lerp(120f, 950f, t / 0.45f);
            float sine = Mathf.Sin(2f * Mathf.PI * freq * t);
            float hum = Mathf.Sin(2f * Mathf.PI * 50f * t) * 0.4f;
            float envelope = Mathf.Sin(Mathf.PI * (t / 0.45f));
            data[i] = (sine * 0.6f + hum) * envelope;
        }

        var clip = AudioClip.Create("CRTStartup", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip CreateKeyClickSound()
    {
        int sampleRate = 44100;
        int samples = (int)(sampleRate * 0.035f);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float noise = (Random.value * 2f - 1f) * 0.4f;
            float click = Mathf.Sin(2f * Mathf.PI * 1800f * (float)i / sampleRate) * 0.6f;
            float env = 1f - t;
            data[i] = (click + noise) * env * env;
        }

        var clip = AudioClip.Create("KeyClick", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip CreateAlertSound()
    {
        int sampleRate = 44100;
        int samples = (int)(sampleRate * 0.18f);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float sine = Mathf.Sin(2f * Mathf.PI * 280f * t);
            float env = 1f - ((float)i / samples);
            data[i] = sine * env;
        }

        var clip = AudioClip.Create("AlertBuzz", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip CreatePotentiometerClickSound()
    {
        int sampleRate = 44100;
        int samples = (int)(sampleRate * 0.045f);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float click = Mathf.Sin(2f * Mathf.PI * 920f * (float)i / sampleRate);
            float click2 = Mathf.Sin(2f * Mathf.PI * 1480f * (float)i / sampleRate) * 0.4f;
            float noise = (Random.value * 2f - 1f) * 0.35f;
            float env = Mathf.Exp(-t * 28f);
            data[i] = (click + click2 + noise) * env * 0.6f;
        }

        var clip = AudioClip.Create("PotentiometerClick", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
