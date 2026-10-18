using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Authentic two-panel retro file manager «ВЕКТОР-КОМАНДИР» (Vector Commander).
/// Faithful tribute to Norton Commander / Volkov Commander on Soviet 1980s CRTs:
/// - Left Panel: Source mainframe database C:\СЕКРЕТНО (secret files, dossiers, directives)
/// - Right Panel: Target removable cartridge A:\КАРТРИДЖ (shows free space, copied archives, drive bay thermal status)
/// - Bottom F1-F10 function key strip (1Помощь, 2Статус, 3Чтение, 4Дешифр, 5Копия, 8Охлажд, 10Выход)
/// - Interactive file copying with real-time transfer progress bar & integration with DiskDriveBay thermal load!
/// - Built-in F3 text viewer for secret directives and technical lore.
/// </summary>
public class VectorCommander : MonoBehaviour
{
    public static VectorCommander Instance { get; private set; }

    [System.Serializable]
    public class VirtualFile
    {
        public string name;
        public int sizeBytes;
        public string date;
        public bool isDirectory;
        public bool isDecrypted;
        [TextArea(3, 10)] public string content;

        public VirtualFile(string name, int sizeBytes, string date, bool isDirectory, string content = "", bool isDecrypted = false)
        {
            this.name = name;
            this.sizeBytes = sizeBytes;
            this.date = date;
            this.isDirectory = isDirectory;
            this.content = content;
            this.isDecrypted = isDirectory || isDecrypted;
        }

        public string GetFormattedSize()
        {
            if (isDirectory) return "<ПАПКА>";
            if (sizeBytes < 1024) return $"{sizeBytes} Б";
            return $"{sizeBytes / 1024} КБ";
        }

        public string GetStatusBadge()
        {
            if (isDirectory) return "<color=#88CCAA>ГОТОВ </color>";
            return isDecrypted ? "<color=#00FFAA>РАСШИФР</color>" : "<color=#FFCC00>ЗАШИФР </color>";
        }
    }

    [Header("UI References")]
    [SerializeField] private Text headerTitleText;
    [SerializeField] private Text headerClockText;

    [Header("Left Panel (Source C:\\СЕКРЕТНО)")]
    [SerializeField] private Text leftPanelTitleText;
    [SerializeField] private Text leftPanelContentText;
    [SerializeField] private Text leftPanelFooterText;

    [Header("Right Panel (Target A:\\КАРТРИДЖ)")]
    [SerializeField] private Text rightPanelTitleText;
    [SerializeField] private Text rightPanelContentText;
    [SerializeField] private Text rightPanelFooterText;

    [Header("Command & Progress Line")]
    [SerializeField] private Text commandLineText;
    [SerializeField] private Text progressBarText;
    [SerializeField] private Text thermalAlertText;

    [Header("Viewer Modal (F3 Чтение)")]
    [SerializeField] private GameObject viewerModal;
    [SerializeField] private Text viewerTitleText;
    [SerializeField] private Text viewerBodyText;
    [SerializeField] private Button viewerCloseButton;

    [Header("Help Modal (F1 Помощь)")]
    [SerializeField] private GameObject helpModal;
    [SerializeField] private Button helpCloseButton;

    public bool IsAnyModalOpen => (helpModal != null && helpModal.activeSelf) || (viewerModal != null && viewerModal.activeSelf);

    [Header("Function Key Buttons (F1-F10)")]
    [SerializeField] private Button btnF1;
    [SerializeField] private Button btnF2;
    [SerializeField] private Button btnF3;
    [SerializeField] private Button btnF4;
    [SerializeField] private Button btnF5;
    [SerializeField] private Button btnF8;
    [SerializeField] private Button btnF10;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    private AudioClip keyTickClip;
    private AudioClip seekTickClip;
    private AudioClip copySuccessClip;
    private AudioClip errorBuzzClip;

    // Virtual File Systems
    private readonly List<VirtualFile> leftFiles = new List<VirtualFile>();
    private readonly List<VirtualFile> rightFiles = new List<VirtualFile>();

    private int leftSelectedIndex = 0;
    private int rightSelectedIndex = 0;
    private bool isLeftPanelActive = true;

    private bool isCopying = false;
    private float copyProgress = 0f;
    private string copyingFileName = "";
    private Coroutine copyCoroutine;

    private float cursorTimer = 0f;
    private bool cursorVisible = true;

    // Cartridge specifications
    private const int CARTRIDGE_CAPACITY_BYTES = 512 * 1024; // 512 KB

    private void Awake()
    {
        Instance = this;
        GenerateAudioClips();
        InitializeVirtualFiles();
    }

    private void Start()
    {
        SetupButtonListeners();
        UpdateAllVisuals();
    }

