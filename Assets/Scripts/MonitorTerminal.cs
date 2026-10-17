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
    [SerializeField] private GameObject activeScreenPanel;
    [SerializeField] private GameObject idleScreenPanel;
    [SerializeField] private GameObject offScreenPanel;

    [Header("Screen Lighting & Sound")]
    [SerializeField] private Light screenGlowLight;
    [SerializeField] private AudioSource terminalAudio;

    private bool isRoomLightOn = true;
    private bool isZoomedIn = false;
    private bool isMainframeOnline = false;
    private float cursorBlinkTimer = 0f;
    private bool cursorVisible = true;
    private Coroutine crtTestCoroutine;
    private readonly List<string> logLines = new List<string>();

    // Procedural sound clips
    private AudioClip crtStartupClip;
    private AudioClip keyClickClip;
    private AudioClip alertClip;

    public bool IsZoomedIn => isZoomedIn;
    public bool IsMainframeOnline => isMainframeOnline;

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
        }

        GenerateAudioClips();
        SetupWipButtons();
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

            if (isZoomedIn)
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
            if (t != null) t.text = "📋 Дорожная карта [1]";
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
            if (t != null) t.text = "📊 Статус модулей [2]";
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
            if (t != null) t.text = "📺 Тест ЭЛТ-экрана [3]";
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
            if (t != null) t.text = "💡 Свет в кабинете [4]";
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
        isMainframeOnline = online;

        if (online)
        {
            if (offScreenPanel != null) offScreenPanel.SetActive(false);
            if (screenGlowLight != null) screenGlowLight.enabled = true;
            if (headerPanel != null) headerPanel.SetActive(true);

            var title = headerPanel != null ? headerPanel.transform.Find("TitleText")?.GetComponent<Text>() : null;
            if (title != null)
            {
                title.text = "ЭВМ «ВЕКТОР-1» // СИСТЕМА ДЕКОДЕР [WORK IN PROGRESS]";
                title.color = new Color(0.0f, 1.0f, 0.85f);
            }

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

            UpdateIdleScreenText();

            // Populate initial WIP log
            logLines.Clear();
            AddLog("<color=#00FFAA>===========================================================</color>");
            AddLog("<color=#FFCC00><b>/// РАБОЧИЙ ТЕРМИНАЛ «ДЕКОДЕР» — WORK IN PROGRESS ///</b></color>");
            AddLog("<color=#88DDCC>Интерфейс оператора находится в активной разработке.</color>");
            AddLog("<color=#00FFAA>===========================================================</color>");
            AddLog("<color=#72D572>[04:12] Силовое питание от мейнфрейма «Вектор-1» получено.</color>");
            AddLog("<color=#72D572>[04:12] Ядро DECODER-OS v0.4 загружено с магнитной ленты.</color>");
            AddLog("<color=#FFCC00>[04:12] ⚠ МОДУЛЬ ДЕШИФРАЦИИ: В РАЗРАБОТКЕ (WORK IN PROGRESS).</color>");
            AddLog("<color=#88DDCC>[04:12] Выберите пункт [1] Дорожная карта для просмотра задач.</color>");

            if (terminalAudio != null && crtStartupClip != null)
            {
                terminalAudio.PlayOneShot(crtStartupClip, 0.65f);
            }
        }
        else
        {
            // Server is OFFLINE: computer screen is completely unpowered
            if (offScreenPanel != null) offScreenPanel.SetActive(true);
            if (screenGlowLight != null) screenGlowLight.enabled = false;
            if (headerPanel != null) headerPanel.SetActive(false);
            if (idleScreenPanel != null) idleScreenPanel.SetActive(false);
            if (activeScreenPanel != null) activeScreenPanel.SetActive(false);
        }
    }

    public void SetZoomedIn(bool zoomed)
    {
        isZoomedIn = zoomed;

        if (!isMainframeOnline)
        {
            if (offScreenPanel != null) offScreenPanel.SetActive(true);
            if (activeScreenPanel != null) activeScreenPanel.SetActive(false);
            if (idleScreenPanel != null) idleScreenPanel.SetActive(false);
            return;
        }

        if (activeScreenPanel != null) activeScreenPanel.SetActive(zoomed);
        if (idleScreenPanel != null) idleScreenPanel.SetActive(!zoomed);
        if (offScreenPanel != null) offScreenPanel.SetActive(false);

        if (terminalAudio != null && keyClickClip != null)
        {
            terminalAudio.PlayOneShot(keyClickClip, 0.4f);
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
        }
#else
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) ShowRoadmap();
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) ShowWipModules();
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) RunCrtTest();
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) ToggleRoomLight();
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
        AddLog("<color=#FFCC00>================ ДОРОЖНАЯ КАРТА ПРОЕКТА (ROADMAP) ================</color>");
        AddLog("<color=#00FF88>[✔] Локация НИИ:</color> Архитектура кабинета, мебель, ретро-стиль, коллизии");
        AddLog("<color=#00FF88>[✔] Карманные часы:</color> Таймер обратного отсчета + круговой радар");
        AddLog("<color=#00FF88>[✔] Сканирование:</color> Засечка охранников лучом, затухание люминофора CRT");
        AddLog("<color=#00FF88>[✔] Мейнфрейм «Вектор-1»:</color> Рубильник, прогрев ламп, магнитная лента");
        AddLog("<color=#FFCC00>[▶] ТЕРМИНАЛ ДЕШИФРАТОРА:</color> <b>В РАЗРАБОТКЕ (WORK IN PROGRESS)</b>");
        AddLog("    • Мини-игра подбора шифров AES и радиочастот (в разработке)");
        AddLog("    • Чтение дампов секретных директив из памяти ЭВМ");
        AddLog("<color=#FFCC00>==================================================================</color>");
    }

    public void ShowWipModules()
    {
        PlayKeyClick();
        AddLog("<color=#00FFFF>=================== СТАТУС ПОДСИСТЕМ ТЕРМИНАЛА ===================</color>");
        AddLog("<color=#00FF88>[ПИТАНИЕ]</color> Трансформатор 1500В, лампы накала: СТАБИЛЬНО ОНЛАЙН");
        AddLog("<color=#00FF88>[НОСИТЕЛЬ]</color> Ядро DECODER-KERNEL-70: СЧИТАНО С ЛЕНТЫ В ПАМЯТЬ");
        AddLog("<color=#00FF88>[ЛОКАТОР]</color> Синхронизация с часами: 3 ПАТРУЛЬНЫХ СЕКТОРА АКТИВНЫ");
        AddLog("<color=#FFCC00>[КРИПТО]</color> Модуль дешифровки кодов: <color=#FFAA33>[ В РАЗРАБОТКЕ / WIP ]</color>");
        AddLog("<color=#FFCC00>[ОКОННЫЙ СТЕК]</color> Графический интерфейс: <color=#FFAA33>[ PRE-ALPHA v0.4 ]</color>");
        AddLog("<color=#FFCC00>Прогресс готовности системы: [██████████████░░░░░░] 68%</color>");
    }

    public void RunCrtTest()
    {
        PlayKeyClick();
        if (crtTestCoroutine != null) StopCoroutine(crtTestCoroutine);
        crtTestCoroutine = StartCoroutine(CrtTestRoutine());
    }

    private IEnumerator CrtTestRoutine()
    {
        AddLog("<color=#00FF88>[CRT-DIAG] Инициализация калибровки трубки ЭЛТ-70...</color>");
        yield return new WaitForSeconds(0.35f);
        AddLog("[CRT-DIAG] Кадровая развертка: 50 Гц | Люминофор: P31 Зеленый | Фокус: 100%");
        yield return new WaitForSeconds(0.35f);
        AddLog("[CRT-DIAG] ░░▒▒▓▓████████████████████████████████████▓▓▒▒░░");
        yield return new WaitForSeconds(0.35f);
        AddLog("<color=#00FFFF>[CRT-DIAG] Калибровка монитора завершена. Матрица символов исправна.</color>");
    }

    public void ToggleRoomLight()
    {
        PlayKeyClick();
        if (roomLight != null)
        {
            isRoomLightOn = !isRoomLightOn;
            roomLight.enabled = isRoomLightOn;
            string stateStr = isRoomLightOn ? "<color=#00FF88>ВКЛЮЧЕНО</color>" : "<color=#FF4444>ВЫКЛЮЧЕНО</color>";
            AddLog($"[СВЕТ] Освещение кабинета: {stateStr}");
        }
        else
        {
            AddLog("[СВЕТ] Ошибка: Источник освещения не найден.");
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
}
