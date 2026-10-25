using System;
using System.IO;
using UnityEngine;

namespace GokiTakeover
{
    /// <summary>
    /// Progress save (spec 12, 7.1): unlocked stages, best results, cosmetics, and one checkpoint taken at
    /// the start of a sortie and whenever the nest is closed. JSON in persistentDataPath, written through a
    /// temporary file; an unreadable file is moved aside as .bak and a fresh save is used, never an exception.
    /// Positions of individual roaches are not saved: a resumed run restarts at the nest with the colony intact.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;
        [Serializable] public sealed class StageRecord
        {
            public string stage;
            public int clears;
            public string bestRating = "";
            public float bestSeconds;
            public int bestFood;
        }
        [Serializable] public sealed class Checkpoint
        {
            public bool valid;
            public string stage = "";
            public string savedAt = "";
            public int food, totalCollected, nestLevel = 1, allies, upgradesBought, born;
            public int[] upgrades = new int[0];
            public float[] zones = new float[0];
            public float fear, fearGained, seconds;
            public int discoveries, successors;
        }

        public int version = CurrentVersion;
        public string[] unlocked = { "stage01" };
        public StageRecord[] records = new StageRecord[0];
        public string[] rewards = new string[0];
        public string skin = "";
        public bool tutorialDone;
        public bool guideDone;   // the full chapter-1 guide (2026-10-24); tutorialDone was the shorter first version
        public Checkpoint checkpoint = new Checkpoint();

        public static string PathOverride { get; set; }
        public static string FilePath => PathOverride ?? Path.Combine(Application.persistentDataPath, "save.json");
        public static string LastLoadMessage { get; private set; }

        public static SaveData Load()
        {
            LastLoadMessage = null;
            try
            {
                if (!File.Exists(FilePath)) return new SaveData();
                var loaded = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                if (loaded == null || loaded.version < 1) throw new InvalidDataException("empty or unknown save");
                loaded.Repair();
                return loaded;
            }
            catch (Exception e)
            {
                try { File.Copy(FilePath, FilePath + ".bak", true); } catch (Exception) { }
                LastLoadMessage = "セーブデータを読めなかったため新しく始めます（元のファイルは .bak に保存）";
                Debug.LogWarning("[GOKI] " + LastLoadMessage + ": " + e.Message);
                return new SaveData();
            }
        }

        public bool Save()
        {
            try
            {
                Repair();
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(this, true));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(temp, FilePath);
                return true;
            }
            catch (Exception e) { Debug.LogWarning("[GOKI] セーブできません: " + e.Message); return false; }
        }

        public bool IsUnlocked(string stage) => Array.IndexOf(unlocked, stage) >= 0;
        public bool HasReward(string reward) => !string.IsNullOrEmpty(reward) && Array.IndexOf(rewards, reward) >= 0;
        public bool HasCheckpoint(string stage) => checkpoint != null && checkpoint.valid && (stage == null || checkpoint.stage == stage);
        public StageRecord Record(string stage)
        {
            foreach (var r in records) if (r.stage == stage) return r;
            return null;
        }

        /// <summary>Stores a clear; returns true for each first-time unlock so the result screen can announce it.</summary>
        public void RecordClear(string stage, string rating, float seconds, int food, string unlock, string reward, out bool newStage, out bool newReward)
        {
            var r = Record(stage);
            if (r == null)
            {
                r = new StageRecord { stage = stage };
                Array.Resize(ref records, records.Length + 1);
                records[records.Length - 1] = r;
            }
            r.clears++;
            if (string.IsNullOrEmpty(r.bestRating) || string.CompareOrdinal(rating, r.bestRating) < 0) r.bestRating = rating;   // "A" < "B" < "C"
            if (r.bestSeconds <= 0 || seconds < r.bestSeconds) r.bestSeconds = seconds;
            r.bestFood = Mathf.Max(r.bestFood, food);
            newStage = Add(ref unlocked, unlock);
            newReward = Add(ref rewards, reward);
            if (checkpoint.stage == stage) checkpoint = new Checkpoint();
        }

        private static bool Add(ref string[] list, string item)
        {
            if (string.IsNullOrEmpty(item) || Array.IndexOf(list, item) >= 0) return false;
            Array.Resize(ref list, list.Length + 1);
            list[list.Length - 1] = item;
            return true;
        }

        private void Repair()
        {
            if (unlocked == null || unlocked.Length == 0) unlocked = new[] { "stage01" };
            if (records == null) records = new StageRecord[0];
            if (rewards == null) rewards = new string[0];
            if (skin == null || !HasReward(skin)) skin = "";
            if (checkpoint == null) checkpoint = new Checkpoint();
            if (checkpoint.upgrades == null) checkpoint.upgrades = new int[0];
            if (checkpoint.zones == null) checkpoint.zones = new float[0];
        }
    }
}