    private void Update()
    {
        if (!gameObject.activeInHierarchy) return;

        // Blinking cursor
        cursorTimer += Time.deltaTime;
        if (cursorTimer >= 0.45f)
        {
            cursorTimer = 0f;
            cursorVisible = !cursorVisible;
            UpdateCommandLinePrompt();
        }

        // Live header clock
        if (headerClockText != null)
        {
            headerClockText.text = DateTime.Now.ToString("HH:mm:ss");
        }

        // Live thermal status on right panel
        UpdateThermalAlert();

        // Handle keyboard navigation if modal is closed
        if (MonitorTerminal.Instance != null && MonitorTerminal.Instance.IsZoomedIn)
        {
            HandleKeyboardInput();
        }
    }

    private void InitializeVirtualFiles()
    {
        leftFiles.Clear();
        rightFiles.Clear();

        // Source database C:\СЕКРЕТНО (10 specialized spy file formats)
        leftFiles.Add(new VirtualFile("..", 0, "19.10.84", true, "Корневой каталог диска C:\\", true));
        leftFiles.Add(new VirtualFile("ИНСТРУКЦИЯ_ЭВМ.TXT", 12 * 1024, "05.10.84", false,
            "МИНИСТЕРСТВО РАДИОПРОМЫШЛЕННОСТИ СССР // СПЕЦИАЛЬНЫЙ НИИ «ВЕКТОР»\n" +
            "СЛУЖЕБНОЕ РУКОВОДСТВО ОПЕРАТОРА ПО КРИПТОАНАЛИЗУ И ДЕШИФРАЦИИ\n" +
            "───────────────────────────────────────────────────────────────────\n\n" +
            "1. ОБЩИЙ РЕГЛАМЕНТ РАБОТЫ:\n" +
            "• Зашифрованные файлы на диске C:\\ помечены маркером [ЗАШИФР].\n" +
            "• Выберите файл стрелками и нажмите [4] ДЕШИФР или [ENTER] для запуска модуля.\n" +
            "• После решения криптозадачи файл переходит в статус [РАСШИФР].\n" +
            "• Нажмите [5] КОПИЯ для записи на сменный картридж в накопителе A:\\.\n" +
            "• Следите за нагревом диска! При 90°C сработает защита от расплавления.\n\n" +
            "2. МОДУЛЬ «МАШИНА ТЬЮРИНГА» (ФАЙЛ ДЕКОДЕР_V2.BIN):\n" +
            "Главное ядро защищено контрольным числом инкремента (+1):\n" +
            "• Состояние q0: автоматическая перемотка каретки к младшему биту.\n" +
            "• Состояние q1: сложение двоичных разрядов в столбик (1011 + 1 = 1100):\n" +
            "  - При чтении '1': 1 + 1 = 10 ──► Запись [0], Сдвиг [← ВЛЕВО], Состояние [q1]\n" +
            "  - При чтении '0': 0 + 1 = 1  ──► Запись [1], Сдвиг [• СТОП ], Состояние [HALT]\n" +
            "• Управление: [ПРОБЕЛ] — шаг каретки (отладка), [ENTER] — пуск, [R] — сброс.\n\n" +
            "3. ЧАСТОТНЫЙ СПЕКТР (ДИРЕКТИВА_42.DOC):\n" +
            "Сдвигайте алфавит стрелками, пока пики гласных не совпадут с буквами О, Е, А.\n\n" +
            "4. КАРТОТЕКА НИИ (КАРТОТЕКА_НИИ.TXT):\n" +
            "Пароль администратора: Отдел [КРИПТОГРАФИЯ], Допуск [ОСОБЫЙ], Смена [НОЧНАЯ].\n\n" +
            "5. ДАМП ОЗУ (ДАМП_ПАМЯТИ.MEM):\n" +
            "Адрес тревоги 0x4F0A замораживается инъекцией NOP-байтов [FF] [FF] [00] [00].", true));

        leftFiles.Add(new VirtualFile("ДЕКОДЕР_V2.BIN", 64 * 1024, "18.10.84", false,
            ">> ГЛАВНАЯ ЦЕЛЬ ОПЕРАЦИИ <<\n" +
            "Исполняемое ядро криптоалгоритма «Вектор-70».\n" +
            "Защищено контрольной суммой перфоленты. Требуется Машина Тьюринга.", false));

        leftFiles.Add(new VirtualFile("ДИРЕКТИВА_42.DOC", 16 * 1024, "10.10.84", false,
            "СЕКРЕТНАЯ ДИРЕКТИВА УПРАВЛЕНИЯ №42-Б\n" +
            "Зашифрована шифром подстановки Цезаря. Требуется Частотный анализ.", false));

        leftFiles.Add(new VirtualFile("КАРТОТЕКА_НИИ.TXT", 32 * 1024, "15.10.84", false,
            "База данных личного состава дежурной смены Сектора 7.\n" +
            "Требуется Реляционный фильтр для нахождения пароля администратора.", false));

        leftFiles.Add(new VirtualFile("ДАМП_ПАМЯТИ.MEM", 128 * 1024, "14.10.84", false,
            "Дамп сторожевого вектора оперативной памяти.\n" +
            "Требуется Hex-инъекция байтов NOP для заморозки тревоги.", false));

        leftFiles.Add(new VirtualFile("ПЕРЕХВАТ_УКВ.RAD", 48 * 1024, "12.10.84", false,
            "Запись эфира армейской радиостанции 144.200 МГц.\n" +
            "Требуется Осциллограф Лиссажу для слияния несущей частоты.", false));

        leftFiles.Add(new VirtualFile("МАРШРУТЫ_СЕТИ.NET", 64 * 1024, "11.10.84", false,
            "Таблица коммутации релейных шлюзов подземного бункера.\n" +
            "Требуется матричная маршрутизация пакета данных.", false));

        leftFiles.Add(new VirtualFile("КОД_ХЭММИНГА.PAR", 24 * 1024, "09.10.84", false,
            "Ферритовая матрица контрольных сумм ОЗУ.\n" +
            "Требуется устранение битового сбоя по алгоритму Хэмминга.", false));

        leftFiles.Add(new VirtualFile("СЕКТОРЫ_ДИСКА.DSK", 256 * 1024, "07.10.84", false,
            "Фрагментированный секторный архив жесткого диска ЕС-5061.\n" +
            "Требуется секторный захват кластеров в тайминг вращения.", false));

        leftFiles.Add(new VirtualFile("БУФЕР_СТЕКА.STK", 16 * 1024, "05.10.84", false,
            "Стек регистров доступа операционной системы.\n" +
            "Требуется переполнение буфера до значения 16 командами PUSH/POP.", false));

        leftFiles.Add(new VirtualFile("ШИФР_ФИАЛКА.ROT", 96 * 1024, "03.10.84", false,
            "Векторная схема объекта, зашифрованная машиной «Фиалка-М125».\n" +
            "Требуется установка роторов по ключу дня [ IV - II - VII ].", false));

        leftSelectedIndex = 2; // Initially highlight ДЕКОДЕР_V2.BIN
    }

