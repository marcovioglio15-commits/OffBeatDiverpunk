using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Runs a complete Windows player build while verifying the repaired project caches.
/// </summary>
internal static class OffBeatBuildVerification
{
    #region Methods

    #region Build
    /// <summary>
    /// Builds enabled scenes with production preprocessors and fails batch mode on any build error.
    /// </summary>
    public static void Run()
    {
        // Keep the user's normal output selection separate from this verification artifact.
        const BuildTarget target = BuildTarget.StandaloneWindows64;
        string previousLocation = EditorUserBuildSettings.GetBuildLocation(target);
        string outputDirectory = Path.GetFullPath("../TaskArtifacts/OffBeat/VerifiedWindowsBuild");
        List<string> scenes = new List<string>();

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            if (scene.enabled)
                scenes.Add(scene.path);

        if (scenes.Count == 0)
            throw new InvalidOperationException("No enabled scenes are configured for the player build.");

        Directory.CreateDirectory(outputDirectory);

        try
        {
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                target = target,
                locationPathName = Path.Combine(outputDirectory, PlayerSettings.productName + ".exe"),
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[OffBeatBuildVerification] Result=" + report.summary.result +
                      ", Errors=" + report.summary.totalErrors + ", Warnings=" + report.summary.totalWarnings +
                      ", Output=" + report.summary.outputPath + ", Duration=" + report.summary.totalTime);

            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Windows build failed; inspect the preceding build diagnostics.");
        }
        finally
        {
            EditorUserBuildSettings.SetBuildLocation(target, previousLocation);
        }
    }
    #endregion

    #endregion
}
