using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Hub and controller for the 10 authentic Soviet spy mini-games:
/// 1. Turing Tape (.BIN) - Rule-based Turing head tape processing
/// 2. Frequency Analysis (.DOC) - Interactive histogram cipher shift
/// 3. Relational Punchcards (.TXT) - 3-predicate database card filtering
/// 4. Hex Memory Patch (.MEM) - Buffer patch and CRC checksum match
/// 5. Oscilloscope Lissajous (.RAD) - Dual sine wave frequency & phase tuning
/// 6. Relay Matrix Routing (.NET) - 4x4 directional arrow switchboard packet routing
/// 7. Hamming Parity Matrix (.PAR) - Single-bit error correction in ferrite core matrix
/// 8. Disk Sector Timing (.DSK) - Rotating platter sector read alignment
/// 9. Stack Overflow LIFO (.STK) - Push/Pop register accumulation without overflow
/// 10. Rotor Cipher Machine (.ROT) - 3-rotor mechanical cipher offset decryption
/// </summary>
public class SpyMiniGames : MonoBehaviour
{
    public static SpyMiniGames Instance { get; private set; }

    public enum GameType
    {
        None,
        TuringTape,         // .BIN
        FrequencyAnalysis,  // .DOC
        DatabaseFilter,     // .TXT
        HexMemoryPatch,     // .MEM
        Oscilloscope,       // .RAD
        RelayRouting,       // .NET
        HammingParity,      // .PAR
        DiskSectorTiming,   // .DSK
        StackOverflow,      // .STK
        RotorCipher,        // .ROT
        OmegaMasterProtocol // .TOP
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

    // --- Sub-game data states ---

    // 1. Turing Machine State
    private readonly char[] turingInitialTape = { '_', '1', '0', '1', '1', '_' };
    private readonly char[] turingTargetTape  = { '_', '1', '1', '0', '0', '_' };
    private char[] turingTape = new char[6];
    private int turingHeadIndex = 1;
    private int turingCurrentState = 0; // 0 = q0 (Seek LSB), 1 = q1 (Increment), 2 = HALT
    private int turingStepCount = 0;
    private bool turingIsRunning = false;
    private Coroutine turingRunCoroutine = null;
    private string turingLastActionNote = "Каретка на ячейке #1 (q0). Нажмите [ПРОБЕЛ] для пошагового теста.";
    private int turingActiveRuleIndex = -1; // 0..5
    private int turingSelectedSlot = 0; // 0..5
    private int[] turingSlotValues = new int[6] { 1, 1, 0, 0, 1, 1 }; // Jumbled initial values

    // 2. Frequency
    private int freqShift = 4;
    private readonly int freqTargetShift = 7;

    // 3. Database
    private int dbDeptIndex = 0;  // 0: ВСЕ, 1: КРИПТО, 2: ОХРАНА, 3: СВЯЗЬ
    private int dbClearance = 0;  // 0: ВСЕ, 1: ДОПУСК-1, 2: ОСОБЫЙ
    private int dbShiftIndex = 0; // 0: ВСЕ, 1: ДЕНЬ, 2: НОЧЬ

    // 4. Hex Memory
    private string hexTarget = "0x4F0A";
    private int hexEntered = 0;
    private readonly int hexRequired = 4;

    // 5. Oscilloscope
    private float oscFrequency = 1.2f;
    private float oscPhase = 45f;
    private const float TARGET_FREQ = 2.5f;
    private const float TARGET_PHASE = 180f;

    // 6. Relay Routing
    private int[,] relayMatrix = new int[4, 4]; // 0: Right, 1: Down, 2: Left, 3: Up
    private int packetX = 0, packetY = 0;
    private float packetStepTimer = 0f;

    // 7. Hamming Parity
    private int[,] parityBits = new int[4, 4];
    private int badRow = 2, badCol = 1;

    // 8. Disk Sector
    private float diskAngle = 0f;
    private int sectorsCaptured = 0;
    private const int SECTORS_NEEDED = 3;
    private float targetSectorAngle = 180f;

    // 9. Stack
    private readonly List<int> stackRegisters = new List<int>();
    private const int STACK_TARGET = 16;
    private const int STACK_MAX_CAPACITY = 22;

    // 10. Rotor Cipher
    private int rotor1 = 2, rotor2 = 5, rotor3 = 1;
    private readonly int targetR1 = 4, targetR2 = 2, targetR3 = 7;

    // 11. Omega Master Protocol (.TOP)
    private int omegaSelectedChannel = 0; // 0..3
    private int omegaCh1Index = 1;        // Target: 3 ("1100")
    private int omegaCh2Shift = 3;        // Target: 7 ("Δ = +7")
    private int omegaCh3Index = 0;        // Target: 2 ("0x4F0A")
    private int omegaCh4Index = 0;        // Target: 1 ("IV - II - VII")

    // Common dynamic display text for the active game
    private Text dynamicDisplayText;

    public bool IsMiniGameActive => isGameActive;

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
        if (!isGameActive) return;

        // If help overlay is open, route input to overlay
        if (isHelpOpen)
        {
            HandleKeyboardInput();
            return;
        }

        // Active game update loops (no countdown timer - infinite calm analysis mode)
        switch (currentGame)
        {
            case GameType.Oscilloscope:
                UpdateOscilloscopeDisplay();
                break;
            case GameType.DiskSectorTiming:
                UpdateDiskSectorGame();
                break;
            case GameType.RelayRouting:
                UpdateRelayRoutingGame();
                break;
        }

        // Keyboard controls
        HandleKeyboardInput();
    }

    public static GameType GetGameTypeForFile(string fileName)
    {
        string upper = fileName.ToUpper();
        if (upper.EndsWith(".TOP") || upper.EndsWith(".FIN") || upper.EndsWith(".OMG")) return GameType.OmegaMasterProtocol;
        if (upper.EndsWith(".BIN") || upper.EndsWith(".EXE")) return GameType.TuringTape;
        if (upper.EndsWith(".DOC") || upper.EndsWith(".DIR")) return GameType.FrequencyAnalysis;
        if (upper.EndsWith(".TXT") || upper.EndsWith(".LST")) return GameType.DatabaseFilter;
        if (upper.EndsWith(".MEM") || upper.EndsWith(".DMP")) return GameType.HexMemoryPatch;
        if (upper.EndsWith(".RAD") || upper.EndsWith(".OSC")) return GameType.Oscilloscope;
        if (upper.EndsWith(".NET") || upper.EndsWith(".ROU")) return GameType.RelayRouting;
        if (upper.EndsWith(".PAR") || upper.EndsWith(".CHK")) return GameType.HammingParity;
        if (upper.EndsWith(".DSK") || upper.EndsWith(".TRK")) return GameType.DiskSectorTiming;
        if (upper.EndsWith(".STK") || upper.EndsWith(".BUF")) return GameType.StackOverflow;
        if (upper.EndsWith(".ROT") || upper.EndsWith(".SHF") || upper.EndsWith(".СХМ")) return GameType.RotorCipher;
        return GameType.TuringTape;
    }

