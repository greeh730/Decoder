﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Hub and controller for authentic Soviet spy mini-games:
/// 1. Turing Tape (.BIN) - Rule-based Turing head tape increment
/// 2. Frequency Analysis (.DOC) - Interactive histogram cipher shift
/// 3. Relational Punchcards (.TXT) - 3-predicate database card filtering
/// 4. File Search (.LST) - 14-item realistic Soviet bureaucratic file search
/// 5. Password PIN (.KEY) - 4-digit desk note PIN archive unlock (4821)
/// 6. Network Restoration (.NET) - 6-channel telecom switchboard routing (Channels 1, 4, 6)
/// 7. Caesar Archive (.ZIP) - Shift-3 Caesar cipher attack archive (DWWDFN -> ATTACK)
/// 8. Password Formula (.ZIP) - Mathematical formula cipher ((17*23) - 42 = 0349)
/// 9. Dossier Deduction (.DOC) - 4-candidate KGB saboteur deduction table (Baker / Dallas / 22:30)
/// 10. Rotor Cipher Machine (.ROT) - 3-rotor Fialka mechanical cipher
/// 11. Omega Master Protocol (.TOP) - Final 4-channel synchronization protocol
/// </summary>
public class SpyMiniGames : MonoBehaviour
{
    public static SpyMiniGames Instance { get; private set; }

    public enum GameType
    {
        None,
        TuringTape,            // 1. .BIN
        FrequencyAnalysis,     // 2. .DOC
        DatabaseFilter,        // 3. .TXT
        FileSearch,            // 4. .LST (Поиск нужного файла среди 14 отчетов)
        PasswordSimple,        // 5. .KEY (Подбор пароля 4821)
        NetworkRestoration,    // 6. .NET (Восстановление 6-канальной связи A->B)
        CaesarArchive,         // 7. .ZIP (Архив Цезаря ATTACK)
        PasswordFormula,       // 8. .ZIP (Пароль (A*B)-C = 0349)
        DossierDeduction,      // 9. .DOC (Досье Бейкер / Даллас)
        RotorCipher,           // 10. .ROT
        OmegaMasterProtocol    // 11. .TOP
    }

    [Header("UI Containers")]
    [SerializeField] private GameObject miniGameRootPanel;
    [SerializeField] private Text gameTitleText;
    [SerializeField] private Text gameDescriptionText;
    [SerializeField] private Text statusFeedbackText;
    [SerializeField] private Text timerCountdownText;
    [SerializeField] private Transform gameContentArea;
    [SerializeField] private Button btnCancel;
    [SerializeField] private Button btnAction;
    [SerializeField] private Text btnActionText;
    [SerializeField] private Button btnRestart;
    [SerializeField] private Button btnHelp;
    [SerializeField] private GameObject helpOverlayPanel;
    [SerializeField] private Text helpOverlayText;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    private AudioClip clickClip;
    private AudioClip successClip;
    private AudioClip failClip;
    private AudioClip relayTickClip;

    // Active state
    private GameType currentGame = GameType.None;
    private VectorCommander.VirtualFile activeFile;
    private Action<bool> onGameFinishedCallback;
    private bool isGameActive = false;
    private bool isHelpOpen = false;
    private bool isNoteOpen = false;
    private bool isSuccessTransition = false;

    // Error lockout & Suspicion mechanics
    private float errorLockoutTimer = 0f;
    private int currentPuzzleErrors = 0;

    // Common dynamic display text for the active game
    private Text dynamicDisplayText;

    public bool IsMiniGameActive => isGameActive;
    public GameType ActiveGameType => currentGame;
    public bool IsNoteOpen => isNoteOpen;

    // --- Sub-game data states ---

    // 1. Turing Machine State
    private readonly char[] turingInitialTape = { '_', '1', '0', '1', '1', '_' };
    private readonly char[] turingTargetTape  = { '_', '1', '1', '0', '0', '_' };
    private char[] turingTape = new char[6];
    private int turingHeadIndex = 1;
    private int turingCurrentState = 0; // 0 = q0, 1 = q1, 2 = HALT
    private int turingStepCount = 0;
    private bool turingIsRunning = false;
    private Coroutine turingRunCoroutine = null;
    private string turingLastActionNote = "Каретка на ячейке #1 (q0). Нажмите [ПРОБЕЛ] для пошагового теста.";
    private int turingActiveRuleIndex = -1;
    private int turingSelectedSlot = 0;
    private int[] turingSlotValues = new int[6] { 1, 1, 0, 0, 1, 1 };

    // 2. Frequency Analysis State
    private int freqShift = 4;
    private readonly int freqTargetShift = 7;

    // 3. Database Filter State
    private int dbDeptIndex = 0;
    private int dbClearance = 0;
    private int dbShiftIndex = 0;

    // 4. File Search State: 14 realistic bureaucratic institute files
    private int fileSearchSelectedIndex = 0;
    private readonly string[] fileSearchList = new string[]
    {
        "01. РАСПИСАНИЕ_СТОЛОВОЙ_ОКТЯБРЬ.DOC      [4 КБ]   Хозяйственный",
        "02. ВЫДАЧА_СПИРТА_ДЛЯ_ПРОТИРКИ_ЭВМ.DOC   [8 КБ]   Складской",
        "03. ПОСТАВКА_ГАЗЕТ_ПРАВДА_ИЗВЕСТИЯ.LST   [2 КБ]   Хозяйственный",
        "04. ГРАФИК_УБОРКИ_МАШИННОГО_ЗАЛА.TXT     [6 КБ]   Хозяйственный",
        "05. АКТ_СПИСАНИЯ_СТУЛЬЕВ_И_МЕБЕЛИ.LOG    [3 КБ]   Складской",
        "06. ЖУРНАЛ_ТЕМПЕРАТУР_СЕКТОРА_3.LOG      [14 КБ]  Технический",
        "07. ОТ_ЦЕНТР_ДИРЕКТИВА_ОСОБАЯ_07.DAT     [64 КБ]  Спецхран", // <-- TARGET (Index 6)
        "08. ЗАЯВКА_НА_ПЕРФОКАРТЫ_ГОСТ_10860.LST   [5 КБ]   Складской",
        "09. СПИСОК_КАНЦТОВАРОВ_И_ЛЕНТЫ_АЦПУ.TXT   [7 КБ]   Хозяйственный",
        "10. ТЕХНИЧЕСКИЙ_ПАСПОРТ_ЭЛЕКТРОЩИТА.SYS   [16 КБ]  Технический",
        "11. ПРОТОКОЛ_ЗАМЕНЫ_ЛАМП_НАКАЛА_1984.LOG  [9 КБ]   Технический",
        "12. ИНВЕНТАРНАЯ_ОПИСЬ_БИБЛИОТЕКИ_НИИ.DOC  [11 КБ]  Хозяйственный",
        "13. РЕГЛАМЕНТ_ОБСЛУЖИВАНИЯ_ШПИНДЕЛЯ.SYS   [18 КБ]  Технический",
        "14. СМЕТА_НА_ПОКРАСКУ_СТЕН_ВЦ.TXT         [4 КБ]   Хозяйственный"
    };
    private const int FILE_SEARCH_CORRECT_INDEX = 6; // 07. ОТ_ЦЕНТР_ДИРЕКТИВА_ОСОБАЯ_07.DAT

    // 5. Simple PIN State
    private string simplePinInput = "";
    private const string SIMPLE_PIN_CORRECT = "4821";

    // 6. Network Restoration State: 6-channel dual bus
    private int netSelectedLine = 0; // 0..5
    private bool[] netLinesConnected = new bool[6] { false, false, false, false, false, false };
    // Correct active lines: 0 (Север), 3 (Оптика), 5 (Резерв)
    // Faulty lines that must NOT be active: 1 (КЗ), 2 (Рассинхрон), 4 (Перегрев)

    // 7. Caesar Archive State
    private int caesarSelectedIndex = 0;
    private readonly string[] caesarFiles = new string[]
    {
        "DWWDFN_80.ZIP    [128 КБ]  (Шифр Цезаря: DWWDFN)",
        "UHWUHDW_80.ZIP   [96 КБ]   (Шифр Цезаря: UHWUHDW)",
        "VXSSOB_80.ZIP    [64 КБ]   (Шифр Цезаря: VXSSOB)",
        "UDGLR_77.ZIP     [48 КБ]   (Шифр Цезаря: UDGLR)"
    };
    private const int CAESAR_CORRECT_INDEX = 0; // DWWDFN -> ATTACK

    // 8. Password Formula State
    private string formulaPinInput = "";
    private const int FORMULA_A = 17;
    private const int FORMULA_B = 23;
    private const int FORMULA_C = 42;
    private const string FORMULA_PIN_CORRECT = "0349"; // (17 * 23) - 42 = 391 - 42 = 349 -> "0349"

    // 9. Dossier Deduction State
    private int dossierSelectedIndex = 0;
    private readonly string[] dossierRows = new string[]
    {
        "1. Капитан Соколов   │ Москва    │ 19:40 │ Кабинет 1-02",
        "2. Лейтенант Бейкер  │ Даллас    │ 22:30 │ Серверная 3-08",
        "3. Инженер Морозов   │ Ленинград │ 08:15 │ Лаборатория 2-15",
        "4. Майор Воронин     │ Минск     │ 14:00 │ Архив 1-09"
    };
    private const int DOSSIER_CORRECT_INDEX = 1; // Бейкер / Даллас / 22:30 / 3-08

    // 10. Rotor Cipher State
    private int rotor1 = 2, rotor2 = 5, rotor3 = 1;
    private readonly int targetR1 = 4, targetR2 = 2, targetR3 = 7;

    // 11. Omega Master Protocol State
    private int omegaSelectedChannel = 0;
    private int omegaCh1Index = 1;
    private int omegaCh2Shift = 3;
    private int omegaCh3Index = 0;
    private int omegaCh4Index = 0;

    private void Awake()
    {
        Instance = this;
        GenerateAudioClips();
    }

