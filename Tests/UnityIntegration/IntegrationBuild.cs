using UnityEditor;
public static class IntegrationBuild
{
    public static void Build()
    {
        var report=BuildPipeline.BuildPlayer(new[]{"Assets/Scenes/GameScene.unity"},"/tmp/td-integration-build/Integration.app",BuildTarget.StandaloneOSX,BuildOptions.Development);
        if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new System.Exception(report.summary.result.ToString());
    }
}
