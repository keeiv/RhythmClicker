using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace ClickerGame
{
    public class PlayRecord
    {
        public long Id { get; set; }
        public string User { get; set; } = "";
        public string SongId { get; set; } = "";
        public string Difficulty { get; set; } = "";
        public int Score { get; set; }
        public int MaxCombo { get; set; }
        public int Hit { get; set; }
        public int Miss { get; set; }
        public double Accuracy { get; set; }
        public string Grade { get; set; } = "";
        public string PlayedAt { get; set; } = "";
    }

    public class PlayerSummary
    {
        public int TotalPlays { get; set; }
        public double AvgAccuracy { get; set; }
        public int BestScore { get; set; }
        public int BestCombo { get; set; }
        public int TotalHit { get; set; }
        public int TotalMiss { get; set; }
        public int CountSS { get; set; }
        public int CountS { get; set; }
        public int CountA { get; set; }
        public int CountB { get; set; }
        public int CountC { get; set; }
        public int CountD { get; set; }
    }

    public class StatsDatabase : IDisposable
    {
        private readonly SqliteConnection _conn;

        public StatsDatabase(string dbPath = "stats.db")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dbPath)) ?? ".");
            _conn = new SqliteConnection($"Data Source={dbPath}");
            _conn.Open();
            InitSchema();
        }

        private void InitSchema()
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS plays (
                    id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    user        TEXT NOT NULL DEFAULT 'guest',
                    song_id     TEXT NOT NULL,
                    difficulty  TEXT NOT NULL,
                    score       INTEGER NOT NULL,
                    max_combo   INTEGER NOT NULL,
                    hit         INTEGER NOT NULL,
                    miss        INTEGER NOT NULL,
                    accuracy    REAL NOT NULL,
                    grade       TEXT NOT NULL,
                    played_at   TEXT NOT NULL DEFAULT (datetime('now'))
                );
                CREATE INDEX IF NOT EXISTS idx_plays_user ON plays(user);
                CREATE INDEX IF NOT EXISTS idx_plays_song ON plays(song_id);
            ";
            cmd.ExecuteNonQuery();
        }

        public void RecordPlay(string user, string songId, string difficulty,
            int score, int maxCombo, int hit, int miss, double accuracy, string grade)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO plays (user, song_id, difficulty, score, max_combo, hit, miss, accuracy, grade)
                VALUES (@u, @s, @d, @sc, @mc, @h, @m, @a, @g)";
            cmd.Parameters.AddWithValue("@u", user ?? "guest");
            cmd.Parameters.AddWithValue("@s", songId);
            cmd.Parameters.AddWithValue("@d", difficulty);
            cmd.Parameters.AddWithValue("@sc", score);
            cmd.Parameters.AddWithValue("@mc", maxCombo);
            cmd.Parameters.AddWithValue("@h", hit);
            cmd.Parameters.AddWithValue("@m", miss);
            cmd.Parameters.AddWithValue("@a", accuracy);
            cmd.Parameters.AddWithValue("@g", grade);
            cmd.ExecuteNonQuery();
        }

        public PlayerSummary GetSummary(string? user = null)
        {
            var s = new PlayerSummary();
            using var cmd = _conn.CreateCommand();
            string where = user != null ? "WHERE user = @u" : "";
            cmd.CommandText = $@"
                SELECT
                    COUNT(*),
                    COALESCE(AVG(accuracy), 0),
                    COALESCE(MAX(score), 0),
                    COALESCE(MAX(max_combo), 0),
                    COALESCE(SUM(hit), 0),
                    COALESCE(SUM(miss), 0),
                    COALESCE(SUM(CASE WHEN grade='SS' THEN 1 ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN grade='S'  THEN 1 ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN grade='A'  THEN 1 ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN grade='B'  THEN 1 ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN grade='C'  THEN 1 ELSE 0 END), 0),
                    COALESCE(SUM(CASE WHEN grade='D'  THEN 1 ELSE 0 END), 0)
                FROM plays {where}";
            if (user != null) cmd.Parameters.AddWithValue("@u", user);
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                s.TotalPlays  = r.GetInt32(0);
                s.AvgAccuracy = r.GetDouble(1);
                s.BestScore   = r.GetInt32(2);
                s.BestCombo   = r.GetInt32(3);
                s.TotalHit    = r.GetInt32(4);
                s.TotalMiss   = r.GetInt32(5);
                s.CountSS     = r.GetInt32(6);
                s.CountS      = r.GetInt32(7);
                s.CountA      = r.GetInt32(8);
                s.CountB      = r.GetInt32(9);
                s.CountC      = r.GetInt32(10);
                s.CountD      = r.GetInt32(11);
            }
            return s;
        }

        public List<PlayRecord> GetRecentPlays(string? user = null, int limit = 10)
        {
            var list = new List<PlayRecord>();
            using var cmd = _conn.CreateCommand();
            string where = user != null ? "WHERE user = @u" : "";
            cmd.CommandText = $"SELECT id,user,song_id,difficulty,score,max_combo,hit,miss,accuracy,grade,played_at FROM plays {where} ORDER BY id DESC LIMIT @lim";
            if (user != null) cmd.Parameters.AddWithValue("@u", user);
            cmd.Parameters.AddWithValue("@lim", limit);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new PlayRecord
                {
                    Id = r.GetInt64(0),
                    User = r.GetString(1),
                    SongId = r.GetString(2),
                    Difficulty = r.GetString(3),
                    Score = r.GetInt32(4),
                    MaxCombo = r.GetInt32(5),
                    Hit = r.GetInt32(6),
                    Miss = r.GetInt32(7),
                    Accuracy = r.GetDouble(8),
                    Grade = r.GetString(9),
                    PlayedAt = r.GetString(10),
                });
            }
            return list;
        }

        public bool PlayExistsByTime(string user, string songId, string diff, string playedAt)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM plays WHERE user=@u AND song_id=@s AND difficulty=@d AND played_at=@t";
            cmd.Parameters.AddWithValue("@u", user);
            cmd.Parameters.AddWithValue("@s", songId);
            cmd.Parameters.AddWithValue("@d", diff);
            cmd.Parameters.AddWithValue("@t", playedAt);
            return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
        }

        public void RecordPlayWithTime(string user, string songId, string difficulty,
            int score, int maxCombo, int hit, int miss, double accuracy, string grade, string playedAt)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO plays (user, song_id, difficulty, score, max_combo, hit, miss, accuracy, grade, played_at)
                VALUES (@u, @s, @d, @sc, @mc, @h, @m, @a, @g, @t)";
            cmd.Parameters.AddWithValue("@u", user ?? "guest");
            cmd.Parameters.AddWithValue("@s", songId);
            cmd.Parameters.AddWithValue("@d", difficulty);
            cmd.Parameters.AddWithValue("@sc", score);
            cmd.Parameters.AddWithValue("@mc", maxCombo);
            cmd.Parameters.AddWithValue("@h", hit);
            cmd.Parameters.AddWithValue("@m", miss);
            cmd.Parameters.AddWithValue("@a", accuracy);
            cmd.Parameters.AddWithValue("@g", grade);
            cmd.Parameters.AddWithValue("@t", playedAt);
            cmd.ExecuteNonQuery();
        }

        public void Dispose()
        {
            _conn?.Close();
            _conn?.Dispose();
        }
    }
}
