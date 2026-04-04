using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ClickerGame
{
    /// <summary>
    /// Client-side cloud sync manager. Communicates with ClickerServer REST API.
    /// All operations are fire-and-forget safe (failures are silently logged).
    /// </summary>
    public class CloudSyncManager
    {
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public string ServerUrl { get; set; }
        public bool IsConnected { get; private set; }
        public string LastError { get; private set; } = "";

        public CloudSyncManager(string serverUrl = "http://localhost:5000")
        {
            ServerUrl = serverUrl.TrimEnd('/');
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        }

        // ── Connection Check ────────────────────────────────────────
        public async Task<bool> CheckConnectionAsync()
        {
            try
            {
                var resp = await _http.GetAsync($"{ServerUrl}/api/ping");
                IsConnected = resp.IsSuccessStatusCode;
                return IsConnected;
            }
            catch
            {
                IsConnected = false;
                return false;
            }
        }

        // ── Auth ────────────────────────────────────────────────────
        public async Task<(bool ok, string message)> RegisterAsync(string username, string passwordHash)
        {
            try
            {
                var body = Json(new { username, passwordHash });
                var resp = await _http.PostAsync($"{ServerUrl}/api/auth/register", body);
                var result = await ReadJson<AuthResponse>(resp);
                return (result?.Ok ?? false, result?.Message ?? "Unknown error");
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return (false, "Server unreachable");
            }
        }

        public async Task<(bool ok, string message)> LoginAsync(string username, string passwordHash)
        {
            try
            {
                var body = Json(new { username, passwordHash });
                var resp = await _http.PostAsync($"{ServerUrl}/api/auth/login", body);
                var result = await ReadJson<AuthResponse>(resp);
                return (result?.Ok ?? false, result?.Message ?? "Unknown error");
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return (false, "Server unreachable");
            }
        }

        // ── Play Records ────────────────────────────────────────────
        public async Task UploadPlayAsync(string user, string songId, string difficulty,
            int score, int maxCombo, int hit, int miss, double accuracy, string grade, string playedAt)
        {
            try
            {
                var plays = new[] { new { songId, difficulty, score, maxCombo, hit, miss, accuracy, grade, playedAt } };
                var body = Json(new { user, plays });
                await _http.PostAsync($"{ServerUrl}/api/sync/plays", body);
            }
            catch (Exception ex) { LastError = ex.Message; }
        }

        public async Task<List<PlayRecordDto>> DownloadPlaysAsync(string user)
        {
            try
            {
                var resp = await _http.GetAsync($"{ServerUrl}/api/sync/plays?user={Uri.EscapeDataString(user)}");
                var json = await resp.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<PlayRecordDto>>(json, _jsonOpts) ?? new();
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return new();
            }
        }

        public async Task UploadAllPlaysAsync(string user, List<PlayRecord> records)
        {
            try
            {
                var plays = new List<object>();
                foreach (var r in records)
                {
                    plays.Add(new
                    {
                        songId = r.SongId,
                        difficulty = r.Difficulty,
                        score = r.Score,
                        maxCombo = r.MaxCombo,
                        hit = r.Hit,
                        miss = r.Miss,
                        accuracy = r.Accuracy,
                        grade = r.Grade,
                        playedAt = r.PlayedAt
                    });
                }
                var body = Json(new { user, plays });
                await _http.PostAsync($"{ServerUrl}/api/sync/plays", body);
            }
            catch (Exception ex) { LastError = ex.Message; }
        }

        // ── Achievements ────────────────────────────────────────────
        public async Task UploadAchievementsAsync(string user, List<Achievement> achievements)
        {
            try
            {
                var achs = new List<object>();
                foreach (var a in achievements)
                {
                    achs.Add(new { achievementId = a.Id, unlocked = a.Unlocked, unlockedAt = a.UnlockedAt });
                }
                var body = Json(new { user, achievements = achs });
                await _http.PostAsync($"{ServerUrl}/api/sync/achievements", body);
            }
            catch (Exception ex) { LastError = ex.Message; }
        }

        public async Task<List<AchievementSyncDto>> DownloadAchievementsAsync(string user)
        {
            try
            {
                var resp = await _http.GetAsync($"{ServerUrl}/api/sync/achievements?user={Uri.EscapeDataString(user)}");
                var json = await resp.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<AchievementSyncDto>>(json, _jsonOpts) ?? new();
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return new();
            }
        }

        // ── Settings ────────────────────────────────────────────────
        public async Task UploadSettingsAsync(string user, GameSettings settings)
        {
            try
            {
                var settingsJson = JsonSerializer.Serialize(settings, _jsonOpts);
                var body = Json(new { user, settingsJson });
                await _http.PostAsync($"{ServerUrl}/api/sync/settings", body);
            }
            catch (Exception ex) { LastError = ex.Message; }
        }

        public async Task<GameSettings?> DownloadSettingsAsync(string user)
        {
            try
            {
                var resp = await _http.GetAsync($"{ServerUrl}/api/sync/settings?user={Uri.EscapeDataString(user)}");
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("ok", out var okProp) && okProp.GetBoolean()
                    && root.TryGetProperty("settings", out var settingsProp)
                    && settingsProp.ValueKind == JsonValueKind.String)
                {
                    var settingsStr = settingsProp.GetString();
                    if (!string.IsNullOrEmpty(settingsStr))
                        return JsonSerializer.Deserialize<GameSettings>(settingsStr, _jsonOpts);
                }
                return null;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return null;
            }
        }

        // ── Full Sync (called after login) ──────────────────────────
        public async Task<SyncResult> FullSyncAsync(string user, StatsDatabase? localDb,
            AchievementManager? achManager, SettingsManager? settingsManager)
        {
            var result = new SyncResult();
            if (!await CheckConnectionAsync())
            {
                result.Message = "Server offline";
                return result;
            }

            // Upload local plays → server
            if (localDb != null)
            {
                var localPlays = localDb.GetRecentPlays(user, 9999);
                if (localPlays.Count > 0)
                    await UploadAllPlaysAsync(user, localPlays);

                // Download server plays → local
                var serverPlays = await DownloadPlaysAsync(user);
                int imported = 0;
                foreach (var sp in serverPlays)
                {
                    if (!localDb.PlayExistsByTime(user, sp.SongId, sp.Difficulty, sp.PlayedAt))
                    {
                        localDb.RecordPlayWithTime(user, sp.SongId, sp.Difficulty,
                            sp.Score, sp.MaxCombo, sp.Hit, sp.Miss, sp.Accuracy, sp.Grade, sp.PlayedAt);
                        imported++;
                    }
                }
                result.PlaysImported = imported;
            }

            // Sync achievements (merge: union of unlocked)
            if (achManager != null)
            {
                await UploadAchievementsAsync(user, achManager.GetAll());
                var serverAchs = await DownloadAchievementsAsync(user);
                foreach (var sa in serverAchs)
                {
                    if (sa.Unlocked)
                        achManager.ForceUnlock(sa.AchievementId, sa.UnlockedAt);
                }
                result.AchievementsSynced = serverAchs.Count;
            }

            // Sync settings (server wins if exists, otherwise upload local)
            if (settingsManager != null)
            {
                var serverSettings = await DownloadSettingsAsync(user);
                if (serverSettings != null)
                {
                    settingsManager.Settings = serverSettings;
                    settingsManager.Save();
                    result.SettingsSynced = true;
                }
                else
                {
                    await UploadSettingsAsync(user, settingsManager.Settings);
                    result.SettingsSynced = true;
                }
            }

            result.Success = true;
            result.Message = "Sync complete";
            return result;
        }

        // ── Helpers ─────────────────────────────────────────────────
        StringContent Json(object obj) =>
            new(JsonSerializer.Serialize(obj, _jsonOpts), Encoding.UTF8, "application/json");

        async Task<T?> ReadJson<T>(HttpResponseMessage resp) where T : class
        {
            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(json, _jsonOpts);
        }

        record AuthResponse(bool Ok, string Message);

        // ── Profile ─────────────────────────────────────────────────
        public async Task<bool> UploadProfileAsync(string user, string avatarId, string bannerId, string bio, string region)
        {
            try
            {
                var resp = await _http.PostAsync($"{ServerUrl}/api/profile",
                    Json(new { user, avatarId, bannerId, bio, region }));
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<PlayerProfileDto?> GetProfileAsync(string user)
        {
            try
            {
                var resp = await _http.GetAsync($"{ServerUrl}/api/profile?user={Uri.EscapeDataString(user)}");
                if (!resp.IsSuccessStatusCode) return null;
                var json = await resp.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.GetProperty("ok").GetBoolean()) return null;
                var p = root.GetProperty("profile");
                var profile = new PlayerProfileDto
                {
                    User = p.GetProperty("user").GetString() ?? user,
                    AvatarId = p.GetProperty("avatarId").GetString() ?? "default",
                    BannerId = p.GetProperty("bannerId").GetString() ?? "default",
                    Bio = p.GetProperty("bio").GetString() ?? "",
                    Region = p.GetProperty("region").GetString() ?? "",
                    CreatedAt = p.GetProperty("createdAt").GetString() ?? "",
                    TotalPlays = p.GetProperty("totalPlays").GetInt32(),
                    BestCombo = p.GetProperty("bestCombo").GetInt32(),
                    AvgAccuracy = p.GetProperty("avgAccuracy").GetDouble(),
                };
                if (p.TryGetProperty("badges", out var badgesArr))
                    foreach (var b in badgesArr.EnumerateArray())
                        profile.Badges.Add(new BadgeDto
                        {
                            BadgeId = b.GetProperty("badgeId").GetString() ?? "",
                            BadgeName = b.GetProperty("badgeName").GetString() ?? "",
                            BadgeColor = b.GetProperty("badgeColor").GetString() ?? "#FFD700"
                        });
                if (p.TryGetProperty("bestGrades", out var gradesArr))
                    foreach (var g in gradesArr.EnumerateArray())
                        profile.BestGrades.Add(new BestGradeDto
                        {
                            SongId = g.GetProperty("songId").GetString() ?? "",
                            Difficulty = g.GetProperty("difficulty").GetString() ?? "",
                            BestScore = g.GetProperty("bestScore").GetInt32(),
                            BestGrade = g.GetProperty("bestGrade").GetString() ?? "",
                            BestCombo = g.GetProperty("bestCombo").GetInt32(),
                            BestAccuracy = g.GetProperty("bestAccuracy").GetDouble()
                        });
                return profile;
            }
            catch { return null; }
        }

        public async Task<List<PlayerSearchResult>> SearchPlayersAsync(string query)
        {
            var results = new List<PlayerSearchResult>();
            try
            {
                var resp = await _http.GetAsync($"{ServerUrl}/api/players/search?q={Uri.EscapeDataString(query)}");
                if (!resp.IsSuccessStatusCode) return results;
                var json = await resp.Content.ReadAsStringAsync();
                var arr = JsonDocument.Parse(json).RootElement;
                foreach (var el in arr.EnumerateArray())
                {
                    var item = new PlayerSearchResult
                    {
                        Username = el.GetProperty("username").GetString() ?? "",
                        AvatarId = el.GetProperty("avatarId").GetString() ?? "default",
                        Region = el.GetProperty("region").GetString() ?? "",
                        TotalPlays = el.GetProperty("totalPlays").GetInt32(),
                        BestCombo = el.GetProperty("bestCombo").GetInt32(),
                        AvgAccuracy = el.GetProperty("avgAccuracy").GetDouble()
                    };
                    if (el.TryGetProperty("badges", out var badges))
                        foreach (var b in badges.EnumerateArray())
                            item.Badges.Add(new BadgeDto
                            {
                                BadgeId = b.GetProperty("badgeId").GetString() ?? "",
                                BadgeName = b.GetProperty("badgeName").GetString() ?? "",
                                BadgeColor = b.GetProperty("badgeColor").GetString() ?? "#FFD700"
                            });
                    results.Add(item);
                }
            }
            catch { }
            return results;
        }
    }

    // ── Sync DTOs ───────────────────────────────────────────────────
    public class PlayRecordDto
    {
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

    public class AchievementSyncDto
    {
        public string AchievementId { get; set; } = "";
        public bool Unlocked { get; set; }
        public string UnlockedAt { get; set; } = "";
    }

    public class SyncResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public int PlaysImported { get; set; }
        public int AchievementsSynced { get; set; }
        public bool SettingsSynced { get; set; }
    }

    public class PlayerProfileDto
    {
        public string User { get; set; } = "";
        public string AvatarId { get; set; } = "default";
        public string BannerId { get; set; } = "default";
        public string Bio { get; set; } = "";
        public string Region { get; set; } = "";
        public string CreatedAt { get; set; } = "";
        public int TotalPlays { get; set; }
        public int BestCombo { get; set; }
        public double AvgAccuracy { get; set; }
        public List<BadgeDto> Badges { get; set; } = new();
        public List<BestGradeDto> BestGrades { get; set; } = new();
    }

    public class BadgeDto
    {
        public string BadgeId { get; set; } = "";
        public string BadgeName { get; set; } = "";
        public string BadgeColor { get; set; } = "#FFD700";
    }

    public class BestGradeDto
    {
        public string SongId { get; set; } = "";
        public string Difficulty { get; set; } = "";
        public int BestScore { get; set; }
        public string BestGrade { get; set; } = "";
        public int BestCombo { get; set; }
        public double BestAccuracy { get; set; }
    }

    public class PlayerSearchResult
    {
        public string Username { get; set; } = "";
        public string AvatarId { get; set; } = "default";
        public string Region { get; set; } = "";
        public int TotalPlays { get; set; }
        public int BestCombo { get; set; }
        public double AvgAccuracy { get; set; }
        public List<BadgeDto> Badges { get; set; } = new();
    }
}
