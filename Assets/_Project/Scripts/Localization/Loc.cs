using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Lumaquarium
{
    public enum Language { Vietnamese, English }

    // Vietnamese is the source language: code, scenes and catalogs are written in it, and each Vietnamese
    // string is also its own key in Resources/Localization/en.txt (one "vietnamese<TAB>english" line each,
    // \n for a line break). A string with no English line shows in Vietnamese.
    public static class Loc
    {
        public const string PrefsKey = "Lumaquarium.Language";
        public const string TablePath = "Localization/en";

        private static Language? current;
        private static Dictionary<string, string> english, vietnamese;

        public static event Action Changed;

        // English until the player picks a language in Settings.
        public static Language Current
        {
            get
            {
                current ??= PlayerPrefs.HasKey(PrefsKey) ? (Language)PlayerPrefs.GetInt(PrefsKey) : Language.English;
                return current.Value;
            }
        }

        public static void Set(Language language)
        {
            if (language == Current) return;
            current = language;
            PlayerPrefs.SetInt(PrefsKey, (int)language);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void Toggle() => Set(Current == Language.Vietnamese ? Language.English : Language.Vietnamese);

        // The current language's text for a Vietnamese string.
        public static string T(string text) => Current == Language.English ? English(text) : text;

        // T, then string.Format; the Vietnamese format is the key, e.g. Loc.F("Đặt ({0})", stock).
        public static string F(string format, params object[] args) => string.Format(Culture, T(format), args);

        public static string English(string text) =>
            !string.IsNullOrEmpty(text) && EnglishTable.TryGetValue(text, out var translated) ? translated : text;

        public static bool HasEnglish(string text) => text != null && EnglishTable.ContainsKey(text);

        // The Vietnamese key behind a string in either language, or null when it is not in the table.
        public static string Key(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            if (EnglishTable.ContainsKey(text)) return text;
            return VietnameseTable.TryGetValue(text, out var key) ? key : null;
        }

        public static CultureInfo Culture => CultureInfo.GetCultureInfo(Current == Language.English ? "en-US" : "vi-VN");

        // Coin amounts: 1.500 in Vietnamese, 1,500 in English.
        public static string Money(int amount) => amount.ToString("N0", Culture);

        private static Dictionary<string, string> EnglishTable =>
            english ??= Parse(Resources.Load<TextAsset>(TablePath)?.text);

        private static Dictionary<string, string> VietnameseTable
        {
            get
            {
                if (vietnamese != null) return vietnamese;
                vietnamese = new Dictionary<string, string>();
                foreach (var pair in EnglishTable) vietnamese.TryAdd(pair.Value, pair.Key);
                return vietnamese;
            }
        }

        public static Dictionary<string, string> Parse(string table)
        {
            var map = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(table)) return map;
            foreach (string raw in table.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;
                int tab = line.IndexOf('\t');
                if (tab <= 0 || tab == line.Length - 1) continue;
                map[Unescape(line.Substring(0, tab))] = Unescape(line.Substring(tab + 1));
            }
            return map;
        }

        private static string Unescape(string text) => text.Replace("\\n", "\n");
    }
}
