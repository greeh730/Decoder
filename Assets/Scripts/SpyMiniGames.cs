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
        RotorCipher         // .ROT
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
    private float gameTimer = 45f;
    private bool isGameActive = false;

    // --- Sub-game data states ---

    // 1. Turing
    private int[] turingTape = new int[8];
    private int turingHeadIndex = 0;
    private int turingSelectedRule = 1; // 1: ? -> 1, R; 2: ? -> 0, R

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

        if (miniGameRootPanel != null)
        {
            miniGameRootPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (!isGameActive) return;

        // Countdown timer
        gameTimer -= Time.deltaTime;
        if (timerCountdownText != null)
        {
            int sec = Mathf.Max(0, Mathf.CeilToInt(gameTimer));
            timerCountdownText.text = $"ТАЙМЕР ДО СБРОСА: <color={(sec < 10 ? "#FF3333" : "#00FFAA")}>{sec} СЕК</color>";
        }

        if (gameTimer <= 0f)
        {
            OnGameFailed("ВРЕМЯ ИСТЕКЛО! СИСТЕМА ЗАБЛОКИРОВАНА ТАЙМАУТОМ.");
            return;
        }

        // Active game update loops
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
        gameTimer = 45f;

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
                gameTitleText.text = "╔════ МАШИНА ТЬЮРИНГА: ПЕРФОЛЕНТА ЯДРА ════╗";
                gameDescriptionText.text = "Заполните пропуски [?] на перфоленте. [Стрелки Влево/Вправо] — выбор правила. [Enter/Пробел] — запустить каретку.";
                turingTape = new int[] { 1, 0, -1, 1, 0, -1, 1, 1 };
                turingHeadIndex = 0;
                turingSelectedRule = 1;
                btnActionText.text = "[ПУСК КАРЕТКИ]";
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

        string tapeStr = "";
        for (int i = 0; i < turingTape.Length; i++)
        {
            string val = turingTape[i] == -1 ? "?" : turingTape[i].ToString();
            if (i == turingHeadIndex)
                tapeStr += $"<b><color=#00FFAA>[ ▼{val}▼ ]</color></b> ";
            else
                tapeStr += $"[ {val} ] ";
        }

        string ruleStr = turingSelectedRule == 1 ? "<b><color=#00FFAA>► ПРАВИЛО 1: ЕСЛИ ?, ЗАПИСАТЬ [1] И ВПРАВО ◄</color></b>\n  ПРАВИЛО 2: ЕСЛИ ?, ЗАПИСАТЬ [0] И ВПРАВО" :
                                                   "  ПРАВИЛО 1: ЕСЛИ ?, ЗАПИСАТЬ [1] И ВПРАВО\n<b><color=#00FFAA>► ПРАВИЛО 2: ЕСЛИ ?, ЗАПИСАТЬ [0] И ВПРАВО ◄</color></b>";

        dynamicDisplayText.text =
            $"<size=22><color=#66CCAA>ПЕРФОЛЕНТА ЯДРА «ВЕКТОР-2»:</color></size>\n\n" +
            $"    {tapeStr}\n\n" +
            $"<color=#00AA77>───────────────────────────────────────────────────────────</color>\n" +
            $"<size=18>ВЫБОР МИКРОКОДОВОГО ПРАВИЛА УПРАВЛЕНИЯ ГОЛОВКОЙ:</size>\n\n" +
            $"{ruleStr}\n\n" +
            $"<color=#FFFF66>[ПРОБЕЛ / ENTER] — Запустить цикл считывания перфоленты</color>";
    }

    public void StepTuringMachine()
    {
        PlayRelay();
        StartCoroutine(RunTuringRoutine());
    }

    private IEnumerator RunTuringRoutine()
    {
        for (int i = 0; i < turingTape.Length; i++)
        {
            turingHeadIndex = i;
            PlayClick();
            if (turingTape[i] == -1)
            {
                turingTape[i] = (turingSelectedRule == 1) ? 1 : 0;
            }
            UpdateTuringDisplay();
            yield return new WaitForSeconds(0.18f);
        }

        // Check success: Correct sequence has 1s in filled slots
        if (turingTape[2] == 1 && turingTape[5] == 1)
        {
            OnGameSuccess("КОНТРОЛЬНАЯ СУММА СОВПАЛА! ЯДРО «ДЕКОДЕР_V2.BIN» РАСШИФРОВАНО!");
        }
        else
        {
            OnGameFailed("ОШИБКА ДВОИЧНОЙ СУММЫ! ПРАВИЛО НЕ СООТВЕТСТВУЕТ СТАНДАРТУ ГОСТ.");
        }
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
            $"<color=#FFFF66>[1/2/3] Выбор ротора  |  [ВВЕРХ / ВНИЗ] Проворот  |  [ENTER] Пуск</color>";
    }

    // --- Interaction Input Router ---

    private void HandleKeyboardInput()
    {
        if (IsKey(KeyCode.Escape))
        {
            CancelMiniGame();
            return;
        }

        switch (currentGame)
        {
            case GameType.TuringTape:
                if (IsKey(KeyCode.LeftArrow) || IsKey(KeyCode.RightArrow))
                {
                    turingSelectedRule = (turingSelectedRule == 1) ? 2 : 1;
                    PlayClick();
                    UpdateTuringDisplay();
                }
                else if (IsKey(KeyCode.Return) || IsKey(KeyCode.Space))
                {
                    StepTuringMachine();
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
        }
    }

    private void OnMainActionButtonPressed()
    {
        switch (currentGame)
        {
            case GameType.TuringTape: StepTuringMachine(); break;
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
        PlayClick();
        if (miniGameRootPanel != null) miniGameRootPanel.SetActive(false);
        onGameFinishedCallback?.Invoke(false);
    }

    private IEnumerator CloseAfterDelay(bool success, float delay)
    {
        yield return new WaitForSeconds(delay);
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
            case KeyCode.Alpha1: return kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame;
            case KeyCode.Alpha2: return kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame;
            case KeyCode.Alpha3: return kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame;
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
