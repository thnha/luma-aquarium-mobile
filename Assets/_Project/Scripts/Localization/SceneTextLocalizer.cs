using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lumaquarium
{
    // The UI builders bake labels in Vietnamese. When a scene loads, and again when the language changes,
    // every label whose whole text is a table entry (in either language) is switched to the current language.
    // Text that code builds from parts (prices, counts) is not in the table; that code calls Loc itself.
    public static class SceneTextLocalizer
    {
        // The key each label last showed, as two keys can share one English text ("Tắt" and "Đang tắt" are both "Off").
        private static readonly Dictionary<TMP_Text, string> shown = new Dictionary<TMP_Text, string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Loc.Changed -= ApplyAll;
            Loc.Changed += ApplyAll;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply(scene);

        public static void ApplyAll()
        {
            foreach (var gone in shown.Keys.Where(label => label == null).ToList()) shown.Remove(gone);
            for (int i = 0; i < SceneManager.sceneCount; i++) Apply(SceneManager.GetSceneAt(i));
        }

        public static void Apply(Scene scene)
        {
            if (!scene.isLoaded) return;
            foreach (var root in scene.GetRootGameObjects())
                Apply(root);
        }

        public static void Apply(GameObject root)
        {
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.GetComponentInParent<TMP_InputField>(true) is { } input && input.textComponent == label) continue; // typed text
                string key = shown.TryGetValue(label, out var last) && (label.text == last || label.text == Loc.English(last))
                    ? last : Loc.Key(label.text);
                if (key == null) continue;
                shown[label] = key;
                string text = Loc.T(key);
                if (label.text != text) label.text = text;
            }
        }
    }
}
