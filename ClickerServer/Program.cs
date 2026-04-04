using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:5000");
var app = builder.Build();

// ── Database ────────────────────────────────────────────────────────
var db = new ServerDatabase("server_data.db");

// ── Auth ────────────────────────────────────────────────────────────
app.MapPost("/api/auth/register", (RegisterRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.PasswordHash))
        return Results.Ok(new AuthResponse(false, "Username and password required"));
    if (db.UserExists(req.Username))
        return Results.Ok(new AuthResponse(false, "Username already exists"));
    db.CreateUser(req.Username, req.PasswordHash);
    return Results.Ok(new AuthResponse(true, "Registered"));
});

app.MapPost("/api/auth/login", (LoginRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.PasswordHash))
        return Results.Ok(new AuthResponse(false, "Username and password required"));
    if (!db.ValidateUser(req.Username, req.PasswordHash))
        return Results.Ok(new AuthResponse(false, "Invalid username or password"));
    return Results.Ok(new AuthResponse(true, "Login successful"));
});

// ── Play Records ────────────────────────────────────────────────────
app.MapPost("/api/sync/plays", (SyncPlaysRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.User)) return Results.BadRequest("User required");
    int added = 0;
    foreach (var p in req.Plays)
    {
        if (!db.PlayExists(req.User, p.SongId, p.Difficulty, p.PlayedAt))
        {
            db.InsertPlay(req.User, p.SongId, p.Difficulty, p.Score, p.MaxCombo,
                p.Hit, p.Miss, p.Accuracy, p.Grade, p.PlayedAt);
            added++;
        }
    }
    return Results.Ok(new { ok = true, added });
});

app.MapGet("/api/sync/plays", (string user) =>
{
    if (string.IsNullOrWhiteSpace(user)) return Results.BadRequest("User required");
    var plays = db.GetPlays(user);
    return Results.Ok(plays);
});

// ── Achievements ────────────────────────────────────────────────────
app.MapPost("/api/sync/achievements", (SyncAchievementsRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.User)) return Results.BadRequest("User required");
    foreach (var a in req.Achievements)
    {
        db.UpsertAchievement(req.User, a.AchievementId, a.Unlocked, a.UnlockedAt);
    }
    return Results.Ok(new { ok = true });
});

app.MapGet("/api/sync/achievements", (string user) =>
{
    if (string.IsNullOrWhiteSpace(user)) return Results.BadRequest("User required");
    var achs = db.GetAchievements(user);
    return Results.Ok(achs);
});

// ── Settings ────────────────────────────────────────────────────────
app.MapPost("/api/sync/settings", (SettingsDto req) =>
{
    if (string.IsNullOrWhiteSpace(req.User)) return Results.BadRequest("User required");
    db.UpsertSettings(req.User, req.SettingsJson);
    return Results.Ok(new { ok = true });
});

app.MapGet("/api/sync/settings", (string user) =>
{
    if (string.IsNullOrWhiteSpace(user)) return Results.BadRequest("User required");
    var json = db.GetSettings(user);
    return json != null ? Results.Ok(new { ok = true, settings = json })
                        : Results.Ok(new { ok = false, settings = (string?)null });
});

// ── Health ──────────────────────────────────────────────────────────
app.MapGet("/api/ping", () => Results.Ok(new { ok = true, server = "RhythmClicker Server", version = "1.0" }));

Console.WriteLine("═══════════════════════════════════════");
Console.WriteLine("  RhythmClicker Server v1.0");
Console.WriteLine("  Listening on http://0.0.0.0:5000");
Console.WriteLine("═══════════════════════════════════════");

app.Run();

// ── DTO Models ──────────────────────────────────────────────────────
record RegisterRequest(string Username, string PasswordHash);
record LoginRequest(string Username, string PasswordHash);
record AuthResponse(bool Ok, string Message);

record PlayDto(string User, string SongId, string Difficulty, int Score,
    int MaxCombo, int Hit, int Miss, double Accuracy, string Grade, string PlayedAt);
record SyncPlaysRequest(string User, List<PlayDto> Plays);

record AchievementDto(string AchievementId, bool Unlocked, string UnlockedAt);
record SyncAchievementsRequest(string User, List<AchievementDto> Achievements);

record SettingsDto(string User, string SettingsJson);

// ── Server Database ─────────────────────────────────────────────────
class ServerDatabase : IDisposable
{
    private readonly SqliteConnection _conn;

    public ServerDatabase(string dbPath)
    {
        _conn = new SqliteConnection($"Data Source={dbPath}");
        _conn.Open();
        InitSchema();
    }

