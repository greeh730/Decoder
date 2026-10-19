using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Master Victory and Game Completion System for DECODER.
/// Triggers when the player decrypts the final 80%-locked master file «АРХИВ_ОМЕГА.TOP»
/// or copies it to the cartridge.
/// Features:
/// 1. Triumphant Soviet retro synthesizer fanfare (synthesized in code).
/// 2. Authentic Soviet Victory Debriefing Dossier (OnGUI retro CRT modal).
/// 3. Options to return to terminal to read decrypted documents, restart, or quit.
/// </summary>
public class GameVictorySystem : MonoBehaviour
{
    public static GameVictorySystem Instance { get; private set; }

    public static bool IsGameWon { get; private set; } = false;
    public static bool IsVictoryScreenOpen { get; private set; } = false;
    public static string VictoryReason { get; private set; } = "";

    [Header("Audio")]
    private AudioSource victoryAudioSource;
    private AudioClip fanfareClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (Instance == null)
        {
            var existing = FindObjectsByType<GameVictorySystem>(FindObjectsInactive.Include);
            if (existing != null && existing.Length > 0)
            {
                Instance = existing[0];
            }
            else
            {
                var go = new GameObject("GameVictorySystem");
                Instance = go.AddComponent<GameVictorySystem>();
                DontDestroyOnLoad(go);
            }
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        victoryAudioSource = gameObject.AddComponent<AudioSource>();
        victoryAudioSource.spatialBlend = 0f; // 2D stereo sound
        victoryAudioSource.volume = 1.0f;
        victoryAudioSource.playOnAwake = false;

        GenerateSovietFanfareClip();
    }

