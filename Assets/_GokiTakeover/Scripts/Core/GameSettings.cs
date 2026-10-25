using System;
using System.IO;
using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// Player settings and encyclopedia progress, stored as JSON in persistentDataPath (spec 12).
    /// An unreadable file is moved aside as .bak and defaults are used; loading never throws.
    /// </summary>
    [Serializable]
    public sealed class GameSettings
    {
        public const int CurrentVersion = 1;
        public int version = CurrentVersion;
        public float mouseSensitivity = 2f;
        public float masterVolume = 0.8f;
        public int quality = 1;              // 0 低 / 1 中
        public bool speechBubbles = true;
        public bool cameraShake = true;
        public bool fullscreen = true;       // borderless full screen at the desktop resolution; off = the 1280×720 window
        public string[] codex = new string[0];   // encyclopedia entries met at least once

        /// <summary>Full screen or the window, in a standalone player only. A command line that picks the screen
        /// itself (-screen-fullscreen, used by the test runs) is left alone.</summary>
        public void ApplyScreen()
        {
            if (Application.isEditor || Array.IndexOf(Environment.GetCommandLineArgs(), "-screen-fullscreen") >= 0) return;
            if (fullscreen)
            {
                if (Screen.fullScreenMode != FullScreenMode.FullScreenWindow)
                    Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
            }
            else if (Screen.fullScreenMode != FullScreenMode.Windowed) Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
        }
        // The player remembers its last window mode; the saved setting decides instead.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyAtStartup() { if (!Application.isEditor) Load().ApplyScreen(); }

        public static string PathOverride { get; set; }
        public static string FilePath => PathOverride ?? Path.Combine(Application.persistentDataPath, "settings.json");
        public static string LastLoadMessage { get; private set; }

        public static GameSettings Load()
        {
            LastLoadMessage = null;
            try
            {
                if (!File.Exists(FilePath)) return new GameSettings();
                var loaded = JsonUtility.FromJson<GameSettings>(File.ReadAllText(FilePath));
                if (loaded == null || loaded.version < 1) throw new InvalidDataException("empty or unknown settings");
                loaded.Clamp();
                return loaded;
            }
            catch (Exception e)
            {
                try { File.Copy(FilePath, FilePath + ".bak", true); } catch (Exception) { }
                LastLoadMessage = "設定ファイルを読めなかったため初期設定に戻しました（元のファイルは .bak に保存）";
                Debug.LogWarning("[GOKI] " + LastLoadMessage + ": " + e.Message);
                return new GameSettings();
            }
        }

        public bool Save()
        {
            try
            {
                Clamp();
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(this, true));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(temp, FilePath);
                return true;
            }
            catch (Exception e) { Debug.LogWarning("[GOKI] 設定を保存できません: " + e.Message); return false; }
        }

        public bool Knows(string entry) => Array.IndexOf(codex, entry) >= 0;
        public bool Learn(string entry)
        {
            if (string.IsNullOrEmpty(entry) || Knows(entry)) return false;
            Array.Resize(ref codex, codex.Length + 1);
            codex[codex.Length - 1] = entry;
            return true;
        }

        private void Clamp()
        {
            mouseSensitivity = Mathf.Clamp(mouseSensitivity, 0.5f, 5f);
            masterVolume = Mathf.Clamp01(masterVolume);
            quality = Mathf.Clamp(quality, 0, 1);
            if (codex == null) codex = new string[0];
        }
    }
}
