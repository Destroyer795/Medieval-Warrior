using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Drives a full play-through in batch mode: enters Play mode on the main menu and lets
// SmokeTestDriver (runtime, editor-only) click through everything. Exits Unity with
// code 0 when every check passed.
//
// Batch: Unity -batchmode -projectPath . -executeMethod SmokeTest.RunBatch -shotDir <folder>
// Menu:  Medieval Warrior > Run Smoke Test
[InitializeOnLoad]
public static class SmokeTest
{
    public const string FlagFile = "Temp/mw_smoke.txt";
    public const string DoneFile = "Temp/mw_smoke_done.txt";
    const double TimeoutSeconds = 900;

    static SmokeTest()
    {
        if (File.Exists(FlagFile)) EditorApplication.update += Poll;
    }

    [MenuItem("Medieval Warrior/Run Smoke Test")]
    static void RunFromMenu() => Begin(Path.GetFullPath("Temp/SmokeShots"));

    public static void RunBatch()
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-shotDir");
        Begin(i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.GetFullPath("Temp/SmokeShots"));
    }

    public static void Begin(string shotDir)
    {
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory(shotDir);
        File.WriteAllText(FlagFile, shotDir);
        if (File.Exists(DoneFile)) File.Delete(DoneFile);
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
        EditorApplication.update -= Poll;
        EditorApplication.update += Poll;
        // Let the editor finish its own start-up work first: on a fresh clone (no UserSettings/Search.index)
        // Unity's search indexer throws during start-up, and entering play mode in the same moment stalls batch mode.
        SessionState.SetBool(EnteredKey, false);
        SessionState.SetFloat(EnterAtKey, (float)EditorApplication.timeSinceStartup + 8f);
        Debug.Log("[SMOKE] waiting for the editor to settle before entering play mode");
    }

    const string EnteredKey = "mw.smoke.entered", EnterAtKey = "mw.smoke.enterAt";

    static void Poll()
    {
        if (!File.Exists(FlagFile)) { EditorApplication.update -= Poll; return; }

        if (!SessionState.GetBool(EnteredKey, true) && EditorApplication.timeSinceStartup > SessionState.GetFloat(EnterAtKey, 0f)
            && !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            SessionState.SetBool(EnteredKey, true);
            Debug.Log("[SMOKE] entering play mode");
            EditorApplication.EnterPlaymode();
            return;
        }

        if (File.Exists(DoneFile))
        {
            string result = File.ReadAllText(DoneFile).Trim();
            File.Delete(DoneFile);
            File.Delete(FlagFile);
            EditorApplication.update -= Poll;
            Debug.Log("[SMOKE] finished: " + result);
            if (Application.isBatchMode) EditorApplication.Exit(result.StartsWith("PASS") ? 0 : 2);
            else EditorApplication.isPlaying = false;
            return;
        }

        if ((DateTime.Now - File.GetLastWriteTime(FlagFile)).TotalSeconds > TimeoutSeconds)
        {
            Debug.LogError("[SMOKE] timed out");
            File.Delete(FlagFile);
            if (Application.isBatchMode) EditorApplication.Exit(3);
        }
    }
}
