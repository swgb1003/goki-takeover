using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GokiTakeover.Editor
{
    public static class PhaseOneBuild
    {
        [MenuItem("Tools/GOKI TAKEOVER/Build Windows 720p", true)]
        private static bool CanBuild() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem("Tools/GOKI TAKEOVER/Build Windows 720p", priority = 30)]
        public static void Build()
        {
            BuildScene(KitchenStageGenerator.ScenePath, "Build/GokiTakeover_Phase1/GokiTakeover.exe", BuildOptions.Development);
        }

        [MenuItem("Tools/GOKI TAKEOVER/Build Cinematic Windows 720p", priority = 31)]
        public static void BuildPolished()
        {
            BuildScene(PolishedKitchenGenerator.ScenePath, "Build/GokiTakeover_Polished/GokiTakeover.exe", BuildOptions.None);
        }

        public static void BuildPolishedValidation()
        {
            BuildScene(PolishedKitchenGenerator.ScenePath, "Build/GokiTakeover_Polished_Validation/GokiTakeover.exe", BuildOptions.Development);
        }

        public static void BuildPolishedBoth()
        {
            BuildPolishedValidation();
            BuildPolished();
        }

        [MenuItem("Tools/GOKI TAKEOVER/Build Cinematic Windows 720p", true)]
        private static bool CanBuildPolished() => !EditorApplication.isPlayingOrWillChangePlaymode;

        internal static void BuildScene(string scenePath, string destination, BuildOptions options) => BuildScenes(new[] { scenePath }, destination, options);

        // The first scene starts the player; the rest are loaded by name (Phase 5: title -> stage).
        internal static void BuildScenes(string[] scenePaths, string destination, BuildOptions options, FullScreenMode mode = FullScreenMode.Windowed)
        {
            foreach (var scenePath in scenePaths)
                if (!File.Exists(scenePath))
                    throw new System.InvalidOperationException("[GOKI] 先に対応するキッチンシーンを生成してください: " + scenePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            int oldWidth = PlayerSettings.defaultScreenWidth, oldHeight = PlayerSettings.defaultScreenHeight;
            FullScreenMode oldMode = PlayerSettings.fullScreenMode;
            bool oldResizable = PlayerSettings.resizableWindow;
            try
            {
                PlayerSettings.defaultScreenWidth = 1280;
                PlayerSettings.defaultScreenHeight = 720;
                PlayerSettings.fullScreenMode = mode;   // the game itself switches to the saved choice (GameSettings.fullscreen)
                PlayerSettings.resizableWindow = false;
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenePaths, locationPathName = destination,
                    target = BuildTarget.StandaloneWindows64, options = options
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new System.InvalidOperationException("[GOKI] Windowsビルド失敗: " + report.summary.result);
                Debug.Log("[GOKI] Windows 1280×720ビルド成功: " + destination + " / FPSは実行して計測してください。");
            }
            finally
            {
                PlayerSettings.defaultScreenWidth = oldWidth;
                PlayerSettings.defaultScreenHeight = oldHeight;
                PlayerSettings.fullScreenMode = oldMode;
                PlayerSettings.resizableWindow = oldResizable;
            }
        }
    }
}