    private void Start()
    {
        if (btnCancel != null)
        {
            btnCancel.onClick.RemoveAllListeners();
            btnCancel.onClick.AddListener(CancelMiniGame);
        }
        if (btnAction != null)
        {
            btnAction.onClick.RemoveAllListeners();
            btnAction.onClick.AddListener(OnMainActionButtonPressed);
        }
        if (btnRestart != null)
        {
            btnRestart.onClick.RemoveAllListeners();
            btnRestart.onClick.AddListener(RestartCurrentGameToStartingPoint);
        }
        if (btnHelp != null)
        {
            btnHelp.onClick.RemoveAllListeners();
            btnHelp.onClick.AddListener(() => ToggleHelp(!isHelpOpen));
        }

        if (miniGameRootPanel != null)
        {
            miniGameRootPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (!isGameActive || isSuccessTransition) return;

        // 1. Error lockout timer management
        if (errorLockoutTimer > 0f)
        {
            errorLockoutTimer -= Time.deltaTime;
            if (errorLockoutTimer <= 0f)
            {
                errorLockoutTimer = 0f;
                if (statusFeedbackText != null)
                {
                    statusFeedbackText.text = "<color=#00FFAA>[БЛОКИРОВКА СНЯТА: СИСТЕМА ГОТОВА К ВВОДУ]</color>";
                }
            }
            else
            {
                if (statusFeedbackText != null)
                {
                    statusFeedbackText.text = $"<color=#FF5555><b>[ВВОД ЗАБЛОКИРОВАН: {errorLockoutTimer:F1}с] Ошибка ввода!</b></color>";
                }
            }
        }

        // 2. Guard breach auto-collapse check:
        // If guard breaches the door into room, collapse puzzle window to let player hide/deceive
        var guards = FindObjectsByType<GuardPawn>(FindObjectsInactive.Exclude);
        if (guards != null && guards.Length > 0)
        {
            foreach (var g in guards)
            {
                if (g != null && (g.CurrentState == GuardPawn.GuardState.BreachOpeningDoor || 
                                  g.CurrentState == GuardPawn.GuardState.BreachEnteringRoom ||
                                  g.CurrentState == GuardPawn.GuardState.InspectingRoomDoorway))
                {
                    if (isGameActive || (miniGameRootPanel != null && miniGameRootPanel.activeSelf))
                    {
                        OnGuardInspectionBreach();
                    }
                    return;
                }
            }
        }

        // 3. Handle Overlay input
        if (isHelpOpen || isNoteOpen)
        {
            HandleOverlayKeyboardInput();
            return;
        }

        // 4. Ignore input while locked out
        if (errorLockoutTimer > 0f)
        {
            if (IsKey(KeyCode.Escape)) CancelMiniGame();
            return;
        }

        // 5. Active keyboard controls
        HandleKeyboardInput();
    }

    public static GameType GetGameTypeForFile(string fileName)
    {
        string upper = fileName.ToUpper();
        if (upper.EndsWith(".TOP") || upper.EndsWith(".FIN") || upper.EndsWith(".OMG")) return GameType.OmegaMasterProtocol;
        if (upper.EndsWith(".BIN") || upper.EndsWith(".EXE")) return GameType.TuringTape;
        if (upper.EndsWith(".DOC") && (upper.Contains("ДИРЕКТИВА") || upper.Contains("СПЕКТР"))) return GameType.FrequencyAnalysis;
        if (upper.EndsWith(".TXT") && (upper.Contains("КАРТОТЕКА") || upper.Contains("БАЗА"))) return GameType.DatabaseFilter;
        if (upper.Contains("ПОИСК") || upper.EndsWith(".LST")) return GameType.FileSearch;
        if (upper.Contains("СЕЙФ") || upper.EndsWith(".KEY")) return GameType.PasswordSimple;
        if (upper.Contains("СВЯЗЬ") || upper.EndsWith(".NET")) return GameType.NetworkRestoration;
        if (upper.Contains("АТАКИ") || (upper.EndsWith(".ZIP") && upper.Contains("ПЛАН"))) return GameType.CaesarArchive;
        if (upper.Contains("КАРТЫ") || (upper.EndsWith(".ZIP") && upper.Contains("ГЕНШТАБ"))) return GameType.PasswordFormula;
        if (upper.Contains("ДОСЬЕ") || upper.Contains("САБОТАЖ") || upper.EndsWith(".DOC")) return GameType.DossierDeduction;
        if (upper.EndsWith(".ROT") || upper.EndsWith(".СХМ")) return GameType.RotorCipher;
        return GameType.FileSearch;
    }

    public void LaunchMiniGame(VectorCommander.VirtualFile file, Action<bool> onComplete)
    {
        activeFile = file;
        onGameFinishedCallback = onComplete;
        currentGame = GetGameTypeForFile(file.name);
        isGameActive = true;
        isHelpOpen = false;
        isNoteOpen = false;
        errorLockoutTimer = 0f;
        currentPuzzleErrors = 0;

        if (timerCountdownText != null)
        {
            timerCountdownText.text = "<color=#00FFAA>РЕЖИМ: БЕЗ ОГРАНИЧЕНИЯ ВРЕМЕНИ (АНАЛИЗ)</color>";
        }

        if (helpOverlayPanel != null) helpOverlayPanel.SetActive(false);
        InitializeSpecificGame(currentGame);
        if (miniGameRootPanel != null) miniGameRootPanel.SetActive(true);
        PlayClick();
    }

    private void InitializeSpecificGame(GameType type)
    {
        if (gameContentArea != null)
        {
            dynamicDisplayText = gameContentArea.GetComponentInChildren<Text>();
        }

        switch (type)
        {
            case GameType.TuringTape:
                gameTitleText.text = "╔════ МАШИНА ТЬЮРИНГА: ДВОИЧНЫЙ ИНКРЕМЕНТ ЯДРА ════╗";
                gameDescriptionText.text = "Прибавьте 1 к двоичному числу (1011 + 1 = 1100). Настройте микрокод переноса в состоянии q1.";
                btnActionText.text = "[ ПУСК [ENTER] ]";
                turingTape = (char[])turingInitialTape.Clone();
                turingHeadIndex = 1;
                turingCurrentState = 0;
                turingStepCount = 0;
                turingIsRunning = false;
                turingActiveRuleIndex = -1;
                turingLastActionNote = "Каретка на ячейке #1 (q0). Нажмите [ПРОБЕЛ] для шага.";
                turingSelectedSlot = 0;
                turingSlotValues = new int[6] { 1, 1, 0, 0, 1, 1 };
                UpdateTuringDisplay();
                break;

            case GameType.FrequencyAnalysis:
                gameTitleText.text = "╔════ ЧАСТОТНЫЙ СПЕКТР ШИФРА ПОДСТАНОВКИ ════╗";
                gameDescriptionText.text = "[Стрелки Влево/Вправо]: подберите сдвиг алфавита, чтобы пики гласных совпали с эталоном (О, Е, А).";
                freqShift = 2;
                btnActionText.text = "[ПРИМЕНИТЬ КЛЮЧ]";
                UpdateFrequencyDisplay();
                break;

            case GameType.DatabaseFilter:
                gameTitleText.text = "╔════ РЕЛЯЦИОННЫЙ ФИЛЬТР: КАРТОТЕКА БАЗЫ ════╗";
                gameDescriptionText.text = "[1] Отдел, [2] Допуск, [3] Смена. Сузьте картотеку до 1 карточки с паролем администратора.";
                dbDeptIndex = 0;
                dbClearance = 0;
                dbShiftIndex = 0;
                btnActionText.text = "[ВЫБОРКА ДАННЫХ]";
                UpdateDatabaseDisplay();
                break;

            case GameType.FileSearch:
                gameTitleText.text = "╔════ ПОИСК НУЖНОГО ФАЙЛА В КАТАЛОГЕ СИСТЕМЫ ════╗";
                gameDescriptionText.text = "[Вверх/Вниз] Прокрутка каталога, [ENTER] Открыть. Найдите среди хозяйственных бумаг директиву Центра.";
                fileSearchSelectedIndex = 0;
                btnActionText.text = "[ОТКРЫТЬ ФАЙЛ]";
                UpdateFileSearchDisplay();
                break;

            case GameType.PasswordSimple:
                gameTitleText.text = "╔════ ВВОД ПАРОЛЯ: ЗАЩИЩЕННЫЙ АРХИВ СВЯЗИ ════╗";
                gameDescriptionText.text = "Введите 4-значный пин-код из записки связиста [4821]. [N] Прочесть записку, [ENTER] Проверить.";
                simplePinInput = "";
                btnActionText.text = "[ВВЕСТИ ПАРОЛЬ]";
                UpdatePasswordSimpleDisplay();
                break;

            case GameType.NetworkRestoration:
                gameTitleText.text = "╔════ КОММУТАТОР СВЯЗИ: ВОССТАНОВЛЕНИЕ ЛИНИИ A -> B ════╗";
                gameDescriptionText.text = "[1..6 / Вверх / Вниз] Выбор канала, [ПРОБЕЛ] ВКЛ/ВЫКЛ, [ENTER] Проверить. Включите исправные каналы!";
                netSelectedLine = 0;
                netLinesConnected = new bool[6] { false, false, false, false, false, false };
                btnActionText.text = "[ПРОВЕРИТЬ ЛИНИЮ]";
                UpdateNetworkRestorationDisplay();
                break;

            case GameType.CaesarArchive:
                gameTitleText.text = "╔════ АРХИВ: ПЛАН АТАКИ (ШИФР ЦЕЗАРЯ СО СДВИГОМ 3) ════╗";
                gameDescriptionText.text = "[1..4 / Вверх / Вниз] Выбор архива, [ENTER] Открыть. Найдите архив со словом ATTACK (сдвиг назад на 3: D->A).";
                caesarSelectedIndex = 0;
                btnActionText.text = "[ИЗВЛЕЧЬ АРХИВ]";
                UpdateCaesarArchiveDisplay();
                break;

            case GameType.PasswordFormula:
                gameTitleText.text = "╔════ КРИПТОГРАФИЧЕСКИЙ ПАРОЛЬ: КАРТЫ ГЕНШТАБА ════╗";
                gameDescriptionText.text = "Формула: (A × B) - C = (17 × 23) - 42. Введите 4 цифры [0..9], [Backspace], [ENTER] Открыть.";
                formulaPinInput = "";
                btnActionText.text = "[ВВЕСТИ КОД]";
                UpdatePasswordFormulaDisplay();
                break;

            case GameType.DossierDeduction:
                gameTitleText.text = "╔════ ОСОБЫЙ ОТДЕЛ: ДЕДУКЦИЯ КАРТОТЕКИ ДИВЕРСАНТА ════╗";
                gameDescriptionText.text = "[1..4 / Вверх / Вниз] Выбор подозреваемого, [ENTER] Указать цель. Ориентировка: город вне СССР, серверная ночью.";
                dossierSelectedIndex = 0;
                btnActionText.text = "[УКАЗАТЬ ПОДОЗРЕВАЕМОГО]";
                UpdateDossierDeductionDisplay();
                break;

            case GameType.RotorCipher:
                gameTitleText.text = "╔════ ШИФРАТОР М-125 «ФИАЛКА»: РОТОРНЫЙ КЛЮЧ ════╗";
                gameDescriptionText.text = "[1/2/3] Выбор ротора, [Вверх/Вниз] поворот барабана. Выставите ключ дня из блокнота: IV - II - VII.";
                rotor1 = 1; rotor2 = 6; rotor3 = 3;
                btnActionText.text = "[ПРОВОРОТ РОТОРОВ]";
                UpdateRotorDisplay();
                break;

            case GameType.OmegaMasterProtocol:
                gameTitleText.text = "╔════ МАСТЕР-ПРОТОКОЛ «ОМЕГА»: СНЯТИЕ ГЛАВНОГО ШИФРА ════╗";
                gameDescriptionText.text = "[1-4 / Вверх / Вниз] — Выбор канала, [← / → / Пробел] — Калибровка, [ENTER] — Снятие шифра.";
                omegaSelectedChannel = 0;
                omegaCh1Index = 1;
                omegaCh2Shift = 3;
                omegaCh3Index = 0;
                omegaCh4Index = 0;
                btnActionText.text = "[ ВЗЛОМАТЬ АРХИВ «ОМЕГА» [ENTER] ]";
                UpdateOmegaDisplay();
                break;
        }

        if (statusFeedbackText != null)
        {
            statusFeedbackText.text = "<color=#00FFAA>[СИСТЕМА ГОТОВА К ВВОДУ КЛЮЧА]</color>";
        }
    }

    public void TriggerErrorLockout(string reason)
    {
        errorLockoutTimer = 2.5f;
        currentPuzzleErrors++;
        PlayFail();

        if (statusFeedbackText != null)
        {
            statusFeedbackText.text = $"<color=#FF3333><b>[ОШИБКА ВВОДА #{currentPuzzleErrors}/3] {reason} (Блокировка 2.5с)</b></color>";
        }

        if (currentPuzzleErrors >= 2)
        {
            ToggleHelp(true);
        }

        if (currentPuzzleErrors >= 3)
        {
            GuardPawn.DispatchGuardForTerminalInspection("3 ошибки криптографического ввода в терминале — аварийный протокол");
            if (statusFeedbackText != null)
            {
                statusFeedbackText.text = "<color=#FF0000><b>[ТРЕВОГА!] Сбой терминала! Охрана идёт с проверкой!</b></color>";
            }
            MainframeBootQuest.Instance?.ShowNotification("<color=#FF0000>[⚠ ОХРАНА ИДЁТ!]</color> 1. Вырубите рубильник на стене [E]! 2. Спрячьтесь за стойку ЭВМ в тень!");
        }
    }

    // --- Mini-Game 1: Turing Tape Logic ---

    private void UpdateTuringDisplay()
    {
        if (dynamicDisplayText == null) return;

        string initTapeStr = "";
        string targTapeStr = "";
        string currTapeStr = "";

        for (int i = 0; i < turingTape.Length; i++)
        {
            initTapeStr += $"[{turingInitialTape[i]}] ";
            targTapeStr += $"[{turingTargetTape[i]}] ";
            if (i == turingHeadIndex)
                currTapeStr += $"<b><color=#00FFAA>►[{turingTape[i]}]◄</color></b> ";
            else
                currTapeStr += $" [{turingTape[i]}]  ";
        }

        string stateLabel = turingCurrentState == 0 ? "q0" : (turingCurrentState == 1 ? "q1" : "HALT");
        string stateCol   = turingCurrentState == 0 ? "#66CCFF" : (turingCurrentState == 1 ? "#FFAA33" : "#00FF88");

        string spacePad = new string(' ', Mathf.Clamp(turingHeadIndex * 6, 0, 40));
        string carriageVisual =
            $"  ЯЧЕЙКИ:        #0    #1    #2    #3    #4    #5\n" +
            $"  КАРЕТКА:   {spacePad}<b><color={stateCol}>┌──────┐</color></b>  (Такт: {turingStepCount}/25, Состояние: <color={stateCol}>{stateLabel}</color>)\n" +
            $"             {spacePad}<b><color={stateCol}>│ {stateLabel.PadRight(4)} │</color></b>\n" +
            $"             {spacePad}<b><color={stateCol}>└──┬───┘</color></b>\n" +
            $"             {spacePad}<b><color={stateCol}>   ▼   </color></b>\n" +
            $"  ПЕРФОЛЕНТА: {currTapeStr}";

        string[] writeOptions = { "0", "1", "_" };
        string[] moveOptions  = { "← ВЛЕВО", "→ ВПРАВО", "• СТОП" };
        string[] stateOptions = { "q0", "q1", "HALT" };

        string FormatSlot(int slotIndex, string val)
        {
            if (slotIndex == turingSelectedSlot)
                return $"<b><color=#FFFF00>►[ {val} ]◄</color></b>";
            else
                return $"<color=#00FFAA>[ {val} ]</color>";
        }

        string slot0 = FormatSlot(0, writeOptions[turingSlotValues[0]]);
        string slot1 = FormatSlot(1, moveOptions[turingSlotValues[1]]);
        string slot2 = FormatSlot(2, stateOptions[turingSlotValues[2]]);

        string slot3 = FormatSlot(3, writeOptions[turingSlotValues[3]]);
        string slot4 = FormatSlot(4, moveOptions[turingSlotValues[4]]);
        string slot5 = FormatSlot(5, stateOptions[turingSlotValues[5]]);

        string r0Mark = turingActiveRuleIndex == 0 ? "<b><color=#FFFF33>►</color></b>" : " ";
        string r1Mark = turingActiveRuleIndex == 1 ? "<b><color=#FFFF33>►</color></b>" : " ";
        string r2Mark = turingActiveRuleIndex == 2 ? "<b><color=#FFFF33>►</color></b>" : " ";
        string r3Mark = turingActiveRuleIndex == 3 ? "<b><color=#FFFF33>►</color></b>" : " ";
        string r4Mark = turingActiveRuleIndex == 4 ? "<b><color=#FFFF33>►</color></b>" : " ";
        string r5Mark = turingActiveRuleIndex == 5 ? "<b><color=#FFFF33>►</color></b>" : " ";

        string tableVisual =
            $"<color=#66CCAA>ТАБЛИЦА МИКРОКОДА (ПЕРЕХОДЫ СОСТОЯНИЙ ЭВМ):</color>\n" +
            $"   СОСТ │ ЧИТАЕТ │  ЗАПИСЬ  │   НАПРАВЛЕНИЕ   │  ПЕРЕХОД В  │ ПРИМЕЧАНИЕ\n" +
            $"  ──────┼────────┼──────────┼─────────────────┼─────────────┼─────────────────────────\n" +
            $"  {r0Mark} q0   │   0    │    0     │    → ВПРАВО     │     q0      │ Поиск младшего разряда\n" +
            $"  {r1Mark} q0   │   1    │    1     │    → ВПРАВО     │     q0      │ Поиск младшего разряда\n" +
            $"  {r2Mark} q0   │   _    │    _     │    ← ВЛЕВО      │     q1      │ Край числа ──► шаг на LSB\n" +
            $"  ──────┼────────┼──────────┼─────────────────┼─────────────┼─────────────────────────\n" +
            $"  {r3Mark} q1   │   1    │{slot0.PadRight(10)}│ {slot1.PadRight(16)}│ {slot2.PadRight(12)}│ <b>1+1=0 (перенос влево!)</b>\n" +
            $"  {r4Mark} q1   │   0    │{slot3.PadRight(10)}│ {slot4.PadRight(16)}│ {slot5.PadRight(12)}│ <b>0+1=1 (останов!)</b>\n" +
            $"  {r5Mark} q1   │   _    │    1     │    • СТОП       │    HALT     │ Перенос за старший бит";

        dynamicDisplayText.text =
            $"<color=#88CCAA>ИСХОДНОЕ ЧИСЛО:</color> {initTapeStr} (11 в двоичной)   │   <color=#00FFAA>ЦЕЛЕВОЙ КЛЮЧ ЯДРА:</color> {targTapeStr} (12 = 11 + 1)\n\n" +
            $"{carriageVisual}\n" +
            $"  <color=#FFFF88>► {turingLastActionNote}</color>\n\n" +
            $"{tableVisual}\n\n" +
            $"<color=#FFFF66>[СТРЕЛКИ ВВЕРХ/ВНИЗ] Слот  │  [ВЛЕВО/ВПРАВО] Значение  │  [ПРОБЕЛ] Шаг  │  [ENTER] Пуск  │  [N] Записка  │  [H] Справка</color>";

        if (btnActionText != null)
        {
            btnActionText.text = turingIsRunning ? "[ ПАУЗА [ENTER] ]" : "[ ПУСК КАРЕТКИ [ENTER] ]";
        }
    }

    public void StepTuringSingle()
    {
        if (turingCurrentState == 2)
        {
            CheckTuringGoal();
            return;
        }

        if (turingStepCount >= 25)
        {
            turingIsRunning = false;
            TriggerErrorLockout("Превышен лимит тактов (25)! Каретка зациклилась.");
            turingLastActionNote = "<color=#FF3333>ОШИБКА: Превышен лимит тактов (25)! Каретка зациклилась.</color>";
            UpdateTuringDisplay();
            return;
        }

        if (turingHeadIndex < 0 || turingHeadIndex >= turingTape.Length)
        {
            turingIsRunning = false;
            TriggerErrorLockout("Каретка сошла с перфоленты за пределы буфера!");
            turingLastActionNote = "<color=#FF3333>ОШИБКА: Каретка сошла с перфоленты за пределы буфера!</color>";
            UpdateTuringDisplay();
            return;
        }

        char readSymbol = turingTape[turingHeadIndex];
        char writeSymbol = readSymbol;
        int moveDir = 0;
        int nextState = turingCurrentState;
        int ruleIndex = -1;

        if (turingCurrentState == 0)
        {
            if (readSymbol == '0') { writeSymbol = '0'; moveDir = 1; nextState = 0; ruleIndex = 0; }
            else if (readSymbol == '1') { writeSymbol = '1'; moveDir = 1; nextState = 0; ruleIndex = 1; }
            else if (readSymbol == '_') { writeSymbol = '_'; moveDir = -1; nextState = 1; ruleIndex = 2; }
        }
        else if (turingCurrentState == 1)
        {
            char[] writes = { '0', '1', '_' };
            int[] moves = { -1, 1, 0 };
            int[] states = { 0, 1, 2 };

            if (readSymbol == '1')
            {
                ruleIndex = 3;
                writeSymbol = writes[turingSlotValues[0]];
                moveDir = moves[turingSlotValues[1]];
                nextState = states[turingSlotValues[2]];
            }
            else if (readSymbol == '0')
            {
                ruleIndex = 4;
                writeSymbol = writes[turingSlotValues[3]];
                moveDir = moves[turingSlotValues[4]];
                nextState = states[turingSlotValues[5]];
            }
            else if (readSymbol == '_')
            {
                writeSymbol = '1'; moveDir = 0; nextState = 2; ruleIndex = 5;
            }
        }

        turingActiveRuleIndex = ruleIndex;
        turingStepCount++;

        turingTape[turingHeadIndex] = writeSymbol;
        PlayClick();

        string stateNameBefore = turingCurrentState == 0 ? "q0" : "q1";
        string stateNameAfter  = nextState == 0 ? "q0" : (nextState == 1 ? "q1" : "HALT");
        string moveName = moveDir == -1 ? "← ВЛЕВО" : (moveDir == 1 ? "→ ВПРАВО" : "• СТОП");

        turingLastActionNote = $"Такт #{turingStepCount}: [{stateNameBefore}] прочитал '{readSymbol}' ──► записал '{writeSymbol}', сдвиг {moveName}, перешел в [{stateNameAfter}]";

        turingHeadIndex += moveDir;
        turingCurrentState = nextState;

        UpdateTuringDisplay();

        if (turingCurrentState == 2)
        {
            CheckTuringGoal();
        }
    }

    public void RunTuringContinuous()
    {
        if (turingIsRunning)
        {
            turingIsRunning = false;
            if (turingRunCoroutine != null) StopCoroutine(turingRunCoroutine);
            turingLastActionNote = "Выполнение приостановлено оператором.";
            UpdateTuringDisplay();
            return;
        }

        turingIsRunning = true;
        if (turingRunCoroutine != null) StopCoroutine(turingRunCoroutine);
        turingRunCoroutine = StartCoroutine(TuringRunLoop());
    }

    private IEnumerator TuringRunLoop()
    {
        while (turingIsRunning && turingCurrentState != 2 && turingStepCount < 25)
        {
            StepTuringSingle();
            yield return new WaitForSeconds(0.22f);
        }
        turingIsRunning = false;
        UpdateTuringDisplay();
    }

    public void ResetTuringTape()
    {
        if (turingRunCoroutine != null) StopCoroutine(turingRunCoroutine);
        turingIsRunning = false;
        turingTape = (char[])turingInitialTape.Clone();
        turingHeadIndex = 1;
        turingCurrentState = 0;
        turingStepCount = 0;
        turingActiveRuleIndex = -1;
        turingLastActionNote = "Каретка возвращена на ячейку #1 (q0). Готова к тесту.";
        PlayRelay();
        if (statusFeedbackText != null)
            statusFeedbackText.text = "<color=#00FFAA>[СИСТЕМА СБРОШЕНА] Нажмите [ПРОБЕЛ] для шага или [ENTER] для пуска.</color>";
        UpdateTuringDisplay();
    }

    private void CheckTuringGoal()
    {
        turingIsRunning = false;
        bool match = true;
        for (int i = 0; i < turingTape.Length; i++)
        {
            if (turingTape[i] != turingTargetTape[i])
            {
                match = false;
                break;
            }
        }

        if (match)
        {
            turingLastActionNote = "<color=#00FF88>★ ТАКТ ЗАВЕРШЕН (HALT). РЕЗУЛЬТАТ СОВПАЛ С ЦЕЛЕВЫМ КЛЮЧОМ!</color>";
            OnGameSuccess("КРИПТОГРАФИЧЕСКИЙ ИНКРЕМЕНТ УСПЕШЕН! ЯДРО «ДЕКОДЕР_V2.BIN» РАСШИФРОВАНО.");
        }
        else
        {
            string cur = new string(turingTape);
            string tgt = new string(turingTargetTape);
            turingLastActionNote = $"<color=#FF5555>ОСТАНОВ: Результат '{cur}' не совпадает с целью '{tgt}'!</color>";
            TriggerErrorLockout($"Результат '{cur}' не равен '{tgt}'!");
        }
        UpdateTuringDisplay();
    }

    // --- Mini-Game 2: Frequency Analysis ---

    private void UpdateFrequencyDisplay()
    {
        if (dynamicDisplayText == null) return;

        int barsCount = 12;
        string referenceBars = "ЭТАЛОН:  ";
        string cipherBars =    "СДВИГ:   ";

        for (int i = 0; i < barsCount; i++)
        {
            bool isPeakRef = (i == 3 || i == 7 || i == 10);
            referenceBars += isPeakRef ? "<b><color=#00FFAA>█</color></b> " : "<color=#447755>▄</color> ";

            int shiftedIndex = (i + freqShift) % barsCount;
            bool isPeakCipher = (shiftedIndex == (3 + freqTargetShift) % barsCount ||
                                 shiftedIndex == (7 + freqTargetShift) % barsCount ||
                                 shiftedIndex == (10 + freqTargetShift) % barsCount);
            cipherBars += isPeakCipher ? "<b><color=#FFFF00>█</color></b> " : "<color=#556633>▄</color> ";
        }

        string previewText = (freqShift == freqTargetShift) ?
            "<color=#00FF88>«СОВЕРШЕННО СЕКРЕТНО. ДИРЕКТИВА №42: ПРОВЕСТИ ИСПЫТАНИЯ»</color>" :
            "<color=#888888>«ХФЖТШЦУЩФ ЦФРТШУЩФ. ЕЙСПЛУЙЖБ №78: ТСФЖФЕЙУЭ ЙЦТЮУБЩЙС»</color>";

        dynamicDisplayText.text =
            $"<size=22><color=#66CCAA>ГИСТОГРАММА ЧАСТОТНОСТИ СИМВОЛОВ:</color></size>\n\n" +
            $"  {referenceBars} <color=#66CCAA>[ПИКИ: О, Е, А]</color>\n" +
            $"  {cipherBars} <color=#FFFF00>[СМЕЩЕНИЕ: +{freqShift}]</color>\n\n" +
            $"<color=#00AA77>───────────────────────────────────────────────────────────</color>\n" +
            $"<size=18>ПРЕДПРОСМОТР РАСШИФРОВКИ:</size>\n\n" +
            $"  {previewText}\n\n" +
            $"<color=#FFFF66>[СТРЕЛКИ ВЛЕВО / ВПРАВО] Сдвиг  │  [ENTER] Применить  │  [N] Записка  │  [H] Справка</color>";
    }

    // --- Mini-Game 3: Database Filter ---

    private void UpdateDatabaseDisplay()
    {
        if (dynamicDisplayText == null) return;

        string[] depts = { "ВСЕ", "КРИПТОГРАФИЯ", "СЛУЖБА ОХРАНЫ", "УЗЕЛ СВЯЗИ" };
        string[] clearances = { "ВСЕ", "ДОПУСК-1 (ОБЩИЙ)", "ОСОБЫЙ (СЕКРЕТНО)" };
        string[] shifts = { "ВСЕ", "ДНЕВНАЯ", "НОЧНАЯ" };

        int matchesCount = 180;
        if (dbDeptIndex == 1) matchesCount /= 4;
        if (dbClearance == 2) matchesCount /= 3;
        if (dbShiftIndex == 2) matchesCount /= 3;
        if (dbDeptIndex == 1 && dbClearance == 2 && dbShiftIndex == 2) matchesCount = 1;

        string cardInfo = (matchesCount == 1) ?
            "<color=#00FFAA>КАРТОЧКА #104: БУРЕВЕСТНИК И.В. // ПАРОЛЬ АДМИНИСТРАТОРА: [ 8 4 9 1 ]</color>" :
            $"<color=#AAAAAA>Найдено совпадений в базе: {matchesCount} карточек. Уточните критерии.</color>";

        dynamicDisplayText.text =
            $"<size=22><color=#66CCAA>КРИТЕРИИ ВЫБОРКИ ПЕРФОКАРТ:</color></size>\n\n" +
            $"  [1] ОТДЕЛ БАЗЫ:   <b><color=#00FFAA>{depts[dbDeptIndex]}</color></b>\n" +
            $"  [2] УРОВЕНЬ:      <b><color=#00FFAA>{clearances[dbClearance]}</color></b>\n" +
            $"  [3] ДЕЖУРСТВО:    <b><color=#00FFAA>{shifts[dbShiftIndex]}</color></b>\n\n" +
            $"<color=#00AA77>───────────────────────────────────────────────────────────</color>\n" +
            $"<size=18>РЕЗУЛЬТАТ ПОИСКА:</size>\n\n" +
            $"  {cardInfo}\n\n" +
            $"<color=#FFFF66>[1/2/3] Фильтр  │  [ENTER] Зафиксировать пароль  │  [N] Записка  │  [H] Справка</color>";
    }

    // --- Mini-Game 4: File Search (14 items, authentic Soviet bureaucracy) ---

    private void UpdateFileSearchDisplay()
    {
        if (dynamicDisplayText == null) return;

        string fileListStr = "";
        for (int i = 0; i < fileSearchList.Length; i++)
        {
            bool isSelected = (i == fileSearchSelectedIndex);
            string prefix = isSelected ? "<b><color=#FFFF00>► </color></b>" : "  ";
            string colorTag = isSelected ? "#00FFAA" : "#88CCAA";

            fileListStr += $"{prefix}<color={colorTag}>{fileSearchList[i]}</color>\n";
        }

        dynamicDisplayText.text =
            $"<size=20><color=#66CCAA>КАТАЛОГ СЛУЖЕБНОЙ ДОКУМЕНТАЦИИ И ОТЧЕТОВ (C:\\СЕКРЕТНО):</color></size>\n\n" +
            $"{fileListStr}\n" +
            $"<color=#00AA77>───────────────────────────────────────────────────────────</color>\n" +
            $"  <b>ОРИЕНТИРОВКА:</b> Найдите среди рутинной канцелярии секретную директиву Центра!\n\n" +
            $"<color=#FFFF66>[ВВЕРХ / ВНИЗ / W / S] Выбор строки  │  [ENTER / ПРОБЕЛ] Открыть  │  [N] Записка</color>";
    }

    // --- Mini-Game 5: Simple PIN Password ---

    private void UpdatePasswordSimpleDisplay()
    {
        if (dynamicDisplayText == null) return;

        string pinDisplay = "";
        for (int i = 0; i < 4; i++)
        {
            if (i < simplePinInput.Length)
                pinDisplay += $"<b><color=#00FFAA>[ {simplePinInput[i]} ]</color></b>  ";
            else
                pinDisplay += "<color=#558866>[ _ ]</color>  ";
        }

        dynamicDisplayText.text =
            $"<size=22><color=#66CCAA>АРХИВ СВЯЗИ «СЕЙФ_СВЯЗИ.KEY» ЗАЩИЩЕН ПИН-КОДОМ:</color></size>\n\n" +
            $"          ┌───────────────────────────────────┐\n" +
            $"          │      ВВЕДИТЕ 4-ЗНАЧНЫЙ ПАРОЛЬ     │\n" +
            $"          │                                   │\n" +
            $"          │       {pinDisplay}│\n" +
            $"          │                                   │\n" +
            $"          └───────────────────────────────────┘\n\n" +
            $"<color=#00AA77>───────────────────────────────────────────────────────────</color>\n" +
            $"  <b>► СЛУЖЕБНАЯ ЗАПИСКА ДЕЖУРНОГО [N]:</b> Код архива связи: <b>[ 4 8 2 1 ]</b>\n\n" +
            $"<color=#FFFF66>[0..9] Ввод цифр  │  [BACKSPACE] Стереть  │  [ENTER] Подтвердить  │  [N] Записка  │  [H] Справка</color>";
    }

    // --- Mini-Game 6: Network Restoration (6 channels switchboard) ---

    private void UpdateNetworkRestorationDisplay()
    {
        if (dynamicDisplayText == null) return;

        string[] channelNames = new string[]
        {
            "КАНАЛ I (СЕВЕРНАЯ МАГИСТРАЛЬ):    ",
            "КАНАЛ II (ЦЕНТРАЛЬНЫЙ КАБЕЛЬ):     ",
            "КАНАЛ III (ЮЖНЫЙ РЕТРАНСЛЯТОР):    ",
            "КАНАЛ IV (ОПТИЧЕСКИЙ ВОЛНОВОД):    ",
            "КАНАЛ V (ФЕРРИТОВЫЙ ДРОССЕЛЬ):     ",
            "КАНАЛ VI (РЕЗЕРВНАЯ ВИТАЯ ПАРА):   "
        };

        string[] channelTelemetry = new string[]
        {
            "U=220В, f=50Гц, Затухание: 0.8дБ  [ИСПРАВЕН, ОБРЫВ ЦЕПИ]",
            "U=0В, КЗ НА КОРПУС                [АВАРИЯ: УТЕЧКА ТОКА НА ЗЕМЛЮ!]",
            "Δφ=180°, ПРОТИВОФАЗА              [СБОЙ: ДЕСТРУКТИВНАЯ ФАЗА!]",
            "Лазер 1310нм, Затухание: 0.15дБ   [ИСПРАВЕН, ШЛЮЗ ЗАКРЫТ]",
            "I=48А, Температура: 110°C         [ПЕРЕГРЕВ: РИСК ВОЗГОРАНИЯ!]",
            "R=600 Ом, Экран заземлен          [ИСПРАВЕН, ОБРЫВ ЦЕПИ]"
        };

        string channelListStr = "";
        bool hasShortCircuit = netLinesConnected[1] || netLinesConnected[2] || netLinesConnected[4];
        int correctConnected = 0;
        if (netLinesConnected[0]) correctConnected++;
        if (netLinesConnected[3]) correctConnected++;
        if (netLinesConnected[5]) correctConnected++;

        for (int i = 0; i < 6; i++)
        {
            bool isSelected = (i == netSelectedLine);
            bool isConn = netLinesConnected[i];

            string prefix = isSelected ? "<b><color=#FFFF00>► </color></b>" : "  ";
            string stateBadge = isConn ? "<b><color=#00FF88>[● ВКЛ]</color></b>" : "<color=#888888>[○ ВЫКЛ]</color>";
            string col = isSelected ? "#FFFF88" : "#AACCBB";

            channelListStr += $"{prefix}<color={col}>[{i + 1}] {channelNames[i]}</color> {stateBadge}\n" +
                              $"     <color=#669988>Диагностика: {channelTelemetry[i]}</color>\n";
        }

        string integrityStr;
        if (hasShortCircuit)
        {
            integrityStr = "<color=#FF2222><b>[КЗ / ПЕРЕГРУЗКА!] АВАРИЙНЫЙ КАНАЛ ЗАМКНУЛ ШИНУ! СИГНАЛ: 0%</b></color>";
        }
        else
        {
            int percent = Mathf.RoundToInt((correctConnected / 3.0f) * 100f);
            if (percent == 100)
                integrityStr = $"<b><color=#00FF88>● СВЯЗЬ 100% (СЕВЕР + ОПТИКА + РЕЗЕРВ). ШИНА ГОТОВА К ЗАПУСКУ!</color></b>";
            else
                integrityStr = $"<color=#FFCC00>○ ЦЕЛОСТНОСТЬ ЛИНИИ: {percent}% ({correctConnected}/3 исправных каналов)</color>";
        }

        dynamicDisplayText.text =
            $"<size=20><color=#66CCAA>КОММУТАЦИОННАЯ ПАНЕЛЬ СЕТИ: ПЕРЕДАТЧИК [A] ─────► СЕРВЕР [B]</color></size>\n\n" +
            $"{channelListStr}\n" +
            $"<color=#00AA77>───────────────────────────────────────────────────────────</color>\n" +
            $"  СТАТУС ШИНЫ: {integrityStr}\n\n" +
            $"<color=#FFFF66>[1..6 / ВВЕРХ / ВНИЗ] Канал  │  [ПРОБЕЛ] ВКЛ / ВЫКЛ  │  [ENTER] Проверить  │  [N] Записка</color>";
    }

    // --- Mini-Game 7: Caesar Archive ---

    private void UpdateCaesarArchiveDisplay()
    {
        if (dynamicDisplayText == null) return;

        string listStr = "";
        for (int i = 0; i < caesarFiles.Length; i++)
        {
            string prefix = (i == caesarSelectedIndex) ? "<b><color=#FFFF00>► </color></b>" : "  ";
            string col = (i == caesarSelectedIndex) ? "#00FFAA" : "#88CCAA";
            listStr += $"{prefix}<color={col}>[{i + 1}] {caesarFiles[i]}</color>\n";
        }

        dynamicDisplayText.text =
            $"<size=22><color=#66CCAA>СПИСОК ЗАШИФРОВАННЫХ АРХИВОВ ШТАБА:</color></size>\n\n" +
            $"{listStr}\n" +
            $"<color=#00AA77>───────────────────────────────────────────────────────────</color>\n" +
            $"  <b>ЗАДАНИЕ:</b> «Найди архив с планом атаки. Файлы зашифрованы Цезарем».\n" +
            $"  <b>КЛЮЧ СДВИГА:</b> Сдвиг на 3 буквы вперед (A ──► D). Для чтения сдвигайте назад (D ──► A).\n" +
            $"  <b>ЦЕЛЬ:</b> Имя расшифрованного архива должно быть <b>«ATTACK»</b>.\n\n" +
            $"<color=#FFFF66>[1..4 / ВВЕРХ / ВНИЗ] Выбор файла  │  [ENTER] Извлечь  │  [N] Записка  │  [H] Справка</color>";
    }

    // --- Mini-Game 8: Password Formula ---

    private void UpdatePasswordFormulaDisplay()
    {
        if (dynamicDisplayText == null) return;

        string pinDisplay = "";
        for (int i = 0; i < 4; i++)
        {
            if (i < formulaPinInput.Length)
                pinDisplay += $"<b><color=#00FFAA>[ {formulaPinInput[i]} ]</color></b>  ";
            else
                pinDisplay += "<color=#558866>[ _ ]</color>  ";
        }

        dynamicDisplayText.text =
            $"<size=22><color=#66CCAA>РАЗВЕДДАННЫЕ: ШИФРОВАННЫЙ АРХИВ «КАРТЫ_ГЕНШТАБА.ZIP»</color></size>\n\n" +
            $"  СЧИТАННЫЕ С РЕГИСТРОВ ПАРАМЕТРЫ ШИФРА:\n" +
            $"      <b><color=#00FFAA>[ A = {FORMULA_A} ]</color></b>        <b><color=#00FFAA>[ B = {FORMULA_B} ]</color></b>        <b><color=#00FFAA>[ C = {FORMULA_C} ]</color></b>\n\n" +
            $"  ФОРМУЛА РАСШИФРОВКИ ИЗ ОРИЕНТИРОВКИ:   <b><color=#FFFF00>PIN = (A × B) - C</color></b>\n\n" +
            $"          ┌───────────────────────────────────┐\n" +
            $"          │    ВВЕДИТЕ 4-ЗНАЧНЫЙ РЕЗУЛЬТАТ    │\n" +
            $"          │                                   │\n" +
            $"          │       {pinDisplay}│\n" +
            $"          │                                   │\n" +
            $"          └───────────────────────────────────┘\n\n" +
            $"<color=#00AA77>───────────────────────────────────────────────────────────</color>\n" +
            $"  <b>ПРИМЕЧАНИЕ:</b> Если итог состоит из 3 цифр, дополните его нулем слева (0XXX)!\n\n" +
            $"<color=#FFFF66>[0..9] Ввод цифр  │  [BACKSPACE] Стереть  │  [ENTER] Подтвердить  │  [N] Записка  │  [H] Справка</color>";
    }

    // --- Mini-Game 9: Dossier Deduction ---

    private void UpdateDossierDeductionDisplay()
    {
        if (dynamicDisplayText == null) return;

        string rowsStr = "     СОТРУДНИК           │ ГОРОД     │ ВРЕМЯ │ МЕСТОПОЛОЖЕНИЕ\n" +
                         "  ───────────────────────┼───────────┼───────┼───────────────────\n";

        for (int i = 0; i < dossierRows.Length; i++)
        {
            string prefix = (i == dossierSelectedIndex) ? "<b><color=#FFFF00>► </color></b>" : "  ";
            string col = (i == dossierSelectedIndex) ? "#00FFAA" : "#88CCAA";
            rowsStr += $"{prefix}<color={col}>{dossierRows[i]}</color>\n";
        }

        dynamicDisplayText.text =
            $"<size=22><color=#66CCAA>КАРТОТЕКА ОСОБОГО ОТДЕЛА: ПОИСК ДИВЕРСАНТА</color></size>\n\n" +
            $"{rowsStr}\n" +
            $"<color=#00AA77>───────────────────────────────────────────────────────────</color>\n" +
            $"  <b>ОРИЕНТИРОВКА РАЗВЕДКИ:</b>\n" +
            $"  • Прибыл не из СССР (город за пределами Советского Союза);\n" +
            $"  • Находился в серверном секторе (блок 3-XX) в ночное время (после 22:00).\n\n" +
            $"<color=#FFFF66>[1..4 / ВВЕРХ / ВНИЗ] Строка  │  [ENTER] Указать цель  │  [N] Записка  │  [H] Справка</color>";
    }

    // --- Mini-Game 10: Rotor Cipher (Fialka / Enigma) ---

    private void UpdateRotorDisplay()
    {
        if (dynamicDisplayText == null) return;

        string[] roman = { "0", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX" };
        string r1Str = roman[Mathf.Clamp(rotor1, 0, 9)];
        string r2Str = roman[Mathf.Clamp(rotor2, 0, 9)];
        string r3Str = roman[Mathf.Clamp(rotor3, 0, 9)];

        bool matches = (rotor1 == targetR1 && rotor2 == targetR2 && rotor3 == targetR3);
        string resultText = matches ?
            "<color=#00FF88>РОТОРЫ СИНХРОНИЗИРОВАНЫ: «ВЕКТОРНАЯ СХЕМА БУНКЕРА РАСШИФРОВАНА»</color>" :
            "<color=#888888>ЗАШИФРОВАННЫЙ ПОТОК: [ 9F-B4-02-C8-11-AA ]</color>";

        dynamicDisplayText.text =
            $"<size=22><color=#66CCAA>ШИФРОВАЛЬНАЯ МАШИНА «ФИАЛКА-М125»:</color></size>\n\n" +
            $"      РОТОР 1        РОТОР 2        РОТОР 3   \n" +
            $"     ╔═══════╗      ╔═══════╗      ╔═══════╗  \n" +
            $"     ║  {r1Str.PadRight(3)}  ║      ║  {r2Str.PadRight(3)}  ║      ║  {r3Str.PadRight(3)}  ║  \n" +
            $"     ╚═══════╝      ╚═══════╝      ╚═══════╝  \n\n" +
            $"  ПОДСКАЗКА ИЗ БЛОКНОТА: Ключ дежурного криптографа [ IV - II - VII ]\n" +
            $"  {resultText}\n\n" +
            $"<color=#FFFF66>[1/2/3] Выбор ротора  │  [ВВЕРХ / ВНИЗ] Проворот  │  [ENTER] Пуск  │  [N] Записка  │  [H] Справка</color>";
    }

    // --- Mini-Game 11: Omega Master Protocol (.TOP) ---

    private void UpdateOmegaDisplay()
    {
        if (dynamicDisplayText == null) return;

        string[] ch1Options = { "0000 [СБРОС]", "1011 [ИСХОДНЫЙ]", "0101 [ИНВЕРСИЯ]", "1100 [СИНХРОНИЗИРОВАН]" };
        string[] ch3Options = { "0000 [ПУСТО]", "4821 [АРХИВ СВЯЗИ]", "0349 [СИНХРОНИЗИРОВАН]", "9999 [ТРЕВОГА]" };
        string[] ch4Options = { "I - I - I [ДЕФОЛТ]", "IV - II - VII [СИНХРОНИЗИРОВАН]", "III - V - I [ВЧ-СВЯЗЬ]", "VII - VII - VII [СБРОС]" };

        bool ch1Ok = (omegaCh1Index == 3);
        bool ch2Ok = (omegaCh2Shift == 7);
        bool ch3Ok = (omegaCh3Index == 2);
        bool ch4Ok = (omegaCh4Index == 1);
        bool allOk = ch1Ok && ch2Ok && ch3Ok && ch4Ok;

        string ch1Badge = ch1Ok ? "<color=#00FFAA>[● ЗАХВАЧЕН]</color>" : "<color=#FF6666>[○ РАССИНХРОН]</color>";
        string ch2Badge = ch2Ok ? "<color=#00FFAA>[● ЗАХВАЧЕН]</color>" : "<color=#FF6666>[○ РАССИНХРОН]</color>";
        string ch3Badge = ch3Ok ? "<color=#00FFAA>[● ЗАХВАЧЕН]</color>" : "<color=#FF6666>[○ РАССИНХРОН]</color>";
        string ch4Badge = ch4Ok ? "<color=#00FFAA>[● ЗАХВАЧЕН]</color>" : "<color=#FF6666>[○ РАССИНХРОН]</color>";

        string cur1 = (omegaSelectedChannel == 0) ? "<b><color=#FFFF00>► 1. ЯДРО ТЬЮРИНГА (ИНКРЕМЕНТ):</color></b>" : "  1. ЯДРО ТЬЮРИНГА (ИНКРЕМЕНТ):";
        string cur2 = (omegaSelectedChannel == 1) ? "<b><color=#FFFF00>► 2. ЧАСТОТНЫЙ СПЕКТР (СДВИГ):</color></b>" : "  2. ЧАСТОТНЫЙ СПЕКТР (СДВИГ):";
        string cur3 = (omegaSelectedChannel == 2) ? "<b><color=#FFFF00>► 3. КРИПТОКОД АРХИВА КАРТ:</color></b>" : "  3. КРИПТОКОД АРХИВА КАРТ:";
        string cur4 = (omegaSelectedChannel == 3) ? "<b><color=#FFFF00>► 4. РОТОРНЫЙ КЛЮЧ «ФИАЛКА»:</color></b>" : "  4. РОТОРНЫЙ КЛЮЧ «ФИАЛКА»:";

        string val1 = (omegaSelectedChannel == 0) ? $"<b><color=#00FFAA>◄ [ {ch1Options[omegaCh1Index]} ] ►</color></b>" : $"[ {ch1Options[omegaCh1Index]} ]";
        string val2 = (omegaSelectedChannel == 1) ? $"<b><color=#00FFAA>◄ [ Δ = +{omegaCh2Shift} (ЦЕЗАРЬ) ] ►</color></b>" : $"[ Δ = +{omegaCh2Shift} (ЦЕЗАРЬ) ]";
        string val3 = (omegaSelectedChannel == 2) ? $"<b><color=#00FFAA>◄ [ {ch3Options[omegaCh3Index]} ] ►</color></b>" : $"[ {ch3Options[omegaCh3Index]} ]";
        string val4 = (omegaSelectedChannel == 3) ? $"<b><color=#00FFAA>◄ [ {ch4Options[omegaCh4Index]} ] ►</color></b>" : $"[ {ch4Options[omegaCh4Index]} ]";

        string statusLine = allOk
            ? "<b><color=#00FFAA>★ ВСЕ 4 КРИПТОКАНАЛА СИНХРОНИЗИРОВАНЫ! НАЖМИТЕ [ENTER] ДЛЯ СНЯТИЯ ШИФРА «ОМЕГА»! ★</color></b>"
            : "<color=#FFCC00>Синхронизируйте все 4 криптоканала мастер-ключа, используя данные из взломанных файлов.</color>";

        dynamicDisplayText.text =
            "<color=#66CCAA>ГОСКОМИТЕТ СССР // СИСТЕМА СПЕЦИАЛЬНОГО КРИПТОАНАЛИЗА ЭВМ «ВЕКТОР-70»</color>\n" +
            "<color=#00AA77>═══════════════════════════════════════════════════════════════════════════════════</color>\n\n" +
            $"{cur1}\n   {val1.PadRight(45)} {ch1Badge}\n\n" +
            $"{cur2}\n   {val2.PadRight(45)} {ch2Badge}\n\n" +
            $"{cur3}\n   {val3.PadRight(45)} {ch3Badge}\n\n" +
            $"{cur4}\n   {val4.PadRight(45)} {ch4Badge}\n\n" +
            "<color=#00AA77>───────────────────────────────────────────────────────────────────────────────────</color>\n" +
            $"{statusLine}\n\n" +
            "<color=#FFFF66>УПРАВЛЕНИЕ: [1..4] Выбор канала │ [← / → / ПРОБЕЛ] Значение │ [ENTER] Взлом архива</color>\n" +
            "<color=#88CCAA>ПОДСКАЗКА: Ключи из расшифрованных файлов: 1) 1100  2) +7  3) 0349  4) IV-II-VII</color>";
    }

    // --- Overlay System: Help & Desk Note ---

    public void ToggleHelp(bool open)
    {
        isHelpOpen = open;
        if (isHelpOpen) isNoteOpen = false;
        PlayClick();
        if (helpOverlayPanel != null)
        {
            helpOverlayPanel.SetActive(isHelpOpen);
        }
        if (isHelpOpen && helpOverlayText != null)
        {
            helpOverlayText.text = GetHelpTextForCurrentGame();
        }
    }

    public void ToggleDeskNote(bool open)
    {
        isNoteOpen = open;
        if (isNoteOpen) isHelpOpen = false;
        PlayClick();
        if (helpOverlayPanel != null)
        {
            helpOverlayPanel.SetActive(isNoteOpen);
        }
        if (isNoteOpen && helpOverlayText != null)
        {
            helpOverlayText.text = "<color=#FFFF88><b>СЛУЖЕБНАЯ ЗАПИСКА СО СТОЛА ОПЕРАТОРА:</b></color>\n\n" +
                                  DeskPaperNote.GetNoteText(currentGame);
        }
    }

    public string GetHelpTextForCurrentGame()
    {
        switch (currentGame)
        {
            case GameType.TuringTape:
                return
                    "<b><color=#00FFAA>МОДУЛЬ 01: МАШИНА ТЬЮРИНГА (ДВОИЧНЫЙ СУММАТОР НА 1)</color></b>\n" +
                    "<color=#66CCAA>──────────────────────────────────────────────────────────────────────────</color>\n" +
                    "<color=#FFFF88><b>ЗАДАЧА:</b></color> Прибавить 1 к двоичному числу <b>1011</b> (11 в дес.) и получить <b>1100</b> (12 в дес.).\n\n" +
                    "<color=#FFFF88><b>ПРИНЦИП РАБОТЫ АВТОМАТА:</b></color>\n" +
                    "1. <b>Состояние q0:</b> Перемотка вправо до знака '_', шаг влево на младший значащий бит.\n" +
                    "2. <b>Состояние q1 (Сложение с переносом):</b>\n" +
                    "   • Если под кареткой <color=#FFFF33>'1'</color>: 1+1=0 с переносом ──► Запись [0], Сдвиг [← ВЛЕВО], Переход в [q1].\n" +
                    "   • Если под кареткой <color=#00FFAA>'0'</color>: 0+1=1 без переноса ──► Запись [1], Сдвиг [• СТОП ], Переход в [HALT].\n\n" +
                    "<color=#FFFF88><b>УПРАВЛЕНИЕ:</b></color>\n" +
                    "• [ВВЕРХ/ВНИЗ] выбор ячейки таблицы, [ВЛЕВО/ВПРАВО] изменение значения.\n" +
                    "• [ПРОБЕЛ] пошаговый тест, [ENTER] непрерывный пуск, [R] сброс.";

            case GameType.FrequencyAnalysis:
                return
                    "<b><color=#00FFAA>МОДУЛЬ 02: ЧАСТОТНЫЙ СПЕКТРОАНАЛИЗАТОР (ШИФР ПОДСТАНОВКИ)</color></b>\n" +
                    "<color=#66CCAA>──────────────────────────────────────────────────────────────────────────</color>\n" +
                    "<color=#FFFF88><b>ЗАДАЧА:</b></color> Вскрыть шифр сдвига секретной директивы управления.\n\n" +
                    "<color=#FFFF88><b>ПРИНЦИП РАБОТЫ:</b></color>\n" +
                    "• [СТРЕЛКИ ВЛЕВО/ВПРАВО] — циклический сдвиг фазы алфавита.\n" +
                    "• Совместите главные резонансные пики обеих кривых (буквы О, Е, А) и нажмите [ENTER].";

            case GameType.DatabaseFilter:
                return
                    "<b><color=#00FFAA>МОДУЛЬ 03: РЕЛЯЦИОННЫЙ ФИЛЬТР КАРТОТЕКИ (БАЗА СЕКТОРА 7)</color></b>\n" +
                    "<color=#66CCAA>──────────────────────────────────────────────────────────────────────────</color>\n" +
                    "<color=#FFFF88><b>ЗАДАЧА:</b></color> Локализовать карточку ночного криптографа и считать пароль.\n\n" +
                    "<color=#FFFF88><b>РЕШЕНИЕ:</b></color>\n" +
                    "• [1] Отдел: [КРИПТОГРАФИЯ], [2] Допуск: [ОСОБЫЙ], [3] Смена: [НОЧНАЯ].\n" +
                    "• Выборка сузится до 1 сотрудника с паролем [ 8 4 9 1 ]. Нажмите [ENTER].";

            case GameType.FileSearch:
                return
                    "<b><color=#00FFAA>МОДУЛЬ 04: ПОИСК НУЖНОГО ФАЙЛА В КАТАЛОГЕ</color></b>\n" +
                    "<color=#66CCAA>──────────────────────────────────────────────────────────────────────────</color>\n" +
                    "<color=#FFFF88><b>ЗАДАЧА:</b></color> Найти в списке файлов системы секретную директиву Центра.\n\n" +
                    "<color=#FFFF88><b>ПОДСКАЗКА:</b></color>\n" +
                    "• Среди столовых расписаний, списаний мебели и заявок на спирт найдите документ:\n" +
                    "  <b>«07. ОТ_ЦЕНТР_ДИРЕКТИВА_ОСОБАЯ_07.DAT»</b>.\n" +
                    "• Наведите курсор стрелками [ВВЕРХ/ВНИЗ] или клавишами [W/S] и нажмите [ENTER].";

            case GameType.PasswordSimple:
                return
                    "<b><color=#00FFAA>МОДУЛЬ 05: ПОДБОР ПАРОЛЯ ЗАЩИЩЕННОГО АРХИВА</color></b>\n" +
                    "<color=#66CCAA>──────────────────────────────────────────────────────────────────────────</color>\n" +
                    "<color=#FFFF88><b>ЗАДАЧА:</b></color> Ввести 4-значный пин-код доступа к секретному архиву связи.\n\n" +
                    "<color=#FFFF88><b>ПОДСКАЗКА:</b></color>\n" +
                    "• На столе возле монитора лежит служебная записка связиста с кодом: <b>[ 4 8 2 1 ]</b>.\n" +
                    "• Наберите [4] [8] [2] [1] и нажмите [ENTER] для разблокировки архива.";

            case GameType.NetworkRestoration:
                return
                    "<b><color=#00FFAA>МОДУЛЬ 06: ВОССТАНОВЛЕНИЕ СОЕДИНЕНИЯ С СЕРВЕРОМ</color></b>\n" +
                    "<color=#66CCAA>──────────────────────────────────────────────────────────────────────────</color>\n" +
                    "<color=#FFFF88><b>ЗАДАЧА:</b></color> Восстановить разорванную магистраль от узла [A] к узлу [B].\n\n" +
                    "<color=#FFFF88><b>ПОДСКАЗКА ПО ТЕЛЕМЕТРИИ:</b></color>\n" +
                    "• Включите <b>[1] Канал I (Север)</b> ──► [ВКЛ] (исправен).\n" +
                    "• Включите <b>[4] Канал IV (Оптика)</b> ──► [ВКЛ] (исправен).\n" +
                    "• Включите <b>[6] Канал VI (Резерв)</b> ──► [ВКЛ] (исправен).\n" +
                    "• Каналы II (КЗ), III (Рассинхрон) и V (Перегрев 48А) оставьте [ВЫКЛ]!\n" +
                    "• Нажмите [ENTER] для восстановления связи на 100%.";

            case GameType.CaesarArchive:
                return
                    "<b><color=#00FFAA>МОДУЛЬ 07: ДЕШИФРАТОР АРХИВА (ШИФР ЦЕЗАРЯ СО СДВИГОМ 3)</color></b>\n" +
                    "<color=#66CCAA>──────────────────────────────────────────────────────────────────────────</color>\n" +
                    "<color=#FFFF88><b>ЗАДАЧА:</b></color> Найти архив с директивой «ATTACK».\n\n" +
                    "<color=#FFFF88><b>ПОДСКАЗКА:</b></color>\n" +
                    "• Буквы в названиях сдвинуты на 3 вперед: A->D, T->W, C->F, K->N.\n" +
                    "• Слово «ATTACK» зашифровано как <b>«DWWDFN_80.ZIP»</b>.\n" +
                    "• Выберите первый архив в списке и нажмите [ENTER].";

            case GameType.PasswordFormula:
                return
                    "<b><color=#00FFAA>МОДУЛЬ 08: ВЫЧИСЛИТЕЛЬНЫЙ ШИФР (АРХИВ КАРТ ГЕНШТАБА)</color></b>\n" +
                    "<color=#66CCAA>──────────────────────────────────────────────────────────────────────────</color>\n" +
                    "<color=#FFFF88><b>ЗАДАЧА:</b></color> Вычислить 4-значный PIN по формуле (A × B) - C.\n\n" +
                    "<color=#FFFF88><b>ВЫЧИСЛЕНИЕ:</b></color>\n" +
                    "• A = 17,  B = 23,  C = 42.\n" +
                    "• (17 × 23) = 391.\n" +
                    "• 391 - 42 = 349.\n" +
                    "• Дополняем нулем до 4 знаков: <b>[ 0 3 4 9 ]</b>. Нажмите [ENTER].";

            case GameType.DossierDeduction:
                return
                    "<b><color=#00FFAA>МОДУЛЬ 09: ДЕДУКЦИЯ КАРТОТЕКИ САБОТАЖНИКА</color></b>\n" +
                    "<color=#66CCAA>──────────────────────────────────────────────────────────────────────────</color>\n" +
                    "<color=#FFFF88><b>ЗАДАЧА:</b></color> Вычислить диверсанта по ориентировке контрразведки.\n\n" +
                    "<color=#FFFF88><b>ДЕДУКТИВНЫЙ АНАЛИЗ:</b></color>\n" +
                    "• Город вне СССР: Москва, Ленинград, Минск исключаются ──► только Даллас!\n" +
                    "• Время после 22:00 в серверном блоке (3-08) ──► 22:30.\n" +
                    "• Подозреваемый: <b>Строка 2: Лейтенант Бейкер</b>. Нажмите [ENTER].";

            case GameType.RotorCipher:
                return
                    "<b><color=#00FFAA>МОДУЛЬ 10: ШИФРОВАЛЬНАЯ МАШИНА «ФИАЛКА-М125»</color></b>\n" +
                    "<color=#66CCAA>──────────────────────────────────────────────────────────────────────────</color>\n" +
                    "<color=#FFFF88><b>ЗАДАЧА:</b></color> Выставить суточный ключ на роторах шифратора.\n\n" +
                    "<color=#FFFF88><b>РЕШЕНИЕ:</b></color>\n" +
                    "• Роторы: [1] IV (4), [2] II (2), [3] VII (7).\n" +
                    "• Выставьте комбинацию <b>[ IV - II - VII ]</b> и нажмите [ENTER].";

            case GameType.OmegaMasterProtocol:
                return
                    "<b><color=#00FFAA>ФИНАЛЬНЫЙ МОДУЛЬ: МАСТЕР-ПРОТОКОЛ «ОМЕГА»</color></b>\n" +
                    "<color=#66CCAA>──────────────────────────────────────────────────────────────────────────</color>\n" +
                    "<color=#FFFF88><b>КЛЮЧИ ИЗ РАСШИФРОВАННЫХ ФАЙЛОВ:</b></color>\n" +
                    "1. Тьюринг: [ 1 1 0 0 ]\n" +
                    "2. Сдвиг Цезаря: [ Δ = +7 ]\n" +
                    "3. Код карт: [ 0 3 4 9 ]\n" +
                    "4. Роторы «Фиалка»: [ IV - II - VII ]\n" +
                    "Синхронизируйте все 4 канала и нажмите [ENTER].";

            default:
                return "Инструкция отсутствует.";
        }
    }

    public void RestartCurrentGameToStartingPoint()
    {
        if (!isGameActive) return;

        PlayClick();
        errorLockoutTimer = 0f;
        currentPuzzleErrors = 0;

        if (turingRunCoroutine != null)
        {
            StopCoroutine(turingRunCoroutine);
            turingRunCoroutine = null;
        }
        turingIsRunning = false;

        switch (currentGame)
        {
            case GameType.TuringTape:
                turingTape = (char[])turingInitialTape.Clone();
                turingHeadIndex = 1;
                turingCurrentState = 0;
                turingStepCount = 0;
                turingActiveRuleIndex = -1;
                turingSelectedSlot = 0;
                turingSlotValues = new int[6] { 1, 1, 0, 0, 1, 1 };
                turingLastActionNote = "Каретка возвращена в стартовую точку (ячейка #1, q0).";
                UpdateTuringDisplay();
                break;

            case GameType.FrequencyAnalysis:
                freqShift = 2;
                UpdateFrequencyDisplay();
                break;

            case GameType.DatabaseFilter:
                dbDeptIndex = 0;
                dbClearance = 0;
                dbShiftIndex = 0;
                UpdateDatabaseDisplay();
                break;

            case GameType.FileSearch:
                fileSearchSelectedIndex = 0;
                UpdateFileSearchDisplay();
                break;

            case GameType.PasswordSimple:
                simplePinInput = "";
                UpdatePasswordSimpleDisplay();
                break;

            case GameType.NetworkRestoration:
                netSelectedLine = 0;
                netLinesConnected = new bool[6] { false, false, false, false, false, false };
                UpdateNetworkRestorationDisplay();
                break;

            case GameType.CaesarArchive:
                caesarSelectedIndex = 0;
                UpdateCaesarArchiveDisplay();
                break;

            case GameType.PasswordFormula:
                formulaPinInput = "";
                UpdatePasswordFormulaDisplay();
                break;

            case GameType.DossierDeduction:
                dossierSelectedIndex = 0;
                UpdateDossierDeductionDisplay();
                break;

            case GameType.RotorCipher:
                rotor1 = 1;
                rotor2 = 6;
                rotor3 = 3;
                UpdateRotorDisplay();
                break;

            case GameType.OmegaMasterProtocol:
                omegaSelectedChannel = 0;
                omegaCh1Index = 1;
                omegaCh2Shift = 3;
                omegaCh3Index = 0;
                omegaCh4Index = 0;
                UpdateOmegaDisplay();
                break;
        }

        if (statusFeedbackText != null)
        {
            statusFeedbackText.text = "<color=#00FFAA><b>[ПЕРЕЗАПУСК] Задача возвращена в стартовую точку!</b></color>";
        }
    }

    private void HandleOverlayKeyboardInput()
    {
        if (IsKey(KeyCode.H) || IsKey(KeyCode.Escape) || IsKey(KeyCode.Return) || IsKey(KeyCode.Space) || IsKey(KeyCode.N))
        {
            if (isHelpOpen) ToggleHelp(false);
            if (isNoteOpen) ToggleDeskNote(false);
        }
    }

    private void HandleKeyboardInput()
    {
        if (IsKey(KeyCode.H))
        {
            ToggleHelp(!isHelpOpen);
            return;
        }

        if (IsKey(KeyCode.N))
        {
            ToggleDeskNote(!isNoteOpen);
            return;
        }

        if (IsKey(KeyCode.R))
        {
            RestartCurrentGameToStartingPoint();
            return;
        }

        if (IsKey(KeyCode.Escape))
        {
            CancelMiniGame();
            return;
        }

        switch (currentGame)
        {
            case GameType.TuringTape:
                if (IsKey(KeyCode.UpArrow))
                {
                    if (turingSelectedSlot >= 3) turingSelectedSlot -= 3;
                    else turingSelectedSlot = (turingSelectedSlot - 1 + 6) % 6;
                    PlayClick();
                    UpdateTuringDisplay();
                }
                else if (IsKey(KeyCode.DownArrow))
                {
                    if (turingSelectedSlot < 3) turingSelectedSlot += 3;
                    else turingSelectedSlot = (turingSelectedSlot + 1) % 6;
                    PlayClick();
                    UpdateTuringDisplay();
                }
                else if (IsKey(KeyCode.LeftArrow))
                {
                    turingSlotValues[turingSelectedSlot] = (turingSlotValues[turingSelectedSlot] - 1 + 3) % 3;
                    PlayClick();
                    UpdateTuringDisplay();
                }
                else if (IsKey(KeyCode.RightArrow))
                {
                    turingSlotValues[turingSelectedSlot] = (turingSlotValues[turingSelectedSlot] + 1) % 3;
                    PlayClick();
                    UpdateTuringDisplay();
                }
                else if (IsKey(KeyCode.Space))
                {
                    StepTuringSingle();
                }
                else if (IsKey(KeyCode.Return))
                {
                    RunTuringContinuous();
                }
                break;

            case GameType.FrequencyAnalysis:
                if (IsKey(KeyCode.LeftArrow))
                {
                    freqShift = (freqShift - 1 + 12) % 12;
                    PlayClick();
                    UpdateFrequencyDisplay();
                }
                else if (IsKey(KeyCode.RightArrow))
                {
                    freqShift = (freqShift + 1) % 12;
                    PlayClick();
                    UpdateFrequencyDisplay();
                }
                else if (IsKey(KeyCode.Return))
                {
                    OnMainActionButtonPressed();
                }
                break;

            case GameType.DatabaseFilter:
                if (IsKey(KeyCode.Alpha1)) { dbDeptIndex = (dbDeptIndex + 1) % 4; PlayClick(); UpdateDatabaseDisplay(); }
                else if (IsKey(KeyCode.Alpha2)) { dbClearance = (dbClearance + 1) % 3; PlayClick(); UpdateDatabaseDisplay(); }
                else if (IsKey(KeyCode.Alpha3)) { dbShiftIndex = (dbShiftIndex + 1) % 3; PlayClick(); UpdateDatabaseDisplay(); }
                else if (IsKey(KeyCode.Return))
                {
                    OnMainActionButtonPressed();
                }
                break;

            case GameType.FileSearch:
                if (IsKey(KeyCode.UpArrow) || IsKey(KeyCode.W))
                {
                    fileSearchSelectedIndex = (fileSearchSelectedIndex - 1 + fileSearchList.Length) % fileSearchList.Length;
                    PlayClick();
                    UpdateFileSearchDisplay();
                }
                else if (IsKey(KeyCode.DownArrow) || IsKey(KeyCode.S))
                {
                    fileSearchSelectedIndex = (fileSearchSelectedIndex + 1) % fileSearchList.Length;
                    PlayClick();
                    UpdateFileSearchDisplay();
                }
                else if (IsKey(KeyCode.Return) || IsKey(KeyCode.Space))
                {
                    OnMainActionButtonPressed();
                }
                break;

            case GameType.PasswordSimple:
                HandlePinDigitInput(ref simplePinInput, UpdatePasswordSimpleDisplay);
                break;

            case GameType.NetworkRestoration:
                if (IsKey(KeyCode.UpArrow) || IsKey(KeyCode.W))
                {
                    netSelectedLine = (netSelectedLine - 1 + 6) % 6;
                    PlayClick();
                    UpdateNetworkRestorationDisplay();
                }
                else if (IsKey(KeyCode.DownArrow) || IsKey(KeyCode.S))
                {
                    netSelectedLine = (netSelectedLine + 1) % 6;
                    PlayClick();
                    UpdateNetworkRestorationDisplay();
                }
                else if (IsKey(KeyCode.Alpha1)) { netSelectedLine = 0; PlayClick(); UpdateNetworkRestorationDisplay(); }
                else if (IsKey(KeyCode.Alpha2)) { netSelectedLine = 1; PlayClick(); UpdateNetworkRestorationDisplay(); }
                else if (IsKey(KeyCode.Alpha3)) { netSelectedLine = 2; PlayClick(); UpdateNetworkRestorationDisplay(); }
                else if (IsKey(KeyCode.Alpha4)) { netSelectedLine = 3; PlayClick(); UpdateNetworkRestorationDisplay(); }
                else if (IsKey(KeyCode.Alpha5)) { netSelectedLine = 4; PlayClick(); UpdateNetworkRestorationDisplay(); }
                else if (IsKey(KeyCode.Alpha6)) { netSelectedLine = 5; PlayClick(); UpdateNetworkRestorationDisplay(); }
                else if (IsKey(KeyCode.Space))
                {
                    netLinesConnected[netSelectedLine] = !netLinesConnected[netSelectedLine];
                    PlayRelay();
                    UpdateNetworkRestorationDisplay();
                }
                else if (IsKey(KeyCode.Return))
                {
                    OnMainActionButtonPressed();
                }
                break;

            case GameType.CaesarArchive:
                if (IsKey(KeyCode.UpArrow) || IsKey(KeyCode.W))
                {
                    caesarSelectedIndex = (caesarSelectedIndex - 1 + caesarFiles.Length) % caesarFiles.Length;
                    PlayClick();
                    UpdateCaesarArchiveDisplay();
                }
                else if (IsKey(KeyCode.DownArrow) || IsKey(KeyCode.S))
                {
                    caesarSelectedIndex = (caesarSelectedIndex + 1) % caesarFiles.Length;
                    PlayClick();
                    UpdateCaesarArchiveDisplay();
                }
                else if (IsKey(KeyCode.Alpha1)) { caesarSelectedIndex = 0; PlayClick(); UpdateCaesarArchiveDisplay(); }
                else if (IsKey(KeyCode.Alpha2)) { caesarSelectedIndex = 1; PlayClick(); UpdateCaesarArchiveDisplay(); }
                else if (IsKey(KeyCode.Alpha3)) { caesarSelectedIndex = 2; PlayClick(); UpdateCaesarArchiveDisplay(); }
                else if (IsKey(KeyCode.Alpha4)) { caesarSelectedIndex = 3; PlayClick(); UpdateCaesarArchiveDisplay(); }
                else if (IsKey(KeyCode.Return) || IsKey(KeyCode.Space))
                {
                    OnMainActionButtonPressed();
                }
                break;

            case GameType.PasswordFormula:
                HandlePinDigitInput(ref formulaPinInput, UpdatePasswordFormulaDisplay);
                break;

            case GameType.DossierDeduction:
                if (IsKey(KeyCode.UpArrow) || IsKey(KeyCode.W))
                {
                    dossierSelectedIndex = (dossierSelectedIndex - 1 + dossierRows.Length) % dossierRows.Length;
                    PlayClick();
                    UpdateDossierDeductionDisplay();
                }
                else if (IsKey(KeyCode.DownArrow) || IsKey(KeyCode.S))
                {
                    dossierSelectedIndex = (dossierSelectedIndex + 1) % dossierRows.Length;
                    PlayClick();
                    UpdateDossierDeductionDisplay();
                }
                else if (IsKey(KeyCode.Alpha1)) { dossierSelectedIndex = 0; PlayClick(); UpdateDossierDeductionDisplay(); }
                else if (IsKey(KeyCode.Alpha2)) { dossierSelectedIndex = 1; PlayClick(); UpdateDossierDeductionDisplay(); }
                else if (IsKey(KeyCode.Alpha3)) { dossierSelectedIndex = 2; PlayClick(); UpdateDossierDeductionDisplay(); }
                else if (IsKey(KeyCode.Alpha4)) { dossierSelectedIndex = 3; PlayClick(); UpdateDossierDeductionDisplay(); }
                else if (IsKey(KeyCode.Return) || IsKey(KeyCode.Space))
                {
                    OnMainActionButtonPressed();
                }
                break;

            case GameType.RotorCipher:
                if (IsKey(KeyCode.Alpha1)) { rotor1 = (rotor1 + 1) % 10; PlayRelay(); UpdateRotorDisplay(); }
                else if (IsKey(KeyCode.Alpha2)) { rotor2 = (rotor2 + 1) % 10; PlayRelay(); UpdateRotorDisplay(); }
                else if (IsKey(KeyCode.Alpha3)) { rotor3 = (rotor3 + 1) % 10; PlayRelay(); UpdateRotorDisplay(); }
                else if (IsKey(KeyCode.Return))
                {
                    OnMainActionButtonPressed();
                }
                break;

            case GameType.OmegaMasterProtocol:
                if (IsKey(KeyCode.Alpha1)) { omegaSelectedChannel = 0; PlayClick(); UpdateOmegaDisplay(); }
                else if (IsKey(KeyCode.Alpha2)) { omegaSelectedChannel = 1; PlayClick(); UpdateOmegaDisplay(); }
                else if (IsKey(KeyCode.Alpha3)) { omegaSelectedChannel = 2; PlayClick(); UpdateOmegaDisplay(); }
                else if (IsKey(KeyCode.Alpha4)) { omegaSelectedChannel = 3; PlayClick(); UpdateOmegaDisplay(); }
                else if (IsKey(KeyCode.UpArrow) || IsKey(KeyCode.W))
                {
                    omegaSelectedChannel = (omegaSelectedChannel + 3) % 4;
                    PlayClick();
                    UpdateOmegaDisplay();
                }
                else if (IsKey(KeyCode.DownArrow) || IsKey(KeyCode.S))
                {
                    omegaSelectedChannel = (omegaSelectedChannel + 1) % 4;
                    PlayClick();
                    UpdateOmegaDisplay();
                }
                else if (IsKey(KeyCode.LeftArrow) || IsKey(KeyCode.A))
                {
                    if (omegaSelectedChannel == 0) omegaCh1Index = (omegaCh1Index + 3) % 4;
                    else if (omegaSelectedChannel == 1) omegaCh2Shift = (omegaCh2Shift + 12) % 13;
                    else if (omegaSelectedChannel == 2) omegaCh3Index = (omegaCh3Index + 3) % 4;
                    else if (omegaSelectedChannel == 3) omegaCh4Index = (omegaCh4Index + 3) % 4;
                    PlayRelay();
                    UpdateOmegaDisplay();
                }
                else if (IsKey(KeyCode.RightArrow) || IsKey(KeyCode.D) || IsKey(KeyCode.Space))
                {
                    if (omegaSelectedChannel == 0) omegaCh1Index = (omegaCh1Index + 1) % 4;
                    else if (omegaSelectedChannel == 1) omegaCh2Shift = (omegaCh2Shift + 1) % 13;
                    else if (omegaSelectedChannel == 2) omegaCh3Index = (omegaCh3Index + 1) % 4;
                    else if (omegaSelectedChannel == 3) omegaCh4Index = (omegaCh4Index + 1) % 4;
                    PlayRelay();
                    UpdateOmegaDisplay();
                }
                else if (IsKey(KeyCode.Return))
                {
                    OnMainActionButtonPressed();
                }
                break;
        }
    }

    private void HandlePinDigitInput(ref string pinStr, Action updateAction)
    {
        for (int d = 0; d <= 9; d++)
        {
            if (IsDigitPressed(d))
            {
                if (pinStr.Length < 4)
                {
                    pinStr += d.ToString();
                    PlayClick();
                    updateAction?.Invoke();
                }
                return;
            }
        }

        if (IsKey(KeyCode.Backspace))
        {
            if (pinStr.Length > 0)
            {
                pinStr = pinStr.Substring(0, pinStr.Length - 1);
                PlayClick();
                updateAction?.Invoke();
            }
            return;
        }

        if (IsKey(KeyCode.Return))
        {
            OnMainActionButtonPressed();
        }
    }

    private void OnMainActionButtonPressed()
    {
        if (errorLockoutTimer > 0f) return;

        switch (currentGame)
        {
            case GameType.TuringTape:
                RunTuringContinuous();
                break;

            case GameType.FrequencyAnalysis:
                if (freqShift == freqTargetShift)
                    OnGameSuccess("ШИФР ПОДСТАНОВКИ ВЗЛОМАН! ТЕКСТ ДИРЕКТИВЫ ЧИТАЕМ.");
                else
                    TriggerErrorLockout("Неверный сдвиг фазы! Текст остается зашифрованным.");
                break;

            case GameType.DatabaseFilter:
                if (dbDeptIndex == 1 && dbClearance == 2 && dbShiftIndex == 2)
                    OnGameSuccess("ПАРОЛЬ АДМИНИСТРАТОРА НАЙДЕН: 8491!");
                else
                    TriggerErrorLockout("Выборка слишком широкая! Уточните все 3 критерия.");
                break;

            case GameType.FileSearch:
                if (fileSearchSelectedIndex == FILE_SEARCH_CORRECT_INDEX)
                {
                    OnGameSuccess("ДИРЕКТИВА ЦЕНТРА «ОТ_ЦЕНТР_07.DAT» НАЙДЕНА И СЧИТАНА В ПАМЯТЬ!");
                }
                else
                {
                    TriggerErrorLockout("Это рядовой хозяйственный документ! Найдите секретную директиву Центра.");
                }
                break;

            case GameType.PasswordSimple:
                if (simplePinInput.Length < 4)
                {
                    TriggerErrorLockout("Введите все 4 цифры пароля!");
                }
                else if (simplePinInput == SIMPLE_PIN_CORRECT)
                {
                    OnGameSuccess("ПАРОЛЬ ПРИНЯТ! ЗАЩИЩЕННЫЙ АРХИВ СВЯЗИ РАЗБЛОКИРОВАН.");
                }
                else
                {
                    TriggerErrorLockout("Неверный пин-код! Сверьтесь с запиской на столе (4821).");
                    simplePinInput = "";
                    UpdatePasswordSimpleDisplay();
                }
                break;

            case GameType.NetworkRestoration:
                bool hasShortCircuit = netLinesConnected[1] || netLinesConnected[2] || netLinesConnected[4];
                bool allCorrectActive = netLinesConnected[0] && netLinesConnected[3] && netLinesConnected[5];

                if (!hasShortCircuit && allCorrectActive)
                {
                    OnGameSuccess("СВЯЗЬ С СЕРВЕРОМ ВОССТАНОВЛЕНА НА 100%! МАРШРУТ A ──► B АКТИВЕН.");
                }
                else if (hasShortCircuit)
                {
                    TriggerErrorLockout("Авария на линии! Канал с коротким замыканием или перегрузкой замкнул шину!");
                }
                else
                {
                    TriggerErrorLockout("Шина не скоммутирована! Подключите все 3 исправных канала (Север, Оптика, Резерв).");
                }
                break;

            case GameType.CaesarArchive:
                if (caesarSelectedIndex == CAESAR_CORRECT_INDEX)
                {
                    OnGameSuccess("АРХИВ «DWWDFN_80.ZIP» РАСШИФРОВАН: ПЛАН АТАКИ ИЗВЛЕЧЕН!");
                }
                else
                {
                    TriggerErrorLockout("Выбран неверный архив! Сдвиг букв не соответствует слову «ATTACK».");
                }
                break;

            case GameType.PasswordFormula:
                if (formulaPinInput.Length < 4)
                {
                    TriggerErrorLockout("Введите все 4 цифры вычисленного пароля!");
                }
                else if (formulaPinInput == FORMULA_PIN_CORRECT)
                {
                    OnGameSuccess("ПАРОЛЬ 0349 ВЕРЕН! ТОПОГРАФИЧЕСКИЕ КАРТЫ РАСШИФРОВАНЫ.");
                }
                else
                {
                    TriggerErrorLockout("Неверный результат формулы! Пересчитайте (17 * 23) - 42.");
                    formulaPinInput = "";
                    UpdatePasswordFormulaDisplay();
                }
                break;

            case GameType.DossierDeduction:
                if (dossierSelectedIndex == DOSSIER_CORRECT_INDEX)
                {
                    OnGameSuccess("ДИВЕРСАНТ ВЫЧИСЛЕН: ЛЕЙТЕНАНТ БЕЙКЕР! ДОСЬЕ ПЕРЕДАНО В ОСОБЫЙ ОТДЕЛ.");
                }
                else
                {
                    TriggerErrorLockout("Ошибка дедукции! Проверьте город прибытия и ночное время по ориентировке.");
                }
                break;

            case GameType.RotorCipher:
                if (rotor1 == targetR1 && rotor2 == targetR2 && rotor3 == targetR3)
                    OnGameSuccess("МАШИНА «ФИАЛКА» РАСШИФРОВАЛА ВЕКТОРНЫЙ ПЛАН БУНКЕРА!");
                else
                    TriggerErrorLockout("Неверная комбинация роторов! Ключ дня не подошел.");
                break;

            case GameType.OmegaMasterProtocol:
                bool ch1Ok = (omegaCh1Index == 3);
                bool ch2Ok = (omegaCh2Shift == 7);
                bool ch3Ok = (omegaCh3Index == 2);
                bool ch4Ok = (omegaCh4Index == 1);
                if (ch1Ok && ch2Ok && ch3Ok && ch4Ok)
                {
                    OnGameSuccess("МАСТЕР-АРХИВ «ОМЕГА» ПОЛНОСТЬЮ ДЕШИФРОВАН! СТРАТЕГИЧЕСКАЯ ПОБЕДА!");
                }
                else
                {
                    TriggerErrorLockout("Не все криптоканалы синхронизированы! Проверьте ключи взлома.");
                }
                break;
        }
    }

    private void OnGameSuccess(string message)
    {
        isSuccessTransition = true;
        PlaySuccess();
        if (statusFeedbackText != null)
        {
            statusFeedbackText.text = $"<color=#00FF88><b>[УСПЕХ] {message}</b></color>";
        }
        MainframeBootQuest.Instance?.ShowNotification($"<color=#00FF88>[ДЕШИФРОВКА УСПЕШНА]</color> {message}");

        StartCoroutine(CloseAfterDelay(true, 1.4f));
    }

    public void CancelMiniGame()
    {
        isGameActive = false;
        isSuccessTransition = false;
        isHelpOpen = false;
        isNoteOpen = false;
        if (helpOverlayPanel != null) helpOverlayPanel.SetActive(false);
        if (turingRunCoroutine != null) StopCoroutine(turingRunCoroutine);
        turingIsRunning = false;
        PlayClick();
        if (miniGameRootPanel != null) miniGameRootPanel.SetActive(false);
        onGameFinishedCallback?.Invoke(false);
    }

    /// <summary>
    /// Forcibly collapses the puzzle window, closes any viewer/modal, and unzooms player from the monitor
    /// when the guard comes to inspect the room, allowing the player to sit in the chair and deceive the guard.
    /// </summary>
    public void OnGuardInspectionBreach()
    {
        if (isGameActive || (miniGameRootPanel != null && miniGameRootPanel.activeSelf))
        {
            CancelMiniGame();
        }

        if (VectorCommander.Instance != null)
        {
            VectorCommander.Instance.CloseHelp();
            VectorCommander.Instance.CloseViewer();
        }

        if (PlayerController.Instance != null && PlayerController.Instance.IsInspectingMonitor)
        {
            PlayerController.Instance.ExitMonitor();
        }

        MainframeBootQuest.Instance?.ShowNotification("<color=#FF3333>[⚠ ОХРАНА У ДВЕРИ!]</color> Шаги в тамбуре! Опустите рубильник питания и спрячьтесь за ЭВМ!");
    }

    /// <summary>
    /// Called when the deceit event succeeds: resets the error count so the player can retry the puzzle.
    /// </summary>
    public void OnDeceitSuccess()
    {
        currentPuzzleErrors = 0;
        errorLockoutTimer = 0f;
        Debug.Log("<color=#00FFAA><b>[ДЕШИФРОВКА]</b></color> Скрытность успешна! Счётчик ошибок терминала сброшен (0/3).");
        MainframeBootQuest.Instance?.ShowNotification("<color=#00FFAA>[ОХРАНА УШЛА]</color> Охранник проверил тёмный зал и ушёл. Включите рубильник [E] и продолжайте взлом.");
    }

    private IEnumerator CloseAfterDelay(bool success, float delay)
    {
        yield return new WaitForSeconds(delay);
        isGameActive = false;
        isSuccessTransition = false;
        isHelpOpen = false;
        isNoteOpen = false;
        if (helpOverlayPanel != null) helpOverlayPanel.SetActive(false);
        if (miniGameRootPanel != null) miniGameRootPanel.SetActive(false);
        onGameFinishedCallback?.Invoke(success);
    }

    private bool IsDigitPressed(int d)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;
        switch (d)
        {
            case 0: return kb.digit0Key.wasPressedThisFrame || kb.numpad0Key.wasPressedThisFrame;
            case 1: return kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame;
            case 2: return kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame;
            case 3: return kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame;
            case 4: return kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame;
            case 5: return kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame;
            case 6: return kb.digit6Key.wasPressedThisFrame || kb.numpad6Key.wasPressedThisFrame;
            case 7: return kb.digit7Key.wasPressedThisFrame || kb.numpad7Key.wasPressedThisFrame;
            case 8: return kb.digit8Key.wasPressedThisFrame || kb.numpad8Key.wasPressedThisFrame;
            case 9: return kb.digit9Key.wasPressedThisFrame || kb.numpad9Key.wasPressedThisFrame;
            default: return false;
        }
#else
        KeyCode main = (KeyCode)((int)KeyCode.Alpha0 + d);
        KeyCode num = (KeyCode)((int)KeyCode.Keypad0 + d);
        return Input.GetKeyDown(main) || Input.GetKeyDown(num);
#endif
    }

    private bool IsKey(KeyCode code)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;
        switch (code)
        {
            case KeyCode.Escape: return kb.escapeKey.wasPressedThisFrame;
            case KeyCode.Return: return kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
            case KeyCode.Space: return kb.spaceKey.wasPressedThisFrame;
            case KeyCode.Backspace: return kb.backspaceKey.wasPressedThisFrame;
            case KeyCode.LeftArrow: return kb.leftArrowKey.wasPressedThisFrame;
            case KeyCode.RightArrow: return kb.rightArrowKey.wasPressedThisFrame;
            case KeyCode.UpArrow: return kb.upArrowKey.wasPressedThisFrame;
            case KeyCode.DownArrow: return kb.downArrowKey.wasPressedThisFrame;
            case KeyCode.A: return kb.aKey.wasPressedThisFrame;
            case KeyCode.D: return kb.dKey.wasPressedThisFrame;
            case KeyCode.W: return kb.wKey.wasPressedThisFrame;
            case KeyCode.S: return kb.sKey.wasPressedThisFrame;
            case KeyCode.F: return kb.fKey.wasPressedThisFrame;
            case KeyCode.H: return kb.hKey.wasPressedThisFrame;
            case KeyCode.N: return kb.nKey.wasPressedThisFrame;
            case KeyCode.R: return kb.rKey.wasPressedThisFrame;
            case KeyCode.Alpha1: return kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame;
            case KeyCode.Alpha2: return kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame;
            case KeyCode.Alpha3: return kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame;
            case KeyCode.Alpha4: return kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame;
            case KeyCode.Alpha5: return kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame;
            case KeyCode.Alpha6: return kb.digit6Key.wasPressedThisFrame || kb.numpad6Key.wasPressedThisFrame;
            default: return false;
        }
#else
        return Input.GetKeyDown(code);
#endif
    }