    private void SetupButtonListeners()
    {
        if (btnF1 != null) { btnF1.onClick.RemoveAllListeners(); btnF1.onClick.AddListener(ShowHelp); }
        if (btnF2 != null) { btnF2.onClick.RemoveAllListeners(); btnF2.onClick.AddListener(ShowStatus); }
        if (btnF3 != null) { btnF3.onClick.RemoveAllListeners(); btnF3.onClick.AddListener(ViewSelectedFile); }
        if (btnF4 != null) { btnF4.onClick.RemoveAllListeners(); btnF4.onClick.AddListener(ShowDecryptorMode); }
        if (btnF5 != null) { btnF5.onClick.RemoveAllListeners(); btnF5.onClick.AddListener(CopySelectedFile); }
        if (btnF8 != null) { btnF8.onClick.RemoveAllListeners(); btnF8.onClick.AddListener(CheckCoolingStatus); }
        if (btnF10 != null) { btnF10.onClick.RemoveAllListeners(); btnF10.onClick.AddListener(ExitToStartScreen); }

        // Guaranteed runtime hookup for modal close buttons
        if (helpModal != null)
        {
            var btn = helpCloseButton != null ? helpCloseButton : helpModal.GetComponentInChildren<Button>(true);
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(CloseHelp);
            }
        }

