using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ClickerGame
{
    public class Achievement
    {
        public string Id { get; set; } = "";
        public string NameKey { get; set; } = "";
        public string DescKey { get; set; } = "";
        public bool Unlocked { get; set; }
        public string UnlockedAt { get; set; } = "";
    }

    public class AchievementData
    {
        public List<Achievement> Achievements { get; set; } = new();
    }

    /// <summary>
    /// Manages achievements. Stored as encrypted .rc file.
    /// </summary>
    public class AchievementManager
    {
        private readonly string _path;
        private AchievementData _data;

        // Popup queue for newly unlocked
        public Queue<Achievement> PendingPopups { get; } = new();

        // All achievement definitions
        static readonly (string id, string nameKey, string descKey)[] Definitions = new[]
        {
            ("first_play",   "ach_first_play",   "ach_first_play_desc"),
            ("first_fc",     "ach_first_fc",      "ach_first_fc_desc"),
            ("first_s",      "ach_first_s",       "ach_first_s_desc"),
            ("first_ss",     "ach_first_ss",      "ach_first_ss_desc"),
            ("combo_50",     "ach_combo_50",      "ach_combo_50_desc"),
            ("combo_100",    "ach_combo_100",     "ach_combo_100_desc"),
            ("plays_10",     "ach_plays_10",      "ach_plays_10_desc"),
            ("plays_50",     "ach_plays_50",      "ach_plays_50_desc"),
            ("plays_100",    "ach_plays_100",     "ach_plays_100_desc"),
            ("all_songs",    "ach_all_songs",     "ach_all_songs_desc"),
            ("grade_all_a",  "ach_grade_all_a",   "ach_grade_all_a_desc"),
            ("accuracy_95",  "ach_accuracy_95",   "ach_accuracy_95_desc"),
        };

        public AchievementManager(string path = "achievements.rc")
        {
            _path = path;
            Load();
        }

        void Load()
        {
            if (File.Exists(_path))
            {
                try
                {
                    _data = RcFileManager.ReadEncrypted<AchievementData>(_path);
                }
                catch { _data = new AchievementData(); }
            }
            else
            {
                _data = new AchievementData();
            }

            // Ensure all definitions exist
            foreach (var (id, nameKey, descKey) in Definitions)
            {
                if (!_data.Achievements.Any(a => a.Id == id))
                    _data.Achievements.Add(new Achievement { Id = id, NameKey = nameKey, DescKey = descKey });
            }
        }

        void Save()
        {
            RcFileManager.WriteEncrypted(_path, _data);
        }

        public bool IsUnlocked(string id) =>
            _data.Achievements.FirstOrDefault(a => a.Id == id)?.Unlocked ?? false;

        public List<Achievement> GetAll() => _data.Achievements;

        public int UnlockedCount => _data.Achievements.Count(a => a.Unlocked);
        public int TotalCount => _data.Achievements.Count;

        bool TryUnlock(string id)
        {
            var ach = _data.Achievements.FirstOrDefault(a => a.Id == id);
            if (ach == null || ach.Unlocked) return false;
            ach.Unlocked = true;
            ach.UnlockedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            PendingPopups.Enqueue(ach);
            Save();
            return true;
        }

        /// <summary>
        /// Force-unlock an achievement from server sync (no popup).
        /// </summary>
        public void ForceUnlock(string id, string unlockedAt)
        {
            var ach = _data.Achievements.FirstOrDefault(a => a.Id == id);
            if (ach == null || ach.Unlocked) return;
            ach.Unlocked = true;
            ach.UnlockedAt = !string.IsNullOrEmpty(unlockedAt) ? unlockedAt
                : DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            Save();
        }

        /// <summary>
        /// Check and unlock achievements after a play session.
        /// </summary>
        public void CheckAfterPlay(int totalPlays, int maxCombo, string grade,
            double accuracy, bool isFullCombo, int uniqueSongsPlayed, int totalSongs)
        {
            // First play
            if (totalPlays >= 1) TryUnlock("first_play");

            // Full combo
            if (isFullCombo) TryUnlock("first_fc");

            // Grades
            if (grade == "S" || grade == "SS") TryUnlock("first_s");
            if (grade == "SS") TryUnlock("first_ss");

            // Combo milestones
            if (maxCombo >= 50) TryUnlock("combo_50");
            if (maxCombo >= 100) TryUnlock("combo_100");

            // Play count milestones
            if (totalPlays >= 10) TryUnlock("plays_10");
            if (totalPlays >= 50) TryUnlock("plays_50");
            if (totalPlays >= 100) TryUnlock("plays_100");

            // All songs played
            if (totalSongs > 0 && uniqueSongsPlayed >= totalSongs)
                TryUnlock("all_songs");

            // Accuracy
            if (accuracy >= 95.0) TryUnlock("accuracy_95");
        }
    }
}