    private void GenerateAudioClips()
    {
        // 1. Load authentic recorded audio from Sound folder
        var loadedClick = Resources.Load<AudioClip>("Sound/MouseClick");
        var loadedRelay = Resources.Load<AudioClip>("Sound/MinigameSound");
        var loadedSuccess = Resources.Load<AudioClip>("Sound/CipherSuccess");
        var loadedBonk = Resources.Load<AudioClip>("Sound/Bonk");
        var loadedAlarm = Resources.Load<AudioClip>("Sound/ErrorAlarm");

        if (loadedClick != null) clickClip = loadedClick;
        if (loadedRelay != null) relayTickClip = loadedRelay;
        if (loadedSuccess != null) successClip = loadedSuccess;
        if (loadedBonk != null) failClip = loadedBonk;
        else if (loadedAlarm != null) failClip = loadedAlarm;

        int sr = 44100;

        // Fallbacks if not found
        if (clickClip == null)
        {
            int n1 = (int)(sr * 0.025f);
            float[] d1 = new float[n1];
            for (int i = 0; i < n1; i++) d1[i] = Mathf.Sin(2f * Mathf.PI * 1600f * (float)i / sr) * (1f - (float)i / n1);
            clickClip = AudioClip.Create("MG_Click", n1, 1, sr, false);
            clickClip.SetData(d1, 0);
        }

        if (successClip == null)
        {
            int n2 = (int)(sr * 0.35f);
            float[] d2 = new float[n2];
            for (int i = 0; i < n2; i++)
            {
                float t = (float)i / sr;
                float freq = t < 0.15f ? 880f : 1760f;
                d2[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Sin(Mathf.PI * (t / 0.35f)) * 0.5f;
            }
            successClip = AudioClip.Create("MG_Success", n2, 1, sr, false);
            successClip.SetData(d2, 0);
        }

        if (failClip == null)
        {
            int n3 = (int)(sr * 0.25f);
            float[] d3 = new float[n3];
            for (int i = 0; i < n3; i++) d3[i] = Mathf.Sin(2f * Mathf.PI * 140f * (float)i / sr) * (1f - (float)i / n3) * 0.6f;
            failClip = AudioClip.Create("MG_Fail", n3, 1, sr, false);
            failClip.SetData(d3, 0);
        }

        if (relayTickClip == null)
        {
            int n4 = (int)(sr * 0.045f);
            float[] d4 = new float[n4];
            for (int i = 0; i < n4; i++)
            {
                float t = (float)i / n4;
                d4[i] = (Mathf.Sin(2f * Mathf.PI * 520f * (float)i / sr) + (UnityEngine.Random.value * 2f - 1f) * 0.3f) * (1f - t);
            }
            relayTickClip = AudioClip.Create("MG_Relay", n4, 1, sr, false);
            relayTickClip.SetData(d4, 0);
        }
    }

    private void PlayClick()
    {
        if (audioSource != null && clickClip != null) { audioSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f); audioSource.PlayOneShot(clickClip, 0.4f); }
    }

    private void PlaySuccess()
    {
        if (audioSource != null && successClip != null) audioSource.PlayOneShot(successClip, 0.6f);
    }

    private void PlayFail()
    {
        if (audioSource != null && failClip != null) audioSource.PlayOneShot(failClip, 0.5f);
    }

    private void PlayRelay()
    {
        if (audioSource != null && relayTickClip != null) { audioSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f); audioSource.PlayOneShot(relayTickClip, 0.5f); }
    }
}
