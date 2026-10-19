using System;
using UnityEngine;

public enum GameLanguage
{
    Russian = 0,
    English = 1
}

/// <summary>
/// Centralized bilingual localization system (Russian / English).
/// Persists user choice in PlayerPrefs and notifies listeners upon language change.
/// </summary>
public static class LocalizationManager
{
    private const string PrefKey = "DECODER_LANGUAGE";
    private static GameLanguage currentLanguage = GameLanguage.Russian;
    private static bool isInitialized = false;

    public static event Action OnLanguageChanged;

    public static GameLanguage CurrentLanguage
    {
        get
        {
            EnsureInitialized();
            return currentLanguage;
        }
        set
        {
            EnsureInitialized();
            if (currentLanguage != value)
            {
                currentLanguage = value;
                PlayerPrefs.SetInt(PrefKey, (int)currentLanguage);
                PlayerPrefs.Save();
                OnLanguageChanged?.Invoke();
            }
        }
    }

    public static bool IsEnglish => CurrentLanguage == GameLanguage.English;

    private static void EnsureInitialized()
    {
        if (isInitialized) return;
        currentLanguage = (GameLanguage)PlayerPrefs.GetInt(PrefKey, (int)GameLanguage.Russian);
        isInitialized = true;
    }

    /// <summary>
    /// Returns the text corresponding to the currently selected language.
    /// </summary>
    public static string T(string ru, string en)
    {
        EnsureInitialized();
        return currentLanguage == GameLanguage.English ? en : ru;
    }

    public static void ToggleLanguage()
    {
        CurrentLanguage = (CurrentLanguage == GameLanguage.Russian) ? GameLanguage.English : GameLanguage.Russian;
    }

    private static readonly System.Collections.Generic.Dictionary<string, string> PromptDictionary =
        new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // Breaker
        { "Нажмите [E / ЛКМ] — Включить главный рубильник питания", "Press [E / LMB] — Turn on main power breaker" },
        { "Нажмите [E / ЛКМ] — Отключить главный рубильник питания", "Press [E / LMB] — Turn off main power breaker" },
        { "Нажмите [E / ЛКМ] — Выключить главный рубильник питания", "Press [E / LMB] — Turn off main power breaker" },
        { "[E] Включить главный рубильник (Электрощиток)", "[E] Turn on main breaker (Power Panel)" },
        { "[E] Выключить главный рубильник (Электрощиток)", "[E] Turn off main breaker (Power Panel)" },
        { "[E] Отключить главный рубильник (Электрощиток)", "[E] Turn off main breaker (Power Panel)" },

        // Filament & Vacuum tubes
        { "[ЛКМ / E] Переключить тумблер накала ламп I", "[LMB / E] Toggle tube filament switch I" },
        { "[ЛКМ / E] Переключить тумблер накала ламп II", "[LMB / E] Toggle tube filament switch II" },
        { "[E] Включить накал ламп (FILAMENT)", "[E] Turn on tube filament (FILAMENT)" },
        { "[E] Выключить накал ламп (FILAMENT)", "[E] Turn off tube filament (FILAMENT)" },

        // High Voltage
        { "Тумблер высокого напряжения (HV)", "High voltage toggle switch (HV)" },
        { "[E] Включить высокое напряжение (HV)", "[E] Turn on high voltage (HV)" },
        { "[E] Снять высокое напряжение (Standby)", "[E] Cut high voltage (Standby)" },
        { "Нажмите [E] — Снять анодное напряжение (Горячий резерв / Standby)", "Press [E] — Cut anode voltage (Hot Standby)" },
        { "Нажмите [E] — Подать анодное напряжение (Быстрый запуск ЭВМ)", "Press [E] — Apply anode voltage (Quick Boot)" },
        { "Нажмите [E] — Подать высокое напряжение (HV)", "Press [E] — Apply high voltage (HV)" },

        // Bus switches
        { "[ЛКМ / E] Переключить тумблер шины #1", "[LMB / E] Toggle bus switch #1" },
        { "[ЛКМ / E] Переключить тумблер шины #2", "[LMB / E] Toggle bus switch #2" },
        { "[ЛКМ / E] Переключить тумблер шины #3", "[LMB / E] Toggle bus switch #3" },
        { "[ЛКМ / E] Переключить тумблер шины #4", "[LMB / E] Toggle bus switch #4" },
        { "[E] Переключить тумблер шины #1", "[E] Toggle bus switch #1" },
        { "[E] Переключить тумблер шины #2", "[E] Toggle bus switch #2" },
        { "[E] Переключить тумблер шины #3", "[E] Toggle bus switch #3" },
        { "[E] Переключить тумблер шины #4", "[E] Toggle bus switch #4" },

        // IPL Start Button
        { "[ЛКМ / E] Нажать кнопку [ПУСК / START IPL]", "[LMB / E] Press button [START IPL]" },
        { "[E] Нажать [START IPL] (Запуск ЭВМ)", "[E] Press [START IPL] (Boot Mainframe)" },

        // Magnetic Tape
        { "[ЛКМ / E] Вставить бобину с ядром ОС в накопитель", "[LMB / E] Insert OS kernel tape reel into drive" },
        { "[E] Вставить бобину ленты ядра ОС", "[E] Insert OS kernel tape reel" },
        { "[ЛКМ / E] Взять бобину с ядром ОС [DECODER-KERNEL-TAPE]", "[LMB / E] Take OS kernel tape reel [DECODER-KERNEL-TAPE]" },
        { "[E] Взять бобину ленты [DECODER-KERNEL-TAPE]", "[E] Take tape reel [DECODER-KERNEL-TAPE]" },
        { "Бобина с ядром ОС установлена [DECODER-KERNEL]", "OS kernel tape reel installed [DECODER-KERNEL]" },
        { "Бобина ленты с ядром ОС в инвентаре [DECODER-KERNEL]", "OS kernel tape reel in inventory [DECODER-KERNEL]" },

