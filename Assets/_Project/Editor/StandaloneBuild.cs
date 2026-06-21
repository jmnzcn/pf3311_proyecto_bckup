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

    [MenuItem("PF-3311/Build Windows Standalone (+ bundle secrets)")]
    public static void BuildWindowsFromMenu()
    {
        PerformWindowsBuildInternal(exitEditor: false);
    }

    public static void PerformWindowsBuild()
    {
        PerformWindowsBuildInternal(exitEditor: true);
    }

    static void PerformWindowsBuildInternal(bool exitEditor)
    {
        Directory.CreateDirectory(BuildFolder);

        var scenes = new[] { MainScene };
        var missing = scenes.Where(s => !File.Exists(s)).ToArray();
        if (missing.Length > 0)
        {
            Debug.LogError("Missing scene(s): " + string.Join(", ", missing));
            if (exitEditor) EditorApplication.Exit(1);
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
            if (exitEditor) EditorApplication.Exit(1);
            return;
        }

        BundleLocalSecretsForDistribution();

        Debug.Log(
            "Build succeeded in " + summary.totalTime + " → " +
            summary.outputPath + " (" + summary.totalSize + " bytes)");

        if (exitEditor)
            EditorApplication.Exit(0);
    }

    /// <summary>
    /// Copies gitignored _config/LocalSecrets.json next to the .exe for standalone distribution.
    /// </summary>
    static void BundleLocalSecretsForDistribution()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string source = Path.Combine(projectRoot, "_config", "LocalSecrets.json");
        string destDir = Path.Combine(projectRoot, BuildFolder, "_config");
        string dest = Path.Combine(destDir, "LocalSecrets.json");

        if (!File.Exists(source))
        {
            Debug.LogWarning(
                "Build OK pero falta _config/LocalSecrets.json en el proyecto. " +
                "Copiá LocalSecrets.example.json → LocalSecrets.json con las claves antes de distribuir el .exe.");
            return;
        }

        Directory.CreateDirectory(destDir);
        File.Copy(source, dest, overwrite: true);
        Debug.Log("Bundled LocalSecrets.json → " + dest);
    }
}
#endif