    private void Update()
    {
        if (!IsGameWon) return;

        // Toggle victory dossier on [V]
        bool vPressed = false;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && kb.vKey.wasPressedThisFrame) vPressed = true;
#else
        if (Input.GetKeyDown(KeyCode.V)) vPressed = true;
#endif
        if (vPressed)
        {
            if (IsVictoryScreenOpen) CloseVictoryScreen();
            else ReopenVictoryScreen();
        }
    }

    /// <summary>
    /// Master trigger to pass/beat the game.
    /// </summary>
    public static void TriggerVictory(string reason = "ФИНАЛЬНЫЙ АРХИВ ВЗЛОМАН")
    {
        if (IsGameWon) return;

        IsGameWon = true;
        IsVictoryScreenOpen = true;
        VictoryReason = reason;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (Instance != null)
        {
            Instance.PlayFanfare();
        }

        MainframeBootQuest.Instance?.ShowNotification(
            "<color=#00FFFF><b>★ ★ ★ МИССИЯ ВЫПОЛНЕНА! СВЕРХСЕКРЕТНЫЙ АРХИВ ВЗЛОМАН! ПОБЕДА! ★ ★ ★</b></color>");
    }

    public static void CloseVictoryScreen()
    {
        IsVictoryScreenOpen = false;
        MainframeBootQuest.Instance?.ShowNotification(
            "<color=#00FFAA>[ДОСЬЕ ПОБЕДЫ СВЕРНУТО]</color> Нажмите [V] в любой момент для повторного открытия.");
    }

    public static void ReopenVictoryScreen()
    {
        IsVictoryScreenOpen = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public static void RestartGame()
    {
        IsGameWon = false;
        IsVictoryScreenOpen = false;
        VictoryReason = "";
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void PlayFanfare()
    {
        if (victoryAudioSource != null && fanfareClip != null)
        {
            victoryAudioSource.Stop();
            victoryAudioSource.PlayOneShot(fanfareClip, 1.0f);
        }
    }

    /// <summary>
    /// Synthesizes a glorious, authentic Soviet 1980s retro polyphonic synthesizer fanfare.
    /// </summary>
    private void GenerateSovietFanfareClip()
    {
        int sampleRate = 44100;
        float duration = 4.2f;
        int totalSamples = (int)(sampleRate * duration);
        float[] data = new float[totalSamples];

        // Notes (Hz): C4=261.63, E4=329.63, G4=392.00, A4=440.00, B4=493.88, C5=523.25, D5=587.33, E5=659.25, G5=783.99, C6=1046.50
        // Chord progression:
        // Section 1 (0.0 - 0.7s): C Major [C4, E4, G4, C5]
        // Section 2 (0.7 - 1.4s): F Major [F4 (349.23), A4, C5, F5 (698.46)]
        // Section 3 (1.4 - 2.2s): G Major [G4, B4, D5, G5]
        // Section 4 (2.2 - 4.2s): Grand Triumph C Major [C4, G4, E5, C6]

        float[] f1 = { 261.63f, 329.63f, 392.00f, 523.25f };
        float[] f2 = { 349.23f, 440.00f, 523.25f, 698.46f };
        float[] f3 = { 392.00f, 493.88f, 587.33f, 783.99f };
        float[] f4 = { 261.63f, 392.00f, 659.25f, 1046.50f };

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            float[] chord;
            float sectionT, sectionLen;

            if (t < 0.7f)
            {
                chord = f1;
                sectionT = t;
                sectionLen = 0.7f;
            }
            else if (t < 1.4f)
            {
                chord = f2;
                sectionT = t - 0.7f;
                sectionLen = 0.7f;
            }
            else if (t < 2.2f)
            {
                chord = f3;
                sectionT = t - 1.4f;
                sectionLen = 0.8f;
            }
            else
            {
                chord = f4;
                sectionT = t - 2.2f;
                sectionLen = 2.0f;
            }

            // Envelope: Fast attack, steady body, release
            float attack = Mathf.Clamp01(sectionT / 0.04f);
            float release = Mathf.Clamp01((sectionLen - sectionT) / 0.06f);
            float env = attack * release;

            float sample = 0f;
            for (int k = 0; k < chord.Length; k++)
            {
                float freq = chord[k];
                // Rich analog synth harmonics with slight chorus detune
                float osc1 = Mathf.Sin(2f * Mathf.PI * freq * t);
                float osc2 = Mathf.Sin(2f * Mathf.PI * (freq * 1.003f) * t) * 0.7f;
                float harm = Mathf.Sin(2f * Mathf.PI * (freq * 2.0f) * t) * 0.35f;
                sample += (osc1 + osc2 + harm) * 0.22f;
            }

            // Master decay at the very end
            float masterEnv = Mathf.Clamp01((duration - t) / 0.5f);
            data[i] = Mathf.Clamp(sample * env * masterEnv * 0.65f, -1f, 1f);
        }

        fanfareClip = AudioClip.Create("SovietVictoryFanfare", totalSamples, 1, sampleRate, false);
        fanfareClip.SetData(data, 0);
    }

    private void OnGUI()
    {
        // Persistent victory badge when dossier is minimized
        if (IsGameWon && !IsVictoryScreenOpen)
        {
            var winBadge = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            winBadge.normal.textColor = new Color(0.0f, 1.0f, 0.67f);
            GUI.color = new Color(0.04f, 0.16f, 0.10f, 0.88f);
            GUI.Box(new Rect(15f, 12f, 260f, 28f), "★ ОПЕРАЦИЯ ЗАВЕРШЕНА [V: ДОСЬЕ]", winBadge);
            GUI.color = Color.white;
            return;
        }

        if (!IsGameWon || !IsVictoryScreenOpen) return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        float screenW = Screen.width;
        float screenH = Screen.height;

        // Dark Soviet emerald-green vignette overlay
        GUI.color = new Color(0.02f, 0.08f, 0.05f, 0.94f);
        GUI.DrawTexture(new Rect(0, 0, screenW, screenH), Texture2D.whiteTexture);

        // Frame dimensions
        float boxW = Mathf.Min(780f, screenW - 40f);
        float boxH = Mathf.Min(560f, screenH - 40f);
        float boxX = (screenW - boxW) * 0.5f;
        float boxY = (screenH - boxH) * 0.5f;

        // Glowing border frame
        GUI.color = new Color(0.0f, 0.85f, 0.55f, 0.95f);
        GUI.Box(new Rect(boxX, boxY, boxW, boxH), "");

        // Inner area
        GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(boxX + 28f, boxY + 22f, boxW - 56f, boxH - 44f));

        // Header
        var titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(Mathf.Clamp(screenW * 0.022f, 20f, 30f)),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = new Color(0.0f, 1.0f, 0.67f);

        var subStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(Mathf.Clamp(screenW * 0.013f, 13f, 17f)),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        subStyle.normal.textColor = new Color(1.0f, 0.85f, 0.35f);

        var dossierStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = Mathf.RoundToInt(Mathf.Clamp(screenW * 0.012f, 12f, 14f)),
            alignment = TextAnchor.UpperLeft,
            wordWrap = true,
            padding = new RectOffset(14, 14, 12, 12)
        };
        dossierStyle.normal.textColor = new Color(0.85f, 1.0f, 0.92f);

        GUILayout.Label("★   ЗАДАНИЕ ВЫПОЛНЕНО: ИГРА ПРОЙДЕНА   ★", titleStyle);
        GUILayout.Space(4f);
        GUILayout.Label("СЕКРЕТНЫЙ АРХИВ НИИ «ВЕКТОР» ПОЛНОСТЬЮ ВЗЛОМАН", subStyle);
        GUILayout.Space(14f);

        // Gather operational statistics
        int totalFiles = 10;
        int decryptedCount = 10;
        int pct = 100;
        if (VectorCommander.Instance != null)
        {
            totalFiles = VectorCommander.Instance.GetTotalRegularFilesCount();
            decryptedCount = VectorCommander.Instance.GetDecryptedRegularFilesCount();
            pct = Mathf.RoundToInt(VectorCommander.Instance.GetDecryptionPercentage() * 100f);
        }

        string timeStr = "12:45";
        if (PocketWatchController.Instance != null)
        {
            float tRem = PocketWatchController.Instance.TimeRemaining;
            int m = Mathf.FloorToInt(tRem / 60f);
            int s = Mathf.FloorToInt(tRem % 60f);
            timeStr = $"{m:00}:{s:00}";
        }

        string dossierText =
            "<b>┌─── СЛУЖЕБНЫЙ ОТЧЕТ ОПЕРАЦИИ «ДЕКОДЕР» ────────────────────────┐</b>\n\n" +
            $"• <b>ГЛАВНАЯ ЦЕЛЬ:</b> <color=#00FFAA>АРХИВ_ОМЕГА.TOP [ДЕШИФРОВАН / ЗАХВАЧЕН]</color>\n" +
            $"• <b>РАСШИФРОВАНО ФАЙЛОВ БАЗЫ:</b> <color=#FFFF66>{decryptedCount} из {totalFiles} ({pct}%)</color>\n" +
            $"• <b>ВРЕМЯ ДО ПРИБЫТИЯ ПОДКРЕПЛЕНИЯ:</b> <color=#00FFAA>{timeStr}</color>\n" +
            $"• <b>СТАТУС ПАТРУЛЯ КОМЕНДАТУРЫ:</b> <color=#00FFAA>ТРЕВОГА СНЯТА / ОБХОД ЗАБЛОКИРОВАН</color>\n" +
            $"• <b>КАНАЛ ЭВАКУАЦИИ:</b> <color=#00FFAA>СЕВЕРНЫЙ ШЛЮЗ ОТКРЫТ. ДАННЫЕ В БЕЗОПАСНОСТИ.</color>\n\n" +
            "<b>ИТОГ МИССИИ:</b> Все стратегические программы, векторные топологии подземного бункера\n" +
            "и ядро алгоритма дешифрации успешно перехвачены. Задание Госкомитета выполнено на «ОТЛИЧНО»!\n" +
            "<b>└──────────────────────────────────────────────────────────────┘</b>";

        GUILayout.Box(dossierText, dossierStyle, GUILayout.ExpandHeight(true));
        GUILayout.Space(12f);

        // Action buttons
        GUILayout.BeginHorizontal();

        var btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = Mathf.RoundToInt(Mathf.Clamp(screenW * 0.013f, 13f, 16f)),
            fontStyle = FontStyle.Bold
        };
        btnStyle.normal.textColor = Color.white;

        if (GUILayout.Button("[ V ]   ТЕРМИНАЛ / ЧТЕНИЕ АРХИВА", btnStyle, GUILayout.Height(42f)))
        {
            CloseVictoryScreen();
        }

        GUILayout.Space(10f);

        if (GUILayout.Button("[ R ]   НАЧАТЬ ЗАНОВО", btnStyle, GUILayout.Height(42f)))
        {
            RestartGame();
        }

        GUILayout.Space(10f);

        if (GUILayout.Button("[ ESC ]   ВЫЙТИ ИЗ ИГРЫ", btnStyle, GUILayout.Height(42f)))
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        GUILayout.EndHorizontal();
        GUILayout.Space(6f);
        GUILayout.EndArea();
    }
}
