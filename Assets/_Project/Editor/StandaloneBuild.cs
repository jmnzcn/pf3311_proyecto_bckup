#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Batch build for CI and command-line: Unity.exe -batchmode -executeMethod StandaloneBuild.PerformWindowsBuild
/// </summary>
public static class StandaloneBuild
{
    const string BuildFolder = "Build/Windows";
    const string ExecutableName = "ExperimentPrototypeB03230.exe";
    const string MainScene = "Assets/Scenes/SampleScene.unity";

    public static void PerformWindowsBuild()
    {
        Directory.CreateDirectory(BuildFolder);

        var scenes = new[] { MainScene };
        var missing = scenes.Where(s => !File.Exists(s)).ToArray();
        if (missing.Length > 0)
        {
            Debug.LogError("Missing scene(s): " + string.Join(", ", missing));
            EditorApplication.Exit(1);
            return;
        }

        var output = Path.Combine(BuildFolder, ExecutableName);
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };

        Debug.Log("Starting Windows standalone build → " + Path.GetFullPath(output));

        BuildReport report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        if (summary.result != BuildResult.Succeeded)
        {
            Debug.LogError("Build failed: " + summary.result + " (" + summary.totalErrors + " errors)");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log(
            "Build succeeded in " + summary.totalTime + " → " +
            summary.outputPath + " (" + summary.totalSize + " bytes)");
        EditorApplication.Exit(0);
    }
}
#endif
