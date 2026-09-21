using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NeuroSlop.EditorTools
{
    /// <summary>
    /// Windows builds of the scenes in the build list. The development build includes the
    /// QA autopilot: run it with "NeuroSlop.exe -autopilot" (add "-autopilot-quit" to exit on the results screen).
    /// </summary>
    public static class PlayerBuilds
    {
        public const string ReleasePath = "Builds/NeuroSlop_MVP/NeuroSlop.exe";
        public const string DevelopmentPath = "Builds/NeuroSlop_Dev/NeuroSlop.exe";

        [MenuItem("NeuroSlop/Build Windows Player (release)")]
        public static BuildReport Release() => Build(ReleasePath, BuildOptions.None);

        [MenuItem("NeuroSlop/Build Windows Player (development + autopilot)")]
        public static BuildReport Development() => Build(DevelopmentPath, BuildOptions.Development);

        static BuildReport Build(string path, BuildOptions options)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = options
            });
            var summary = report.summary;
            Debug.Log($"NeuroSlop build: {summary.result} → {path} ({summary.totalErrors} errors, {summary.totalWarnings} warnings, {summary.totalSize / (1024 * 1024)} MB)");
            return report;
        }
    }
}
