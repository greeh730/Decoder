using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controls the interactive workstation terminal on the monitor screen.
/// Provides actions for toggling room lighting, running decryption sequences,
/// diagnostics, and interactive logs.
/// </summary>
public class MonitorTerminal : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Canvas terminalCanvas;
    [SerializeField] private Text terminalLogText;
    [SerializeField] private Text statusText;
    [SerializeField] private Text clockText;
    [SerializeField] private Light roomLight;

    [Header("Interactive Panels")]
    [SerializeField] private GameObject activeScreenPanel;
    [SerializeField] private GameObject idleScreenPanel;

    private bool isRoomLightOn = true;
    private bool isDecoding = false;
    private bool isZoomedIn = false;
    private Coroutine decodeCoroutine;
    private readonly List<string> logLines = new List<string>();

    public bool IsZoomedIn => isZoomedIn;

    private void Awake()
    {
        if (roomLight == null)
        {
            var lightObj = GameObject.Find("Directional Light");
            if (lightObj != null) roomLight = lightObj.GetComponent<Light>();
        }
    }

    private bool isMainframeOnline = false;

    public bool IsMainframeOnline => isMainframeOnline;

    private void Start()
    {
        SetMainframeOnline(false);
        SetZoomedIn(false);
    }

    public void SetMainframeOnline(bool online)
    {
        isMainframeOnline = online;

        var idleText = idleScreenPanel != null ? idleScreenPanel.GetComponentInChildren<Text>() : null;
        if (idleText != null)
        {
            if (online)
            {
                idleText.text = "DECODER TERMINAL\n<size=17><color=#99FFCC>STATUS: ONLINE</color></size>\n\n<size=22><color=#FFFF66>[F] Нажмите для входа в систему</color></size>";
            }
            else
            {
                idleText.text = "DECODER WORKSTATION\n<size=17><color=#FF6666>STATUS: OFFLINE (НЕТ ПИТАНИЯ ЭВМ)</color></size>\n\n<size=19><color=#FFCC00>Для пуска изучите инструкцию в шкафчике</color></size>";
            }
        }

        if (online)
        {
            AddLog("<color=#00FFCC>[SYSTEM] ЭВМ Декодер-70 подключена.</color>");
            AddLog("[SYSTEM] Все функциональные подсистемы активны.");
        }
    }

    private void Update()
    {
        if (clockText != null)
        {
            clockText.text = isMainframeOnline ? System.DateTime.Now.ToString("HH:mm:ss") : "--:--:--";
        }

        if (isZoomedIn && isMainframeOnline)
        {
            HandleKeyboardShortcuts();
        }
    }

    private void HandleKeyboardShortcuts()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) ToggleRoomLight();
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) StartDecryption();
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) ShowSystemDiagnostics();
            if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) ShowSecretNotes();
        }
#else
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) ToggleRoomLight();
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) StartDecryption();
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) ShowSystemDiagnostics();
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) ShowSecretNotes();
#endif
    }

    public void SetZoomedIn(bool zoomed)
    {
        isZoomedIn = zoomed;
        if (activeScreenPanel != null) activeScreenPanel.SetActive(zoomed);
        if (idleScreenPanel != null) idleScreenPanel.SetActive(!zoomed);
    }

    public void AddLog(string msg)
    {
        string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
        logLines.Add($"<color=#72D572>[{timestamp}]</color> {msg}");
        if (logLines.Count > 12) logLines.RemoveAt(0);

        if (terminalLogText != null)
        {
            terminalLogText.text = string.Join("\n", logLines);
        }
    }

    public void ToggleRoomLight()
    {
        if (roomLight != null)
        {
            isRoomLightOn = !isRoomLightOn;
            roomLight.enabled = isRoomLightOn;
            string stateStr = isRoomLightOn ? "<color=#00FF88>ВКЛЮЧЕНО</color>" : "<color=#FF4444>ВЫКЛЮЧЕНО</color>";
            AddLog($"[LIGHT] Освещение комнаты: {stateStr}");
        }
        else
        {
            AddLog("[WARN] Источник света не найден.");
        }
    }

    public void StartDecryption()
    {
        if (isDecoding)
        {
            AddLog("[DECRYPT] Процесс уже выполняется...");
            return;
        }

        if (decodeCoroutine != null) StopCoroutine(decodeCoroutine);
        decodeCoroutine = StartCoroutine(DecryptionRoutine());
    }

    private IEnumerator DecryptionRoutine()
    {
        isDecoding = true;
        AddLog("<color=#FFCC00>[DECRYPT] Инициализация протокола декодирования...</color>");
        yield return new WaitForSeconds(0.4f);
        AddLog("[DECRYPT] Перебор ключей AES-256... 28%");
        yield return new WaitForSeconds(0.5f);
        AddLog("[DECRYPT] Проверка хэш-сигнатуры SHA-512... 67%");
        yield return new WaitForSeconds(0.5f);
        AddLog("[DECRYPT] Расшифровка защищенного блока данных... 94%");
        yield return new WaitForSeconds(0.4f);
        AddLog("<color=#00FFFF>[DECRYPT SUCCESS] Код доступа: [DECODER-770-ALPHA]</color>");
        AddLog("<color=#00FFFF>[STATUS] Секретные протоколы комнаты расшифрованы!</color>");
        isDecoding = false;
    }

    public void ShowSystemDiagnostics()
    {
        float fps = 1f / Mathf.Max(Time.smoothDeltaTime, 0.001f);
        AddLog($"[DIAG] FPS: {fps:F0} | Физика: PhysX 3D Active | Хитбоксы: 16");
        AddLog("[DIAG] Комната: Room oblck (Scale 0.44) OK");
        AddLog("[DIAG] Температура: 21.8°C | Связь: Стабильна 100%");
    }

    public void ShowSecretNotes()
    {
        AddLog("<color=#E0A96D>[FILES] Заметка #042 'Проект Decoder':</color>");
        AddLog("Все метрики комнаты откалиброваны под человека.");
        AddLog("Коллизии пола, мебели и стен активны.");
        AddLog("Интерфейс терминала готов к эксплуатации.");
    }
}
