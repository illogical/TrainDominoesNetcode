using UnityEditor;
public static class LegacyBuild { public static void Build() { var r=BuildPipeline.BuildPlayer(new[]{"Assets/Scenes/GameScene.unity"}, "/tmp/td-legacy-build/Legacy.app", BuildTarget.StandaloneOSX, BuildOptions.Development); if(r.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new System.Exception(r.summary.result.ToString()); } }
