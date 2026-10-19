using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Renders the Lamp (Light/Breaker) and Eye (Visibility/Line-of-Sight) stealth status indicators
/// when a raid or guard inspection is imminent or active.
/// 
/// Indicators:
/// 1. Lamp:
///    - Red Lamp: Power/Light is ON (danger - breaker not cut).
///    - Green Lamp: Power is CUT / Light is OFF (safe).
/// 2. Eye:
///    - Red Open Eye: Player is VISIBLE to guards (open sight line, sitting in chair, or running).
///    - Green Closed Eye: Player is HIDDEN in cover (behind server rack / obstacle).
/// </summary>
public static class StealthIndicatorUI
{
    private static Texture2D _lampOnTex;
    private static Texture2D _lampOffTex;
    private static Texture2D _eyeOpenTex;
    private static Texture2D _eyeClosedTex;
    private static bool _texturesInitialized = false;

    // Doorway eye observation point for checking line-of-sight from the hall
    private static readonly Vector3 DefaultDoorwayEyePos = new Vector3(9.67f, 1.65f, 0.30f);

    public static bool IsTexturesReady => _texturesInitialized;

    /// <summary>
    /// Checks whether power is cut and lights are safely disabled.
    /// Safe = True (Green), Dangerous/Active = False (Red).
    /// </summary>
    public static bool IsLightSafe()
    {
        if (MainframeBootQuest.Instance == null) return true;
        // Power is safe if breaker is cut or system is still completely powered off
        return MainframeBootQuest.Instance.IsPowerCutByBreaker || 
               MainframeBootQuest.Instance.CurrentStage == BootStage.PowerOff;
    }

    /// <summary>
    /// Checks whether the player is currently hidden from the doorway/guard view.
    /// Safe/Hidden = True (Green Closed Eye), Exposed/Visible = False (Red Open Eye).
    /// </summary>
    public static bool IsPlayerHidden(out string reasonIfNotHidden)
    {
        reasonIfNotHidden = "";

        if (PlayerController.Instance == null)
        {
            reasonIfNotHidden = LocalizationManager.T("Игрок не найден", "Player not found");
            return false;
        }

        // 1. Sitting in the operator chair in full view
        if (PlayerController.Instance.IsSitting)
        {
            reasonIfNotHidden = LocalizationManager.T("В кресле оператора", "In operator chair");
            return false;
        }

        // 2. Running produces footstep noise and movement
        if (PlayerController.Instance.IsRunning)
        {
            reasonIfNotHidden = LocalizationManager.T("Бег / шум шагов", "Running / Footstep noise");
            return false;
        }

        // 3. Determine vantage point: use active inspecting guard if present, else doorway
        Vector3 guardEye = DefaultDoorwayEyePos;
        var guards = Object.FindObjectsByType<GuardPawn>(FindObjectsInactive.Exclude);
        if (guards != null)
        {
            foreach (var g in guards)
            {
                if (g == null) continue;
                if (g.CurrentState == GuardPawn.GuardState.InspectingRoomDoorway ||
                    g.CurrentState == GuardPawn.GuardState.BreachEnteringRoom ||
                    g.CurrentState == GuardPawn.GuardState.BreachOpeningDoor)
                {
                    guardEye = g.transform.position + Vector3.up * 1.65f;
                    break;
                }
            }
        }

        Vector3 pPos = PlayerController.Instance.transform.position;
        Vector3 pChest = pPos + Vector3.up * 1.10f;
        Vector3 pHead = pPos + Vector3.up * 1.65f;

        // Proximity check: too close to the doorway
        float distToGuard = Vector3.Distance(guardEye, pPos);
        if (distToGuard < 2.20f)
        {
            reasonIfNotHidden = LocalizationManager.T("Слишком близко к двери!", "Too close to doorway!");
            return false;
        }

        // Line-of-sight check 1: Chest
        bool hitObstacleChest = Physics.Linecast(guardEye, pChest, out RaycastHit hitChest, ~0, QueryTriggerInteraction.Ignore);
        bool chestVisible = !hitObstacleChest || (hitChest.collider != null && hitChest.collider.transform.root == PlayerController.Instance.transform);

        // Line-of-sight check 2: Head
        bool hitObstacleHead = Physics.Linecast(guardEye, pHead, out RaycastHit hitHead, ~0, QueryTriggerInteraction.Ignore);
        bool headVisible = !hitObstacleHead || (hitHead.collider != null && hitHead.collider.transform.root == PlayerController.Instance.transform);

        if (chestVisible || headVisible)
        {
            reasonIfNotHidden = LocalizationManager.T("На линии взгляда охраны", "In guard's line of sight");
            return false;
        }

        reasonIfNotHidden = LocalizationManager.T("Вне видимости охраны", "Out of guard's sight");
        return true;
    }

