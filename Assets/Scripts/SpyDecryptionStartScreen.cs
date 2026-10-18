using System;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Authentic retro CRT splash/boot screen for the workstation.
/// Faithful to the minimalist reference:
/// 1. Top hero graphics: Left Avatar, Center Logo with Arrow, Right Complex illustration.
/// 2. Clean horizontal divider line.
/// 3. Minimalist retro status lines with dashed blocks:
///    ====================   СИСТЕМА   ====================
///    ====================   ВЕРСИЯ 2.1   ================= _
/// 4. Animated blinking cursor.
/// </summary>
public class SpyDecryptionStartScreen : MonoBehaviour
{
    public static SpyDecryptionStartScreen Instance { get; private set; }

    [Header("Top Hero Section Sprites")]
    [SerializeField] private Image avatarImage;
    [SerializeField] private Image logoImage;
    [SerializeField] private Image schematicImage;

    [Header("Status Lines (Matching Reference)")]
    [SerializeField] private Text statusLine1;
    [SerializeField] private Text statusLine2;
    [SerializeField] private Text promptActionText;
    [SerializeField] private Text headerTitleText;
    [SerializeField] private Text headerClockText;

    [Header("Visual Tuning")]
    [SerializeField] private Color phosphorBright = new Color(0.0f, 1.0f, 0.67f, 1.0f);   // #00FFAA
    [SerializeField] private Color phosphorHot = new Color(0.63f, 1.0f, 0.86f, 1.0f);      // #A0FFE0
    [SerializeField] private Color phosphorDim = new Color(0.12f, 0.63f, 0.42f, 1.0f);     // #1FA16B

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    private AudioClip keyBeepClip;

    private float cursorTimer = 0f;
    private bool cursorVisible = true;

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
        UpdateStatusLines();
    }

    private void Update()
    {
        if (headerClockText != null)
        {
            headerClockText.text = DateTime.Now.ToString("HH:mm:ss");
        }

        // Blinking cursor animation
        cursorTimer += Time.deltaTime;
        if (cursorTimer >= 0.45f)
        {
            cursorTimer = 0f;
            cursorVisible = !cursorVisible;
            UpdateStatusLines();
        }

        // Interactive key press / click
        if (MonitorTerminal.Instance != null && MonitorTerminal.Instance.IsZoomedIn)
        {
            if (IsAnyKeyPressedThisFrame())
            {
                OnStartScreenClicked();
            }
        }
    }

    private bool IsAnyKeyPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
        var mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)) return true;
        return false;
#else
        return Input.anyKeyDown || Input.GetMouseButtonDown(0);
#endif
    }

    private void UpdateStatusLines()
    {
        string cursor = cursorVisible ? "_" : " ";

        if (statusLine1 != null)
        {
            statusLine1.text = "■■■■■■■■■■■■■■■■■■■■■■■■■■■■   СИСТЕМА: ГОТОВА   ■■■■■■■■■■■■■■■■■■■■■■■■■■■■";
        }

        if (statusLine2 != null)
        {
            statusLine2.text = $"■■■■■■■■■■■■■■■■■■■■■■■■■■■■   ВЕРСИЯ: 2.1   ■■■■■■■■■■■■■■■■■■■■■■■■ {cursor}";
        }

        if (promptActionText != null)
        {
            promptActionText.text = $"[ НАЖМИТЕ ЛЮБУЮ КЛАВИШУ ДЛЯ ВХОДА В СИСТЕМУ {cursor} ]";
        }
    }

    public void OnStartScreenClicked()
    {
        PlayKeyBeep();
        MainframeBootQuest.Instance?.ShowNotification(
            "<color=#00FFAA>[ВЕКТОР-ОС]</color> Загрузка рабочей среды...");
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
