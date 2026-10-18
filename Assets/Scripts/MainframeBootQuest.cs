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
/// </summary>
public class MainframeBootQuest : MonoBehaviour
{
    public static MainframeBootQuest Instance { get; private set; }

    [Header("Quest State")]
    [SerializeField] private BootStage currentStage = BootStage.PowerOff;
    [SerializeField] private bool hasTapeInInventory = false;
    [SerializeField] private bool isTapeInserted = false;
    [SerializeField] private bool[] addressSwitches = new bool[4] { false, false, false, false };
    private readonly bool[] targetCode = new bool[4] { true, false, true, true }; // 1-0-1-1

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

    [Header("UI Overlay - Engineer Manual")]
    [SerializeField] private bool isReadingManual = false;

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

    public BootStage CurrentStage => currentStage;
    public bool IsReadingManual => isReadingManual;
    public bool HasTape => hasTapeInInventory;

    public bool IsNoiseActive => (currentStage == BootStage.SystemOnline || currentStage == BootStage.SystemBooting);
    public float NoiseRadius => IsNoiseActive ? 5.5f : 0f;

    private AudioSource humAudioSource;
    private AudioClip humLoopClip;
    private AudioClip relayClackClip;

    private void Awake()
    {
        Instance = this;

        currentStage = BootStage.PowerOff;
        hasTapeInInventory = false;
        isTapeInserted = false;
        addressSwitches = new bool[4] { false, false, false, false };

        CreateMaterials();
        SetupAudio();
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
        tubeGlowMat.SetColor("_BaseColor", new Color(1f, 0.45f, 0.05f));
        tubeGlowMat.SetColor("_EmissionColor", new Color(1f, 0.4f, 0.05f) * 0f);
        tubeGlowMat.EnableKeyword("_EMISSION");
    }

    // ==========================================
    // INTERACTION ACTIONS
    // ==========================================

    public void ToggleBreaker()
    {
        if (currentStage == BootStage.PowerOff)
        {
            currentStage = BootStage.BreakerOn;
            ShowNotification("Главный рубильник ВКЛЮЧЕН. Силовая сеть под напряжением.");
        }
        else if (currentStage == BootStage.BreakerOn)
        {
            currentStage = BootStage.PowerOff;
            ShowNotification("Главный рубильник ВЫКЛЮЧЕН. Питание обесточено.");
        }
        else
        {
            ShowNotification("Питание серверов активно. Не отключайте рубильник во время работы.");
            return;
        }

        UpdateAllVisuals();
    }