    public void LaunchMiniGame(VectorCommander.VirtualFile file, Action<bool> onComplete)
    {
        activeFile = file;
        onGameFinishedCallback = onComplete;
        currentGame = GetGameTypeForFile(file.name);
        isGameActive = true;
        isHelpOpen = false;

        if (timerCountdownText != null)
        {
            timerCountdownText.text = "<color=#00FFAA>РЕЖИМ: БЕЗ ОГРАНИЧЕНИЯ ВРЕМЕНИ</color>";
        }

        if (helpOverlayPanel != null) helpOverlayPanel.SetActive(false);
        if (miniGameRootPanel != null) miniGameRootPanel.SetActive(true);
        PlayClick();

        InitializeSpecificGame(currentGame);
    }

    private void InitializeSpecificGame(GameType type)
    {
        // Clear dynamic display text reference
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
                turingSlotValues = new int[6] { 1, 1, 0, 0, 1, 1 }; // Jumbled initial configuration
                UpdateTuringDisplay();
                break;

            case GameType.FrequencyAnalysis:
                gameTitleText.text = "╔════ ЧАСТОТНЫЙ СПЕКТР ШИФРА ПОДСТАНОВКИ ════╗";
                gameDescriptionText.text = "[Стрелки Влево/Вправо] или регулятор: подберите сдвиг алфавита, чтобы пики гласных совпали с эталоном.";
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

            case GameType.HexMemoryPatch:
                gameTitleText.text = "╔════ HEX-ПАТЧЕР: СНЯТИЕ СТОРОЖЕВОГО БУФЕРА ════╗";
                gameDescriptionText.text = "Введите NOP-инструкцию [F] [F] [0] [0] для нейтрализации сторожевого адреса 0x4F0A.";
                hexEntered = 0;
                btnActionText.text = "[ИНЪЕКЦИЯ БАЙТА]";
                UpdateHexDisplay();
                break;

            case GameType.Oscilloscope:
                gameTitleText.text = "╔════ ОСЦИЛЛОГРАФ: СВЕДЕНИЕ РАДИОЧАСТОТЫ ════╗";
                gameDescriptionText.text = "[A/D] — Частота несущей, [W/S] — Фаза. Сложите интерференцию в резонансный круг.";
                oscFrequency = 1.0f;
                oscPhase = 30f;
                btnActionText.text = "[ЗАХВАТ СИГНАЛА]";
                UpdateOscilloscopeDisplay();
                break;

            case GameType.RelayRouting:
                gameTitleText.text = "╔════ РЕЛЕЙНЫЙ МАРШРУТИЗАТОР: ОБХОД ШЛЮЗОВ ════╗";
                gameDescriptionText.text = "[Стрелки] — выбор узла, [Пробел] — повернуть стрелку. Проложите путь от (0,0) к (3,3).";
                InitRelayMatrix();
                btnActionText.text = "[ПЕРЕДАТЬ ПАКЕТ]";
                UpdateRelayDisplay();
                break;

            case GameType.HammingParity:
                gameTitleText.text = "╔════ МАТРИЦА ХЭММИНГА: КОРРЕКЦИЯ ЧЕТНОСТИ ════╗";
                gameDescriptionText.text = "Строка 3 и Столбец 2 имеют нечетную сумму! [Стрелки] — курсор, [Enter/Пробел] — инвертировать бит.";
                InitHammingMatrix();
                btnActionText.text = "[ИНВЕРСИЯ БИТА]";
                UpdateHammingDisplay();
                break;

            case GameType.DiskSectorTiming:
                gameTitleText.text = "╔════ СЕКТОРНЫЙ ЗАХВАТ: ШПИНДЕЛЬ ЕС-5061 ════╗";
                gameDescriptionText.text = "Нажимайте [Пробел] точно в момент прохождения головки над зеленым сектором дорожки!";
                diskAngle = 0f;
                sectorsCaptured = 0;
                targetSectorAngle = 180f;
                btnActionText.text = "[ЗАХВАТ СЕКТОРА]";
                UpdateDiskSectorDisplay();
                break;

            case GameType.StackOverflow:
                gameTitleText.text = "╔════ СТЕК LIFO: ПЕРЕПОЛНЕНИЕ РЕГИСТРА ДОПУСКА ════╗";
                gameDescriptionText.text = "[1] PUSH 2, [2] PUSH 4, [3] PUSH 8, [Back] POP. Наберите сумму ровно 16, не превысив 22!";
                stackRegisters.Clear();
                btnActionText.text = "[СБРОС ВЕРШИНЫ]";
                UpdateStackDisplay();
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

    // --- Mini-Game 1: Turing Tape Logic ---

    private void UpdateTuringDisplay()
    {
        if (dynamicDisplayText == null) return;

        // 1. Initial and Target Tape
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

        // 2. Carriage Head Pointer
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

        // 3. Configurable slots
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

        // Active rule markers
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

        // Assemble screen
        dynamicDisplayText.text =
            $"<color=#88CCAA>ИСХОДНОЕ ЧИСЛО:</color> {initTapeStr} (11 в двоичной)   │   <color=#00FFAA>ЦЕЛЕВОЙ КЛЮЧ ЯДРА:</color> {targTapeStr} (12 = 11 + 1)\n\n" +
            $"{carriageVisual}\n" +
            $"  <color=#FFFF88>► {turingLastActionNote}</color>\n\n" +
            $"{tableVisual}\n\n" +
            $"<color=#FFFF66>[СТРЕЛКИ ВВЕРХ/ВНИЗ] Слот  │  [ВЛЕВО/ВПРАВО] Значение  │  [ПРОБЕЛ] Шаг  │  [ENTER] Пуск  │  [R] В старт  │  [H] Справка</color>";

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
            PlayFail();
            turingLastActionNote = "<color=#FF3333>ОШИБКА: Превышен лимит тактов (25)! Каретка зациклилась.</color>";
            if (statusFeedbackText != null)
                statusFeedbackText.text = "<color=#FF3333><b>[ЗАЦИКЛИВАНИЕ] Каретка застряла! Нажмите [R] для сброса.</b></color>";
            UpdateTuringDisplay();
            return;
        }

        if (turingHeadIndex < 0 || turingHeadIndex >= turingTape.Length)
        {
            turingIsRunning = false;
            PlayFail();
            turingLastActionNote = "<color=#FF3333>ОШИБКА: Каретка сошла с перфоленты за пределы буфера!</color>";
            if (statusFeedbackText != null)
                statusFeedbackText.text = "<color=#FF3333><b>[ВЫХОД ЗА ПРЕДЕЛЫ ЛЕНТЫ] Нажмите [R] для сброса.</b></color>";
            UpdateTuringDisplay();
            return;
        }

        char readSymbol = turingTape[turingHeadIndex];
        char writeSymbol = readSymbol;
        int moveDir = 0;
        int nextState = turingCurrentState;
        int ruleIndex = -1;

        if (turingCurrentState == 0) // q0: Seek right edge
        {
            if (readSymbol == '0')
            {
                writeSymbol = '0'; moveDir = 1; nextState = 0; ruleIndex = 0;
            }
            else if (readSymbol == '1')
            {
                writeSymbol = '1'; moveDir = 1; nextState = 0; ruleIndex = 1;
            }
            else if (readSymbol == '_')
            {
                writeSymbol = '_'; moveDir = -1; nextState = 1; ruleIndex = 2;
            }
        }
        else if (turingCurrentState == 1) // q1: Increment
        {
            char[] writes = { '0', '1', '_' };
            int[] moves = { -1, 1, 0 }; // Left, Right, Stop
            int[] states = { 0, 1, 2 }; // q0, q1, HALT

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

        // Apply write
        turingTape[turingHeadIndex] = writeSymbol;
        PlayClick();

        string stateNameBefore = turingCurrentState == 0 ? "q0" : "q1";
        string stateNameAfter  = nextState == 0 ? "q0" : (nextState == 1 ? "q1" : "HALT");
        string moveName = moveDir == -1 ? "← ВЛЕВО" : (moveDir == 1 ? "→ ВПРАВО" : "• СТОП");

        turingLastActionNote = $"Такт #{turingStepCount}: [{stateNameBefore}] прочитал '{readSymbol}' ──► записал '{writeSymbol}', сдвиг {moveName}, перешел в [{stateNameAfter}]";

        // Apply move and state
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
            PlayFail();
            string cur = new string(turingTape);
            string tgt = new string(turingTargetTape);
            turingLastActionNote = $"<color=#FF5555>ОСТАНОВ: Результат '{cur}' не совпадает с целью '{tgt}'!</color>";
            if (statusFeedbackText != null)
                statusFeedbackText.text = "<color=#FF3333><b>[НЕСООТВЕТСТВИЕ КЛЮЧА] Нажмите [R] для сброса и скорректируйте микрокод.</b></color>";
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
            $"<color=#FFFF66>[СТРЕЛКИ ВЛЕВО / ВПРАВО] — Сдвиг регистра | [ENTER] — Применить</color>";
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
            $"<color=#FFFF66>[1/2/3] — Переключить фильтр | [ENTER] — Зафиксировать пароль</color>";
    }

    // --- Mini-Game 4: Hex Memory Patch ---

    private void UpdateHexDisplay()
    {
        if (dynamicDisplayText == null) return;

        string[] enteredBytes = { "??", "??", "??", "??" };
        string[] targetBytes = { "FF", "FF", "00", "00" };
        for (int i = 0; i < hexEntered; i++) enteredBytes[i] = targetBytes[i];

        string statusCol = hexEntered == hexRequired ? "#00FF88" : "#FFCC00";

        dynamicDisplayText.text =
            $"<size=22><color=#66CCAA>ДАМП СТОРОЖЕВОГО БУФЕРА ТРЕВОГИ:</color></size>\n\n" +
            $"  АДРЕС       00 01 02 03  ASCII\n" +
            $"  0x4F00      5A 8C 12 00  Z...\n" +
            $"  0x4F04      E4 10 99 AA  ....\n" +
            $"  <color=#FF4444><b>0x4F08      01 00 [{enteredBytes[0]} {enteredBytes[1]}]</b></color>  <color=#FFFF66><-- СТОРОЖЕВОЙ ВЕКТОР ({hexTarget})</color>\n" +
            $"  <color=#FF4444><b>0x4F0C      [{enteredBytes[2]} {enteredBytes[3]}] 82 C0</b></color>\n\n" +
            $"<color=#00AA77>───────────────────────────────────────────────────────────</color>\n" +
            $"  ВВЕДЕНО БАЙТОВ ПАТЧА: <color={statusCol}>{hexEntered} / {hexRequired}</color>\n\n" +
            $"<color=#FFFF66>[ПРОБЕЛ / ENTER] — Инъекция байта NOP (FF/00) в память</color>";
    }

    // --- Mini-Game 5: Oscilloscope Signal Tuning ---

    private void UpdateOscilloscopeDisplay()
    {
        if (dynamicDisplayText == null) return;

        float freqDiff = Mathf.Abs(oscFrequency - TARGET_FREQ);
        float phaseDiff = Mathf.Abs(oscPhase - TARGET_PHASE);
        bool inResonance = freqDiff < 0.25f && phaseDiff < 15f;

        string waveVisual = inResonance ?
            "<color=#00FFAA>       ╭───────────╮       \n    ╭──╯           ╰──╮    \n────╯                 ╰────\n    ╰──╮           ╭──╯    \n       ╰───────────╯       </color>" :
            "<color=#558866>   ~  ^  ~  v  ~  ^  ~     \n ~~   ~~   ~~   ~~   ~~    \n---------------------------\n ~~   ~~   ~~   ~~   ~~    \n   v  ~  ^  ~  v  ~  ^     </color>";

        string audioStatus = inResonance ?
            "<color=#00FF88>РЕЗОНАНС! [ПЕРЕХВАТ 144.200 МГц]: «ВЫЗЫВАЮ БАЗУ, КОД: 7392»</color>" :
            "<color=#88AA99>ПОМЕХИ В ЭФИРЕ: ШУМ НЕСУЩЕЙ ЧАСТОТЫ...</color>";

        dynamicDisplayText.text =
            $"<size=22><color=#66CCAA>ЭКРАН ЭЛТ-ОСЦИЛЛОГРАФА (ФИГУРЫ ЛИССАЖУ):</color></size>\n\n" +
            $"{waveVisual}\n\n" +
            $"  ЧАСТОТА: <b><color=#00FFAA>{oscFrequency:F1} МГц</color></b> (Цель: 2.5)  |  ФАЗА: <b><color=#00FFAA>{Mathf.RoundToInt(oscPhase)}°</color></b> (Цель: 180°)\n" +
            $"  {audioStatus}\n\n" +
            $"<color=#FFFF66>[A/D] — Частота  |  [W/S] — Фаза  |  [ENTER] — Захватить частоту</color>";
    }

    // --- Mini-Game 6: Relay Matrix Routing ---

    private void InitRelayMatrix()
    {
        relayMatrix = new int[4, 4];
        packetX = 0; packetY = 0;
        packetStepTimer = 0f;
        // Default arrows
        for (int r = 0; r < 4; r++)
            for (int c = 0; c < 4; c++)
                relayMatrix[r, c] = 0; // Right
    }

    private void UpdateRelayRoutingGame()
    {
        packetStepTimer += Time.deltaTime;
        if (packetStepTimer >= 0.75f)
        {
            packetStepTimer = 0f;
            int dir = relayMatrix[packetY, packetX];
            if (dir == 0 && packetX < 3) packetX++;
            else if (dir == 1 && packetY < 3) packetY++;
            else if (dir == 2 && packetX > 0) packetX--;
            else if (dir == 3 && packetY > 0) packetY--;

            PlayClick();
            UpdateRelayDisplay();

            // Destination reached?
            if (packetX == 3 && packetY == 3)
            {
                OnGameSuccess("ПАКЕТ ДАННЫХ УСПЕШНО ДОСТАВЛЕН В ОБХОД ШЛЮЗОВ СЕТИ!");
            }
        }
    }

    private void UpdateRelayDisplay()
    {
        if (dynamicDisplayText == null) return;

        string[] arrowSymbols = { "→", "↓", "←", "↑" };
        string gridStr = "";

        for (int r = 0; r < 4; r++)
        {
            gridStr += "    ";
            for (int c = 0; c < 4; c++)
            {
                bool isPacket = (r == packetY && c == packetX);
                bool isStart = (r == 0 && c == 0);
                bool isGoal = (r == 3 && c == 3);
                bool isFirewall = (r == 1 && c == 2) || (r == 2 && c == 1);

                if (isPacket)
                    gridStr += "<b><color=#FFFF00>[●]</color></b> ";
                else if (isStart)
                    gridStr += "<color=#00FFAA>[S]</color> ";
                else if (isGoal)
                    gridStr += "<b><color=#00FF88>[G]</color></b> ";
                else if (isFirewall)
                    gridStr += "<color=#FF3333>[X]</color> ";
                else
                    gridStr += $"<color=#72D572>[{arrowSymbols[relayMatrix[r, c]]}]</color> ";
            }
            gridStr += "\n";
        }

        dynamicDisplayText.text =
            $"<size=22><color=#66CCAA>МАТРИЦА ПАКЕТНОЙ МАРШРУТИЗАЦИИ (4x4):</color></size>\n\n" +
            $"{gridStr}\n" +
            $"  [S] — ВХОД  |  [G] — ПРИЕМНИК (КАРТРИДЖ)  |  [X] — БРАНДМАУЭР\n" +
            $"<color=#00AA77>───────────────────────────────────────────────────────────</color>\n" +
            $"<color=#FFFF66>[ПРОБЕЛ / КЛИК] — Повернуть текущее реле по часовой стрелке</color>";
    }

    // --- Mini-Game 7: Hamming Parity ---

    private void InitHammingMatrix()
    {
        parityBits = new int[4, 4]
        {
            { 1, 0, 1, 0 },
            { 0, 1, 1, 0 },
            { 1, 1, 0, 1 }, // Corrupted bit at [2, 1]
            { 0, 1, 0, 1 }
        };
        badRow = 2; badCol = 1;
    }

    private void UpdateHammingDisplay()
    {
        if (dynamicDisplayText == null) return;

        string grid = "       КОЛ:  0   1   2   3    ЧЕТНОСТЬ\n";
        for (int r = 0; r < 4; r++)
        {
            int rowSum = 0;
            grid += $"  СТР {r}:  ";
            for (int c = 0; c < 4; c++)
            {
                rowSum += parityBits[r, c];
                if (r == badRow && c == badCol)
                    grid += $"<b><color=#FFCC00>[{parityBits[r, c]}]</color></b> ";
                else
                    grid += $"[{parityBits[r, c]}] ";
            }
            bool rowEven = (rowSum % 2 == 0);
            grid += rowEven ? " <color=#00FF88>[OK]</color>\n" : " <color=#FF3333>[СБОЙ]</color>\n";
        }

        dynamicDisplayText.text =
            $"<size=22><color=#66CCAA>ФЕРРИТОВАЯ МАТРИЦА ОПЕРАТИВНОЙ ПАМЯТИ:</color></size>\n\n" +
            $"{grid}\n" +
            $"  Обнаружена ошибка четности на пересечении Стр {badRow} и Кол {badCol}!\n" +
            $"<color=#00AA77>───────────────────────────────────────────────────────────</color>\n" +
            $"<color=#FFFF66>[ПРОБЕЛ / ENTER] — Инвертировать битый бит и восстановить матрицу</color>";
    }

    // --- Mini-Game 8: Disk Sector Timing ---

    private void UpdateDiskSectorGame()
    {
        diskAngle = (diskAngle + 180f * Time.deltaTime) % 360f;
        UpdateDiskSectorDisplay();
    }

    private void UpdateDiskSectorDisplay()
    {
        if (dynamicDisplayText == null) return;

        float diff = Mathf.Abs(Mathf.DeltaAngle(diskAngle, targetSectorAngle));
        bool isAligned = diff < 28f;

        string alignIndicator = isAligned ?
            "<b><color=#00FFAA>[ ГОЛОВКА НАД СЕКТОРОМ! ЖМИТЕ ПРОБЕЛ! ]</color></b>" :
            "<color=#558866>[ ВРАЩЕНИЕ ШПИНДЕЛЯ... ОЖИДАНИЕ ДОРОЖКИ ]</color>";

        dynamicDisplayText.text =
            $"<size=22><color=#66CCAA>СКОРОСТЬ ВРАЩЕНИЯ ШПИНДЕЛЯ: 2400 ОБ/МИН</color></size>\n\n" +
            $"  УГОЛ ГОЛОВКИ: {Mathf.RoundToInt(diskAngle)}°   |   СЕКТОР ЦЕЛИ: {Mathf.RoundToInt(targetSectorAngle)}°\n\n" +
            $"  {alignIndicator}\n\n" +
            $"  ЗАХВАЧЕНО СЕКТОРОВ: <b><color=#00FF88>{sectorsCaptured} / {SECTORS_NEEDED}</color></b>\n" +
            $"<color=#00AA77>───────────────────────────────────────────────────────────</color>\n" +
            $"<color=#FFFF66>[ПРОБЕЛ] — Захват кластера в тайминг (Промах = нагрев диска!)</color>";
    }

    public void AttemptDiskSectorCapture()
    {
        float diff = Mathf.Abs(Mathf.DeltaAngle(diskAngle, targetSectorAngle));
        if (diff < 32f)
        {
            sectorsCaptured++;
            PlayClick();
            targetSectorAngle = (targetSectorAngle + 110f) % 360f;
            if (sectorsCaptured >= SECTORS_NEEDED)
            {
                OnGameSuccess("ВСЕ КЛАСТЕРЫ ДИСКА УСПЕШНО СЧИТАНЫ И СОБРАНЫ В АРХИВ!");
            }
        }
        else
        {
            PlayFail();
            if (DiskDriveBay.Instance != null)
            {
                // Penalty heat on miss
                MainframeBootQuest.Instance?.ShowNotification("<color=#FF5555>[ПРОМАХ!] Холостой оборот шпинделя (+4°C к диску)</color>");
            }
        }
    }

    // --- Mini-Game 9: Stack Overflow LIFO ---

    private void UpdateStackDisplay()
    {
        if (dynamicDisplayText == null) return;

        int currentSum = 0;
        string stackVisual = "";
        for (int i = stackRegisters.Count - 1; i >= 0; i--)
        {
            currentSum += stackRegisters[i];
            stackVisual += $"    │  [{stackRegisters[i]}]  │\n";
        }
        if (stackRegisters.Count == 0) stackVisual = "    │ (ПУСТО) │\n";
        stackVisual += "    └───┴───┘";

        string sumColor = currentSum == STACK_TARGET ? "#00FF88" : (currentSum > STACK_MAX_CAPACITY ? "#FF3333" : "#FFFF00");

        dynamicDisplayText.text =
            $"<size=22><color=#66CCAA>СТЕК РЕГИСТРОВ ПРОЦЕССОРА (LIFO):</color></size>\n\n" +
            $"{stackVisual}\n\n" +
            $"  ТЕКУЩАЯ СУММА: <b><color={sumColor}>{currentSum}</color></b>  |  ЦЕЛЬ: <b><color=#00FFAA>{STACK_TARGET}</color></b>  (МАКС: {STACK_MAX_CAPACITY})\n" +
            $"<color=#00AA77>───────────────────────────────────────────────────────────</color>\n" +
            $"<color=#FFFF66>[1] PUSH 2  |  [2] PUSH 4  |  [3] PUSH 8  |  [BACKSPACE] POP  |  [ENTER] Проверить</color>";
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
            $"<color=#FFFF66>[1/2/3] Выбор ротора  |  [ВВЕРХ / ВНИЗ] Проворот  |  [ENTER] Пуск  |  [H] Справка</color>";
    }

    // --- Mini-Game 11: Omega Master Protocol (.TOP) ---

    private void UpdateOmegaDisplay()
    {
        if (dynamicDisplayText == null) return;

        string[] ch1Options = { "0000 [СБРОС]", "1011 [ИСХОДНЫЙ]", "0101 [ИНВЕРСИЯ]", "1100 [СИНХРОНИЗИРОВАН]" };
        string[] ch3Options = { "0x0000 [ПУСТО]", "0x1A2F [СБОЙ ОЗУ]", "0x4F0A [СИНХРОНИЗИРОВАН]", "0xFFFF [ТРЕВОГА]" };
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
        string cur3 = (omegaSelectedChannel == 2) ? "<b><color=#FFFF00>► 3. СТОРОЖЕВОЙ АДРЕС ОЗУ:</color></b>" : "  3. СТОРОЖЕВОЙ АДРЕС ОЗУ:";
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
            "<color=#88CCAA>ПОДСКАЗКА: Ключи из расшифрованных файлов: 1) 1100  2) +7  3) 0x4F0A  4) IV-II-VII</color>";
    }

    // --- In-Game Help Overlay System ---

    public void ToggleHelp(bool open)
    {
        isHelpOpen = open;
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
                    "1. <b>Состояние q0 (Поиск младшего разряда):</b>\n" +
                    "   Каретка сканирует перфоленту вправо до пробела '_', затем делает шаг влево на младший значащий бит.\n" +
                    "2. <b>Состояние q1 (Сложение в столбик с флагом переноса):</b>\n" +
                    "   • Если под кареткой <color=#FFFF33>'1'</color>: <b>1 + 1 = 10 (двоичное)</b>.\n" +
                    "     Требуется: Запись <color=#00FFAA>[0]</color>, Сдвиг <color=#00FFAA>[← ВЛЕВО]</color>, Переход в <color=#00FFAA>[q1]</color> (перенос дальше).\n" +
                    "   • Если под кареткой <color=#00FFAA>'0'</color>: <b>0 + 1 = 1 (двоичное)</b>.\n" +
                    "     Требуется: Запись <color=#00FFAA>[1]</color>, Сдвиг <color=#00FFAA>[• СТОП ]</color>, Переход в <color=#00FFAA>[HALT]</color> (перенос исчерпан!).\n" +
                    "   • Если под кареткой <color=#66CCFF>'_'</color> (выход за число): Запись [1], СТОП, HALT.\n\n" +
                    "<color=#FFFF88><b>УПРАВЛЕНИЕ:</b></color>\n" +
                    "• <b>[СТРЕЛКИ ВВЕРХ/ВНИЗ]</b> — выбор слота таблицы микрокода q1.\n" +
                    "• <b>[СТРЕЛКИ ВЛЕВО/ВПРАВО]</b> — изменение значения (0/1/_, ←/→/СТОП, q0/q1/HALT).\n" +
                    "• <b>[ПРОБЕЛ]</b> — пошаговый тест (STEP) для пошаговой проверки логики каретки.\n" +
                    "• <b>[ENTER]</b> — непрерывный пуск до останова (HALT).\n" +
                    "• <b>[R]</b> — сброс перфоленты и каретки в исходное положение.";

            case GameType.FrequencyAnalysis:
                return
                    "<b><color=#00FFAA>МОДУЛЬ 02: ЧАСТОТНЫЙ СПЕКТРОАНАЛИЗАТОР (ШИФР ПОДСТАНОВКИ)</color></b>\n" +
                    "<color=#66CCAA>──────────────────────────────────────────────────────────────────────────</color>\n" +
                    "<color=#FFFF88><b>ЗАДАЧА:</b></color> Вскрыть шифр сдвига (шифр Цезаря) секретной директивы управления.\n\n" +
                    "<color=#FFFF88><b>ПРИНЦИП РАБОТЫ:</b></color>\n" +
                    "В русском языке гласные буквы встречаются с известной эталонной частотой (О, Е, А).\n" +
                    "• Верхняя гистограмма (зеленая) отображает частотный спектр перехваченного документа.\n" +
                    "• Нижняя гистограмма (желтая) отображает эталонный акустический спектр естественного языка.\n" +
                    "• <b>[СТРЕЛКИ ВЛЕВО/ВПРАВО]</b> — циклический сдвиг фазы алфавита (подбор ключа сдвига).\n" +
                    "• Совместите главные резонансные пики обеих кривых и нажмите <b>[ENTER]</b>.";

            case GameType.DatabaseFilter:
                return
                    "<b><color=#00FFAA>МОДУЛЬ 03: РЕЛЯЦИОННЫЙ ФИЛЬТР КАРТОТЕКИ (БАЗА СЕКТОРА 7)</color></b>\n" +
                    "<color=#66CCAA>──────────────────────────────────────────────────────────────────────────</color>\n" +
                    "<color=#FFFF88><b>ЗАДАЧА:</b></color> Локализовать карточку дежурного офицера и считать пароль администратора.\n\n" +
                    "<color=#FFFF88><b>ПРИНЦИП РАБОТЫ:</b></color>\n" +
                    "Согласно регламенту безопасности, мастер-пароль хранится у ночного криптографа:\n" +
                    "• <b>[1]</b> — Выбор отдела: переключите на <b>[КРИПТО]</b>.\n" +
                    "• <b>[2]</b> — Уровень допуска: переключите на <b>[ОСОБЫЙ]</b>.\n" +
                    "• <b>[3]</b> — Смена дежурства: переключите на <b>[НОЧЬ]</b>.\n" +
                    "• Когда фильтр сузит выборку до 1 сотрудника (Смирнов В.П.), нажмите <b>[ENTER]</b>.";

            case GameType.HexMemoryPatch:
                return
                    "<b><color=#00FFAA>МОДУЛЬ 04: HEX-ПАТЧЕР ОПЕРАТИВНОЙ ПАМЯТИ (СТОРОЖЕВОЙ БУФЕР)</color></b>\n" +
                    "<color=#66CCAA>──────────────────────────────────────────────────────────────────────────</color>\n" +
                    "<color=#FFFF88><b>ЗАДАЧА:</b></color> Заморозить сторожевой вектор тревоги по адресу 0x4F0A.\n\n" +
                    "<color=#FFFF88><b>ПРИНЦИП РАБОТЫ:</b></color>\n" +
                    "Для нейтрализации процедуры тревоги необходимо переписать ячейки инструкциями NOP:\n" +
                    "• Нажимайте клавишу <b>[F]</b>, <b>[ПРОБЕЛ]</b> или <b>[ENTER]</b> для последовательной инъекции 4 байт NOP.\n" +
                    "• После заполнения сторожевой буфер отключится без срабатывания сигнализации.";

            case GameType.Oscilloscope:
                return
                    "<b><color=#00FFAA>МОДУЛЬ 05: ЭЛЕКТРОННО-ЛУЧЕВОЙ ОСЦИЛЛОГРАФ (ФИГУРЫ ЛИССАЖУ)</color></b>\n" +
                    "<color=#66CCAA>──────────────────────────────────────────────────────────────────────────</color>\n" +
                    "<color=#FFFF88><b>ЗАДАЧА:</b></color> Свести интерференционную фигуру радиосигнала в резонансный круг.\n\n" +
                    "<color=#FFFF88><b>ПРИНЦИП РАБОТЫ:</b></color>\n" +
                    "• <b>[A] / [D]</b> — плавная регулировка частоты несущей генератора.\n" +
                    "• <b>[W] / [S]</b> — регулировка фазового сдвига сигнала.\n" +
                    "• Сведите две синусоиды в стабильный симметричный круг и нажмите <b>[ENTER]</b>.";

            case GameType.RelayRouting:
                return
                    "<b><color=#00FFAA>МОДУЛЬ 06: РЕЛЕЙНЫЙ МАРШРУТИЗАТОР (ОБХОД ШЛЮЗОВ)</color></b>\n" +
                    "<color=#66CCAA>──────────────────────────────────────────────────────────────────────────</color>\n" +
                    "<color=#FFFF88><b>ЗАДАЧА:</b></color> Проложить непрерывный маршрут для пакета данных от узла (0,0) к (3,3).\n\n" +
                    "<color=#FFFF88><b>ПРИНЦИП РАБОТЫ:</b></color>\n" +
                    "• <b>[СТРЕЛКИ]</b> — выбор ячейки коммутационной панели 4х4.\n" +
                    "• <b>[ПРОБЕЛ]</b> или <b>[ENTER]</b> — поворот стрелки электромагнитного реле на 90°.\n" +
                    "• Настройте стрелки цепочкой от левого верхнего угла до правого нижнего приемника.";

            case GameType.HammingParity:
                return
                    "<b><color=#00FFAA>МОДУЛЬ 07: МАТРИЦА ХЭММИНГА (КОРРЕКЦИЯ ОШИБКИ ПАМЯТИ)</color></b>\n" +
                    "<color=#66CCAA>──────────────────────────────────────────────────────────────────────────</color>\n" +
                    "<color=#FFFF88><b>ЗАДАЧА:</b></color> Найти и инвертировать сбойный бит в ферритовой матрице.\n\n" +
                    "<color=#FFFF88><b>ПРИНЦИП РАБОТЫ:</b></color>\n" +
                    "По алгоритму Хэмминга четность бит в каждой строке и столбце должна быть четной (0):\n" +
                    "• Строка с нечетной суммой подсвечена маркером [НЕЧЕТ!].\n" +
                    "• Столбец с нечетной суммой также имеет сбойный бит четности.\n" +
                    "• <b>[СТРЕЛКИ]</b> — курсор, <b>[ПРОБЕЛ] / [ENTER]</b> — инвертировать бит на пересечении.";

            case GameType.DiskSectorTiming:
                return
                    "<b><color=#00FFAA>МОДУЛЬ 08: СЕКТОРНЫЙ СИНХРОНИЗАТОР НАКОПИТЕЛЯ ЕС-5061</color></b>\n" +
                    "<color=#66CCAA>──────────────────────────────────────────────────────────────────────────</color>\n" +
                    "<color=#FFFF88><b>ЗАДАЧА:</b></color> Захватить 3 контрольных сектора на вращающемся магнитном диске.\n\n" +
                    "<color=#FFFF88><b>ПРИНЦИП РАБОТЫ:</b></color>\n" +
                    "• Следите за вращением сектора диска по круговой шкале.\n" +
                    "• Нажимайте <b>[ПРОБЕЛ]</b> ровно в момент, когда метка сектора проходит через створ головки!\n" +
                    "• Не допускайте промахов: захватите 3 сектора подряд для считывания дорожки.";

            case GameType.StackOverflow:
                return
                    "<b><color=#00FFAA>МОДУЛЬ 09: РЕГИСТРОВЫЙ СТЕК LIFO (КОНТРОЛЬ ПЕРЕПОЛНЕНИЯ)</color></b>\n" +
                    "<color=#66CCAA>──────────────────────────────────────────────────────────────────────────</color>\n" +
                    "<color=#FFFF88><b>ЗАДАЧА:</b></color> Набрать сумму ровно 16 в стеке регистров без превышения лимита (22).\n\n" +
                    "<color=#FFFF88><b>ПРИНЦИП РАБОТЫ:</b></color>\n" +
                    "• <b>[1]</b> — PUSH 2 (поместить 2 в стек).\n" +
                    "• <b>[2]</b> — PUSH 4 (поместить 4 в стек).\n" +
                    "• <b>[3]</b> — PUSH 8 (поместить 8 в стек).\n" +
                    "• <b>[BACKSPACE]</b> — POP (извлечь верхний элемент).\n" +
                    "• При точной сумме 16 нажмите <b>[ENTER]</b>.";

            case GameType.RotorCipher:
                return
                    "<b><color=#00FFAA>МОДУЛЬ 10: ШИФРОВАЛЬНАЯ МАШИНА «ФИАЛКА-М125»</color></b>\n" +
                    "<color=#66CCAA>──────────────────────────────────────────────────────────────────────────</color>\n" +
                    "<color=#FFFF88><b>ЗАДАЧА:</b></color> Выставить суточный ключ на роторах шифратора.\n\n" +
                    "<color=#FFFF88><b>ПРИНЦИП РАБОТЫ:</b></color>\n" +
                    "• <b>[1], [2], [3]</b> — выбор ротора (I, II или III).\n" +
                    "• <b>[СТРЕЛКИ ВВЕРХ/ВНИЗ]</b> — поворот выбранного роторного барабана.\n" +
                    "• Из служебного блокнота дежурного: Ключ дня <b>[ IV - II - VII ]</b> (4 - 2 - 7).\n" +
                    "• После установки правильной комбинации нажмите <b>[ENTER]</b>.";

            case GameType.OmegaMasterProtocol:
                return
                    "<b><color=#00FFAA>ФИНАЛЬНЫЙ МОДУЛЬ: МАСТЕР-ПРОТОКОЛ «ОМЕГА»</color></b>\n" +
                    "<color=#66CCAA>──────────────────────────────────────────────────────────────────────────</color>\n" +
                    "<color=#FFFF88><b>ЗАДАЧА:</b></color> Снять генеральный криптозамок главного архива операции.\n\n" +
                    "<color=#FFFF88><b>ПРИНЦИП РАБОТЫ:</b></color>\n" +
                    "Для дешифрации архива «ОМЕГА» требуется объединить 4 ключа, добытых при взломе файлов архива:\n" +
                    "1. <b>Ядро Тьюринга:</b> Контрольный результат инкремента <b>[ 1 1 0 0 ]</b>.\n" +
                    "2. <b>Частотный сдвиг Цезаря:</b> Шаг совмещения гласных <b>[ Δ = +7 ]</b>.\n" +
                    "3. <b>Сторожевой адрес ОЗУ:</b> Адрес нейтрализации тревоги <b>[ 0x4F0A ]</b>.\n" +
                    "4. <b>Роторы «Фиалка»:</b> Код дня из шифроблокнота <b>[ IV - II - VII ]</b>.\n\n" +
                    "<color=#FFFF88><b>УПРАВЛЕНИЕ:</b></color>\n" +
                    "• <b>[1..4] / [ВВЕРХ/ВНИЗ]</b> — переключение между 4 криптоканалами.\n" +
                    "• <b>[СТРЕЛКИ ВЛЕВО/ВПРАВО] или [ПРОБЕЛ]</b> — калибровка значения канала.\n" +
                    "• После того, как все 4 канала перейдут в статус [● ЗАХВАЧЕН], нажмите <b>[ENTER]</b>.";

            default:
                return "Служебная инструкция отсутствует.";
        }
    }

