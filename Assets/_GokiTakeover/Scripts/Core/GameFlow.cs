using UnityEngine;
using UnityEngine.SceneManagement;

namespace GokiTakeover
{
    /// <summary>
    /// Screen flow between scenes (spec 10.3): title / stage select -> stage -> result -> title.
    /// Players load scenes by name from the build list; the Editor loads them by path so the project's
    /// build settings stay untouched.
    /// </summary>
    public static class GameFlow
    {
        public const string Folder = "Assets/_GokiTakeover/Scenes/";
        public const string TitleScene = "Phase5_Title";
        public static string PendingStage { get; private set; }
        public static bool ResumeRequested { get; private set; }
        public enum TitlePage { Title, StageSelect }
        public static TitlePage ReturnPage { get; private set; }

        public static void StartStage(string sceneName, string stageId, bool resume)
        {
            PendingStage = stageId; ResumeRequested = resume;
            Load(sceneName);
        }
        /// <summary>Called by the stage once it has used the request, so a retry starts fresh.</summary>
        public static void ConsumeResume() { ResumeRequested = false; }
        public static void ToTitle(TitlePage page = TitlePage.Title) { ReturnPage = page; ResumeRequested = false; Load(TitleScene); }
        public static void ConsumeReturnPage() { ReturnPage = TitlePage.Title; }

        private static void Load(string sceneName)
        {
            Time.timeScale = 1;
#if UNITY_EDITOR
            if (Application.isEditor)
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(Folder + sceneName + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif
            SceneManager.LoadScene(sceneName);
        }
    }
}