        if (viewerModal != null)
        {
            var btn = viewerCloseButton != null ? viewerCloseButton : viewerModal.GetComponentInChildren<Button>(true);
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(CloseViewer);
            }
        }
    }

    public void CloseHelp()
    {
        PlayKeyTick();
        if (helpModal != null) helpModal.SetActive(false);
    }

    private void HandleKeyboardInput()
    {
        // Close modal if open
        if (helpModal != null && helpModal.activeSelf)
        {
            if (IsKeyPressed(KeyCode.Escape) || IsKeyPressed(KeyCode.F1) || IsKeyPressed(KeyCode.Alpha1) ||
                IsKeyPressed(KeyCode.Return) || IsKeyPressed(KeyCode.Space) || IsKeyPressed(KeyCode.F10) || IsKeyPressed(KeyCode.Alpha0))
            {
                CloseHelp();
            }
            return;
        }

        if (viewerModal != null && viewerModal.activeSelf)
        {
            if (IsKeyPressed(KeyCode.Escape) || IsKeyPressed(KeyCode.F3) || IsKeyPressed(KeyCode.Alpha3) ||
                IsKeyPressed(KeyCode.Return) || IsKeyPressed(KeyCode.Space) || IsKeyPressed(KeyCode.F10) || IsKeyPressed(KeyCode.Alpha0))
            {
                CloseViewer();
            }
            return;
        }

        // Navigation
        if (IsKeyPressed(KeyCode.UpArrow))
        {
            MoveSelection(-1);
        }
        else if (IsKeyPressed(KeyCode.DownArrow))
        {
            MoveSelection(1);
        }
        else if (IsKeyPressed(KeyCode.Tab))
        {
            isLeftPanelActive = !isLeftPanelActive;
            PlayKeyTick();
            UpdateAllVisuals();
        }
        else if (IsKeyPressed(KeyCode.Return))
        {
            var sel = GetCurrentSelectedFile();
            if (sel != null && !sel.isDirectory && !sel.isDecrypted)
            {
                ShowDecryptorMode();
            }
            else
            {
                ViewSelectedFile();
            }
        }
        else if (IsKeyPressed(KeyCode.F1) || IsKeyPressed(KeyCode.Alpha1))
        {
            ShowHelp();
        }
        else if (IsKeyPressed(KeyCode.F2) || IsKeyPressed(KeyCode.Alpha2))
        {
            ShowStatus();
        }
        else if (IsKeyPressed(KeyCode.F3) || IsKeyPressed(KeyCode.Alpha3))
        {
            ViewSelectedFile();
        }
        else if (IsKeyPressed(KeyCode.F4) || IsKeyPressed(KeyCode.Alpha4))
        {
            ShowDecryptorMode();
        }
        else if (IsKeyPressed(KeyCode.F5) || IsKeyPressed(KeyCode.Alpha5))
        {
            CopySelectedFile();
        }
        else if (IsKeyPressed(KeyCode.F8) || IsKeyPressed(KeyCode.Alpha8))
        {
            CheckCoolingStatus();
        }
        else if (IsKeyPressed(KeyCode.F10) || IsKeyPressed(KeyCode.Alpha0) || IsKeyPressed(KeyCode.Escape))
        {
            ExitToStartScreen();
        }
    }

    private bool IsKeyPressed(KeyCode code)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;

        switch (code)
        {
            case KeyCode.UpArrow: return kb.upArrowKey.wasPressedThisFrame;
            case KeyCode.DownArrow: return kb.downArrowKey.wasPressedThisFrame;
            case KeyCode.Tab: return kb.tabKey.wasPressedThisFrame;
            case KeyCode.Return: return kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
            case KeyCode.Escape: return kb.escapeKey.wasPressedThisFrame;
            case KeyCode.F1: return kb.f1Key.wasPressedThisFrame;
            case KeyCode.F2: return kb.f2Key.wasPressedThisFrame;
            case KeyCode.F3: return kb.f3Key.wasPressedThisFrame;
            case KeyCode.F4: return kb.f4Key.wasPressedThisFrame;
            case KeyCode.F5: return kb.f5Key.wasPressedThisFrame;
            case KeyCode.F8: return kb.f8Key.wasPressedThisFrame;
            case KeyCode.F10: return kb.f10Key.wasPressedThisFrame;
            case KeyCode.Space: return kb.spaceKey.wasPressedThisFrame;
            case KeyCode.Alpha1: return kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame;
            case KeyCode.Alpha2: return kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame;
            case KeyCode.Alpha3: return kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame;
            case KeyCode.Alpha4: return kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame;
            case KeyCode.Alpha5: return kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame;
            case KeyCode.Alpha8: return kb.digit8Key.wasPressedThisFrame || kb.numpad8Key.wasPressedThisFrame;
            case KeyCode.Alpha0: return kb.digit0Key.wasPressedThisFrame || kb.numpad0Key.wasPressedThisFrame;
            default: return false;
        }
#else
        return Input.GetKeyDown(code);