    // --- Restart To Starting Point ---

    public void RestartCurrentGameToStartingPoint()
    {
        if (!isGameActive) return;

        PlayClick();
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

            case GameType.HexMemoryPatch:
                hexEntered = 0;
                UpdateHexDisplay();
                break;

            case GameType.Oscilloscope:
                oscFrequency = 1.0f;
                oscPhase = 30f;
                UpdateOscilloscopeDisplay();
                break;

            case GameType.RelayRouting:
                InitRelayMatrix();
                packetX = 0;
                packetY = 0;
                packetStepTimer = 0f;
                UpdateRelayDisplay();
                break;

            case GameType.HammingParity:
                InitHammingMatrix();
                UpdateHammingDisplay();
                break;

            case GameType.DiskSectorTiming:
                diskAngle = 0f;
                sectorsCaptured = 0;
                targetSectorAngle = 180f;
                UpdateDiskSectorDisplay();
                break;

            case GameType.StackOverflow:
                stackRegisters.Clear();
                UpdateStackDisplay();
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
        MainframeBootQuest.Instance?.ShowNotification("<color=#00FFAA>[ПЕРЕЗАПУСК]</color> Задача возвращена в стартовую точку.");
    }

    // --- Interaction Input Router ---

    private void HandleKeyboardInput()
    {
        if (isHelpOpen)
        {
            if (IsKey(KeyCode.H) || IsKey(KeyCode.Escape) || IsKey(KeyCode.Return) || IsKey(KeyCode.Space))
            {
                ToggleHelp(false);
            }
            return;
        }

        if (IsKey(KeyCode.H))
        {
            ToggleHelp(true);
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
                else if (IsKey(KeyCode.R))
                {
                    ResetTuringTape();
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
                    if (freqShift == freqTargetShift)
                        OnGameSuccess("ШИФР ПОДСТАНОВКИ ВЗЛОМАН! ТЕКСТ ДИРЕКТИВЫ ЧИТАЕМ.");
                    else
                        OnGameFailed("НЕВЕРНЫЙ СДВИГ ФАЗЫ! ТЕКСТ ОСТАЕТСЯ ЗАШИФРОВАННЫМ.");
                }
                break;

            case GameType.DatabaseFilter:
                if (IsKey(KeyCode.Alpha1)) { dbDeptIndex = (dbDeptIndex + 1) % 4; PlayClick(); UpdateDatabaseDisplay(); }
                else if (IsKey(KeyCode.Alpha2)) { dbClearance = (dbClearance + 1) % 3; PlayClick(); UpdateDatabaseDisplay(); }
                else if (IsKey(KeyCode.Alpha3)) { dbShiftIndex = (dbShiftIndex + 1) % 3; PlayClick(); UpdateDatabaseDisplay(); }
                else if (IsKey(KeyCode.Return))
                {
                    if (dbDeptIndex == 1 && dbClearance == 2 && dbShiftIndex == 2)
                        OnGameSuccess("ПАРОЛЬ АДМИНИСТРАТОРА НАЙДЕН: 8491!");
                    else
                        OnGameFailed("ВЫБОРКА СЛИШКОМ ШИРОКАЯ! УТОЧНИТЕ ВСЕ 3 КРИТЕРИЯ.");
                }
                break;

            case GameType.HexMemoryPatch:
                if (IsKey(KeyCode.Return) || IsKey(KeyCode.Space) || IsKey(KeyCode.F))
                {
                    hexEntered = Mathf.Clamp(hexEntered + 1, 0, hexRequired);
                    PlayClick();
                    UpdateHexDisplay();
                    if (hexEntered == hexRequired)
                    {
                        OnGameSuccess("СТОРОЖЕВОЙ БУФЕР НЕЙТРАЛИЗОВАН БАЙТАМИ NOP!");
                    }
                }
                break;

            case GameType.Oscilloscope:
                if (IsKey(KeyCode.A)) { oscFrequency = Mathf.Max(0.5f, oscFrequency - 0.2f); PlayClick(); }
                if (IsKey(KeyCode.D)) { oscFrequency = Mathf.Min(4.0f, oscFrequency + 0.2f); PlayClick(); }
                if (IsKey(KeyCode.W)) { oscPhase = (oscPhase + 15f) % 360f; PlayClick(); }
                if (IsKey(KeyCode.S)) { oscPhase = (oscPhase - 15f + 360f) % 360f; PlayClick(); }
                if (IsKey(KeyCode.Return))
                {
                    float diff = Mathf.Abs(oscFrequency - TARGET_FREQ) + Mathf.Abs(oscPhase - TARGET_PHASE);
                    if (diff < 20f)
                        OnGameSuccess("РЕЗОНАНС ДОСТИГНУТ! РАДИОКАНАЛ ПЕРЕХВАЧЕН.");
                    else
                        OnGameFailed("ЧАСТОТА РАССИНХРОНИЗИРОВАНА. СИГНАЛ УТОНУЛ В ШУМАХ.");
                }
                break;

            case GameType.RelayRouting:
                if (IsKey(KeyCode.Return) || IsKey(KeyCode.Space))
                {
                    relayMatrix[packetY, packetX] = (relayMatrix[packetY, packetX] + 1) % 4;
                    PlayRelay();
                    UpdateRelayDisplay();
                }
                break;

            case GameType.HammingParity:
                if (IsKey(KeyCode.Return) || IsKey(KeyCode.Space))
                {
                    parityBits[badRow, badCol] = 1 - parityBits[badRow, badCol];
                    PlayRelay();
                    UpdateHammingDisplay();
                    OnGameSuccess("БИТОВЫЙ СБОЙ ПАМЯТИ УСТРАНЕН! КОД ХЭММИНГА ВОССТАНОВЛЕН.");
                }
                break;

            case GameType.DiskSectorTiming:
                if (IsKey(KeyCode.Space) || IsKey(KeyCode.Return))
                {
                    AttemptDiskSectorCapture();
                }
                break;

            case GameType.StackOverflow:
                if (IsKey(KeyCode.Alpha1)) { stackRegisters.Add(2); PlayClick(); UpdateStackDisplay(); }
                else if (IsKey(KeyCode.Alpha2)) { stackRegisters.Add(4); PlayClick(); UpdateStackDisplay(); }
                else if (IsKey(KeyCode.Alpha3)) { stackRegisters.Add(8); PlayClick(); UpdateStackDisplay(); }
                else if (IsKey(KeyCode.Backspace))
                {
                    if (stackRegisters.Count > 0) stackRegisters.RemoveAt(stackRegisters.Count - 1);
                    PlayClick();
                    UpdateStackDisplay();
                }
                else if (IsKey(KeyCode.Return))
                {
                    int sum = 0;
                    foreach (var v in stackRegisters) sum += v;
                    if (sum == STACK_TARGET)
                        OnGameSuccess("РЕГИСТР ПЕРЕПОЛНЕН ДО УРОВНЯ АДМИНИСТРАТОРА (16)!");
                    else
                        OnGameFailed($"СУММА {sum} НЕ РАВНА 16! ДОСТУП ЗАПРЕЩЕН.");
                }
                break;

            case GameType.RotorCipher:
                if (IsKey(KeyCode.Alpha1)) { rotor1 = (rotor1 + 1) % 10; PlayRelay(); UpdateRotorDisplay(); }
                else if (IsKey(KeyCode.Alpha2)) { rotor2 = (rotor2 + 1) % 10; PlayRelay(); UpdateRotorDisplay(); }
                else if (IsKey(KeyCode.Alpha3)) { rotor3 = (rotor3 + 1) % 10; PlayRelay(); UpdateRotorDisplay(); }
                else if (IsKey(KeyCode.Return))
                {
                    if (rotor1 == targetR1 && rotor2 == targetR2 && rotor3 == targetR3)
                        OnGameSuccess("МАШИНА «ФИАЛКА» РАСШИФРОВАЛА ВЕКТОРНЫЙ ПЛАН БУНКЕРА!");
                    else
                        OnGameFailed("НЕВЕРНАЯ КОМБИНАЦИЯ РОТОРОВ! КЛЮЧ НЕ ПОДОШЕЛ.");
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

    private void OnMainActionButtonPressed()
    {
        switch (currentGame)
        {
            case GameType.TuringTape: RunTuringContinuous(); break;
            case GameType.FrequencyAnalysis:
                if (freqShift == freqTargetShift) OnGameSuccess("ШИФР ПОДСТАНОВКИ ВЗЛОМАН!");
                else OnGameFailed("НЕВЕРНЫЙ СДВИГ ФАЗЫ!");
                break;
            case GameType.DatabaseFilter:
                if (dbDeptIndex == 1 && dbClearance == 2 && dbShiftIndex == 2) OnGameSuccess("ПАРОЛЬ АДМИНИСТРАТОРА НАЙДЕН!");
                else OnGameFailed("УТОЧНИТЕ ВСЕ 3 КРИТЕРИЯ ВЫБОРКИ!");
                break;
            case GameType.HexMemoryPatch:
                hexEntered = Mathf.Clamp(hexEntered + 1, 0, hexRequired);
                PlayClick();
                UpdateHexDisplay();
                if (hexEntered == hexRequired) OnGameSuccess("СТОРОЖЕВОЙ БУФЕР НЕЙТРАЛИЗОВАН!");
                break;
            case GameType.Oscilloscope:
                float diff = Mathf.Abs(oscFrequency - TARGET_FREQ) + Mathf.Abs(oscPhase - TARGET_PHASE);
                if (diff < 20f) OnGameSuccess("РЕЗОНАНС ДОСТИГНУТ! РАДИОКАНАЛ ПЕРЕХВАЧЕН.");
                else OnGameFailed("СИГНАЛ УТОНУЛ В ШУМАХ.");
                break;
            case GameType.RelayRouting:
                relayMatrix[packetY, packetX] = (relayMatrix[packetY, packetX] + 1) % 4;
                PlayRelay();
                UpdateRelayDisplay();
                break;
            case GameType.HammingParity:
                parityBits[badRow, badCol] = 1 - parityBits[badRow, badCol];
                PlayRelay();
                UpdateHammingDisplay();
                OnGameSuccess("БИТОВЫЙ СБОЙ ПАМЯТИ УСТРАНЕН!");
                break;
            case GameType.DiskSectorTiming: AttemptDiskSectorCapture(); break;
            case GameType.StackOverflow:
                int sum = 0;
                foreach (var v in stackRegisters) sum += v;
                if (sum == STACK_TARGET) OnGameSuccess("РЕГИСТР ПЕРЕПОЛНЕН!");
                else OnGameFailed($"СУММА {sum} НЕ РАВНА 16!");
                break;
            case GameType.RotorCipher:
                if (rotor1 == targetR1 && rotor2 == targetR2 && rotor3 == targetR3)
                    OnGameSuccess("МАШИНА «ФИАЛКА» РАСШИФРОВАЛА ПЛАН!");
                else
                    OnGameFailed("НЕВЕРНАЯ КОМБИНАЦИЯ РОТОРОВ!");
                break;

            case GameType.OmegaMasterProtocol:
                bool ch1Ok = (omegaCh1Index == 3);
                bool ch2Ok = (omegaCh2Shift == 7);
                bool ch3Ok = (omegaCh3Index == 2);
                bool ch4Ok = (omegaCh4Index == 1);
                if (ch1Ok && ch2Ok && ch3Ok && ch4Ok)
                {
                    OnGameSuccess("МАСТЕР-АРХИВ «ОМЕГА» ПОЛНОСТЬЮ ДЕШИФРОВАН! ИГРА ПРОЙДЕНА!");
                }
                else
                {
                    OnGameFailed("НЕ ВСЕ КРИПТОКАНАЛЫ СИНХРОНИЗИРОВАНЫ! ПРОВЕРЬТЕ КЛЮЧИ.");
                }
                break;
        }
    }

    private void OnGameSuccess(string message)
    {
        isGameActive = false;
        PlaySuccess();
        if (statusFeedbackText != null)
        {
            statusFeedbackText.text = $"<color=#00FF88><b>[УСПЕХ] {message}</b></color>";
        }
        MainframeBootQuest.Instance?.ShowNotification($"<color=#00FF88>[ДЕШИФРОВКА УСПЕШНА]</color> {message}");

        StartCoroutine(CloseAfterDelay(true, 1.8f));
    }

    private void OnGameFailed(string message)
    {
        PlayFail();
        if (statusFeedbackText != null)
        {
            statusFeedbackText.text = $"<color=#FF3333><b>[ОШИБКА] {message}</b></color>";
        }
        MainframeBootQuest.Instance?.ShowNotification($"<color=#FF4444>[ОТКАЗ ДЕШИФРАТОРА]</color> {message}");
    }

    public void CancelMiniGame()
    {
        isGameActive = false;
        isHelpOpen = false;
        if (helpOverlayPanel != null) helpOverlayPanel.SetActive(false);
        if (turingRunCoroutine != null) StopCoroutine(turingRunCoroutine);
        turingIsRunning = false;
        PlayClick();
        if (miniGameRootPanel != null) miniGameRootPanel.SetActive(false);
        onGameFinishedCallback?.Invoke(false);
    }

    private IEnumerator CloseAfterDelay(bool success, float delay)
    {
        yield return new WaitForSeconds(delay);
        isHelpOpen = false;
        if (helpOverlayPanel != null) helpOverlayPanel.SetActive(false);
        if (miniGameRootPanel != null) miniGameRootPanel.SetActive(false);
        onGameFinishedCallback?.Invoke(success);
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
            case KeyCode.R: return kb.rKey.wasPressedThisFrame;
            case KeyCode.Alpha1: return kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame;
            case KeyCode.Alpha2: return kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame;
            case KeyCode.Alpha3: return kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame;
            case KeyCode.Alpha4: return kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame;
            default: return false;
        }
#else
        return Input.GetKeyDown(code);
#endif
    }

    // --- Audio Synthesis ---

    private void GenerateAudioClips()
    {
        int sr = 44100;

        // 1. Click
        int n1 = (int)(sr * 0.025f);
        float[] d1 = new float[n1];
        for (int i = 0; i < n1; i++) d1[i] = Mathf.Sin(2f * Mathf.PI * 1600f * (float)i / sr) * (1f - (float)i / n1);
        clickClip = AudioClip.Create("MG_Click", n1, 1, sr, false);
        clickClip.SetData(d1, 0);

        // 2. Success chime
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

        // 3. Fail buzz
        int n3 = (int)(sr * 0.25f);
        float[] d3 = new float[n3];
        for (int i = 0; i < n3; i++) d3[i] = Mathf.Sin(2f * Mathf.PI * 140f * (float)i / sr) * (1f - (float)i / n3) * 0.6f;
        failClip = AudioClip.Create("MG_Fail", n3, 1, sr, false);
        failClip.SetData(d3, 0);

        // 4. Relay clack
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
