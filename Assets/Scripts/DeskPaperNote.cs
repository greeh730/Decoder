using System;
using UnityEngine;

/// <summary>
/// Physical desk paper note prop located on the operator desk beside the terminal.
/// Provides authentic in-world intelligence, decryption keys, and cipher hints
/// corresponding to the active puzzle or overall mission objective.
/// </summary>
public class DeskPaperNote : MonoBehaviour
{
    private static DeskPaperNote _instance;
    public static DeskPaperNote Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = UnityEngine.Object.FindAnyObjectByType<DeskPaperNote>(UnityEngine.FindObjectsInactive.Include);
            }
            return _instance;
        }
        private set { _instance = value; }
    }

    [SerializeField] private WorldInteractable interactable;
    [SerializeField] private AudioSource paperAudio;

    private AudioClip paperRustleClip;

    private void Awake()
    {
        Instance = this;
        if (interactable == null) interactable = GetComponent<WorldInteractable>();
        UpdatePrompt();
        if (interactable != null)
        {
            interactable.MaxDistance = 2.8f;
            interactable.onInteract.RemoveAllListeners();
            interactable.onInteract.AddListener(ReadNote);
        }
        GeneratePaperAudio();
    }

    private void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += UpdatePrompt;
    }

    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= UpdatePrompt;
    }

    private void UpdatePrompt()
    {
        if (interactable != null)
        {
            interactable.PromptText = LocalizationManager.T("[E] Прочесть записку на столе", "[E] Read note on desk");
        }
    }

    public void ReadNote()
    {
        PlayRustle();
        SpyMiniGames.GameType currentType = SpyMiniGames.GameType.None;
        if (SpyMiniGames.Instance != null && SpyMiniGames.Instance.IsMiniGameActive)
        {
            currentType = SpyMiniGames.Instance.ActiveGameType;
        }

        string noteText = GetNoteText(currentType);

        string header = LocalizationManager.T("[СЛУЖЕБНАЯ ЗАПИСКА НА СТОЛЕ]:", "[OFFICIAL DESK NOTE]:");
        Debug.Log($"<color=#FFFF55><b>{header}</b></color>\n{noteText}");

        if (MainframeBootQuest.Instance != null)
        {
            string shortHint = GetShortHint(currentType);
            string bannerTag = LocalizationManager.T("[ЗАПИСКА НА СТОЛЕ]", "[DESK NOTE]");
            MainframeBootQuest.Instance.ShowNotification($"<color=#FFFF00>{bannerTag}</color> {shortHint}");
        }
    }

    public static string GetShortHint(SpyMiniGames.GameType type)
    {
        switch (type)
        {
            case SpyMiniGames.GameType.PasswordSimple:
                return LocalizationManager.T("Пин-код архива спецсвязи: [ 4 8 2 1 ]", "Comms vault archive PIN: [ 4 8 2 1 ]");
            case SpyMiniGames.GameType.CaesarArchive:
                return LocalizationManager.T("Сдвиг Цезаря на 3 буквы назад (D->A). Файл: DWWDFN (ATTACK)", "Caesar shift -3 letters (D->A). Target: DWWDFN (ATTACK)");
            case SpyMiniGames.GameType.PasswordFormula:
                return LocalizationManager.T("Формула архива карт: PIN = (17 * 23) - 42 = [ 0 3 4 9 ]", "Map archive formula: PIN = (17 * 23) - 42 = [ 0 3 4 9 ]");
            case SpyMiniGames.GameType.DossierDeduction:
                return LocalizationManager.T("Диверсант не из СССР: Лейтенант Бейкер (Даллас, 22:30, 3-08)", "Saboteur from abroad: Lt. Baker (Dallas, 22:30, 3-08)");
            case SpyMiniGames.GameType.FileSearch:
                return LocalizationManager.T("Найдите среди хозяйственных документов директиву Центра: ОТ_ЦЕНТР_...", "Find Central Directive among routine reports: FROM_CENTRAL_...");
            case SpyMiniGames.GameType.NetworkRestoration:
                return LocalizationManager.T("Включите каналы I, IV, VI. Каналы II, III, V аварийные — не включать!", "Engage channels I, IV, VI. Channels II, III, V are faulty — DO NOT engage!");
            case SpyMiniGames.GameType.TuringTape:
                return LocalizationManager.T("Бинарный сумматор: двоичный инкремент 1011 + 1 = 1100", "Binary adder: increment 1011 + 1 = 1100");
            case SpyMiniGames.GameType.FrequencyAnalysis:
                return LocalizationManager.T("Частоты: совместите главные пики с буквами О, Е, А", "Frequencies: align resonant peaks with vowels O, E, A");
            case SpyMiniGames.GameType.DatabaseFilter:
                return LocalizationManager.T("Фильтр: КРИПТОГРАФИЯ, ОСОБЫЙ допуск, НОЧНАЯ смена (пароль 8491)", "Filter: CRYPTOGRAPHY, SPECIAL clearance, NIGHT shift (PIN 8491)");
            case SpyMiniGames.GameType.RotorCipher:
                return LocalizationManager.T("Шифр «Фиалка»: суточный ключ [ IV - II - VII ]", "Fialka cipher: daily rotor key [ IV - II - VII ]");
            case SpyMiniGames.GameType.OmegaMasterProtocol:
                return LocalizationManager.T("Мастер-протокол «Омега»: 1) 1100  2) +7  3) 0349  4) IV-II-VII", "Master protocol \"Omega\": 1) 1100  2) +7  3) 0349  4) IV-II-VII");
            default:
                return LocalizationManager.T("Служебная записка: Код сейфа связи [ 4 8 2 1 ] | Цезарь сдвиг 3 (ATTACK)", "Desk note: Comms safe PIN [ 4 8 2 1 ] | Caesar shift 3 (ATTACK)");
        }
    }

    public static string GetNoteText(SpyMiniGames.GameType type)
    {
        bool en = LocalizationManager.IsEnglish;

        switch (type)
        {
            case SpyMiniGames.GameType.PasswordSimple:
                return en
                    ? "╔══════════════════════════════════════════════════════════╗\n" +
                      "║            INTERNAL MEMORANDUM #12 (COMMS)               ║\n" +
                      "║                                                          ║\n" +
                      "║  Temporary access code for secure comms archive:         ║\n" +
                      "║                                                          ║\n" +
                      "║                     [ 4  8  2  1 ]                       ║\n" +
                      "║                                                          ║\n" +
                      "║  Warning: destroy after entering into terminal!          ║\n" +
                      "║  Duty cryptographer, Signal Center.                      ║\n" +
                      "╚══════════════════════════════════════════════════════════╝"
                    : "╔══════════════════════════════════════════════════════════╗\n" +
                      "║              СЛУЖЕБНАЯ ЗАПИСКА №12 (СВЯЗЬ)               ║\n" +
                      "║                                                          ║\n" +
                      "║  Временный код доступа к архиву спецсвязи:               ║\n" +
                      "║                                                          ║\n" +
                      "║                     [ 4  8  2  1 ]                       ║\n" +
                      "║                                                          ║\n" +
                      "║  Внимание: после ввода в терминал уничтожить!            ║\n" +
                      "║  Дежурный шифровальщик узла связи.                      ║\n" +
                      "╚══════════════════════════════════════════════════════════╝";

            case SpyMiniGames.GameType.CaesarArchive:
                return en
                    ? "╔══════════════════════════════════════════════════════════╗\n" +
                      "║            DECRYPTOR MEMO: CAESAR CIPHER                ║\n" +
                      "║                                                          ║\n" +
                      "║  Archive files are shifted 3 alphabet positions ahead:   ║\n" +
                      "║  A ──► D,  B ──► E,  C ──► F,  D ──► G...               ║\n" +
                      "║                                                          ║\n" +
                      "║  To decrypt, shift 3 letters backward: D ──► A.          ║\n" +
                      "║  Target file contains attack directive: \"ATTACK\".        ║\n" +
                      "║  (D-3=A, W-3=T, W-3=T, D-3=A, F-3=C, N-3=K)              ║\n" +
                      "║  Correct file: DWWDFN_80.ZIP                             ║\n" +
                      "╚══════════════════════════════════════════════════════════╝"
                    : "╔══════════════════════════════════════════════════════════╗\n" +
                      "║             ПАМЯТКА ДЕШИФРАТОРУ: ШИФР ЦЕЗАРЯ             ║\n" +
                      "║                                                          ║\n" +
                      "║  Файлы архива сдвинуты на 3 позиции алфавита вперед:     ║\n" +
                      "║  A ──► D,  B ──► E,  C ──► F,  D ──► G...               ║\n" +
                      "║                                                          ║\n" +
                      "║  Для расшифровки сдвигайте на 3 буквы назад: D ──► A.    ║\n" +
                      "║  Искомый архив содержит план атаки: «ATTACK».            ║\n" +
                      "║  (D-3=A, W-3=T, W-3=T, D-3=A, F-3=C, N-3=K)              ║\n" +
                      "║  Правильный файл: DWWDFN_80.ZIP                          ║\n" +
                      "╚══════════════════════════════════════════════════════════╝";

            case SpyMiniGames.GameType.PasswordFormula:
                return en
                    ? "╔══════════════════════════════════════════════════════════╗\n" +
                      "║             MAP ARCHIVE MATHEMATICAL KEY                 ║\n" +
                      "║                                                          ║\n" +
                      "║  Password is computed by formula:                        ║\n" +
                      "║                                                          ║\n" +
                      "║                  PIN = (A × B) - C                       ║\n" +
                      "║                                                          ║\n" +
                      "║  Coefficients: A = 17,  B = 23,  C = 42.                 ║\n" +
                      "║  Computation: (17 × 23) = 391.  391 - 42 = 349.          ║\n" +
                      "║  Zero-padded 4-digit format: [ 0 3 4 9 ]                 ║\n" +
                      "╚══════════════════════════════════════════════════════════╝"
                    : "╔══════════════════════════════════════════════════════════╗\n" +
                      "║             МАТЕМАТИЧЕСКИЙ КЛЮЧ АРХИВА КАРТ              ║\n" +
                      "║                                                          ║\n" +
                      "║  Пароль вычисляется по формуле:                          ║\n" +
                      "║                                                          ║\n" +
                      "║                  PIN = (A × B) - C                       ║\n" +
                      "║                                                          ║\n" +
                      "║  Коэффициенты: A = 17,  B = 23,  C = 42.                 ║\n" +
                      "║  Расчет: (17 × 23) = 391.  391 - 42 = 349.               ║\n" +
                      "║  Формат ровно из 4 цифр с нулем: [ 0 3 4 9 ]             ║\n" +
                      "╚══════════════════════════════════════════════════════════╝";

            case SpyMiniGames.GameType.DossierDeduction:
                return en
                    ? "╔══════════════════════════════════════════════════════════╗\n" +
                      "║             COUNTERINTELLIGENCE DOSSIER ALERT            ║\n" +
                      "║                                                          ║\n" +
                      "║  Suspect profile for mainframe sabotage:                 ║\n" +
                      "║                                                          ║\n" +
                      "║  1. Arrived from abroad (foreign departure city: Dallas);║\n" +
                      "║  2. Located in sector 3-08 at night time (22:30).        ║\n" +
                      "║                                                          ║\n" +
                      "║  Target: Row 2 — Lieutenant Baker.                       ║\n" +
                      "╚══════════════════════════════════════════════════════════╝"
                    : "╔══════════════════════════════════════════════════════════╗\n" +
                      "║             ОРИЕНТИРОВКА ОСОБОГО ОТДЕЛА КГБ              ║\n" +
                      "║                                                          ║\n" +
                      "║  Приметы подозреваемого в саботаже вычислительного узла: ║\n" +
                      "║                                                          ║\n" +
                      "║  1. Прибыл не из СССР (город вылета за рубежом — Даллас);║\n" +
                      "║  2. Находился в секторе 3-08 в ночное время (22:30).     ║\n" +
                      "║                                                          ║\n" +
                      "║  Цель: Строка 2 — Лейтенант Бейкер.                      ║\n" +
                      "╚══════════════════════════════════════════════════════════╝";

            case SpyMiniGames.GameType.FileSearch:
                return en
                    ? "╔══════════════════════════════════════════════════════════╗\n" +
                      "║             DIRECTIVE OF THE SHIFT SUPERVISOR            ║\n" +
                      "║                                                          ║\n" +
                      "║  Among mundane cafeteria, alcohol and furniture logs,   ║\n" +
                      "║  locate and open the classified Central directive:       ║\n" +
                      "║  \"07. ОТ_ЦЕНТР_ДИРЕКТИВА_ОСОБАЯ_07.DAT\".                 ║\n" +
                      "╚══════════════════════════════════════════════════════════╝"
                    : "╔══════════════════════════════════════════════════════════╗\n" +
                      "║              РАСПОРЯЖЕНИЕ НАЧАЛЬНИКА СМЕНЫ               ║\n" +
                      "║                                                          ║\n" +
                      "║  Среди хозяйственных отчетов столовой, спирта и мебели   ║\n" +
                      "║  найдите и откройте секретную директиву Центра:          ║\n" +
                      "║  «ОТ_ЦЕНТР_ДИРЕКТИВА_ОСОБАЯ_07.DAT».                     ║\n" +
                      "╚══════════════════════════════════════════════════════════╝";

            case SpyMiniGames.GameType.NetworkRestoration:
                return en
                    ? "╔══════════════════════════════════════════════════════════╗\n" +
                      "║             BUS COMMUTATION & TELEMETRY MANUAL           ║\n" +
                      "║                                                          ║\n" +
                      "║  Engage functional channels:                             ║\n" +
                      "║  • [1] Channel I (North Bus)   ──► ON                    ║\n" +
                      "║  • [4] Channel IV (Optics)     ──► ON                    ║\n" +
                      "║  • [6] Channel VI (Standby)    ──► ON                    ║\n" +
                      "║  Faulty channels (II: Short, III: Phase, V: Overheat)    ║\n" +
                      "║  must strictly remain OFF!                               ║\n" +
                      "╚══════════════════════════════════════════════════════════╝"
                    : "╔══════════════════════════════════════════════════════════╗\n" +
                      "║             ИНСТРУКЦИЯ ПО КОММУТАЦИИ ЛИНИЙ              ║\n" +
                      "║                                                          ║\n" +
                      "║  Включите исправные каналы:                              ║\n" +
                      "║  • [1] Канал I (Север)   ──► ВКЛ                         ║\n" +
                      "║  • [4] Канал IV (Оптика)  ──► ВКЛ                        ║\n" +
                      "║  • [6] Канал VI (Резерв)  ──► ВКЛ                        ║\n" +
                      "║  Аварийные каналы (II: КЗ, III: рассинхрон, V: перегрев) ║\n" +
                      "║  категорически НЕ включать!                              ║\n" +
                      "╚══════════════════════════════════════════════════════════╝";

            default:
                return en
                    ? "╔══════════════════════════════════════════════════════════╗\n" +
                      "║             CRYPTOGRAPHER DESK NOTEBOOK                  ║\n" +
                      "║                                                          ║\n" +
                      "║  • Comms safe: access PIN [ 4 8 2 1 ].                   ║\n" +
                      "║  • Attack plan: Caesar cipher (shift -3, ATTACK).        ║\n" +
                      "║  • Staff maps: formula PIN = (17*23)-42 = [ 0349 ].      ║\n" +
                      "║  • Sabotage: foreign suspect (Baker / Dallas / 22:30).   ║\n" +
                      "║  • File catalog: Central directive \"FROM_CENTRAL_07\".    ║\n" +
                      "║  • Network bus: connect channels 1, 4 and 6.             ║\n" +
                      "╚══════════════════════════════════════════════════════════╝"
                    : "╔══════════════════════════════════════════════════════════╗\n" +
                      "║             СЛУЖЕБНЫЙ БЛОКНОТ КРИПТОГРАФА                ║\n" +
                      "║                                                          ║\n" +
                      "║  • Сейф спецсвязи: пин-код доступа [ 4 8 2 1 ].          ║\n" +
                      "║  • План атаки: шифр Цезаря (сдвиг 3 назад, ATTACK).      ║\n" +
                      "║  • Карты Генштаба: формула PIN = (17*23)-42 = [ 0349 ].  ║\n" +
                      "║  • Саботаж: диверсант вне СССР (Бейкер / Даллас / 22:30).║\n" +
                      "║  • Каталог файлов: директива Центра «ОТ_ЦЕНТР_07.DAT».   ║\n" +
                      "║  • Коммутация сети: подключить каналы 1, 4 и 6.          ║\n" +
                      "╚══════════════════════════════════════════════════════════╝";
        }
    }

    private void GeneratePaperAudio()
    {
        int sr = 44100;
        int n = (int)(sr * 0.18f);
        float[] d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            d[i] = (UnityEngine.Random.value * 2f - 1f) * Mathf.Sin(Mathf.PI * t) * 0.35f;
        }
        paperRustleClip = AudioClip.Create("PaperRustle", n, 1, sr, false);
        paperRustleClip.SetData(d, 0);
    }

    private void PlayRustle()
    {
        if (paperAudio == null) paperAudio = GetComponent<AudioSource>();
        if (paperAudio != null && paperRustleClip != null)
        {
            paperAudio.pitch = UnityEngine.Random.Range(0.95f, 1.15f);
            paperAudio.PlayOneShot(paperRustleClip, 0.6f);
        }
    }
}