#endif
    }

    private void MoveSelection(int delta)
    {
        PlayKeyTick();
        if (isLeftPanelActive)
        {
            leftSelectedIndex = Mathf.Clamp(leftSelectedIndex + delta, 0, leftFiles.Count - 1);
        }
        else
        {
            if (rightFiles.Count > 0)
            {
                rightSelectedIndex = Mathf.Clamp(rightSelectedIndex + delta, 0, rightFiles.Count - 1);
            }
        }
        UpdateAllVisuals();
    }

    public void SelectLeftFile(int index)
    {
        isLeftPanelActive = true;
        leftSelectedIndex = Mathf.Clamp(index, 0, leftFiles.Count - 1);
        PlayKeyTick();
        UpdateAllVisuals();
    }

    public void SelectRightFile(int index)
    {
        isLeftPanelActive = false;
        if (rightFiles.Count > 0)
        {
            rightSelectedIndex = Mathf.Clamp(index, 0, rightFiles.Count - 1);
        }
        PlayKeyTick();
        UpdateAllVisuals();
    }

    public void UpdateAllVisuals()
    {
        UpdateLeftPanel();
        UpdateRightPanel();
        UpdateCommandLinePrompt();
        UpdateThermalAlert();
    }

    private void UpdateLeftPanel()
    {
        if (leftPanelTitleText != null)
        {
            string marker = isLeftPanelActive ? "◄ АКТИВНА" : "         ";
            leftPanelTitleText.text = $"╔═══ C:\\СЕКРЕТНО {marker} ═════════════════════════════════════╗";
        }

        if (leftPanelContentText != null)
        {
            var lines = new List<string>();
            lines.Add("<color=#66CCAA>  ИМЯ ФАЙЛА           РАЗМЕР   СТАТУС </color>");
            lines.Add("<color=#00AA77>  ────────────────────────────────────</color>");

            for (int i = 0; i < leftFiles.Count; i++)
            {
                var f = leftFiles[i];
                bool isSel = isLeftPanelActive && (i == leftSelectedIndex);
                string nameFormatted = f.name.PadRight(18);
                string sizeFormatted = f.GetFormattedSize().PadLeft(8);
                string statusFormatted = f.GetStatusBadge();

                if (isSel)
                {
                    lines.Add($"<b><color=#00FFAA>► {nameFormatted} {sizeFormatted} {statusFormatted} ◄</color></b>");
                }
                else
                {
                    string col = f.isDirectory ? "#88FFCC" : "#72D572";
                    lines.Add($"<color={col}>  {nameFormatted} {sizeFormatted} {statusFormatted}  </color>");
                }
            }

            leftPanelContentText.text = string.Join("\n", lines);
        }

        if (leftPanelFooterText != null)
        {
            int totalBytes = 0;
            foreach (var f in leftFiles) if (!f.isDirectory) totalBytes += f.sizeBytes;
            leftPanelFooterText.text = $"╚══ ФАЙЛОВ: {leftFiles.Count} ({totalBytes / 1024} КБ) │ СВОБОДНО: 120 МБ ══════════════════╝";
        }
    }

    private void UpdateRightPanel()
    {
        bool hasDriveBay = DiskDriveBay.Instance != null;
        bool isBayRunning = hasDriveBay && DiskDriveBay.Instance.State == DiskDriveBay.DriveBayState.Running;
        bool isCartridgeLocked = isBayRunning;
        int cartridgeId = hasDriveBay ? DiskDriveBay.Instance.CurrentCartridgeId : 1;

        if (rightPanelTitleText != null)
        {
            string marker = !isLeftPanelActive ? "◄ АКТИВНА" : "         ";
            string slotStatus = isCartridgeLocked ? $"[В СЛОТЕ: КАРТРИДЖ #{cartridgeId}]" : "[СЛОТ ПУСТ / ИЗВЛЕЧЕН]";
            rightPanelTitleText.text = $"╔═══ A:\\КАРТРИДЖ {slotStatus} {marker} ═══════════════╗";
        }

        if (rightPanelContentText != null)
        {
            var lines = new List<string>();
            lines.Add("<color=#66CCAA>  ИМЯ ФАЙЛА                РАЗМЕР      СТАТУС </color>");
            lines.Add("<color=#00AA77>  ───────────────────────────────────────────</color>");

            if (!isCartridgeLocked)
            {
                lines.Add("");
                lines.Add("  <color=#FFCC00>═══════════════════════════════════════════</color>");
                lines.Add("  <color=#FFFF66><b>[ ВНИМАНИЕ: СЛОТ ДИСКА НЕ АКТИВЕН ]</b></color>");
                lines.Add("  <color=#99FFCC>Накопитель извлечён или остановлен тормозом.</color>");
                lines.Add("  <color=#99FFCC>Вставьте охлажденный картридж в корзину.</color>");
                lines.Add("  <color=#FFCC00>═══════════════════════════════════════════</color>");
            }
            else if (rightFiles.Count == 0 && !isCopying)
            {
                lines.Add("");
                lines.Add("  <color=#558866>..                       <ВВЕРХ>     ГОТОВ  </color>");
                lines.Add("");
                lines.Add("  <color=#88CCAA><i>(Накопитель пуст. Выберите файл слева</i></color>");
                lines.Add("  <color=#88CCAA><i>и нажмите [5] Копия для записи данных)</i></color>");
            }
            else
            {
                for (int i = 0; i < rightFiles.Count; i++)
                {
                    var f = rightFiles[i];
                    bool isSel = !isLeftPanelActive && (i == rightSelectedIndex);
                    string nameFormatted = f.name.PadRight(22);
                    string sizeFormatted = f.GetFormattedSize().PadLeft(9);
                    string statusFormatted = "СКОПИРОВАН".PadLeft(10);

                    if (isSel)
                    {
                        lines.Add($"<b><color=#00FFAA>► {nameFormatted} {sizeFormatted} {statusFormatted} ◄</color></b>");
                    }
                    else
                    {
                        lines.Add($"<color=#72D572>  {nameFormatted} {sizeFormatted} {statusFormatted}  </color>");
                    }
                }

                if (isCopying)
                {
                    lines.Add("");
                    lines.Add($"  <color=#FFFF00>► {copyingFileName.PadRight(20)} [ЗАПИСЬ {Mathf.RoundToInt(copyProgress * 100f)}%]</color>");
                }
            }

            rightPanelContentText.text = string.Join("\n", lines);
        }

        if (rightPanelFooterText != null)
        {
            int usedBytes = 0;
            foreach (var f in rightFiles) usedBytes += f.sizeBytes;
            int freeBytes = Mathf.Max(0, CARTRIDGE_CAPACITY_BYTES - usedBytes);
            rightPanelFooterText.text = $"╚══ ЗАПИСАНО: {usedBytes / 1024} КБ │ СВОБОДНО: {freeBytes / 1024} / 512 КБ ══════════════╝";
        }
    }

    private void UpdateCommandLinePrompt()
    {
        string cursor = cursorVisible ? "_" : " ";
        if (commandLineText != null)
        {
            if (isCopying)
            {
                commandLineText.text = $"C:\\СЕКРЕТНО> КОПИРОВАНИЕ {copyingFileName} НА A:\\ {cursor}";
            }
            else
            {
                var sel = GetCurrentSelectedFile();
                string target = sel != null ? sel.name : "";
                commandLineText.text = $"C:\\СЕКРЕТНО> ВЫБРАН: {target} {cursor}";
            }
        }

        if (progressBarText != null)
        {
            if (isCopying)
            {
                int blocks = 24;
                int filled = Mathf.RoundToInt(copyProgress * blocks);
                string bar = new string('█', filled) + new string('░', blocks - filled);
                progressBarText.text = $"[ПЕРЕКАЧКА] [{bar}] {Mathf.RoundToInt(copyProgress * 100f)}% (14.2 КБ/с)";
            }
            else
            {
                progressBarText.text = "[РЕЖИМ ГОТОВНОСТИ] Нажмите [5] Копия для записи на картридж";
            }
        }
    }

    private void UpdateThermalAlert()
    {
        if (thermalAlertText == null) return;

        if (DiskDriveBay.Instance == null)
        {
            thermalAlertText.text = "<color=#00FF88>ТЕМПЕРАТУРА КАРТРИДЖА: 35°C [НОРМА]</color>";
            return;
        }

        float temp = DiskDriveBay.Instance.Temperature;
        string tempStr = $"{Mathf.RoundToInt(temp)}°C";

        if (temp >= 90f)
        {
            string blink = cursorVisible ? "▲▲▲" : "   ";
            thermalAlertText.text = $"<b><color=#FF3333>[КРИТИЧЕСКИЙ ПЕРЕГРЕВ: {tempStr}] ИЗВЛЕКИТЕ ДИСК ДЛЯ ОХЛАЖДЕНИЯ! {blink}</color></b>";
        }
        else if (temp >= 75f)
        {
            thermalAlertText.text = $"<color=#FFCC00>[ВНИМАНИЕ: НАГРЕВ КАРТРИДЖА: {tempStr}] Подготовьте резервную корзину</color>";
        }
        else
        {
            thermalAlertText.text = $"<color=#00FF88>ТЕМПЕРАТУРА КАРТРИДЖА: {tempStr} [НОРМАЛЬНАЯ РАБОТА]</color>";
        }
    }

    public VirtualFile GetCurrentSelectedFile()
    {
        if (isLeftPanelActive)
        {
            if (leftSelectedIndex >= 0 && leftSelectedIndex < leftFiles.Count)
                return leftFiles[leftSelectedIndex];
        }
        else
        {
            if (rightSelectedIndex >= 0 && rightSelectedIndex < rightFiles.Count)
                return rightFiles[rightSelectedIndex];
        }
        return null;
    }

    // --- Action Methods ---

    public void CopySelectedFile()
    {
        if (isCopying)
        {
            PlayErrorBuzz();
            MainframeBootQuest.Instance?.ShowNotification("<color=#FFCC00>Перекачка данных уже выполняется!</color>");
            return;
        }

        var file = GetCurrentSelectedFile();
        if (file == null || file.isDirectory)
        {
            PlayErrorBuzz();
            MainframeBootQuest.Instance?.ShowNotification("<color=#FFCC00>Невозможно скопировать каталог!</color>");
            return;
        }

        // Encryption check: cannot copy encrypted files
        if (!file.isDecrypted)
        {
            PlayErrorBuzz();
            MainframeBootQuest.Instance?.ShowNotification(
                $"<color=#FF3333>[ЗАЩИТА]</color> Файл {file.name} зашифрован! Нажмите [4] Дешифр или [ENTER]!");
            return;
        }

        // Check if file is already on cartridge
        if (rightFiles.Exists(f => f.name == file.name))
        {
            PlayErrorBuzz();
            MainframeBootQuest.Instance?.ShowNotification($"<color=#FFCC00>Файл {file.name} уже записан на картридж!</color>");
            return;
        }

        // Check if drive bay has cartridge running
        if (DiskDriveBay.Instance != null)
        {
            if (DiskDriveBay.Instance.State != DiskDriveBay.DriveBayState.Running)
            {
                PlayErrorBuzz();
                MainframeBootQuest.Instance?.ShowNotification(
                    "<color=#FF4444>[ОШИБКА I/O]</color> Картридж не зафиксирован в корзине дисковода!");
                return;
            }

            if (DiskDriveBay.Instance.Temperature >= 90f)
            {
                PlayErrorBuzz();
                MainframeBootQuest.Instance?.ShowNotification(
                    "<color=#FF2222>[ОТКАЗ ПРИВОДА]</color> Температура превышает 90°C! Охладите диск!");
                return;
            }
        }

        // Start copying routine
        if (copyCoroutine != null) StopCoroutine(copyCoroutine);
        copyCoroutine = StartCoroutine(CopyRoutine(file));
    }

    private IEnumerator CopyRoutine(VirtualFile file)
    {
        isCopying = true;
        copyingFileName = file.name;
        copyProgress = 0f;

        PlaySeekSound();
        MainframeBootQuest.Instance?.ShowNotification(
            $"<color=#00FFAA>[ЗАПИСЬ]</color> Начата перекачка {file.name} на сменный картридж...");

        float totalDuration = Mathf.Clamp(file.sizeBytes / 14000f, 2.5f, 6.5f);
        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;
            copyProgress = Mathf.Clamp01(elapsed / totalDuration);

            // Additional heat load during writing
            if (DiskDriveBay.Instance != null && DiskDriveBay.Instance.State == DiskDriveBay.DriveBayState.Running)
            {
                // Extra heat from head positioning
                if (UnityEngine.Random.value < 0.15f) PlaySeekSound();
            }

            // Abort if cartridge was pulled out during writing
            if (DiskDriveBay.Instance != null && DiskDriveBay.Instance.State != DiskDriveBay.DriveBayState.Running)
            {
                isCopying = false;
                PlayErrorBuzz();
                MainframeBootQuest.Instance?.ShowNotification(
                    "<color=#FF4444>[ОБРЫВ ЗАПИСИ]</color> Картридж извлечен! Перекачка аварийно прервана.");
                UpdateAllVisuals();
                yield break;
            }

            UpdateAllVisuals();
            yield return null;
        }

        isCopying = false;
        rightFiles.Add(new VirtualFile(file.name, file.sizeBytes, DateTime.Now.ToString("dd.MM.yy"), false, file.content));
        PlaySuccessSound();

        MainframeBootQuest.Instance?.ShowNotification(
            $"<color=#00FF88>[УСПЕХ]</color> Файл {file.name} ({file.GetFormattedSize()}) успешно перекачан на картридж!");

        // If main objective was copied
        if (file.name == "ДЕКОДЕР_V2.BIN")
        {
            MainframeBootQuest.Instance?.ShowNotification(
                "<color=#00FFFF><b>[МИССИЯ] СЕКРЕТНОЕ ЯДРО ДЕШИФРАТОРА ЗАХВАЧЕНО! ОХЛАДИТЕ ДИСК И ЭВАКУИРУЙТЕСЬ!</b></color>");
        }

        UpdateAllVisuals();
    }

    public void ViewSelectedFile()
    {
        var file = GetCurrentSelectedFile();
        if (file == null) return;

        PlayKeyTick();
        if (viewerModal != null)
        {
            viewerModal.SetActive(true);
            if (viewerTitleText != null)
            {
                viewerTitleText.text = $"┌─── ПРОСМОТР ФАЙЛА: {file.name} ─── [ESC - ВЫХОД] ───┐";
            }
            if (viewerBodyText != null)
            {
                viewerBodyText.text = file.content;
            }
        }
    }

    public void CloseViewer()
    {
        PlayKeyTick();
        if (viewerModal != null) viewerModal.SetActive(false);
    }

    public void ShowHelp()
    {
        PlayKeyTick();
        if (helpModal != null)
        {
            helpModal.SetActive(!helpModal.activeSelf);
        }
    }

    public void ShowStatus()
    {
        PlayKeyTick();
        string temp = DiskDriveBay.Instance != null ? $"{Mathf.RoundToInt(DiskDriveBay.Instance.Temperature)}°C" : "36°C";
        string state = DiskDriveBay.Instance != null ? DiskDriveBay.Instance.State.ToString() : "В СЕТИ";
        MainframeBootQuest.Instance?.ShowNotification(
            $"<color=#00FFFF>[СТАТУС]</color> Мейнфрейм: ОНЛАЙН | Накопитель: {state} | Темп: {temp}");
    }

    public void ShowDecryptorMode()
    {
        PlayKeyTick();
        var file = GetCurrentSelectedFile();
        if (file == null || file.isDirectory)
        {
            PlayErrorBuzz();
            MainframeBootQuest.Instance?.ShowNotification("<color=#FFCC00>Выберите файл для дешифровки!</color>");
            return;
        }

        if (file.isDecrypted)
        {
            PlayKeyTick();
            MainframeBootQuest.Instance?.ShowNotification($"<color=#00FFAA>[ИНФО]</color> Файл {file.name} уже расшифрован!");
            return;
        }

        var hub = SpyMiniGames.Instance;
        if (hub == null)
        {
            var canvas = GameObject.Find("TerminalCanvas");
            if (canvas != null) hub = canvas.GetComponentInChildren<SpyMiniGames>(true);
        }

        if (hub != null)
        {
            gameObject.SetActive(false);
            hub.LaunchMiniGame(file, (success) =>
            {
                gameObject.SetActive(true);
                if (success)
                {
                    file.isDecrypted = true;
                    UpdateAllVisuals();
                }
            });
        }
        else
        {
            PlayErrorBuzz();
            MainframeBootQuest.Instance?.ShowNotification("<color=#FF4444>[ОШИБКА]</color> Модуль криптоанализа не обнаружен в памяти!");
        }
    }

    public void CheckCoolingStatus()
    {
        PlayKeyTick();
        if (CartridgeCoolingRack.Instance != null)
        {
            MainframeBootQuest.Instance?.ShowNotification(
                "<color=#00FF88>[ОХЛАЖДЕНИЕ]</color> Резервная корзина готова. Вставьте горячий диск для сброса тепла.");
        }
        else
        {
            MainframeBootQuest.Instance?.ShowNotification(
                "<color=#00FF88>[ОХЛАЖДЕНИЕ]</color> Радиаторы водяного контура активны.");
        }
    }

    public void ExitToStartScreen()
    {
        PlayKeyTick();
        if (SpyDecryptionStartScreen.Instance != null)
        {
            gameObject.SetActive(false);
            SpyDecryptionStartScreen.Instance.gameObject.SetActive(true);
        }
        else if (MonitorTerminal.Instance != null)
        {
            MonitorTerminal.Instance.SetZoomedIn(false);
        }
    }

    // --- Audio Synthesis ---

    private void GenerateAudioClips()
    {
        int sr = 44100;

        // 1. Key Tick
        int n1 = (int)(sr * 0.025f);
        float[] d1 = new float[n1];
        for (int i = 0; i < n1; i++)
        {
            float t = (float)i / n1;
            d1[i] = Mathf.Sin(2f * Mathf.PI * 1800f * (float)i / sr) * (1f - t);
        }
        keyTickClip = AudioClip.Create("VC_KeyTick", n1, 1, sr, false);
        keyTickClip.SetData(d1, 0);

        // 2. Drive Seek Chatter Tick
        int n2 = (int)(sr * 0.04f);
        float[] d2 = new float[n2];
        for (int i = 0; i < n2; i++)
        {
            float t = (float)i / n2;
            float noise = (UnityEngine.Random.value * 2f - 1f) * 0.4f;
            float click = Mathf.Sin(2f * Mathf.PI * 450f * (float)i / sr) * 0.6f;
            d2[i] = (click + noise) * (1f - t);
        }
        seekTickClip = AudioClip.Create("VC_SeekTick", n2, 1, sr, false);
        seekTickClip.SetData(d2, 0);

        // 3. Success double beep
        int n3 = (int)(sr * 0.22f);
        float[] d3 = new float[n3];
        for (int i = 0; i < n3; i++)
        {
            float t = (float)i / sr;
            float freq = t < 0.11f ? 1200f : 1600f;
            float env = Mathf.Sin(Mathf.PI * (t % 0.11f / 0.11f));
            d3[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.5f;
        }
        copySuccessClip = AudioClip.Create("VC_Success", n3, 1, sr, false);
        copySuccessClip.SetData(d3, 0);

        // 4. Error buzz
        int n4 = (int)(sr * 0.18f);
        float[] d4 = new float[n4];
        for (int i = 0; i < n4; i++)
        {
            float t = (float)i / sr;
            float env = 1f - ((float)i / n4);
            d4[i] = Mathf.Sin(2f * Mathf.PI * 180f * t) * env * 0.6f;
        }
        errorBuzzClip = AudioClip.Create("VC_ErrorBuzz", n4, 1, sr, false);
        errorBuzzClip.SetData(d4, 0);
    }

    private void PlayKeyTick()
    {
        if (audioSource != null && keyTickClip != null)
        {
            audioSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
            audioSource.PlayOneShot(keyTickClip, 0.4f);
        }
    }

    private void PlaySeekSound()
    {
        if (audioSource != null && seekTickClip != null)
        {
            audioSource.pitch = UnityEngine.Random.Range(0.85f, 1.15f);
            audioSource.PlayOneShot(seekTickClip, 0.35f);
        }
    }

    private void PlaySuccessSound()
    {
        if (audioSource != null && copySuccessClip != null)
        {
            audioSource.PlayOneShot(copySuccessClip, 0.65f);
        }
    }

    private void PlayErrorBuzz()
    {
        if (audioSource != null && errorBuzzClip != null)
        {
            audioSource.PlayOneShot(errorBuzzClip, 0.5f);
        }
    }
}
