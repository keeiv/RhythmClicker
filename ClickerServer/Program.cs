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
app.MapGet("/api/ping", () => Results.Ok(new { ok = true, server = "RhythmClicker Server", version = "1.1" }));

// ── Profile ─────────────────────────────────────────────────────────
app.MapPost("/api/profile", (ProfileDto req) =>
{
    if (string.IsNullOrWhiteSpace(req.User)) return Results.BadRequest("User required");
    db.UpsertProfile(req.User, req.AvatarId, req.BannerId, req.Bio, req.Region);
    return Results.Ok(new { ok = true });
});

app.MapGet("/api/profile", (string user) =>
{
    if (string.IsNullOrWhiteSpace(user)) return Results.BadRequest("User required");
    var profile = db.GetProfile(user);
    if (profile == null) return Results.Ok(new { ok = false });
    var badges = db.GetBadges(user);
    var stats = db.GetPlayerStats(user);
    return Results.Ok(new
    {
        ok = true,
        profile = new
        {
            user = profile.Value.user,
            avatarId = profile.Value.avatarId,
            bannerId = profile.Value.bannerId,
            bio = profile.Value.bio,
            region = profile.Value.region,
            createdAt = profile.Value.createdAt,
            badges,
            totalPlays = stats.totalPlays,
            bestCombo = stats.bestCombo,
            avgAccuracy = stats.avgAccuracy,
            bestGrades = stats.bestGrades
        }
    });
});

// ── Search Players ──────────────────────────────────────────────────
app.MapGet("/api/players/search", (string q) =>
{
    if (string.IsNullOrWhiteSpace(q)) return Results.BadRequest("Query required");
    var results = db.SearchPlayers(q);
    return Results.Ok(results);
});

// ── Badges (admin) ──────────────────────────────────────────────────
app.MapPost("/api/badges/grant", (BadgeGrantRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.User) || string.IsNullOrWhiteSpace(req.BadgeId))
        return Results.BadRequest("User and BadgeId required");
    db.GrantBadge(req.User, req.BadgeId, req.BadgeName, req.BadgeColor);
    return Results.Ok(new { ok = true });
});

Console.WriteLine("═══════════════════════════════════════");
Console.WriteLine("  RhythmClicker Server v1.1");
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

record ProfileDto(string User, string AvatarId, string BannerId, string Bio, string Region);
record BadgeGrantRequest(string User, string BadgeId, string BadgeName, string BadgeColor);

// ── Server Database ─────────────────────────────────────────────────
class ServerDatabase : IDisposable
{
    private readonly SqliteConnection _conn;

    public ServerDatabase(string dbPath)
    {
        _conn = new SqliteConnection($"Data Source={dbPath}");
        _conn.Open();
        InitSchema();
        SeedBadges();
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

            CREATE TABLE IF NOT EXISTS profiles (
                user TEXT PRIMARY KEY,
                avatar_id TEXT NOT NULL DEFAULT 'default',
                banner_id TEXT NOT NULL DEFAULT 'default',
                bio TEXT NOT NULL DEFAULT '',
                region TEXT NOT NULL DEFAULT '',
                updated_at TEXT NOT NULL DEFAULT (datetime('now'))
            );

            CREATE TABLE IF NOT EXISTS badges (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                user TEXT NOT NULL,
                badge_id TEXT NOT NULL,
                badge_name TEXT NOT NULL,
                badge_color TEXT NOT NULL DEFAULT '#FFD700',
                granted_at TEXT NOT NULL DEFAULT (datetime('now')),
                UNIQUE(user, badge_id)
            );
        ");
    }

    void SeedBadges()
    {
        string[] devUsers = { "keeiv", "Finn" };
        string[][] seedBadges = {
            new[] { "DEV", "DEV", "#FF4444" },
            new[] { "MoriTeahouse", "MoriTeahouse", "#88DDAA" }
        };
        foreach (var user in devUsers)
            foreach (var b in seedBadges)
                GrantBadge(user, b[0], b[1], b[2]);
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

    // ── Profiles ────────────────────────────────────────────────────
    public void UpsertProfile(string user, string avatarId, string bannerId, string bio, string region)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO profiles (user, avatar_id, banner_id, bio, region, updated_at)
            VALUES (@u, @a, @b, @bio, @r, datetime('now'))
            ON CONFLICT(user) DO UPDATE SET avatar_id=@a, banner_id=@b, bio=@bio, region=@r, updated_at=datetime('now')";
        cmd.Parameters.AddWithValue("@u", user);
        cmd.Parameters.AddWithValue("@a", avatarId ?? "default");
        cmd.Parameters.AddWithValue("@b", bannerId ?? "default");
        cmd.Parameters.AddWithValue("@bio", bio ?? "");
        cmd.Parameters.AddWithValue("@r", region ?? "");
        cmd.ExecuteNonQuery();
    }