    public void ToggleFilament()
    {
        if (currentStage < BootStage.BreakerOn)
        {
            ShowNotification("[ОТКАЗ] Нет питания в электросети! Сначала включите рубильник на щитке.");
            return;
        }

        if (currentStage == BootStage.BreakerOn)
        {
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
        if (currentStage == BootStage.SystemOnline)
        {
            // Enter Hot Standby: Cut HV, cut hum, keep filaments warm!
            currentStage = BootStage.HotStandby;
            StopTransformerHum();
            PlayRelaySound();
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
        string stateStr = addressSwitches[index] ? "ВКЛ [1]" : "ВЫКЛ [0]";
        ShowNotification($"Тумблер шины #{index + 1}: {stateStr}");

        UpdateFrontPanelSwitches();
    }

    public void ToggleAddressSwitch0() => ToggleAddressSwitch(0);
    public void ToggleAddressSwitch1() => ToggleAddressSwitch(1);
    public void ToggleAddressSwitch2() => ToggleAddressSwitch(2);
    public void ToggleAddressSwitch3() => ToggleAddressSwitch(3);
    public void OpenManual() => ToggleManual(true);

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
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public void ShowNotification(string msg)
    {
        onScreenNotification = msg;
        notificationTimer = 4.0f;
    }

    // ==========================================
    // VISUALS UPDATER
    // ==========================================

    public void UpdateAllVisuals()
    {
        // 1. Breaker
        if (breakerHandle != null)
        {
            breakerHandle.localRotation = Quaternion.Euler(currentStage >= BootStage.BreakerOn ? -35f : 35f, 0f, 0f);
        }
        if (breakerLed != null)
        {
            breakerLed.sharedMaterial = currentStage >= BootStage.BreakerOn ? ledGreenMat : ledRedMat;
        }

        // 2. Filament
        if (filamentSwitch != null)
        {
            filamentSwitch.localRotation = Quaternion.Euler(currentStage >= BootStage.FilamentWarming ? -35f : 35f, 0f, 0f);
        }
        if (filamentStatusLed != null)
        {
            filamentStatusLed.sharedMaterial = currentStage >= BootStage.FilamentReady ? ledGreenMat :
                                               (currentStage == BootStage.FilamentWarming ? ledYellowMat : ledOffMat);
        }

        // 3. High Voltage
        bool hvActive = (currentStage >= BootStage.HighVoltageOn && currentStage != BootStage.HotStandby);
        if (hvSwitch != null)
        {
            hvSwitch.localRotation = Quaternion.Euler(hvActive ? -35f : 35f, 0f, 0f);
        }
        if (hvStatusLed != null)
        {
            hvStatusLed.sharedMaterial = hvActive ? ledGreenMat : (currentStage == BootStage.HotStandby ? ledYellowMat : ledOffMat);
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
        if (filamentPointLight != null)
        {
            filamentPointLight.enabled = currentStage >= BootStage.FilamentWarming;
            if (currentStage >= BootStage.FilamentReady) filamentPointLight.intensity = 2.2f;
        }
        if (tubeRenderers != null && tubeGlowMat != null)
        {
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
        float sw = Screen.width;
        float sh = Screen.height;

        // Notification banner (only in room view, not when zoomed into monitor)
        bool isZoomedOnMonitor = MonitorTerminal.Instance != null && MonitorTerminal.Instance.IsZoomedIn;
        if (!string.IsNullOrEmpty(onScreenNotification) && !isReadingManual && !isZoomedOnMonitor)
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
            "   Для Машины Тьюринга (.BIN) используйте сложение в столбик:\n" +
            "   • на '1' пишите 0 и шаг влево в q1; на '0' пишите 1, СТОП и переход в HALT.\n" +
            "   <i>В любой момент мини-игры нажимайте [H] для вызова служебной шпаргалки.</i>";

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
        if (questAudioSource == null) questAudioSource = GetComponent<AudioSource>();
        if (questAudioSource == null) questAudioSource = gameObject.AddComponent<AudioSource>();

        questAudioSource.spatialBlend = 0.85f;
        questAudioSource.minDistance = 1.0f;
        questAudioSource.maxDistance = 15f;

        humAudioSource = gameObject.AddComponent<AudioSource>();
        humAudioSource.spatialBlend = 0.85f;
        humAudioSource.minDistance = 1.5f;
        humAudioSource.maxDistance = 14f;
        humAudioSource.loop = true;
        humAudioSource.volume = 0.55f;

        humLoopClip = CreateTransformerHumClip();
        relayClackClip = CreateRelayClackClip();
        humAudioSource.clip = humLoopClip;
    }

    private void StartTransformerHum()
    {
        if (humAudioSource != null && humLoopClip != null && !humAudioSource.isPlaying)
        {
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

    private AudioClip CreateTransformerHumClip()
    {
        int sampleRate = 44100;
        int samples = sampleRate * 1; // 1-second seamless loop
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            // 50Hz fundamental power line hum
            float h50 = Mathf.Sin(2f * Mathf.PI * 50f * t) * 0.45f;
            // 100Hz rectification harmonic
            float h100 = Mathf.Sin(2f * Mathf.PI * 100f * t) * 0.25f;
            // 150Hz odd harmonic
            float h150 = Mathf.Sin(2f * Mathf.PI * 150f * t) * 0.12f;
            // Subtle cooling blower noise
            float fan = (Random.value * 2f - 1f) * 0.04f;

            data[i] = (h50 + h100 + h150 + fan);
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