    void InitSchema()
    {
        Exec(@"
            CREATE TABLE IF NOT EXISTS users (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                username TEXT NOT NULL UNIQUE COLLATE NOCASE,
                password_hash TEXT NOT NULL,
                created_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            CREATE TABLE IF NOT EXISTS plays (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                user TEXT NOT NULL,
                song_id TEXT NOT NULL,
                difficulty TEXT NOT NULL,
                score INTEGER NOT NULL,
                max_combo INTEGER NOT NULL,
                hit INTEGER NOT NULL,
                miss INTEGER NOT NULL,
                accuracy REAL NOT NULL,
                grade TEXT NOT NULL,
                played_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_plays_user ON plays(user);
            CREATE UNIQUE INDEX IF NOT EXISTS idx_plays_dedup
                ON plays(user, song_id, difficulty, played_at);

            CREATE TABLE IF NOT EXISTS achievements (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                user TEXT NOT NULL,
                achievement_id TEXT NOT NULL,
                unlocked INTEGER NOT NULL DEFAULT 0,
                unlocked_at TEXT,
                UNIQUE(user, achievement_id)
            );

            CREATE TABLE IF NOT EXISTS settings (
                user TEXT PRIMARY KEY,
                settings_json TEXT NOT NULL,
                updated_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
        ");
    }

    void Exec(string sql)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    // ── Users ───────────────────────────────────────────────────────
    public bool UserExists(string username)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM users WHERE username = @u";
        cmd.Parameters.AddWithValue("@u", username);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    public void CreateUser(string username, string passwordHash)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT INTO users (username, password_hash) VALUES (@u, @p)";
        cmd.Parameters.AddWithValue("@u", username);
        cmd.Parameters.AddWithValue("@p", passwordHash);
        cmd.ExecuteNonQuery();
    }

    public bool ValidateUser(string username, string passwordHash)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM users WHERE username = @u AND password_hash = @p";
        cmd.Parameters.AddWithValue("@u", username);
        cmd.Parameters.AddWithValue("@p", passwordHash);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    // ── Plays ───────────────────────────────────────────────────────
    public bool PlayExists(string user, string songId, string diff, string playedAt)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM plays WHERE user=@u AND song_id=@s AND difficulty=@d AND played_at=@t";
        cmd.Parameters.AddWithValue("@u", user);
        cmd.Parameters.AddWithValue("@s", songId);
        cmd.Parameters.AddWithValue("@d", diff);
        cmd.Parameters.AddWithValue("@t", playedAt);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    public void InsertPlay(string user, string songId, string diff,
        int score, int maxCombo, int hit, int miss, double accuracy, string grade, string playedAt)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"INSERT OR IGNORE INTO plays
            (user,song_id,difficulty,score,max_combo,hit,miss,accuracy,grade,played_at)
            VALUES (@u,@s,@d,@sc,@mc,@h,@m,@a,@g,@t)";
        cmd.Parameters.AddWithValue("@u", user);
        cmd.Parameters.AddWithValue("@s", songId);
        cmd.Parameters.AddWithValue("@d", diff);
        cmd.Parameters.AddWithValue("@sc", score);
        cmd.Parameters.AddWithValue("@mc", maxCombo);
        cmd.Parameters.AddWithValue("@h", hit);
        cmd.Parameters.AddWithValue("@m", miss);
        cmd.Parameters.AddWithValue("@a", accuracy);
        cmd.Parameters.AddWithValue("@g", grade);
        cmd.Parameters.AddWithValue("@t", playedAt);
        cmd.ExecuteNonQuery();
    }

    public List<PlayDto> GetPlays(string user)
    {
        var list = new List<PlayDto>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT user,song_id,difficulty,score,max_combo,hit,miss,accuracy,grade,played_at FROM plays WHERE user=@u ORDER BY id DESC";
        cmd.Parameters.AddWithValue("@u", user);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new PlayDto(r.GetString(0), r.GetString(1), r.GetString(2),
                r.GetInt32(3), r.GetInt32(4), r.GetInt32(5), r.GetInt32(6),
                r.GetDouble(7), r.GetString(8), r.GetString(9)));
        }
        return list;
    }

    // ── Achievements ────────────────────────────────────────────────
    public void UpsertAchievement(string user, string achievementId, bool unlocked, string unlockedAt)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO achievements (user, achievement_id, unlocked, unlocked_at)
            VALUES (@u, @a, @ul, @t)
            ON CONFLICT(user, achievement_id) DO UPDATE
            SET unlocked = MAX(unlocked, @ul), unlocked_at = COALESCE(NULLIF(@t,''), unlocked_at)";
        cmd.Parameters.AddWithValue("@u", user);
        cmd.Parameters.AddWithValue("@a", achievementId);
        cmd.Parameters.AddWithValue("@ul", unlocked ? 1 : 0);
        cmd.Parameters.AddWithValue("@t", unlockedAt ?? "");
        cmd.ExecuteNonQuery();
    }

    public List<AchievementDto> GetAchievements(string user)
    {
        var list = new List<AchievementDto>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT achievement_id, unlocked, unlocked_at FROM achievements WHERE user=@u";
        cmd.Parameters.AddWithValue("@u", user);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new AchievementDto(r.GetString(0), r.GetInt32(1) != 0, r.IsDBNull(2) ? "" : r.GetString(2)));
        }
        return list;
    }

    // ── Settings ────────────────────────────────────────────────────
    public void UpsertSettings(string user, string settingsJson)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO settings (user, settings_json, updated_at)
            VALUES (@u, @j, datetime('now'))
            ON CONFLICT(user) DO UPDATE SET settings_json=@j, updated_at=datetime('now')";
        cmd.Parameters.AddWithValue("@u", user);
        cmd.Parameters.AddWithValue("@j", settingsJson);
        cmd.ExecuteNonQuery();
    }

    public string? GetSettings(string user)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT settings_json FROM settings WHERE user=@u";
        cmd.Parameters.AddWithValue("@u", user);
        return cmd.ExecuteScalar() as string;
    }

    public void Dispose()
    {
        _conn?.Close();
        _conn?.Dispose();
    }
}
