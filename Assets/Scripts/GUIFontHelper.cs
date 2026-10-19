using UnityEngine;

/// <summary>
/// Ensures GUI.skin uses a font with full Cyrillic support (Consolas) in WebGL / standalone builds,
/// preventing broken/missing characters in OnGUI overlays and prompts.
/// </summary>
public static class GUIFontHelper
{
    private static Font _cyrillicFont;
    private static bool _initialized = false;

    public static Font CyrillicFont
    {
        get
        {
            if (!_initialized)
            {
                _initialized = true;
                _cyrillicFont = Resources.Load<Font>("Consolas");
                if (_cyrillicFont == null)
                {
                    // Fallback to Arial if available
                    _cyrillicFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
            }
            return _cyrillicFont;
        }
    }

    /// <summary>
    /// Applies Cyrillic font to GUI.skin if not already set. Call at the start of any OnGUI.
    /// </summary>
    public static void Apply()
    {
        var font = CyrillicFont;
        if (font != null && GUI.skin.font != font)
        {
            GUI.skin.font = font;
        }
    }
}
