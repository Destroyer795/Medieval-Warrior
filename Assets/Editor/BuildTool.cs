using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Windows player build, from the menu or batch mode:
// Unity -batchmode -projectPath . -executeMethod BuildTool.BuildWindowsBatch -logFile build.log
public static class BuildTool
{
    public const string OutputDir = "Builds/Windows";
    public const string ExeName = "MedievalWarrior.exe";

    [MenuItem("Medieval Warrior/Build Windows Player")]
    public static void BuildWindowsMenu()
    {
        var report = BuildWindows();
        if (report.summary.result == BuildResult.Succeeded) EditorUtility.RevealInFinder(Path.Combine(OutputDir, ExeName));
    }

    public static void BuildWindowsBatch()
    {
        var report = BuildWindows();
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    public static BuildReport BuildWindows()
    {
        PlayerSettings.productName = "Medieval Warrior";
        PlayerSettings.companyName = "Medieval Warrior Team";
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.runInBackground = false;

        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        Directory.CreateDirectory(OutputDir);
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(OutputDir, ExeName),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(options);
        var s = report.summary;
        Debug.Log($"[BUILD] {s.result}: {s.totalErrors} errors, {s.totalWarnings} warnings, {s.totalSize / (1024 * 1024)} MB, {s.totalTime.TotalSeconds:F0}s -> {s.outputPath}");
        return report;
    }
}
