using System;
using System.Collections.Generic;
using System.IO;

namespace ClickerGame
{
    /// <summary>
    /// Represents a single input event in a replay.
    /// </summary>
    public class ReplayEvent
    {
        public float Time { get; set; }       // Seconds from song start
        public int Column { get; set; }       // Lane 0-3
        public string Judgment { get; set; } = ""; // PERFECT/GREAT/GOOD/MISS
        public int ScoreGained { get; set; }
        public int ComboAt { get; set; }
    }

    /// <summary>
    /// Full replay data for a single play.
    /// </summary>
    public class ReplayData
    {
        public string SongId { get; set; } = "";
        public string Difficulty { get; set; } = "";
        public string Player { get; set; } = "guest";
        public string PlayedAt { get; set; } = "";
        public int FinalScore { get; set; }
        public int MaxCombo { get; set; }
        public int Hit { get; set; }
        public int Miss { get; set; }
        public double Accuracy { get; set; }
        public string Grade { get; set; } = "";
        public List<ReplayEvent> Events { get; set; } = new();
    }

    /// <summary>
    /// Records and plays back replays using .rcp encrypted format.
    /// </summary>
    public class ReplayManager
    {
        private readonly string _replayDir;
        private List<ReplayEvent> _recording = new();
        private bool _isRecording;

        public ReplayManager(string replayDir = "Replays")
        {
            _replayDir = replayDir;
            Directory.CreateDirectory(_replayDir);
        }

        public void StartRecording()
        {
            _recording = new List<ReplayEvent>();
            _isRecording = true;
        }

        public void RecordEvent(float time, int column, string judgment, int scoreGained, int comboAt)
        {
            if (!_isRecording) return;
            _recording.Add(new ReplayEvent
            {
                Time = (float)Math.Round(time, 3),
                Column = column,
                Judgment = judgment,
                ScoreGained = scoreGained,
                ComboAt = comboAt
            });
        }

        public ReplayData StopRecording(string songId, string difficulty, string player,
            int finalScore, int maxCombo, int hit, int miss, double accuracy, string grade)
        {
            _isRecording = false;
            var data = new ReplayData
            {
                SongId = songId,
                Difficulty = difficulty,
                Player = player,
                PlayedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                FinalScore = finalScore,
                MaxCombo = maxCombo,
                Hit = hit,
                Miss = miss,
                Accuracy = accuracy,
                Grade = grade,
                Events = new List<ReplayEvent>(_recording)
            };

            // Save to .rcp file
            string fileName = $"{songId}_{difficulty}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.rcp";
            string path = Path.Combine(_replayDir, fileName);
            RcFileManager.WriteEncrypted(path, data);

            return data;
        }

        /// <summary>Get the best replay for a song+difficulty combo.</summary>
        public ReplayData? GetBestReplay(string songId, string difficulty)
        {
            ReplayData? best = null;
            string pattern = $"{songId}_{difficulty}_*.rcp";

            if (!Directory.Exists(_replayDir)) return null;

            foreach (var file in Directory.GetFiles(_replayDir, pattern))
            {
                try
                {
                    var data = RcFileManager.ReadEncrypted<ReplayData>(file);
                    if (best == null || data.FinalScore > best.FinalScore)
                        best = data;
                }
                catch { }
            }
            return best;
        }

        /// <summary>Get all replays, sorted by score descending.</summary>
        public List<(string file, ReplayData data)> GetAllReplays()
        {
            var list = new List<(string file, ReplayData data)>();
            if (!Directory.Exists(_replayDir)) return list;

            foreach (var file in Directory.GetFiles(_replayDir, "*.rcp"))
            {
                try
                {
                    var data = RcFileManager.ReadEncrypted<ReplayData>(file);
                    list.Add((Path.GetFileName(file), data));
                }
                catch { }
            }
            list.Sort((a, b) => b.data.FinalScore.CompareTo(a.data.FinalScore));
            return list;
        }
    }
}
