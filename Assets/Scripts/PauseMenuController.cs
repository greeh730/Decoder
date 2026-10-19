using System;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Handles in-game Pause Menu triggered by Escape.
/// Pauses timeScale, manages cursor locking, scene restart, language switching, and game exit.
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    private static PauseMenuController _instance;
    public static PauseMenuController Instance
    {
        get
        {
            if (_instance == null) _instance = FindAnyObjectByType<PauseMenuController>();
            return _instance;
        }
        private set { _instance = value; }
    }

    [Header("State")]
    [SerializeField] private bool isPaused = false;
    private float previousTimeScale = 1.0f;

    public bool IsPaused => isPaused;

    private GUIStyle headerStyle;
    private GUIStyle subheaderStyle;
    private GUIStyle buttonStyle;
    private GUIStyle boxStyle;
    private GUIStyle footerStyle;
    private bool stylesInitialized = false;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        isPaused = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (Instance == null)
        {
            var go = new GameObject("PauseMenuController");
            go.AddComponent<PauseMenuController>();
            DontDestroyOnLoad(go);
        }
    }

    private void Update()
    {
        // Don't open pause menu if game is won or game over is active
        if (GuardPatrolSystem.IsGameOver || GameVictorySystem.IsGameWon)
        {
            if (isPaused) ResumeGame();
            return;
        }

        bool escPressed = false;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) escPressed = true;
#else
        if (Input.GetKeyDown(KeyCode.Escape)) escPressed = true;
#endif

        if (escPressed)
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        if (isPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void PauseGame()
    {
        isPaused = true;
        previousTimeScale = (Time.timeScale > 0.01f) ? Time.timeScale : 1.0f;
        Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = (previousTimeScale > 0.01f) ? previousTimeScale : 1.0f;

        if (PlayerController.Instance != null && (PlayerController.Instance.IsInspectingMonitor || PlayerController.Instance.IsCursorUnlocked))
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

    public void RestartGame()
    {
        Time.timeScale = 1.0f;
        isPaused = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void InitStyles()
    {
        if (stylesInitialized && headerStyle != null) return;

        boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.background = MakeTex(2, 2, new Color(0.05f, 0.08f, 0.07f, 0.96f));

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        headerStyle.normal.textColor = new Color(0.2f, 1.0f, 0.5f);

        subheaderStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleCenter
        };
        subheaderStyle.normal.textColor = new Color(0.6f, 0.8f, 0.7f);

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        buttonStyle.normal.background = MakeTex(2, 2, new Color(0.12f, 0.22f, 0.18f, 0.95f));
        buttonStyle.normal.textColor = new Color(0.85f, 1.0f, 0.9f);
        buttonStyle.hover.background = MakeTex(2, 2, new Color(0.2f, 0.45f, 0.3f, 1.0f));
        buttonStyle.hover.textColor = Color.yellow;
        buttonStyle.active.background = MakeTex(2, 2, new Color(0.3f, 0.6f, 0.4f, 1.0f));
        buttonStyle.active.textColor = Color.white;

        footerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            alignment = TextAnchor.MiddleCenter
        };
        footerStyle.normal.textColor = new Color(0.45f, 0.6f, 0.5f);

        stylesInitialized = true;
    }

    private void OnGUI()
    {
        if (!isPaused) return;

        InitStyles();

        float sw = Screen.width;
        float sh = Screen.height;

        // Dark background tint
        GUI.color = new Color(0.0f, 0.0f, 0.0f, 0.75f);
        GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float w = Mathf.Min(460f, sw - 40f);
        float h = 340f;
        float x = (sw - w) * 0.5f;
        float y = (sh - h) * 0.5f;

        GUI.Box(new Rect(x, y, w, h), "", boxStyle);

        GUILayout.BeginArea(new Rect(x + 20, y + 20, w - 40, h - 35));

        // Header
        string title = LocalizationManager.T("СИСТЕМНОЕ МЕНЮ // ПАУЗА", "SYSTEM MENU // PAUSED");
        GUILayout.Label(title, headerStyle);

        string subtitle = LocalizationManager.T("ЭВМ «ВЕКТОР-74» — ОБЪЕКТ ЗАСТЫЛ", "MAINFRAME VECTOR-74 — TIME HALTED");
        GUILayout.Label(subtitle, subheaderStyle);

        GUILayout.Space(20);

        // Resume
        string resumeTxt = LocalizationManager.T("▶  ПРОДОЛЖИТЬ ИГРУ [Esc]", "▶  RESUME GAME [Esc]");
        if (GUILayout.Button(resumeTxt, buttonStyle, GUILayout.Height(42)))
        {
            ResumeGame();
        }

        GUILayout.Space(8);

        // Restart
        string restartTxt = LocalizationManager.T("↺  НАЧАТЬ ЗАНОВО", "↺  RESTART MISSION");
        if (GUILayout.Button(restartTxt, buttonStyle, GUILayout.Height(42)))
        {
            RestartGame();
        }

        GUILayout.Space(8);

        // Language toggle
        string langTxt = LocalizationManager.IsEnglish
            ? "🌐  LANGUAGE: [ ENGLISH ] (Click to switch to Русский)"
            : "🌐  ЯЗЫК: [ РУССКИЙ ] (Нажмите для перехода на English)";
        if (GUILayout.Button(langTxt, buttonStyle, GUILayout.Height(42)))
        {
            LocalizationManager.ToggleLanguage();
        }

        GUILayout.Space(8);

        // Quit
        string quitTxt = LocalizationManager.T("✖  ВЫЙТИ ИЗ ИГРЫ", "✖  QUIT TO DESKTOP");
        if (GUILayout.Button(quitTxt, buttonStyle, GUILayout.Height(42)))
        {
            QuitGame();
        }

        GUILayout.Space(12);

        string footer = LocalizationManager.T(
            "• Tab: освободить/захватить мышь  • Esc: меню паузы",
            "• Tab: toggle mouse cursor  • Esc: pause menu"
        );
        GUILayout.Label(footer, footerStyle);

        GUILayout.EndArea();
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}
