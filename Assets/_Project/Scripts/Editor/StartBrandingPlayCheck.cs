using System;
using System.Collections.Generic;
using System.IO;
using Lumaquarium;
using Lumaquarium.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

// A read-only UI smoke check: does not click Play, create a guest or load remote content.
[InitializeOnLoad]
public static class StartBrandingPlayCheck
{
    const string Pending = "Lumaquarium.StartBrandingPlayCheck";
    static double started;
    static int initialFrame;

    static StartBrandingPlayCheck()
    {
        EditorApplication.playModeStateChanged += OnState;
    }

    [MenuItem("Lumaquarium/UI/Test Branded Start in Play Mode")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before running the start UI check.");
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }

    static void OnState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Pending, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            started = EditorApplication.timeSinceStartup;
            initialFrame = Time.frameCount;
            Application.runInBackground = true;
            EditorApplication.update += Check;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Pending, false);
            EditorApplication.update -= Check;
        }
    }

    static void Check()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if (EditorApplication.timeSinceStartup - started < 1.5) return;
        string result;
        try
        {
            var controller = UnityEngine.Object.FindFirstObjectByType<StartGameScreen>();
            if (!controller) throw new Exception("StartGame controller did not load.");
            if (Time.frameCount <= initialFrame + 5) throw new Exception("Play Mode is not advancing.");
            var serialized = new SerializedObject(controller);
            var controls = (CanvasGroup)serialized.FindProperty("startControls").objectReferenceValue;
            var button = (UnityEngine.UI.Button)serialized.FindProperty("startButton").objectReferenceValue;
            if (controls.alpha < .999f || !controls.interactable || !controls.blocksRaycasts)
                throw new Exception("Start controls did not become visible and interactive after the fade.");
            var eventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            if (eventSystems.Length != 1) throw new Exception("Expected exactly one EventSystem.");
            Canvas.ForceUpdateCanvases();
            var point = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
            var data = new PointerEventData(eventSystems[0]) { position = point };
            var hits = new List<RaycastResult>();
            eventSystems[0].RaycastAll(data, hits);
            if (hits.Count == 0 || hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>() != button)
                throw new Exception("The start button is blocked or misses pointer raycasts.");
            string label = button.GetComponentInChildren<TMPro.TMP_Text>().text;
            string expected = Loc.T(FrontEndScenes.HasGuestProfile ? "Tiếp tục hồ của bạn" : "Chơi ngay");
            if (label != expected) throw new Exception("Wrong new/returning player button label.");
            if (!Application.CanStreamedLevelBeLoaded(FrontEndScenes.Loading))
                throw new Exception("LoadingGame is missing from enabled build scenes.");
            result = "PASS\nFrames advanced: " + (Time.frameCount - initialFrame)
                + "\nControls fade: visible, interactive, raycasts enabled\n"
                + "EventSystem: exactly one\nButton center raycast: hits StartButton\n"
                + "Button label: " + label + "\nLoadingGame: available\n"
                + "No profile writes or remote scene loads were performed.\n";
            Debug.Log("[Lumaquarium] Start branding Play Mode check passed.");
        }
        catch (Exception ex)
        {
            result = "FAIL\n" + ex + "\n";
            Debug.LogError("[Lumaquarium] Start branding Play Mode check failed: " + ex.Message);
        }
        Directory.CreateDirectory("docs/previews/LaunchBranding");
        File.WriteAllText("docs/previews/LaunchBranding/playmode-verification.txt", result);
        EditorApplication.update -= Check;
        EditorApplication.isPlaying = false;
    }
}