    public (string user, string avatarId, string bannerId, string bio, string region, string createdAt)? GetProfile(string user)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"SELECT p.user, p.avatar_id, p.banner_id, p.bio, p.region, u.created_at
            FROM profiles p LEFT JOIN users u ON LOWER(p.user)=LOWER(u.username) WHERE LOWER(p.user)=LOWER(@u)";
        cmd.Parameters.AddWithValue("@u", user);
        using var r = cmd.ExecuteReader();
        if (r.Read())
            return (r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4),
                    r.IsDBNull(5) ? "" : r.GetString(5));
        // No profile yet — return defaults from users table
        using var cmd2 = _conn.CreateCommand();
        cmd2.CommandText = "SELECT username, created_at FROM users WHERE LOWER(username)=LOWER(@u)";
        cmd2.Parameters.AddWithValue("@u", user);
        using var r2 = cmd2.ExecuteReader();
        if (r2.Read())
            return (r2.GetString(0), "default", "default", "", "", r2.GetString(1));
        return null;
    }

    public (int totalPlays, int bestCombo, double avgAccuracy, List<object> bestGrades) GetPlayerStats(string user)
    {
        var grades = new List<object>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"SELECT COUNT(*), COALESCE(MAX(max_combo),0), COALESCE(AVG(accuracy),0) FROM plays WHERE user=@u";
        cmd.Parameters.AddWithValue("@u", user);
        using var r = cmd.ExecuteReader();
        int tp = 0; int bc = 0; double aa = 0;
        if (r.Read()) { tp = r.GetInt32(0); bc = r.GetInt32(1); aa = r.GetDouble(2); }

        // Best grade per song+difficulty
        using var cmd2 = _conn.CreateCommand();
        cmd2.CommandText = @"SELECT song_id, difficulty, MAX(score) as best_score,
            (SELECT grade FROM plays p2 WHERE p2.user=@u AND p2.song_id=plays.song_id AND p2.difficulty=plays.difficulty ORDER BY score DESC LIMIT 1) as best_grade,
            MAX(max_combo), MAX(accuracy)
            FROM plays WHERE user=@u GROUP BY song_id, difficulty ORDER BY best_score DESC LIMIT 50";
        cmd2.Parameters.AddWithValue("@u", user);
        using var r2 = cmd2.ExecuteReader();
        while (r2.Read())
        {
            grades.Add(new
            {
                songId = r2.GetString(0),
                difficulty = r2.GetString(1),
                bestScore = r2.GetInt32(2),
                bestGrade = r2.GetString(3),
                bestCombo = r2.GetInt32(4),
                bestAccuracy = r2.GetDouble(5)
            });
        }
        return (tp, bc, aa, grades);
    }

    // ── Badges ──────────────────────────────────────────────────────
    public void GrantBadge(string user, string badgeId, string badgeName, string badgeColor)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO badges (user, badge_id, badge_name, badge_color)
            VALUES (@u, @bid, @bn, @bc)
            ON CONFLICT(user, badge_id) DO UPDATE SET badge_name=@bn, badge_color=@bc";
        cmd.Parameters.AddWithValue("@u", user);
        cmd.Parameters.AddWithValue("@bid", badgeId);
        cmd.Parameters.AddWithValue("@bn", badgeName);
        cmd.Parameters.AddWithValue("@bc", badgeColor);
        cmd.ExecuteNonQuery();
    }

    public List<object> GetBadges(string user)
    {
        var list = new List<object>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT badge_id, badge_name, badge_color FROM badges WHERE LOWER(user)=LOWER(@u)";
        cmd.Parameters.AddWithValue("@u", user);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new { badgeId = r.GetString(0), badgeName = r.GetString(1), badgeColor = r.GetString(2) });
        return list;
    }

    // ── Search ──────────────────────────────────────────────────────
    public List<object> SearchPlayers(string query)
    {
        var list = new List<object>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"SELECT u.username, u.created_at,
                COALESCE(p.avatar_id, 'default'), COALESCE(p.banner_id, 'default'),
                COALESCE(p.region, ''),
                (SELECT COUNT(*) FROM plays WHERE user=u.username),
                COALESCE((SELECT MAX(max_combo) FROM plays WHERE user=u.username), 0),
                COALESCE((SELECT AVG(accuracy) FROM plays WHERE user=u.username), 0)
            FROM users u LEFT JOIN profiles p ON LOWER(u.username)=LOWER(p.user)
            WHERE u.username LIKE @q LIMIT 20";
        cmd.Parameters.AddWithValue("@q", $"%{query}%");
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var user = r.GetString(0);
            var badges = GetBadges(user);
            list.Add(new
            {
                username = user,
                createdAt = r.GetString(1),
                avatarId = r.GetString(2),
                bannerId = r.GetString(3),
                region = r.GetString(4),
                totalPlays = r.GetInt32(5),
                bestCombo = r.GetInt32(6),
                avgAccuracy = r.GetDouble(7),
                badges
            });
        }
        return list;
    }

    public void Dispose()
    {
        _conn?.Close();
        _conn?.Dispose();
    }
}
