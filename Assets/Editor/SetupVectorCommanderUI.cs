using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class SetupVectorCommanderUI
{
    [MenuItem("DECODER/Setup Vector Commander UI")]
    public static void SetupUI()
    {
        var canvasGO = GameObject.Find("TerminalCanvas");
        if (canvasGO == null)
        {
            Debug.LogError("[DECODER] TerminalCanvas not found in active scene!");
            return;
        }

        var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Consolas.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        // Remove old panel if it exists
        var existing = canvasGO.transform.Find("FileManagerPanel");
        if (existing != null)
        {
            GameObject.DestroyImmediate(existing.gameObject);
        }

        // 1. Root FileManagerPanel
        var panelGO = new GameObject("FileManagerPanel", typeof(RectTransform), typeof(Image), typeof(AudioSource), typeof(VectorCommander));
        panelGO.transform.SetParent(canvasGO.transform, false);

        var pRect = panelGO.GetComponent<RectTransform>();
        pRect.anchorMin = Vector2.zero;
        pRect.anchorMax = Vector2.one;
        pRect.offsetMin = Vector2.zero;
        pRect.offsetMax = Vector2.zero;

        var bgImg = panelGO.GetComponent<Image>();
        bgImg.color = new Color(0.015f, 0.045f, 0.03f, 0.98f);

        var audio = panelGO.GetComponent<AudioSource>();
        audio.playOnAwake = false;

        var vc = panelGO.GetComponent<VectorCommander>();

        // 2. Top Header Bar
        var topBar = new GameObject("TopHeaderBar", typeof(RectTransform), typeof(Image));
        topBar.transform.SetParent(panelGO.transform, false);
        var tbRect = topBar.GetComponent<RectTransform>();
        tbRect.anchorMin = new Vector2(0.015f, 0.942f);
        tbRect.anchorMax = new Vector2(0.985f, 0.988f);
        tbRect.offsetMin = Vector2.zero;
        tbRect.offsetMax = Vector2.zero;
        topBar.GetComponent<Image>().color = new Color(0.04f, 0.16f, 0.10f, 0.95f);

        var headerTitle = CreateText("HeaderTitle", topBar.transform, font, new Vector2(0.015f, 0f), new Vector2(0.75f, 1f), 24, TextAnchor.MiddleLeft, new Color(0f, 1f, 0.65f));
        headerTitle.text = "<b>ВЕКТОР-КОМАНДИР v2.1</b>   [ СЕКТОР 7 // ЭВМ «ВЕКТОР-1» ]";

        var headerClock = CreateText("HeaderClock", topBar.transform, font, new Vector2(0.75f, 0f), new Vector2(0.985f, 1f), 24, TextAnchor.MiddleRight, new Color(0.45f, 0.95f, 0.55f));
        headerClock.text = "04:30:00";

        // 3. Left Panel (C:\СЕКРЕТНО)
        var leftPanel = new GameObject("LeftPanel", typeof(RectTransform), typeof(Image));
        leftPanel.transform.SetParent(panelGO.transform, false);
        var lpRect = leftPanel.GetComponent<RectTransform>();
        lpRect.anchorMin = new Vector2(0.015f, 0.22f);
        lpRect.anchorMax = new Vector2(0.495f, 0.932f);
        lpRect.offsetMin = Vector2.zero;
        lpRect.offsetMax = Vector2.zero;
        leftPanel.GetComponent<Image>().color = new Color(0.025f, 0.08f, 0.05f, 0.95f);

        var lpTitle = CreateText("Title", leftPanel.transform, font, new Vector2(0.015f, 0.93f), new Vector2(0.985f, 0.99f), 18, TextAnchor.MiddleLeft, new Color(0f, 1f, 0.7f));
        lpTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
        var lpContent = CreateText("Content", leftPanel.transform, font, new Vector2(0.015f, 0.07f), new Vector2(0.985f, 0.92f), 20, TextAnchor.UpperLeft, new Color(0.45f, 0.85f, 0.45f));
        var lpFooter = CreateText("Footer", leftPanel.transform, font, new Vector2(0.015f, 0.01f), new Vector2(0.985f, 0.06f), 18, TextAnchor.MiddleCenter, new Color(0f, 0.85f, 0.55f));
        lpFooter.horizontalOverflow = HorizontalWrapMode.Overflow;

        // 4. Right Panel (A:\КАРТРИДЖ)
        var rightPanel = new GameObject("RightPanel", typeof(RectTransform), typeof(Image));
        rightPanel.transform.SetParent(panelGO.transform, false);
        var rpRect = rightPanel.GetComponent<RectTransform>();
        rpRect.anchorMin = new Vector2(0.505f, 0.22f);
        rpRect.anchorMax = new Vector2(0.985f, 0.932f);
        rpRect.offsetMin = Vector2.zero;
        rpRect.offsetMax = Vector2.zero;
        rightPanel.GetComponent<Image>().color = new Color(0.025f, 0.08f, 0.05f, 0.95f);

        var rpTitle = CreateText("Title", rightPanel.transform, font, new Vector2(0.015f, 0.93f), new Vector2(0.985f, 0.99f), 18, TextAnchor.MiddleLeft, new Color(0f, 1f, 0.7f));
        rpTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
        var rpContent = CreateText("Content", rightPanel.transform, font, new Vector2(0.015f, 0.07f), new Vector2(0.985f, 0.92f), 20, TextAnchor.UpperLeft, new Color(0.45f, 0.85f, 0.45f));
        var rpFooter = CreateText("Footer", rightPanel.transform, font, new Vector2(0.015f, 0.01f), new Vector2(0.985f, 0.06f), 18, TextAnchor.MiddleCenter, new Color(0f, 0.85f, 0.55f));
        rpFooter.horizontalOverflow = HorizontalWrapMode.Overflow;

        // 5. Command Line & Progress Area
        var cmdArea = new GameObject("CommandLineArea", typeof(RectTransform), typeof(Image));
        cmdArea.transform.SetParent(panelGO.transform, false);
        var cmdRect = cmdArea.GetComponent<RectTransform>();
        cmdRect.anchorMin = new Vector2(0.015f, 0.085f);
        cmdRect.anchorMax = new Vector2(0.985f, 0.21f);
        cmdRect.offsetMin = Vector2.zero;
        cmdRect.offsetMax = Vector2.zero;
        cmdArea.GetComponent<Image>().color = new Color(0.02f, 0.06f, 0.04f, 0.95f);

        var thermalText = CreateText("ThermalAlert", cmdArea.transform, font, new Vector2(0.015f, 0.66f), new Vector2(0.985f, 0.98f), 19, TextAnchor.MiddleLeft, new Color(0f, 1f, 0.55f));
        var progText = CreateText("ProgressBar", cmdArea.transform, font, new Vector2(0.015f, 0.33f), new Vector2(0.985f, 0.65f), 19, TextAnchor.MiddleLeft, new Color(0.95f, 0.95f, 0.25f));
        var cmdLine = CreateText("CommandLine", cmdArea.transform, font, new Vector2(0.015f, 0.01f), new Vector2(0.985f, 0.32f), 20, TextAnchor.MiddleLeft, new Color(0.45f, 0.95f, 0.65f));

        var lpOutline = leftPanel.AddComponent<Outline>();
        lpOutline.effectColor = new Color(0f, 0.65f, 0.40f, 0.65f);
        lpOutline.effectDistance = new Vector2(1.5f, -1.5f);

        var rpOutline = rightPanel.AddComponent<Outline>();
        rpOutline.effectColor = new Color(0f, 0.65f, 0.40f, 0.65f);
        rpOutline.effectDistance = new Vector2(1.5f, -1.5f);

        var tbOutline = topBar.AddComponent<Outline>();
        tbOutline.effectColor = new Color(0f, 0.55f, 0.35f, 0.5f);
        tbOutline.effectDistance = new Vector2(1f, -1f);

        var cmdOutline = cmdArea.AddComponent<Outline>();
        cmdOutline.effectColor = new Color(0f, 0.55f, 0.35f, 0.5f);
        cmdOutline.effectDistance = new Vector2(1f, -1f);

        // 6. Function Keys Bar (F1-F10)
        var fnBar = new GameObject("FunctionKeysBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        fnBar.transform.SetParent(panelGO.transform, false);
        var fnRect = fnBar.GetComponent<RectTransform>();
        fnRect.anchorMin = new Vector2(0.015f, 0.015f);
        fnRect.anchorMax = new Vector2(0.985f, 0.075f);
        fnRect.offsetMin = Vector2.zero;
        fnRect.offsetMax = Vector2.zero;

        var hlg = fnBar.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        var bF1 = CreateFnButton("1", "Помощь", fnBar.transform, font);
        var bF2 = CreateFnButton("2", "Статус", fnBar.transform, font);
        var bF3 = CreateFnButton("3", "Чтение", fnBar.transform, font);
        var bF4 = CreateFnButton("4", "Дешифр", fnBar.transform, font);
        var bF5 = CreateFnButton("5", "Копия", fnBar.transform, font);
        var bF8 = CreateFnButton("8", "Охлажд", fnBar.transform, font);
        var bF10 = CreateFnButton("10", "Выход", fnBar.transform, font);

        // 7. Viewer Modal (F3 Чтение)
        var vModal = new GameObject("ViewerModal", typeof(RectTransform), typeof(Image));
        vModal.transform.SetParent(panelGO.transform, false);
        var vmRect = vModal.GetComponent<RectTransform>();
        vmRect.anchorMin = new Vector2(0.06f, 0.12f);
        vmRect.anchorMax = new Vector2(0.94f, 0.88f);
        vmRect.offsetMin = Vector2.zero;
        vmRect.offsetMax = Vector2.zero;
        vModal.GetComponent<Image>().color = new Color(0.015f, 0.06f, 0.04f, 0.99f);

        var vmTitle = CreateText("ViewerTitle", vModal.transform, font, new Vector2(0.02f, 0.92f), new Vector2(0.98f, 0.98f), 20, TextAnchor.MiddleCenter, new Color(0f, 1f, 0.75f));
        var vmBody = CreateText("ViewerBody", vModal.transform, font, new Vector2(0.03f, 0.14f), new Vector2(0.97f, 0.90f), 20, TextAnchor.UpperLeft, new Color(0.6f, 0.95f, 0.75f));

        var vmCloseBtn = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        vmCloseBtn.transform.SetParent(vModal.transform, false);
        var cbRect = vmCloseBtn.GetComponent<RectTransform>();
        cbRect.anchorMin = new Vector2(0.35f, 0.03f);
        cbRect.anchorMax = new Vector2(0.65f, 0.10f);
        cbRect.offsetMin = Vector2.zero;
        cbRect.offsetMax = Vector2.zero;
        vmCloseBtn.GetComponent<Image>().color = new Color(0.08f, 0.32f, 0.18f, 1f);
        var cbTxt = CreateText("Text", vmCloseBtn.transform, font, Vector2.zero, Vector2.one, 18, TextAnchor.MiddleCenter, Color.white);
        cbTxt.text = "[ ЗАКРЫТЬ ПРОСМОТР [ESC] ]";
        vmCloseBtn.GetComponent<Button>().onClick.AddListener(() => vc.CloseViewer());
        vModal.SetActive(false);

        // 8. Help Modal (F1)
        var hModal = new GameObject("HelpModal", typeof(RectTransform), typeof(Image));
        hModal.transform.SetParent(panelGO.transform, false);
        var hmRect = hModal.GetComponent<RectTransform>();
        hmRect.anchorMin = new Vector2(0.10f, 0.14f);
        hmRect.anchorMax = new Vector2(0.90f, 0.86f);
        hmRect.offsetMin = Vector2.zero;
        hmRect.offsetMax = Vector2.zero;
        hModal.GetComponent<Image>().color = new Color(0.015f, 0.06f, 0.04f, 0.99f);

        var hmTitle = CreateText("HelpTitle", hModal.transform, font, new Vector2(0.02f, 0.91f), new Vector2(0.98f, 0.98f), 22, TextAnchor.MiddleCenter, new Color(0f, 1f, 0.75f));
        hmTitle.text = "<b>╔════════ ПОМОЩЬ // «ВЕКТОР-КОМАНДИР» v2.1 ════════╗</b>";

        var hmBody = CreateText("HelpBody", hModal.transform, font, new Vector2(0.04f, 0.14f), new Vector2(0.96f, 0.89f), 20, TextAnchor.UpperLeft, new Color(0.7f, 0.95f, 0.75f));
        hmBody.text =
            "<color=#00FFAA><b>НАВИГАЦИЯ И УПРАВЛЕНИЕ:</b></color>\n" +
            "• [Стрелки Вверх / Вниз] — Выбор файла в каталоге базы\n" +
            "• [Tab]                  — Переключение панелей (C: база / A: картридж)\n" +
            "• [F3 / 3] или [Enter]   — Просмотр директивы / документа\n" +
            "• [F5 / 5]               — Перекачка выбранного файла на картридж\n" +
            "• [F8 / 8]               — Проверка системы охлаждения дисков\n" +
            "• [F10 / 10 / Esc]       — Возврат на стартовый экран ЭВМ\n\n" +
            "<color=#FFCC00><b>ПРАВИЛА ТЕПЛОВОГО РЕЖИМА ДИСКА:</b></color>\n" +
            "Во время перекачки данных картридж в мейнфрейме нагревается!\n" +
            "При 75°C подготовьтесь к замене, при 90°C немедленно извлеките диск,\n" +
            "иначе данные будут уничтожены током перегрузки.";

        var hmCloseBtn = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        hmCloseBtn.transform.SetParent(hModal.transform, false);
        var hcbRect = hmCloseBtn.GetComponent<RectTransform>();
        hcbRect.anchorMin = new Vector2(0.35f, 0.03f);
        hcbRect.anchorMax = new Vector2(0.65f, 0.10f);
        hcbRect.offsetMin = Vector2.zero;
        hcbRect.offsetMax = Vector2.zero;
        hmCloseBtn.GetComponent<Image>().color = new Color(0.08f, 0.32f, 0.18f, 1f);
        var hcbTxt = CreateText("Text", hmCloseBtn.transform, font, Vector2.zero, Vector2.one, 18, TextAnchor.MiddleCenter, Color.white);
        hcbTxt.text = "[ ПОНЯТНО [ESC] ]";
        hmCloseBtn.GetComponent<Button>().onClick.AddListener(() => hModal.SetActive(false));
        hModal.SetActive(false);

        // 9. Wire serialized fields
        var so = new SerializedObject(vc);
        so.FindProperty("headerTitleText").objectReferenceValue = headerTitle;
        so.FindProperty("headerClockText").objectReferenceValue = headerClock;
        so.FindProperty("leftPanelTitleText").objectReferenceValue = lpTitle;
        so.FindProperty("leftPanelContentText").objectReferenceValue = lpContent;
        so.FindProperty("leftPanelFooterText").objectReferenceValue = lpFooter;
        so.FindProperty("rightPanelTitleText").objectReferenceValue = rpTitle;
        so.FindProperty("rightPanelContentText").objectReferenceValue = rpContent;
        so.FindProperty("rightPanelFooterText").objectReferenceValue = rpFooter;
        so.FindProperty("commandLineText").objectReferenceValue = cmdLine;
        so.FindProperty("progressBarText").objectReferenceValue = progText;
        so.FindProperty("thermalAlertText").objectReferenceValue = thermalText;
        so.FindProperty("viewerModal").objectReferenceValue = vModal;
        so.FindProperty("viewerTitleText").objectReferenceValue = vmTitle;
        so.FindProperty("viewerBodyText").objectReferenceValue = vmBody;
        so.FindProperty("viewerCloseButton").objectReferenceValue = vmCloseBtn.GetComponent<Button>();
        so.FindProperty("helpModal").objectReferenceValue = hModal;
        so.FindProperty("helpCloseButton").objectReferenceValue = hmCloseBtn.GetComponent<Button>();
        so.FindProperty("btnF1").objectReferenceValue = bF1;
        so.FindProperty("btnF2").objectReferenceValue = bF2;
        so.FindProperty("btnF3").objectReferenceValue = bF3;
        so.FindProperty("btnF4").objectReferenceValue = bF4;
        so.FindProperty("btnF5").objectReferenceValue = bF5;
        so.FindProperty("btnF8").objectReferenceValue = bF8;
        so.FindProperty("btnF10").objectReferenceValue = bF10;
        so.FindProperty("audioSource").objectReferenceValue = audio;
        so.ApplyModifiedProperties();

        // 10. Connect start screen to file manager panel
        var startScreenGO = canvasGO.transform.Find("StartScreenPanel");
        if (startScreenGO != null)
        {
            var startScreen = startScreenGO.GetComponent<SpyDecryptionStartScreen>();
            if (startScreen != null)
            {
                var startSo = new SerializedObject(startScreen);
                var fmpProp = startSo.FindProperty("fileManagerPanel");
                if (fmpProp != null)
                {
                    fmpProp.objectReferenceValue = panelGO;
                    startSo.ApplyModifiedProperties();
                }
            }
        }

        // Start screen is active first, file manager is inactive until player interacts
        panelGO.SetActive(false);

        EditorSceneManager.MarkSceneDirty(canvasGO.scene);
        EditorSceneManager.SaveScene(canvasGO.scene);
        Debug.Log("[DECODER] Vector Commander UI successfully created and saved!");
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
        return t;
    }

    private static Button CreateFnButton(string num, string name, Transform parent, Font font)
    {
        var bGo = new GameObject(num + name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        bGo.transform.SetParent(parent, false);
        var img = bGo.GetComponent<Image>();
        img.color = new Color(0.035f, 0.14f, 0.08f, 0.95f);
        var outline = bGo.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0.5f, 0.3f, 0.7f);
        outline.effectDistance = new Vector2(1f, -1f);

        var btn = bGo.GetComponent<Button>();
        var cb = btn.colors;
        cb.normalColor = new Color(0.035f, 0.14f, 0.08f, 0.95f);
        cb.highlightedColor = new Color(0.08f, 0.38f, 0.20f, 1f);
        cb.pressedColor = new Color(0.15f, 0.65f, 0.35f, 1f);
        btn.colors = cb;

        var txt = CreateText("Text", bGo.transform, font, Vector2.zero, Vector2.one, 19, TextAnchor.MiddleCenter, Color.white);
        txt.text = $"<b><color=#00FFAA> {num} </color></b><color=#DDFFAA>{name}</color>";
        return btn;
    }
}