    /// <summary>
    /// Checks whether a raid or guard inspection is imminent or currently underway.
    /// </summary>
    public static bool IsRaidImminentOrActive(out float highestSuspicion, out string stateTitle)
    {
        highestSuspicion = 0f;
        stateTitle = "";

        // If cheat mode is enabled, always allow the player to see indicators to test hiding spots!
        if (GuardPawn.IsCheatPeaceMode)
        {
            highestSuspicion = 1.0f;
            stateTitle = LocalizationManager.T("ТЕСТ МАСКИРОВКИ (ЧИТ: GreeH)", "STEALTH TEST (CHEAT: GreeH)");
            return true;
        }

        var guards = Object.FindObjectsByType<GuardPawn>(FindObjectsInactive.Exclude);
        if (guards == null || guards.Length == 0) return false;

        foreach (var g in guards)
        {
            if (g == null) continue;
            if (g.Suspicion > highestSuspicion)
            {
                highestSuspicion = g.Suspicion;
            }

            if (g.CurrentState == GuardPawn.GuardState.InspectingRoomDoorway)
            {
                stateTitle = LocalizationManager.T("ОХРАННИК ОСМАТРИВАЕТ ЗАЛ В ДВЕРЯХ!", "GUARD INSPECTING ROOM AT DOORWAY!");
                return true;
            }
            if (g.CurrentState == GuardPawn.GuardState.BreachOpeningDoor || g.CurrentState == GuardPawn.GuardState.BreachEnteringRoom)
            {
                stateTitle = LocalizationManager.T("ОХРАНА ВЗЛАМЫВАЕТ ДВЕРЬ!", "GUARDS BREACHING DOORWAY!");
                return true;
            }
            if (g.CurrentState == GuardPawn.GuardState.BreachMovingToDoor)
            {
                stateTitle = LocalizationManager.T("ОХРАНА ПОДХОДИТ К ДВЕРИ (РЕЙД)!", "GUARDS APPROACHING DOOR (RAID)!");
                return true;
            }
            if (g.CurrentState == GuardPawn.GuardState.ClosingDoorReturning)
            {
                stateTitle = LocalizationManager.T("ОХРАНА УХОДИТ (МАСКИРОВКА УСПЕШНА)", "GUARDS LEAVING (STEALTH SUCCESS)");
                return true;
            }
            if (g.IsTerminalInspectionActive)
            {
                stateTitle = LocalizationManager.T("ТРЕВОГА ТЕРМИНАЛА: ОХРАНА ИДЁТ С ПРОВЕРКОЙ!", "TERMINAL ALERT: GUARD EN ROUTE TO INSPECT!");
                return true;
            }
            if (g.Suspicion >= 0.35f)
            {
                stateTitle = LocalizationManager.T($"ОХРАНА ВСТРЕВОЖЕНА (ТРЕВОГА {(g.Suspicion * 100f):F0}%)", $"GUARDS ON ALERT (SUSPICION {(g.Suspicion * 100f):F0}%)");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Renders the stealth status badges at top-center of the screen.
    /// Call inside OnGUI.
    /// </summary>
    public static void DrawHUD()
    {
        if (GuardPawn.IsGameOver || GameVictorySystem.IsGameWon) return;

        bool showBadge = IsRaidImminentOrActive(out float suspicion, out string raidDesc);
        if (!showBadge) return;

        EnsureTextures();
        GUIFontHelper.Apply();

        float screenW = Screen.width;
        float panelW = 460f;
        float panelH = 88f;
        float panelX = (screenW - panelW) * 0.5f;
        float panelY = 16f;

        bool lightSafe = IsLightSafe();
        bool playerHidden = IsPlayerHidden(out string hiddenFailReason);
        bool allSafe = lightSafe && playerHidden;

        // Pulsing border color
        float pulse = 0.85f + Mathf.PingPong(Time.time * 2.5f, 0.15f);
        Color borderColor = allSafe 
            ? new Color(0.1f * pulse, 1.0f * pulse, 0.65f * pulse, 0.95f) 
            : new Color(1.0f * pulse, 0.25f * pulse, 0.25f * pulse, 0.95f);

        // Background box
        Rect bgRect = new Rect(panelX, panelY, panelW, panelH);
        GUI.color = borderColor;
        GUI.Box(bgRect, "");
        GUI.color = new Color(0.06f, 0.08f, 0.07f, 0.92f);
        GUI.DrawTexture(new Rect(panelX + 2, panelY + 2, panelW - 4, panelH - 4), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Top Header
        var titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = allSafe ? new Color(0.3f, 1.0f, 0.7f) : new Color(1.0f, 0.85f, 0.3f);
        GUI.Label(new Rect(panelX, panelY + 4f, panelW, 18f), $"★ {raidDesc} ★", titleStyle);

        // Separator line
        GUI.color = new Color(borderColor.r, borderColor.g, borderColor.b, 0.45f);
        GUI.DrawTexture(new Rect(panelX + 10, panelY + 23f, panelW - 20, 1), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Left Section: Lamp Indicator (Light / Breaker)
        float leftX = panelX + 16f;
        float iconY = panelY + 27f;
        float iconSize = 42f;

        Texture2D lampTex = lightSafe ? _lampOffTex : _lampOnTex;
        if (lampTex != null)
        {
            GUI.DrawTexture(new Rect(leftX, iconY, iconSize, iconSize), lampTex);
        }

        var labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };

        float leftTextX = leftX + iconSize + 8f;
        string lampTitle = lightSafe 
            ? LocalizationManager.T("<color=#00FFAA>СВЕТ: ВЫКЛЮЧЕН</color>", "<color=#00FFAA>LIGHT: OFF</color>") 
            : LocalizationManager.T("<color=#FF3333>СВЕТ: ВКЛЮЧЕН !</color>", "<color=#FF3333>LIGHT: ON !</color>");
        string lampSub = lightSafe 
            ? LocalizationManager.T("<size=10><color=#AADDCC>Сеть обесточена [РУБИЛЬНИК]</color></size>", "<size=10><color=#AADDCC>Power cut [BREAKER]</color></size>") 
            : LocalizationManager.T("<size=10><color=#FFAAAA>Опустите рубильник [E]!</color></size>", "<size=10><color=#FFAAAA>Turn off breaker [E]!</color></size>");
        GUI.Label(new Rect(leftTextX, iconY - 2f, 140f, 20f), lampTitle, labelStyle);
        GUI.Label(new Rect(leftTextX, iconY + 16f, 140f, 22f), lampSub, labelStyle);

        // Middle Divider
        GUI.color = new Color(1f, 1f, 1f, 0.18f);
        GUI.DrawTexture(new Rect(panelX + 225f, panelY + 26f, 1, 38f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Right Section: Eye Indicator (Visibility / LoS)
        float rightX = panelX + 238f;
        Texture2D eyeTex = playerHidden ? _eyeClosedTex : _eyeOpenTex;
        if (eyeTex != null)
        {
            GUI.DrawTexture(new Rect(rightX, iconY, iconSize, iconSize), eyeTex);
        }

        float rightTextX = rightX + iconSize + 8f;
        string eyeTitle = playerHidden 
            ? LocalizationManager.T("<color=#00FFAA>ОБЗОР: В УКРЫТИИ</color>", "<color=#00FFAA>LOS: IN COVER</color>") 
            : LocalizationManager.T("<color=#FF3333>ОБЗОР: ВИДЕН !</color>", "<color=#FF3333>LOS: EXPOSED !</color>");
        string eyeSub = playerHidden 
            ? LocalizationManager.T("<size=10><color=#AADDCC>Вне поля зрения [ЗА СТОЙКОЙ]</color></size>", "<size=10><color=#AADDCC>Out of sight [BEHIND RACK]</color></size>") 
            : $"<size=10><color=#FFAAAA>{hiddenFailReason}</color></size>";
        GUI.Label(new Rect(rightTextX, iconY - 2f, 160f, 20f), eyeTitle, labelStyle);
        GUI.Label(new Rect(rightTextX, iconY + 16f, 160f, 22f), eyeSub, labelStyle);

        // Bottom Footer status
        var footerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        string footerMsg = allSafe 
            ? LocalizationManager.T("<color=#00FFAA>✔ МАСКИРОВКА НАДЁЖНА — НЕ ШЕВЕЛИТЕСЬ В УКРЫТИИ</color>", "<color=#00FFAA>✔ STEALTH SECURE — STAY STILL IN COVER</color>") 
            : LocalizationManager.T("<color=#FFDD44>⚠ ВЫКЛЮЧИТЕ РУБИЛЬНИК [E] И СПРЯЧЬТЕСЬ ЗА СТОЙКУ ЭВМ!</color>", "<color=#FFDD44>⚠ CUT BREAKER [E] AND HIDE BEHIND MAINFRAME!</color>");
        GUI.Label(new Rect(panelX, panelY + panelH - 20f, panelW, 18f), footerMsg, footerStyle);
    }

    /// <summary>
    /// Generates high quality 64x64 procedural textures for Lamp and Eye icons.
    /// </summary>
    private static void EnsureTextures()
    {
        if (_texturesInitialized) return;

        _lampOnTex = CreateLampTexture(isTurnedOn: true);
        _lampOffTex = CreateLampTexture(isTurnedOn: false);
        _eyeOpenTex = CreateEyeTexture(isOpen: true);
        _eyeClosedTex = CreateEyeTexture(isOpen: false);

        _texturesInitialized = true;
    }

    private static Texture2D CreateLampTexture(bool isTurnedOn)
    {
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color[] cols = new Color[size * size];
        Vector2 bulbCenter = new Vector2(32f, 38f);
        float bulbRadius = 13.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int idx = y * size + x;
                cols[idx] = Color.clear;

                Vector2 pos = new Vector2(x + 0.5f, y + 0.5f);
                float distToBulb = Vector2.Distance(pos, bulbCenter);

                if (isTurnedOn)
                {
                    // === RED GLOWING LAMP (LIGHT ON / DANGER) ===
                    // Soft outer radial halo
                    if (distToBulb < 28f)
                    {
                        float halo = Mathf.Clamp01(1f - (distToBulb / 28f));
                        cols[idx] = new Color(1.0f, 0.15f, 0.10f, halo * 0.45f);
                    }

                    // Radiant warning rays
                    Vector2 dir = pos - bulbCenter;
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    if (distToBulb >= 16f && distToBulb <= 27f)
                    {
                        for (int ray = 0; ray < 8; ray++)
                        {
                            float rayAngle = ray * 45f;
                            float angleDiff = Mathf.Abs(Mathf.DeltaAngle(angle, rayAngle));
                            if (angleDiff < 4.0f)
                            {
                                float rayIntensity = (1f - angleDiff / 4.0f) * (1f - (distToBulb - 16f) / 11f);
                                cols[idx] = Color.Lerp(cols[idx], new Color(1.0f, 0.4f, 0.1f, 0.85f), rayIntensity);
                            }
                        }
                    }

                    // Glass bulb interior
                    if (distToBulb <= bulbRadius)
                    {
                        float innerRatio = distToBulb / bulbRadius;
                        // Bright yellow core fading to vivid red glass rim
                        Color bulbColor = Color.Lerp(new Color(1.0f, 0.95f, 0.5f, 1.0f), new Color(0.95f, 0.15f, 0.15f, 1.0f), innerRatio);
                        cols[idx] = bulbColor;
                    }
                    else if (distToBulb <= bulbRadius + 1.5f)
                    {
                        // Crisp bulb outline
                        cols[idx] = new Color(1.0f, 0.35f, 0.35f, 1.0f);
                    }

                    // Bulb neck tapering down to base
                    if (y >= 20 && y <= 27 && Mathf.Abs(x - 32) <= 7 - (27 - y) * 0.3f)
                    {
                        cols[idx] = new Color(0.95f, 0.2f, 0.15f, 1.0f);
                    }

                    // Screw socket base (metallic steel ridges)
                    if (y >= 13 && y <= 20 && Mathf.Abs(x - 32) <= 6)
                    {
                        bool ridge = (y % 2 == 0);
                        cols[idx] = ridge ? new Color(0.75f, 0.70f, 0.65f, 1.0f) : new Color(0.40f, 0.38f, 0.35f, 1.0f);
                    }
                    // Bottom contact point
                    if (y >= 9 && y <= 12 && Mathf.Abs(x - 32) <= 3)
                    {
                        cols[idx] = new Color(0.25f, 0.25f, 0.25f, 1.0f);
                    }
                }
                else
                {
                    // === GREEN QUIET LAMP (LIGHT CUT / SAFE) ===
                    // Soft soothing green halo
                    if (distToBulb < 24f)
                    {
                        float halo = Mathf.Clamp01(1f - (distToBulb / 24f));
                        cols[idx] = new Color(0.1f, 1.0f, 0.65f, halo * 0.25f);
                    }

                    // Glass bulb interior (cool muted emerald green)
                    if (distToBulb <= bulbRadius)
                    {
                        float innerRatio = distToBulb / bulbRadius;
                        Color glassColor = Color.Lerp(new Color(0.12f, 0.45f, 0.30f, 0.92f), new Color(0.08f, 0.30f, 0.20f, 0.95f), innerRatio);
                        cols[idx] = glassColor;
                    }
                    else if (distToBulb <= bulbRadius + 1.5f)
                    {
                        // Crisp vivid mint-green outline
                        cols[idx] = new Color(0.2f, 1.0f, 0.7f, 1.0f);
                    }

                    // Filament (turned off, cool green filament wire)
                    if (distToBulb <= 7f && y >= 34 && y <= 42)
                    {
                        if (Mathf.Abs(x - 32) <= 4 && (y == 38 || Mathf.Abs(x - 32) == 4))
                        {
                            cols[idx] = new Color(0.3f, 0.9f, 0.6f, 1.0f);
                        }
                    }

                    // Bulb neck
                    if (y >= 20 && y <= 27 && Mathf.Abs(x - 32) <= 7 - (27 - y) * 0.3f)
                    {
                        cols[idx] = new Color(0.12f, 0.5f, 0.35f, 1.0f);
                    }

                    // Screw socket base
                    if (y >= 13 && y <= 20 && Mathf.Abs(x - 32) <= 6)
                    {
                        bool ridge = (y % 2 == 0);
                        cols[idx] = ridge ? new Color(0.35f, 0.75f, 0.55f, 1.0f) : new Color(0.20f, 0.45f, 0.35f, 1.0f);
                    }
                    // Bottom contact point
                    if (y >= 9 && y <= 12 && Mathf.Abs(x - 32) <= 3)
                    {
                        cols[idx] = new Color(0.15f, 0.30f, 0.22f, 1.0f);
                    }
                }
            }
        }

        tex.SetPixels(cols);
        tex.Apply();
        return tex;
    }

    private static Texture2D CreateEyeTexture(bool isOpen)
    {
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color[] cols = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int idx = y * size + x;
                cols[idx] = Color.clear;

                float dx = x - 32f;
                float dy = y - 32f;

                if (isOpen)
                {
                    // === RED OPEN EYE (VISIBLE / EXPOSED / DANGER) ===
                    // Almond shape bounding formula: upper and lower eyelid curves
                    float halfWidth = 23f;
                    float halfHeight = 15f;

                    if (Mathf.Abs(dx) <= halfWidth)
                    {
                        float normalizedX = dx / halfWidth;
                        float curve = (1f - (normalizedX * normalizedX));
                        float eyeTop = 32f + halfHeight * curve;
                        float eyeBottom = 32f - halfHeight * curve;

                        // Soft red ambient alert glow outside
                        float distCenter = Mathf.Sqrt(dx * dx + dy * dy);
                        if (distCenter <= 28f)
                        {
                            float halo = Mathf.Clamp01(1f - distCenter / 28f);
                            cols[idx] = new Color(1.0f, 0.15f, 0.15f, halo * 0.35f);
                        }

                        // Inside eyeball
                        if (y >= eyeBottom && y <= eyeTop)
                        {
                            // Sclera (eyeball white with slight menacing reddish/cream tint)
                            cols[idx] = new Color(0.96f, 0.88f, 0.88f, 1.0f);

                            // Iris: vivid Soviet danger red
                            float distFromIris = Mathf.Sqrt(dx * dx + dy * dy);
                            if (distFromIris <= 9.0f)
                            {
                                cols[idx] = new Color(0.92f, 0.10f, 0.10f, 1.0f);

                                // Inner iris ring
                                if (distFromIris <= 6.5f)
                                {
                                    cols[idx] = new Color(0.75f, 0.05f, 0.05f, 1.0f);
                                }

                                // Pupil (pitch black)
                                if (distFromIris <= 4.2f)
                                {
                                    cols[idx] = new Color(0.04f, 0.04f, 0.04f, 1.0f);
                                }

                                // Specular highlight glint (bright white)
                                if (dx >= -4f && dx <= -1.5f && dy >= 2f && dy <= 4.5f)
                                {
                                    cols[idx] = Color.white;
                                }
                            }
                        }
                        else if (Mathf.Abs(y - eyeTop) <= 2.0f || Mathf.Abs(y - eyeBottom) <= 2.0f)
                        {
                            // Eyelid borders (dark red/crimson outline)
                            cols[idx] = new Color(0.85f, 0.15f, 0.15f, 1.0f);
                        }
                    }
                }
                else
                {
                    // === GREEN CLOSED EYE (HIDDEN / OUT OF SIGHT / SAFE) ===
                    // Soft green peaceful halo
                    float distCenter = Mathf.Sqrt(dx * dx + dy * dy);
                    if (distCenter <= 26f)
                    {
                        float halo = Mathf.Clamp01(1f - distCenter / 26f);
                        cols[idx] = new Color(0.1f, 1.0f, 0.65f, halo * 0.25f);
                    }

                    // Downward curving closed eyelid arc: y = 35 - 10 * (1 - (dx/21)^2)
                    float halfWidth = 21f;
                    if (Mathf.Abs(dx) <= halfWidth)
                    {
                        float normalizedX = dx / halfWidth;
                        float curve = (1f - (normalizedX * normalizedX));
                        float lidY = 35f - 10f * curve; // dips downward in center (closed eye)

                        float distToLid = Mathf.Abs(y - lidY);
                        if (distToLid <= 2.0f)
                        {
                            float alpha = 1f - (distToLid / 2.0f);
                            cols[idx] = new Color(0.15f, 1.0f, 0.65f, alpha);
                        }

                        // 5 Lashes pointing downward from the closed eyelid
                        float[] lashXs = new float[] { -15f, -8f, 0f, 8f, 15f };
                        foreach (float lx in lashXs)
                        {
                            if (Mathf.Abs(dx - lx) <= 1.2f)
                            {
                                float curLidY = 35f - 10f * (1f - (lx / halfWidth) * (lx / halfWidth));
                                if (y <= curLidY && y >= curLidY - 7f)
                                {
                                    cols[idx] = new Color(0.2f, 1.0f, 0.7f, 1.0f);
                                }
                            }
                        }
                    }
                }
            }
        }

        tex.SetPixels(cols);
        tex.Apply();
        return tex;
    }
}