        // Disk Bay & Cartridges
        { "[ЛКМ / E] Нажать динамический тормоз шпинделя (СТОП)", "[LMB / E] Press dynamic spindle brake (STOP)" },
        { "[E] Нажать тормоз дисковода", "[E] Press disk drive brake" },
        { "[E] Вернуть холодный пакет №2 на полку", "[E] Return cold pack #2 to rack" },
        { "[E] Вернуть холодный пакет №1 на полку", "[E] Return cold pack #1 to rack" },
        { "[E] Взять охлажденный пакет №2 (30°C)", "[E] Take cooled pack #2 (30°C)" },
        { "[E] Взять охлажденный пакет №1 (30°C)", "[E] Take cooled pack #1 (30°C)" },

        // Desk, Note, Manual & Chair
        { "[E] Прочесть записку на столе", "[E] Read note on desk" },
        { "[E] Читать «Инструкцию по запуску ЭВМ»", "[E] Read \"Mainframe Boot Manual\"" },
        { "Нажмите [E] — Потенциометр яркости (маскировка)", "Press [E] — Brightness potentiometer (stealth)" },
        { "[Зажать ЛКМ] Яркость ЭЛТ", "[Hold LMB] CRT Brightness" },
        { "[ЛКМ / E] Сесть за терминал", "[LMB / E] Sit at terminal" },
        { "Нажмите [E] — Сесть в кресло", "Press [E] — Sit in chair" },
        { "[ Нажмите [E] — Сесть в кресло ]", "[ Press [E] — Sit in chair ]" },
        { "Нажмите [F] — Терминал", "Press [F] — Terminal" },
        { "[ Монитор обесточен — запустите сервер ]", "[ Monitor unpowered — start server ]" },
        { "[ Диск извлечен — установите накопитель в сервер ]", "[ Disk extracted — insert disk pack into bay ]" },
        { "Нажмите [B] — Быстрая смена яркости (маскировка)", "Press [B] — Quick brightness toggle (stealth)" },
        { "Нажмите [E] / [W,A,S,D] — Встать", "Press [E] / [W,A,S,D] — Stand up" },
        { "Зажмите [ЛКМ] и двигайте мышь — Поворот ручки яркости", "Hold [LMB] & move mouse — Turn brightness knob" },
        { "Нажмите [F] / [E] — Отойти | [B] — Яркость", "Press [F] / [E] — Step back | [B] — Brightness" },
        { "[E] Использовать", "[E] Use" }
    };

    /// <summary>
    /// Translates an in-game world prompt to the currently active language.
    /// </summary>
    public static string TranslatePrompt(string ruPrompt)
    {
        if (string.IsNullOrEmpty(ruPrompt)) return "";
        if (!IsEnglish) return ruPrompt;

        if (PromptDictionary.TryGetValue(ruPrompt, out string en))
        {
            return en;
        }

        // Dynamic formatting regex/replacements for cooling cradles and disk packs
        if (ruPrompt.Contains("Положить горячий пакет №") && ruPrompt.Contains("на охлаждение"))
        {
            return ruPrompt.Replace("[E] Положить горячий пакет №", "[E] Place hot disk pack #")
                           .Replace("на охлаждение", "on cooling rack");
        }
        if (ruPrompt.Contains("Вернуть холодный пакет №") && ruPrompt.Contains("на полку"))
        {
            return ruPrompt.Replace("[E] Вернуть холодный пакет №", "[E] Return cold disk pack #")
                           .Replace("на полку", "to rack");
        }
        if (ruPrompt.Contains("пуста — сначала извлеките горячий диск"))
        {
            return ruPrompt.Replace("пуста — сначала извлеките горячий диск", "empty — extract hot disk first")
                           .Replace("Ячейка", "Cradle");
        }
        if (ruPrompt.Contains("Взять охлажденный пакет №"))
        {
            return ruPrompt.Replace("[E] Взять охлажденный пакет №", "[E] Take cooled disk pack #");
        }
        if (ruPrompt.Contains("остывает") && ruPrompt.Contains("дождитесь 35°C"))
        {
            return ruPrompt.Replace("Пакет №", "Disk pack #")
                           .Replace("остывает", "cooling")
                           .Replace("дождитесь 35°C", "wait for 35°C");
        }

        // Partial match replacements
        string result = ruPrompt;
        result = result.Replace("Переключить тумблер", "Toggle switch")
                       .Replace("Нажать кнопку", "Press button")
                       .Replace("Включить", "Turn on")
                       .Replace("Выключить", "Turn off")
                       .Replace("Нажмите", "Press")
                       .Replace("главный рубильник", "main breaker")
                       .Replace("тумблер накала ламп", "tube filament switch")
                       .Replace("тумблер шины", "bus switch")
                       .Replace("яркость", "brightness")
                       .Replace("Прочесть записку", "Read note")
                       .Replace("Читать «Инструкцию", "Read \"Manual")
                       .Replace("Сесть в кресло", "Sit in chair")
                       .Replace("Встать", "Stand up");

        return result;
    }
}
