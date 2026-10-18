using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Authentic retro CRT start screen for the spy decryption workstation.
/// Inspired by vintage 1970s/1980s monochrome phosphor computer terminals,
/// featuring the 3-panel layout (Agent Dossier, Geometric Arrow Logo, Base Radar Schematic),
/// technical telemetry stream, and animated phosphor prompt.
/// </summary>
public class SpyDecryptionStartScreen : MonoBehaviour
{
    public static SpyDecryptionStartScreen Instance { get; private set; }

    [Header("Top Hero Section Sprites")]
    [SerializeField] private Image avatarImage;
    [SerializeField] private Image logoImage;
    [SerializeField] private Image schematicImage;

    [Header("Text Displays (Consolas Monospace)")]
    [SerializeField] private Text headerTitleText;
    [SerializeField] private Text headerClockText;
    [SerializeField] private Text missionDataText;
    [SerializeField] private Text telemetryDataText;
    [SerializeField] private Text promptText;

    [Header("Visual Tuning")]
    [SerializeField] private Color phosphorBright = new Color(0.0f, 1.0f, 0.67f, 1.0f);   // #00FFAA
    [SerializeField] private Color phosphorHot = new Color(0.63f, 1.0f, 0.86f, 1.0f);      // #A0FFE0
    [SerializeField] private Color phosphorDim = new Color(0.12f, 0.63f, 0.42f, 1.0f);     // #1FA16B

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    private AudioClip keyBeepClip;
    private AudioClip accessGrantedClip;

    private float cursorTimer = 0f;
    private bool cursorVisible = true;
    private float telemetryTimer = 0f;
    private int packetCount = 1420;
    private int signalPercent = 98;

    private void Awake()
    {
        Instance = this;
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.spatialBlend = 0.9f;
        audioSource.minDistance = 0.5f;
        audioSource.maxDistance = 6f;

        GenerateAudioClips();
    }

    private void Start()
    {
        UpdateMissionData();
        UpdateTelemetryData();
        UpdatePromptText();
    }

    private void Update()
    {
        // 1. Clock update
        if (headerClockText != null)
        {
            headerClockText.text = DateTime.Now.ToString("HH:mm:ss");
        }

        // 2. Cursor blink animation
        cursorTimer += Time.deltaTime;
        if (cursorTimer >= 0.45f)
        {
            cursorTimer = 0f;
            cursorVisible = !cursorVisible;
            UpdatePromptText();
        }

        // 3. Live telemetry jitter (subtle authentic terminal activity)
        telemetryTimer += Time.deltaTime;
        if (telemetryTimer >= 1.2f)
        {
            telemetryTimer = 0f;
            packetCount += UnityEngine.Random.Range(3, 9);
            signalPercent = UnityEngine.Random.Range(97, 100);
            UpdateTelemetryData();
        }

        // 4. Interactive click / space feedback
        if (MonitorTerminal.Instance != null && MonitorTerminal.Instance.IsZoomedIn)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0))
            {
                OnStartScreenClicked();
            }
        }
    }

    private void UpdatePromptText()
    {
        if (promptText == null) return;
        string cursor = cursorVisible ? "█" : " ";
        promptText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(phosphorHot)}>► [ НАЖМИТЕ [ПРОБЕЛ] / [ЛКМ] ДЛЯ ВХОДА В РАБОЧУЮ СРЕДУ СИСТЕМЫ {cursor} ]</color>";
    }

    private void UpdateMissionData()
    {
        if (missionDataText == null) return;

        missionDataText.text =
            "<b><color=#00FFAA>/// КОНФИГУРАЦИЯ СИСТЕМЫ ///</color></b>\n" +
            "• ПРОЦЕССОР: <color=#FFFFFF>БИС К1801ВМ2 (16-БИТ, 8.0 МГц)</color>\n" +
            "• ПАМЯТЬ: <color=#99FFCC>64 КБ БАЗОВАЯ // 512 КБ ОЗУ [ТЕСТ: ОК]</color>\n" +
            "• ДИСКОВОД: <color=#FFFF66>ЕС-5061 (ПАКЕТ 29 МБ) [ТОМ 01 // ГОТОВ]</color>\n" +
            "• КАНАЛ СВЯЗИ: <color=#99FFCC>1200 БОД [ОБМЕН ДАННЫМИ: В НОРМЕ]</color>\n" +
            "• ОПЕРАЦИОННАЯ СИСТЕМА: <color=#00FF88>«ВЕКТОР-ОС» РЕДАКЦИЯ 3.2</color>\n" +
            "• АВТОРИЗАЦИЯ: <color=#99FFCC>ДЕЖУРНЫЙ ИНЖЕНЕР [ТАБЕЛЬ №104]</color>";
    }

    private void UpdateTelemetryData()
    {
        if (telemetryDataText == null) return;

        telemetryDataText.text =
            "<b><color=#00FFAA>/// ТЕЛЕМЕТРИЯ ПОДСИСТЕМ ///</color></b>\n" +
            $"• МЕЙНФРЕЙМ ВЕКТОР-1: [ <color=#00FF88>ПИТАНИЕ СТАБИЛЬНО // ОНЛАЙН</color> ]\n" +
            $"• СЕКТОРОВ ОБРАБОТАНО: <color=#00FFAA>{packetCount:N0}</color> БЛОКОВ [СТАБИЛЬНО]\n" +
            "• ДАТЧИКИ ТЕМПЕРАТУРЫ: ШПИНДЕЛЬ ЕС-5061 [<color=#00FF88>30°C // В НОРМЕ</color>]\n" +
            "• МОНИТОР ЭЛТ: ВЫСОКОЕ НАПРЯЖЕНИЕ [ 15 кВ // 50 Гц ]\n" +
            "• БУФЕР ВВОДА-ВЫВОДА: [ <color=#88DDCC>64 КБ СВОБОДНО // ГОТОВ К ЧТЕНИЮ</color> ]\n" +
            "• ГОТОВНОСТЬ К РАБОТЕ: <color=#00FFAA>[ СИСТЕМА ИНИЦИАЛИЗИРОВАНА ]</color>";
    }

    public void OnStartScreenClicked()
    {
        PlayKeyBeep();
        MainframeBootQuest.Instance?.ShowNotification(
            "<color=#00FFAA>[ВЕКТОР-ОС]</color> Загрузка рабочей среды оператора...");
    }

    private void GenerateAudioClips()
    {
        int sampleRate = 44100;
        int samples = (int)(sampleRate * 0.05f);
        float[] clickData = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            clickData[i] = Mathf.Sin(2f * Mathf.PI * 1200f * ((float)i / sampleRate)) * (1f - t);
        }
        keyBeepClip = AudioClip.Create("TerminalBeep", samples, 1, sampleRate, false);
        keyBeepClip.SetData(clickData, 0);
    }

    private void PlayKeyBeep()
    {
        if (audioSource != null && keyBeepClip != null)
        {
            audioSource.pitch = UnityEngine.Random.Range(0.98f, 1.02f);
            audioSource.PlayOneShot(keyBeepClip, 0.45f);
        }
    }
}
