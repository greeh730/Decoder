using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class SetupMiniGamesUI
{
    [MenuItem("DECODER/Setup Spy Mini Games UI")]
    public static void SetupUI()
    {
        var canvasGO = GameObject.Find("TerminalCanvas");
        if (canvasGO == null)
        {
            Debug.LogError("[DECODER] TerminalCanvas not found in active scene!");
            return;
        }

        var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Consolas.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // Remove old hub/panel if exists
        var existingHub = canvasGO.transform.Find("SpyMiniGamesHub");
        if (existingHub != null) GameObject.DestroyImmediate(existingHub.gameObject);

        var existingPanel = canvasGO.transform.Find("MiniGamePanel");
        if (existingPanel != null) GameObject.DestroyImmediate(existingPanel.gameObject);

        // 1. Manager Hub GameObject (Always Active on Canvas)
        var hubGO = new GameObject("SpyMiniGamesHub", typeof(SpyMiniGames), typeof(AudioSource));
        hubGO.transform.SetParent(canvasGO.transform, false);
        var smg = hubGO.GetComponent<SpyMiniGames>();
        var audio = hubGO.GetComponent<AudioSource>();
        audio.playOnAwake = false;

        // 2. Visual Root MiniGamePanel
        var panelGO = new GameObject("MiniGamePanel", typeof(RectTransform), typeof(Image), typeof(Outline));
        panelGO.transform.SetParent(canvasGO.transform, false);

        var pRect = panelGO.GetComponent<RectTransform>();
        pRect.anchorMin = Vector2.zero;
        pRect.anchorMax = Vector2.one;
        pRect.offsetMin = Vector2.zero;
        pRect.offsetMax = Vector2.zero;
        pRect.sizeDelta = Vector2.zero;

        var bgImg = panelGO.GetComponent<Image>();
        bgImg.color = new Color(0.012f, 0.045f, 0.028f, 0.99f);

        var pOutline = panelGO.GetComponent<Outline>();
        pOutline.effectColor = new Color(0f, 0.7f, 0.45f, 0.8f);
        pOutline.effectDistance = new Vector2(2f, -2f);

        // 2. Header Bar
        var topBar = new GameObject("TopBar", typeof(RectTransform), typeof(Image));
        topBar.transform.SetParent(panelGO.transform, false);
        var tbRect = topBar.GetComponent<RectTransform>();
        tbRect.anchorMin = new Vector2(0.015f, 0.91f);
        tbRect.anchorMax = new Vector2(0.985f, 0.985f);
        tbRect.offsetMin = Vector2.zero;
        tbRect.offsetMax = Vector2.zero;
        topBar.GetComponent<Image>().color = new Color(0.03f, 0.14f, 0.08f, 0.95f);

        var titleText = CreateText("GameTitle", topBar.transform, font, new Vector2(0.02f, 0f), new Vector2(0.68f, 1f), 24, TextAnchor.MiddleLeft, new Color(0f, 1f, 0.7f));
        titleText.text = "<b>╔════ МОДУЛЬ КРИПТОАНАЛИЗА ════╗</b>";

        var timerText = CreateText("TimerText", topBar.transform, font, new Vector2(0.70f, 0f), new Vector2(0.98f, 1f), 22, TextAnchor.MiddleRight, new Color(0.45f, 0.95f, 0.55f));
        timerText.text = "ТАЙМЕР: 45 СЕК";

        // 3. Instruction description bar
        var descArea = new GameObject("DescArea", typeof(RectTransform), typeof(Image));
        descArea.transform.SetParent(panelGO.transform, false);
        var dRect = descArea.GetComponent<RectTransform>();
        dRect.anchorMin = new Vector2(0.015f, 0.83f);
        dRect.anchorMax = new Vector2(0.985f, 0.90f);
        dRect.offsetMin = Vector2.zero;
        dRect.offsetMax = Vector2.zero;
        descArea.GetComponent<Image>().color = new Color(0.02f, 0.08f, 0.05f, 0.90f);

        var descText = CreateText("DescText", descArea.transform, font, new Vector2(0.02f, 0f), new Vector2(0.98f, 1f), 19, TextAnchor.MiddleLeft, new Color(0.75f, 0.95f, 0.85f));
        descText.text = "Инструкция по решению задачи...";

        // 4. Central Game Content Area
        var contentBox = new GameObject("ContentBox", typeof(RectTransform), typeof(Image), typeof(Outline));
        contentBox.transform.SetParent(panelGO.transform, false);
        var cbRect = contentBox.GetComponent<RectTransform>();
        cbRect.anchorMin = new Vector2(0.015f, 0.16f);
        cbRect.anchorMax = new Vector2(0.985f, 0.82f);
        cbRect.offsetMin = Vector2.zero;
        cbRect.offsetMax = Vector2.zero;
        contentBox.GetComponent<Image>().color = new Color(0.018f, 0.06f, 0.038f, 0.98f);

        var cbOutline = contentBox.GetComponent<Outline>();
        cbOutline.effectColor = new Color(0f, 0.55f, 0.35f, 0.65f);
        cbOutline.effectDistance = new Vector2(1.5f, -1.5f);

        var dynamicText = CreateText("DynamicDisplayText", contentBox.transform, font, new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.96f), 20, TextAnchor.UpperLeft, new Color(0.85f, 0.98f, 0.90f));
        dynamicText.text = "Загрузка алгоритма...";

        // 5. Feedback status bar
        var statusArea = new GameObject("StatusArea", typeof(RectTransform), typeof(Image));
        statusArea.transform.SetParent(panelGO.transform, false);
        var sRect = statusArea.GetComponent<RectTransform>();
        sRect.anchorMin = new Vector2(0.015f, 0.09f);
        sRect.anchorMax = new Vector2(0.985f, 0.15f);
        sRect.offsetMin = Vector2.zero;
        sRect.offsetMax = Vector2.zero;
        statusArea.GetComponent<Image>().color = new Color(0.02f, 0.07f, 0.045f, 0.90f);

        var statusText = CreateText("StatusText", statusArea.transform, font, new Vector2(0.02f, 0f), new Vector2(0.98f, 1f), 20, TextAnchor.MiddleLeft, new Color(0f, 1f, 0.65f));
        statusText.text = "<color=#00FFAA>[СИСТЕМА ГОТОВА К ВВОДУ КЛЮЧА]</color>";

        // 6. Bottom Action Controls
        var bottomBar = new GameObject("BottomBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        bottomBar.transform.SetParent(panelGO.transform, false);
        var bbRect = bottomBar.GetComponent<RectTransform>();
        bbRect.anchorMin = new Vector2(0.015f, 0.015f);
        bbRect.anchorMax = new Vector2(0.985f, 0.08f);
        bbRect.offsetMin = Vector2.zero;
        bbRect.offsetMax = Vector2.zero;

        var hlg = bottomBar.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 16f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        var btnActionGO = new GameObject("BtnAction", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        btnActionGO.transform.SetParent(bottomBar.transform, false);
        btnActionGO.GetComponent<Image>().color = new Color(0.06f, 0.25f, 0.14f, 0.98f);
        var actionOutline = btnActionGO.GetComponent<Outline>();
        actionOutline.effectColor = new Color(0f, 0.6f, 0.35f, 0.7f);
        actionOutline.effectDistance = new Vector2(1f, -1f);
        var btnAct = btnActionGO.GetComponent<Button>();
        var actTxt = CreateText("Text", btnActionGO.transform, font, Vector2.zero, Vector2.one, 20, TextAnchor.MiddleCenter, Color.white);
        actTxt.text = "[ ВЫПОЛНИТЬ ДЕЙСТВИЕ [ENTER] ]";

        var btnCancelGO = new GameObject("BtnCancel", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        btnCancelGO.transform.SetParent(bottomBar.transform, false);
        btnCancelGO.GetComponent<Image>().color = new Color(0.20f, 0.08f, 0.08f, 0.98f);
        var cancelOutline = btnCancelGO.GetComponent<Outline>();
        cancelOutline.effectColor = new Color(0.6f, 0.2f, 0.2f, 0.7f);
        cancelOutline.effectDistance = new Vector2(1f, -1f);
        var btnCanc = btnCancelGO.GetComponent<Button>();
        var cancTxt = CreateText("Text", btnCancelGO.transform, font, Vector2.zero, Vector2.one, 20, TextAnchor.MiddleCenter, new Color(1f, 0.75f, 0.75f));
        cancTxt.text = "[ ОТМЕНА [ESC] ]";

        // Wire serialized fields on SpyMiniGames
        var so = new SerializedObject(smg);
        so.FindProperty("miniGameRootPanel").objectReferenceValue = panelGO;
        so.FindProperty("gameTitleText").objectReferenceValue = titleText;
        so.FindProperty("gameDescriptionText").objectReferenceValue = descText;
        so.FindProperty("statusFeedbackText").objectReferenceValue = statusText;
        so.FindProperty("timerCountdownText").objectReferenceValue = timerText;
        so.FindProperty("gameContentArea").objectReferenceValue = contentBox.transform;
        so.FindProperty("btnAction").objectReferenceValue = btnAct;
        so.FindProperty("btnActionText").objectReferenceValue = actTxt;
        so.FindProperty("btnCancel").objectReferenceValue = btnCanc;
        so.FindProperty("audioSource").objectReferenceValue = audio;
        so.ApplyModifiedProperties();

        // Keep inactive initially
        panelGO.SetActive(false);

        EditorSceneManager.MarkSceneDirty(canvasGO.scene);
        EditorSceneManager.SaveScene(canvasGO.scene);
        Debug.Log("[DECODER] SpyMiniGames UI successfully created and saved!");
    }

    private static Text CreateText(string name, Transform parent, Font font, Vector2 aMin, Vector2 aMax, int fontSize, TextAnchor align, Color col)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = col;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.lineSpacing = 1.15f;
        return t;
    }
}
