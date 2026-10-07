using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;

public static class SmokeBuild
{
    public static void Build()
    {
        var target = EditorUserBuildSettings.activeBuildTarget;
        if (target != BuildTarget.StandaloneOSX && target != BuildTarget.StandaloneWindows64)
            throw new InvalidOperationException("Select macOS or Windows x64.");
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
        PlayerSettings.productName = "Smoke";
        PlayerSettings.companyName = "Survos";
        PlayerSettings.SetApiCompatibilityLevel(UnityEditor.Build.NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
        if (target == BuildTarget.StandaloneOSX)
            PlayerSettings.SetArchitecture(UnityEditor.Build.NamedBuildTarget.Standalone, 1); // Apple Silicon
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, "Assets/Smoke.unity");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Smoke.unity" }, target = target,
            locationPathName = target == BuildTarget.StandaloneOSX ? "Build/Smoke.app" : "Build/Smoke.exe",
            options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("IL2CPP smoke build failed: " + report.summary.result);
    }
}
