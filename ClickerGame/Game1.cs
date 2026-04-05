using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Audio;

namespace ClickerGame
{
    public class Game1 : Game
    {
        GraphicsDeviceManager? graphics;
        SpriteBatch? spriteBatch;
        Texture2D? pixel;
        int width = 800, height = 600;
        Beatmap? beatmap;
        LinkedList<Note> notes = new();
        SoundEffect? songEffect;
        SoundEffectInstance? songInstance;

        // Menu background music
        SoundEffect? menuMusicEffect;
        SoundEffectInstance? menuMusicInstance;

        Stopwatch stopwatch = new Stopwatch();
        int score = 0;
        KeyboardState kb, prevKb;
        MouseState mouseState, prevMouseState;
        int prevScrollValue;

        // Song selection
        List<SongInfo> songs = new();
        int currentSongIndex = 0;
        string currentDifficulty = "easy";

        // Menu / scene
        enum GameState { Menu, Playing, Result, Account, Language, Stats, BeatmapEditor, Settings, Achievements, ReplayView, Profile, SearchPlayer, EditProfile }
        GameState state = GameState.Menu;
        string[] menuKeys = new[] { "menu_start", "menu_editor", "menu_stats", "menu_profile", "menu_search", "menu_settings", "menu_achievements", "menu_account", "menu_language", "menu_exit" };
        int currentMenuIndex = 0;

        // Language selection
        int languageMenuIndex = 0;

        // Saved windowed dimensions
        int windowedWidth = 800;
        int windowedHeight = 600;

        TextRenderer? textRenderer;
        Texture2D? circleTexture;

        // input feedback
        public class KeyFlash
        {
            public Rectangle Rect;
            public Color Color;
            public float TimeToLive;
            public void Reset(Rectangle rect, Color color, float ttl)
            { Rect = rect; Color = color; TimeToLive = ttl; }
        }
        List<KeyFlash> keyFlashes = new();
        ObjectPool<KeyFlash>? keyFlashPool;

        // result
        int maxScore = 0;
        bool summaryShown = false;
        int resultMenuIndex = 0;
        double songDurationSeconds = 0.0;
        RenderCache? renderCache;

        // account
        AccountsManager? accountsManager;
        string accountUsername = string.Empty;
        string accountPassword = string.Empty;
        bool accountShowMessage = false;
        string accountMessage = string.Empty;
        int accountFieldIndex = 0;
        bool accountIsLoginMode = true;

        string resultGrade = "";

        // Combo/stats
        int combo = 0;
        int maxCombo = 0;
        int hitCount = 0;
        int missCount = 0;

        // Systems
        StatsDatabase? statsDb;
        DiscordRpcManager? discordRpc;
        SettingsManager? settingsManager;
        AchievementManager? achievementManager;
        ReplayManager? replayManager;
        CloudSyncManager? cloudSync;
        string syncStatusText = "";
        float syncStatusTimer = 0f;

        // Profile
        PlayerProfileDto? viewingProfile;
        bool profileLoading = false;
        int profileScrollIndex = 0;

        // Edit Profile
        int editProfileFieldIndex = 0; // 0=avatar, 1=banner, 2=bio, 3=region, 4=save
        string editBio = "";
        string editRegion = "";
        int editAvatarIndex = 0;
        int editBannerIndex = 0;
        bool editProfileSaving = false;
        string editProfileMessage = "";
        float editProfileMsgTimer = 0f;

        // Custom avatar
        Texture2D? customAvatarTexture;
        string customAvatarPath = "";
        static readonly string AvatarsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Avatars");

        // Avatar/Banner presets
        static readonly (string id, string label, Color bg, Color fg, string icon)[] AvatarPresets = new[]
        {
            ("default",  "Default",   new Color(20, 20, 40),    new Color(0, 200, 255),   ""),
            ("blue",     "Ocean",     new Color(20, 60, 120),   new Color(100, 200, 255), "~"),
            ("red",      "Flame",     new Color(120, 20, 20),   new Color(255, 120, 80),  "*"),
            ("green",    "Forest",    new Color(20, 80, 40),    new Color(80, 255, 120),   "T"),
            ("purple",   "Nebula",    new Color(60, 20, 100),   new Color(200, 140, 255), "."),
            ("gold",     "Crown",     new Color(80, 60, 10),    new Color(255, 220, 50),  "W"),
            ("pink",     "Sakura",    new Color(100, 30, 60),   new Color(255, 160, 200), "*"),
            ("cyan",     "Ice",       new Color(10, 60, 80),    new Color(80, 240, 255),  "#"),
            ("orange",   "Sunset",    new Color(100, 50, 10),   new Color(255, 180, 60),  "~"),
            ("white",    "Ghost",     new Color(60, 60, 60),    new Color(220, 220, 230), "?"),
            ("custom",   "Custom",    new Color(40, 40, 40),    new Color(200, 200, 200), "+"),
        };
        static readonly (string id, string label, Color color)[] BannerPresets = new[]
        {
            ("default",  "Default",   new Color(30, 40, 80)),
            ("crimson",  "Crimson",   new Color(100, 20, 30)),
            ("navy",     "Navy",      new Color(15, 25, 80)),
            ("emerald",  "Emerald",   new Color(15, 70, 40)),
            ("violet",   "Violet",    new Color(60, 20, 90)),
            ("midnight", "Midnight",  new Color(10, 10, 30)),
            ("sunset",   "Sunset",    new Color(100, 50, 20)),
            ("rose",     "Rose",      new Color(90, 30, 50)),
        };

        // Search
        string searchQuery = "";
        List<PlayerSearchResult> searchResults = new();
        int searchSelectedIndex = 0;
        bool searchLoading = false;

        // Settings UI state
        int settingsMenuIndex = 0;
        bool settingsBindingMode = false;
        int settingsBindingLane = -1;

        // Achievement popup
        float achievementPopupTimer = 0f;
        string achievementPopupText = "";

        // Replay playback state
        ReplayData? replayData;
        int replayEventIndex;
        List<JudgmentPopup> replayJudgments = new();

        // Achievements screen scroll
        int achievementsScrollIndex = 0;

        // Difficulty abbreviation mapping
        static readonly Dictionary<string, string> DiffAbbrev = new()
        {
            ["easy"] = "EZ",
            ["hard"] = "HD",
            ["difficulty"] = "DIFF",
            ["very_difficulty"] = "VDIFF",
        };

        static string DiffShort(string d) => DiffAbbrev.TryGetValue(d, out var s) ? s : d.ToUpper();

        // Note colors
        static readonly Color[] NoteColors = { new(0, 200, 255), new(255, 60, 140), new(255, 220, 50), new(80, 255, 120) };
        static readonly string[] LaneKeys = { "D", "F", "J", "K" };

        // Judgment popup
        class JudgmentPopup { public string Text = ""; public Color Color; public float Timer; public Vector2 Position; }
        List<JudgmentPopup> judgmentPopups = new();

        // Particles
        class HitParticle { public Vector2 Pos, Vel; public Color Color; public float Life, MaxLife, Size; }
        List<HitParticle> particles = new();
        Random rng = new();

        float shakeTimer;
        float shakeIntensity;
        float beatPulseAlpha;
        SoundEffect? sfxHit;
        SoundEffect? sfxMiss;

        // HP system (SAO-style)
        float hp = GameConfig.InitialHP;
        bool hpDepleted = false;

        // Judgment counters
        int perfectCount = 0;
        int greatCount = 0;
        int goodCount = 0;

        // Video background
        VideoBackgroundPlayer? videoPlayer;
        Texture2D? bgImageTexture;
        string currentVideoPath = "";
        string currentBgImagePath = "";

        // Combo tier
        int ComboTier => combo >= GameConfig.ComboTier4 ? 4
                       : combo >= GameConfig.ComboTier3 ? 3
                       : combo >= GameConfig.ComboTier2 ? 2
                       : combo >= GameConfig.ComboTier1 ? 1 : 0;

        static readonly Color[][] TierNoteColors = new[]
        {
            new[] { new Color(0, 200, 255), new Color(255, 60, 140), new Color(255, 220, 50), new Color(80, 255, 120) },
            new[] { new Color(0, 255, 200), new Color(100, 255, 150), new Color(50, 255, 255), new Color(150, 255, 100) },
            new[] { new Color(200, 255, 0), new Color(255, 255, 50), new Color(150, 255, 0), new Color(255, 200, 0) },
            new[] { new Color(255, 180, 0), new Color(255, 140, 0), new Color(255, 220, 50), new Color(255, 120, 0) },
            new[] { new Color(255, 50, 100), new Color(255, 0, 180), new Color(255, 100, 50), new Color(255, 30, 220) },
        };

        static readonly Color[] TierGlowColor = { new(0,200,255), new(0,255,180), new(200,255,0), new(255,180,0), new(255,50,100) };

        // Lane layout
        const int LaneCount = 4;
        const int LaneWidth = 90;
        const int TotalLaneWidth = LaneCount * LaneWidth;
        const int NoteHeight = 22;
        const int HitZoneHeight = 70;
        int LaneLeft => (width - TotalLaneWidth) / 2;
        int HitZoneY => height - HitZoneHeight - 40;

        float menuTimer = 0f;
        int menuScrollOffset = 0;
        bool isFullscreen = false;
        bool editorMode = false; // legacy playing-editor flag

        // ═══════════ Beatmap Editor State ═══════════
        string edSongName = "";
        string edAuthor = "";
        string edAudioPath = "";
        string edBpm = "120";
        List<Note> edNotes = new();
        float edScrollTime = 0f; // current scroll position in seconds
        float edTotalTime = 10f;
        int edFieldFocus = -1; // -1=timeline, 0=name, 1=author, 2=audio, 3=bpm
        string edMessage = "";
        float edMessageTimer = 0f;
        Note? edDragging = null;
        bool edPreviewing = false;
        SoundEffect? edPreviewEffect;
        SoundEffectInstance? edPreviewInstance;
        Stopwatch edPreviewWatch = new();

        float EdPixelsPerSecond => (height - 120) / 4f; // 4 seconds visible at once
        float EdVisibleSeconds => (height - 120) / EdPixelsPerSecond;

        // Stats cached
        PlayerSummary? cachedStats;
        List<PlayRecord>? cachedRecent;

        class SongInfo
        {
            public string Id { get; set; } = "";
            public string Title { get; set; } = "";
            public string File { get; set; } = "";
            public List<string> Difficulties { get; set; } = new();
        }

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            graphics.PreferredBackBufferWidth = width;
            graphics.PreferredBackBufferHeight = height;
        }

        protected override void Initialize()
        {
            IsMouseVisible = true;
            Window.Title = "RhythmClicker";
            Window.AllowUserResizing = false;

            // Low-latency settings for better hit responsiveness
            IsFixedTimeStep = false;
            graphics!.SynchronizeWithVerticalRetrace = false;
            graphics.ApplyChanges();

            // File drop for editor audio import
            Window.FileDrop += OnFileDrop;

            // Register custom file type icons
            RegisterFileAssociations();

            base.Initialize();
        }

        void OnFileDrop(object? sender, FileDropEventArgs e)
        {
            if (e.Files == null || e.Files.Length == 0) return;
            string f = e.Files[0];
            string ext = Path.GetExtension(f).ToLowerInvariant();

            // osu! .osz package import (ZIP containing .osu + audio)
            if (ext == ".osz")
            {
                try
                {
                    // Pre-compute songId for unique audio naming
                    string tempDir = Path.Combine(Path.GetTempPath(), "rc_osz_peek_" + Path.GetFileNameWithoutExtension(f));
                    string previewId = Path.GetFileNameWithoutExtension(f).Trim().ToLowerInvariant()
                        .Replace(' ', '_').Replace("'", "").Replace("\"", "");
                    if (string.IsNullOrEmpty(previewId)) previewId = "osu_import_" + DateTime.Now.Ticks;

                    var imported = OsuImporter.ImportOsz(f, "Assets", previewId);
                    if (imported.Count == 0) return;

                    // Use first beatmap for metadata
                    var first = imported[0].beatmap;
                    string safeId = (first.Name ?? previewId).Trim().ToLowerInvariant()
                        .Replace(' ', '_').Replace("'", "").Replace("\"", "");
                    if (string.IsNullOrEmpty(safeId)) safeId = previewId;

                    // Sanitize difficulty labels and save each as .rcm
                    var difficulties = new List<string>();
                    foreach (var (bm, diffLabel) in imported)
                    {
                        string safeDiff = diffLabel.Trim().ToLowerInvariant()
                            .Replace(' ', '_').Replace("'", "").Replace("\"", "");
                        if (string.IsNullOrEmpty(safeDiff)) safeDiff = "easy";

                        // Avoid duplicate difficulty names
                        string finalDiff = safeDiff;
                        int dup = 1;
                        while (difficulties.Contains(finalDiff))
                            finalDiff = safeDiff + "_" + (++dup);

                        string rcmPath = Path.Combine("Assets", $"{safeId}_{finalDiff}.rcm");
                        RcFileManager.WriteBeatmap(rcmPath, bm);
                        difficulties.Add(finalDiff);
                    }

                    // Add to songs.json
                    string audioFile = first.AudioFile ?? "song1.wav";
                    if (!File.Exists(Path.Combine("Assets", audioFile))) audioFile = "song1.wav";

                    if (!songs.Any(s => s.Id == safeId))
                    {
                        songs.Add(new SongInfo { Id = safeId, Title = first.Name ?? safeId,
                            File = audioFile, Difficulties = difficulties });
                        var opts = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                        File.WriteAllText("Assets/songs.json", System.Text.Json.JsonSerializer.Serialize(songs, opts));
                    }
                    // Always switch to the imported song and reload
                    currentSongIndex = songs.FindIndex(s => s.Id == safeId);
                    if (currentSongIndex < 0) currentSongIndex = songs.Count - 1;
                    currentDifficulty = difficulties[0];
                    LoadCurrentSong();
                }
                catch { }
                return;
            }

            // osu! single .osu file import
            if (ext == ".osu")
            {
                try
                {
                    var imported = OsuImporter.Import(f);
                    string safeId = (imported.Name ?? "osu_import").Trim().ToLowerInvariant().Replace(' ', '_');
                    if (string.IsNullOrEmpty(safeId)) safeId = "osu_import_" + DateTime.Now.Ticks;

                    // Copy audio file if exists alongside .osu (convert to WAV if needed)
                    string osuDir = Path.GetDirectoryName(f) ?? ".";
                    string audioSrc = Path.Combine(osuDir, imported.AudioFile ?? "");
                    string wavName = "";
                    if (!string.IsNullOrEmpty(imported.AudioFile) && File.Exists(audioSrc))
                    {
                        // Use safeId prefix for unique audio filename
                        wavName = safeId + "_" + Path.GetFileNameWithoutExtension(imported.AudioFile) + ".wav";
                        string audioDest = Path.Combine("Assets", wavName);
                        if (!File.Exists(audioDest))
                        {
                            string ext2 = Path.GetExtension(audioSrc).ToLowerInvariant();
                            if (ext2 == ".wav") File.Copy(audioSrc, audioDest, false);
                            else
                            {
                                try { OsuImporter.ConvertToWavPublic(audioSrc, audioDest); }
                                catch { wavName = ""; }
                            }
                        }
                    }

                    // Save as .rcm
                    string rcmPath = Path.Combine("Assets", safeId + "_easy.rcm");
                    RcFileManager.WriteBeatmap(rcmPath, imported);

                    // Add to songs.json
                    string audioFile = !string.IsNullOrEmpty(wavName) ? wavName : "song1.wav";
                    if (!songs.Any(s => s.Id == safeId))
                    {
                        songs.Add(new SongInfo { Id = safeId, Title = imported.Name ?? safeId,
                            File = audioFile, Difficulties = new List<string> { "easy" } });
                        var opts = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                        File.WriteAllText("Assets/songs.json", System.Text.Json.JsonSerializer.Serialize(songs, opts));
                        currentSongIndex = songs.Count - 1;
                        currentDifficulty = "easy";
                        LoadCurrentSong();
                    }
                }
                catch { }
                return;
            }

            if (state != GameState.BeatmapEditor) return;
            if (ext == ".wav" || ext == ".ogg" || ext == ".mp3")
            {
                edAudioPath = f;
                // Try to determine duration
                try
                {
                    using var fs = File.OpenRead(f);
                    using var se = SoundEffect.FromStream(fs);
                    edTotalTime = (float)se.Duration.TotalSeconds + 1f;
                }
                catch { }
            }
            else if (ext == ".rcm")
            {
                // Import existing beatmap
                try
                {
                    var bm = RcFileManager.ReadBeatmap(f);
                    edNotes = bm.Notes ?? new List<Note>();
                    if (!string.IsNullOrEmpty(bm.Name)) edSongName = bm.Name;
                    if (!string.IsNullOrEmpty(bm.Author)) edAuthor = bm.Author;
                    if (bm.Bpm > 0) edBpm = bm.Bpm.ToString("F0");
                }
                catch { }
            }
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);
            pixel = new Texture2D(GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });

            textRenderer = new TextRenderer(GraphicsDevice);
            circleTexture = CreateCircleTexture(256, Color.White);
            renderCache = new RenderCache(GraphicsDevice);
            keyFlashPool = new ObjectPool<KeyFlash>(() => new KeyFlash(), 16);

            sfxHit = GenerateHitSfx();
            sfxMiss = GenerateMissSfx();

            // Initialize video background player
            videoPlayer = new VideoBackgroundPlayer(GraphicsDevice);

            textRenderer.Precache("CLICK", "Segoe UI", 56, Color.White);
            foreach (var lk in LaneKeys) textRenderer.Precache(lk, "Segoe UI", 18, new Color(180, 180, 200));

            Directory.CreateDirectory("Assets");
            EnsureExampleSongs();

            string songsMeta = "Assets/songs.json";
            if (!File.Exists(songsMeta) || !File.Exists("Assets/.audio_v3"))
                File.WriteAllText(songsMeta, DefaultSongsJson());
            var metaJson = File.ReadAllText(songsMeta);
            songs = System.Text.Json.JsonSerializer.Deserialize<List<SongInfo>>(metaJson,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

            LoadCurrentSong();
            accountsManager = new AccountsManager();
            statsDb = new StatsDatabase();
            settingsManager = new SettingsManager();
            achievementManager = new AchievementManager();
            replayManager = new ReplayManager();
            cloudSync = new CloudSyncManager();

            // Load local profile data
            LoadLocalProfile();

            // Discord RPC
            try { discordRpc = new DiscordRpcManager(); }
            catch { discordRpc = null; }

            // Menu music
            string menuBgmPath = "Assets/menu_bgm.wav";
            if (!File.Exists(menuBgmPath)) GenerateMenuMusicWav(menuBgmPath, 20.0f, 95f, 82.4);
            using (var fs = File.OpenRead(menuBgmPath))
            {
                menuMusicEffect = SoundEffect.FromStream(fs);
                menuMusicInstance = menuMusicEffect.CreateInstance();
                menuMusicInstance.IsLooped = true;
                menuMusicInstance.Volume = settingsManager?.Settings.MusicVolume ?? 0.35f;
            }
            menuMusicInstance.Play();
        }

        protected override void UnloadContent()
        {
            textRenderer?.Dispose();
            statsDb?.Dispose();
            discordRpc?.Dispose();
            videoPlayer?.Dispose();
            bgImageTexture?.Dispose();
            base.UnloadContent();
        }

        string DefaultSongsJson() => @"[
  { ""Id"": ""song1"", ""Title"": ""Example A"", ""File"": ""song1.wav"", ""Difficulties"": [""easy"", ""hard"", ""difficulty"", ""very_difficulty""] },
  { ""Id"": ""song2"", ""Title"": ""Example B"", ""File"": ""song2.wav"", ""Difficulties"": [""easy"", ""hard"", ""difficulty""] },
  { ""Id"": ""song3"", ""Title"": ""Example C"", ""File"": ""song3.wav"", ""Difficulties"": [""easy"", ""hard"", ""difficulty"", ""very_difficulty""] },
  { ""Id"": ""ba_unwelcome"", ""Title"": ""Unwelcome School"", ""File"": ""ba_unwelcome.wav"", ""Difficulties"": [""easy"", ""hard"", ""difficulty"", ""very_difficulty""] },
  { ""Id"": ""ba_constant"", ""Title"": ""Constant Moderato"", ""File"": ""ba_constant.wav"", ""Difficulties"": [""easy"", ""hard"", ""difficulty"", ""very_difficulty""] },
  { ""Id"": ""ba_midsummer"", ""Title"": ""Midsummer Daydream"", ""File"": ""ba_midsummer.wav"", ""Difficulties"": [""easy"", ""hard"", ""difficulty"", ""very_difficulty""] }
]";

        void EnsureExampleSongs()
        {
            string marker = "Assets/.audio_v4";
            if (File.Exists(marker)) return;

            GenerateMusicalWav("Assets/song1.wav", 8.0f, 120f, 110.0);
            GenerateMusicalWav("Assets/song2.wav", 6.0f, 130f, 130.8);
            GenerateMusicalWav("Assets/song3.wav", 10.0f, 100f, 82.4);
            GenerateMenuMusicWav("Assets/menu_bgm.wav", 20.0f, 95f, 82.4);

            // Blue Archive style songs
            GenerateBaStyleWav("Assets/ba_unwelcome.wav", 12.0f, 170f, 164.8, 0);  // Bright uptempo pop-rock
            GenerateBaStyleWav("Assets/ba_constant.wav", 14.0f, 132f, 146.8, 1);   // Smooth piano pop
            GenerateBaStyleWav("Assets/ba_midsummer.wav", 10.0f, 155f, 196.0, 2);  // Energetic electronic

            string[][] allSongDiffs = {
                new[] { "easy", "hard", "difficulty", "very_difficulty" },
                new[] { "easy", "hard", "difficulty" },
                new[] { "easy", "hard", "difficulty", "very_difficulty" },
                new[] { "easy", "hard", "difficulty", "very_difficulty" },
                new[] { "easy", "hard", "difficulty", "very_difficulty" },
                new[] { "easy", "hard", "difficulty", "very_difficulty" },
            };
            float[][] songParams = {
                new[] { 8.0f, 120f }, new[] { 6.0f, 130f }, new[] { 10.0f, 100f },
                new[] { 12.0f, 170f }, new[] { 14.0f, 132f }, new[] { 10.0f, 155f },
            };
            string[] songIds = { "song1", "song2", "song3", "ba_unwelcome", "ba_constant", "ba_midsummer" };

            for (int si = 0; si < songIds.Length; si++)
            {
                foreach (var d in allSongDiffs[si])
                    WriteBeatmapRcm($"Assets/{songIds[si]}_{d}.rcm", songParams[si][0], songParams[si][1], d);
            }

            File.WriteAllText(marker, "v4");
        }

        void WriteBeatmapRcm(string path, float dur, float bpm, string diff)
        {
            var bm = GenerateBeatmapObject(dur, bpm, diff);
            RcFileManager.WriteBeatmap(path, bm);
        }

        void LoadCurrentSong()
        {
            if (songs.Count == 0)
            {
                if (!File.Exists("Assets/song.wav")) GenerateMusicalWav("Assets/song.wav", 3.0f);
                LoadSong("Assets/song.wav", "Assets/beatmap.rcm");
                return;
            }
            var s = songs[Math.Clamp(currentSongIndex, 0, songs.Count - 1)];
            string songPath = Path.Combine("Assets", s.File);
            if (!File.Exists(songPath)) GenerateMusicalWav(songPath, 6.0f);

            string rcmPath = Path.Combine("Assets", s.Id + "_" + currentDifficulty + ".rcm");
            string jsonPath = Path.Combine("Assets", s.Id + "_" + currentDifficulty + ".json");

            if (File.Exists(rcmPath)) { LoadSongRcm(songPath, rcmPath); return; }
            if (File.Exists(jsonPath))
            {
                RcFileManager.MigrateJsonToRcm(jsonPath, rcmPath);
                if (File.Exists(rcmPath)) { LoadSongRcm(songPath, rcmPath); return; }
                LoadSong(songPath, jsonPath); return;
            }
            foreach (var d in s.Difficulties)
            {
                var rp = Path.Combine("Assets", s.Id + "_" + d + ".rcm");
                if (File.Exists(rp)) { currentDifficulty = d; LoadSongRcm(songPath, rp); return; }
            }
            var defBm = GenerateBeatmapObject(6.0f, 120f, currentDifficulty);
            RcFileManager.WriteBeatmap(rcmPath, defBm);
            LoadSongRcm(songPath, rcmPath);
        }

        void LoadSongRcm(string songFilePath, string rcmPath)
        {
            beatmap = RcFileManager.ReadBeatmap(rcmPath);
            notes = new LinkedList<Note>(beatmap.Notes ?? new List<Note>());
            maxScore = (beatmap?.Notes?.Count ?? 0) * 100;
            songInstance?.Stop(); songInstance?.Dispose(); songEffect = null;
            try
            {
                using (var fs = File.OpenRead(songFilePath))
                { songEffect = SoundEffect.FromStream(fs); songInstance = songEffect.CreateInstance(); }
            }
            catch
            {
                // Audio file is not WAV or corrupted — generate fallback
                float dur = (beatmap?.Notes?.Count > 0) ? beatmap.Notes.Max(n => n.Time) + 2f : 6f;
                GenerateMusicalWav(songFilePath, dur, beatmap?.Bpm ?? 120f);
                using (var fs = File.OpenRead(songFilePath))
                { songEffect = SoundEffect.FromStream(fs); songInstance = songEffect.CreateInstance(); }
            }
            songDurationSeconds = songEffect?.Duration.TotalSeconds ?? 0.0;
        }

        void LoadSong(string songFilePath, string beatmapPath)
        {
            var json = File.ReadAllText(beatmapPath);
            beatmap = Beatmap.LoadFromString(json);
            notes = new LinkedList<Note>(beatmap.Notes ?? new List<Note>());
            maxScore = (beatmap?.Notes?.Count ?? 0) * 100;
            songInstance?.Stop(); songInstance?.Dispose(); songEffect = null;
            try
            {
                using (var fs = File.OpenRead(songFilePath))
                { songEffect = SoundEffect.FromStream(fs); songInstance = songEffect.CreateInstance(); }
            }
            catch
            {
                float dur = (beatmap?.Notes?.Count > 0) ? beatmap.Notes.Max(n => n.Time) + 2f : 6f;
                GenerateMusicalWav(songFilePath, dur, beatmap?.Bpm ?? 120f);
                using (var fs = File.OpenRead(songFilePath))
                { songEffect = SoundEffect.FromStream(fs); songInstance = songEffect.CreateInstance(); }
            }
            songDurationSeconds = songEffect?.Duration.TotalSeconds ?? 0.0;
        }

        Texture2D CreateCircleTexture(int size, Color fill)
        {
            var tex = new Texture2D(GraphicsDevice, size, size);
            var data = new Color[size * size];
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r, dy = y + 0.5f - r;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    float a = d < r - 1 ? 1f : d < r ? r - d : 0f;
                    data[y * size + x] = new Color(fill.R, fill.G, fill.B, (byte)(fill.A * a));
                }
            tex.SetData(data);
            return tex;
        }

        // ═══════════════════════════════════════════════════════════════
        // UPDATE
        // ═══════════════════════════════════════════════════════════════

        protected override void Update(GameTime gameTime)
        {
            kb = Keyboard.GetState();
            mouseState = Mouse.GetState();

            // F11 fullscreen toggle
            if (kb.IsKeyDown(Keys.F11) && !prevKb.IsKeyDown(Keys.F11))
            {
                if (isFullscreen) { ExitBorderlessFullscreen(); isFullscreen = false; }
                else { EnterBorderlessFullscreen(); isFullscreen = true; }
            }

            // Escape handling
            if (kb.IsKeyDown(Keys.Escape) && !prevKb.IsKeyDown(Keys.Escape))
            {
                if (state == GameState.Playing)
                {
                    songInstance?.Stop(); stopwatch.Stop(); videoPlayer?.Stop();
                    state = GameState.Menu; ExitBorderlessFullscreen();
                    menuMusicInstance?.Play(); discordRpc?.SetMenu();
                }
                else if (state == GameState.Result)
                {
                    state = GameState.Menu; ExitBorderlessFullscreen();
                    menuMusicInstance?.Play(); discordRpc?.SetMenu();
                }
                else if (state == GameState.Account || state == GameState.Language
                      || state == GameState.Stats || state == GameState.BeatmapEditor
                      || state == GameState.Settings || state == GameState.Achievements
                      || state == GameState.ReplayView || state == GameState.Profile
                      || state == GameState.SearchPlayer)
                {
                    if (state == GameState.BeatmapEditor)
                    { edPreviewInstance?.Stop(); edPreviewing = false; }
                    state = GameState.Menu; discordRpc?.SetMenu();
                }
                else { Exit(); }

                prevKb = kb; prevMouseState = mouseState; prevScrollValue = mouseState.ScrollWheelValue;
                base.Update(gameTime); return;
            }

            switch (state)
            {
                case GameState.Menu: UpdateMenu(gameTime); break;
                case GameState.Language: UpdateLanguage(); break;
                case GameState.Result: UpdateResult(); break;
                case GameState.Account: HandleAccountInput(kb, prevKb); break;
                case GameState.Stats: break; // stats is just display
                case GameState.BeatmapEditor: UpdateEditor(gameTime); break;
                case GameState.Playing: UpdatePlaying(gameTime); break;
                case GameState.Settings: UpdateSettings(); break;
                case GameState.Achievements: UpdateAchievements(); break;
                case GameState.ReplayView: UpdateReplayView(gameTime); break;
                case GameState.Profile: UpdateProfile(); break;
                case GameState.SearchPlayer: UpdateSearchPlayer(); break;
                case GameState.EditProfile: UpdateEditProfile(gameTime); break;
            }

            // Achievement popup timer
            if (achievementPopupTimer > 0)
                achievementPopupTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Sync status timer
            if (syncStatusTimer > 0)
                syncStatusTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;

            prevKb = kb;
            prevMouseState = mouseState;
            prevScrollValue = mouseState.ScrollWheelValue;
            base.Update(gameTime);
        }

        void UpdateMenu(GameTime gt)
        {
            menuTimer += (float)gt.ElapsedGameTime.TotalSeconds;

            // Mouse wheel scroll
            int scrollDeltaMenu = mouseState.ScrollWheelValue - prevScrollValue;
            if (scrollDeltaMenu != 0)
                menuScrollOffset = Math.Max(0, menuScrollOffset - scrollDeltaMenu / 40);

            if (kb.IsKeyDown(Keys.Up) && !prevKb.IsKeyDown(Keys.Up))
                currentMenuIndex = (currentMenuIndex - 1 + menuKeys.Length) % menuKeys.Length;
            if (kb.IsKeyDown(Keys.Down) && !prevKb.IsKeyDown(Keys.Down))
                currentMenuIndex = (currentMenuIndex + 1) % menuKeys.Length;

            // Left/Right = difficulty
            if (kb.IsKeyDown(Keys.Left) && !prevKb.IsKeyDown(Keys.Left) && songs.Count > 0)
            {
                var s = songs[currentSongIndex];
                if (s.Difficulties.Count > 1)
                {
                    int idx = s.Difficulties.IndexOf(currentDifficulty);
                    idx = (idx - 1 + s.Difficulties.Count) % s.Difficulties.Count;
                    currentDifficulty = s.Difficulties[idx];
                    LoadCurrentSong();
                }
            }
            if (kb.IsKeyDown(Keys.Right) && !prevKb.IsKeyDown(Keys.Right) && songs.Count > 0)
            {
                var s = songs[currentSongIndex];
                if (s.Difficulties.Count > 1)
                {
                    int idx = s.Difficulties.IndexOf(currentDifficulty);
                    idx = (idx + 1) % s.Difficulties.Count;
                    currentDifficulty = s.Difficulties[idx];
                    LoadCurrentSong();
                }
            }

            // Tab = switch song
            if (kb.IsKeyDown(Keys.Tab) && !prevKb.IsKeyDown(Keys.Tab) && songs.Count > 1)
            {
                currentSongIndex = (currentSongIndex + 1) % songs.Count;
                var s = songs[currentSongIndex];
                if (!s.Difficulties.Contains(currentDifficulty))
                    currentDifficulty = s.Difficulties.FirstOrDefault() ?? "easy";
                LoadCurrentSong();
            }

            // ═══ Mouse click support for menu ═══
            bool mouseClicked = mouseState.LeftButton == ButtonState.Pressed && prevMouseState.LeftButton == ButtonState.Released;
            if (mouseClicked)
            {
                int mx = mouseState.X, my = mouseState.Y;
                int cx = width / 2;

                // Check menu button clicks
                int optW = 240, optH = 38, gap = 6;
                int optX = cx - optW / 2;
                // Calculate cardY based on DrawMenu layout
                var titleTexH = 48 + 8; // approximate title height
                int scroll = -menuScrollOffset;
                int titleY = 80 + scroll;
                int cardY = titleY + titleTexH + 24 + 98;

                for (int i = 0; i < menuKeys.Length; i++)
                {
                    var btn = new Rectangle(optX, cardY + i * (optH + gap), optW, optH);
                    if (mx >= btn.Left && mx <= btn.Right && my >= btn.Top && my <= btn.Bottom)
                    {
                        currentMenuIndex = i;
                        // Trigger the menu action
                        ExecuteMenuAction(menuKeys[i]);
                        return;
                    }
                }

                // Check difficulty pill clicks
                if (songs.Count > 0)
                {
                    var s = songs[currentSongIndex];
                    int pillY = titleY + titleTexH + 24 + 30;
                    int pillGap = 8;
                    int totalPW = 0;
                    var pillWidths = new int[s.Difficulties.Count];
                    for (int i = 0; i < s.Difficulties.Count; i++)
                        pillWidths[i] = DiffShort(s.Difficulties[i]).Length * 9 + 20; // approximate
                    for (int i = 0; i < s.Difficulties.Count; i++)
                        totalPW += pillWidths[i] + (i > 0 ? pillGap : 0);
                    int px = cx - totalPW / 2;
                    for (int i = 0; i < s.Difficulties.Count; i++)
                    {
                        var pillRect = new Rectangle(px, pillY, pillWidths[i], 26);
                        if (mx >= pillRect.Left && mx <= pillRect.Right && my >= pillRect.Top && my <= pillRect.Bottom)
                        {
                            currentDifficulty = s.Difficulties[i];
                            LoadCurrentSong();
                            return;
                        }
                        px += pillWidths[i] + pillGap;
                    }
                }
            }

            // Mouse hover for menu items
            {
                int mx2 = mouseState.X, my2 = mouseState.Y;
                int cx2 = width / 2;
                int optW2 = 240, optH2 = 38, gap2 = 6;
                int optX2 = cx2 - optW2 / 2;
                int scroll2 = -menuScrollOffset;
                int titleY2 = 80 + scroll2;
                int cardY2 = titleY2 + 48 + 8 + 24 + 98;
                for (int i = 0; i < menuKeys.Length; i++)
                {
                    var btn = new Rectangle(optX2, cardY2 + i * (optH2 + gap2), optW2, optH2);
                    if (mx2 >= btn.Left && mx2 <= btn.Right && my2 >= btn.Top && my2 <= btn.Bottom)
                    { currentMenuIndex = i; break; }
                }
            }

            if (kb.IsKeyDown(Keys.Enter) && !prevKb.IsKeyDown(Keys.Enter))
                ExecuteMenuAction(menuKeys[currentMenuIndex]);
        }

        void ExecuteMenuAction(string key)
        {
                if (key == "menu_start") StartPlaying(false);
                else if (key == "menu_editor")
                {
                    state = GameState.BeatmapEditor;
                    menuMusicInstance?.Stop();
                    InitEditor();
                    discordRpc?.SetEditor("");
                }
                else if (key == "menu_stats")
                {
                    state = GameState.Stats;
                    string? user = accountsManager?.LoggedInUser;
                    cachedStats = statsDb?.GetSummary(user);
                    cachedRecent = statsDb?.GetRecentPlays(user, 8);
                    discordRpc?.SetStats();
                }
                else if (key == "menu_profile")
                {
                    state = GameState.Profile;
                    profileScrollIndex = 0;
                    var user = accountsManager?.LoggedInUser;
                    if (user != null)
                    {
                        // Build local fallback profile immediately
                        viewingProfile = new PlayerProfileDto { User = user };
                        // Apply local saved profile data
                        var lp = LocalProfileData.Load();
                        viewingProfile.AvatarId = lp.AvatarId;
                        viewingProfile.BannerId = lp.BannerId;
                        viewingProfile.Bio = lp.Bio;
                        viewingProfile.Region = lp.Region;
                        if (statsDb != null)
                        {
                            var summary = statsDb.GetSummary(user);
                            if (summary != null)
                            {
                                viewingProfile.TotalPlays = summary.TotalPlays;
                                viewingProfile.BestCombo = summary.BestCombo;
                                viewingProfile.AvgAccuracy = summary.AvgAccuracy;
                            }
                        }
                        // Try cloud fetch to enrich with badges/bio/region
                        if (cloudSync != null)
                        {
                            profileLoading = true;
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    var cloud = await cloudSync.GetProfileAsync(user);
                                    if (cloud != null) viewingProfile = cloud;
                                }
                                catch { }
                                finally { profileLoading = false; }
                            });
                        }
                    }
                    else
                    {
                        viewingProfile = null;
                    }
                }
                else if (key == "menu_search")
                {
                    state = GameState.SearchPlayer;
                    searchQuery = ""; searchResults.Clear(); searchSelectedIndex = 0; searchLoading = false;
                }
                else if (key == "menu_settings")
                {
                    state = GameState.Settings;
                    settingsMenuIndex = 0;
                    settingsBindingMode = false;
                }
                else if (key == "menu_achievements")
                {
                    state = GameState.Achievements;
                    achievementsScrollIndex = 0;
                }
                else if (key == "menu_account")
                {
                    state = GameState.Account;
                    accountUsername = ""; accountPassword = "";
                    accountShowMessage = false; accountFieldIndex = 0;
                    accountIsLoginMode = true;
                }
                else if (key == "menu_language")
                {
                    state = GameState.Language;
                    languageMenuIndex = Array.IndexOf(Localization.All, Localization.Current);
                    if (languageMenuIndex < 0) languageMenuIndex = 0;
                }
                else if (key == "menu_exit") Exit();
        }

        void UpdateLanguage()
        {
            int c = Localization.All.Length;
            if (kb.IsKeyDown(Keys.Up) && !prevKb.IsKeyDown(Keys.Up)) languageMenuIndex = (languageMenuIndex - 1 + c) % c;
            if (kb.IsKeyDown(Keys.Down) && !prevKb.IsKeyDown(Keys.Down)) languageMenuIndex = (languageMenuIndex + 1) % c;
            if (kb.IsKeyDown(Keys.Enter) && !prevKb.IsKeyDown(Keys.Enter))
            { Localization.Current = Localization.All[languageMenuIndex]; state = GameState.Menu; }
        }

        void UpdateResult()
        {
            if (kb.IsKeyDown(Keys.Up) && !prevKb.IsKeyDown(Keys.Up)) resultMenuIndex = (resultMenuIndex - 1 + 3) % 3;
            if (kb.IsKeyDown(Keys.Down) && !prevKb.IsKeyDown(Keys.Down)) resultMenuIndex = (resultMenuIndex + 1) % 3;
            if (kb.IsKeyDown(Keys.Enter) && !prevKb.IsKeyDown(Keys.Enter))
            {
                if (resultMenuIndex == 0) StartPlaying(false);
                else if (resultMenuIndex == 1)
                {
                    // Watch replay
                    string songId = songs.Count > 0 ? songs[currentSongIndex].Id : "unknown";
                    var replay = replayManager?.GetBestReplay(songId, currentDifficulty);
                    if (replay != null) StartReplayView(replay);
                }
                else { state = GameState.Menu; ExitBorderlessFullscreen(); menuMusicInstance?.Play(); discordRpc?.SetMenu(); }
            }
        }

        void UpdatePlaying(GameTime gameTime)
        {
            float time = (float)stopwatch.Elapsed.TotalSeconds;
            float offset = (settingsManager?.Settings.OffsetMs ?? 0) / 1000f;
            float adjTime = time + offset;
            Keys[] keys = GetLaneKeys();

            // Update video background
            videoPlayer?.UpdateTime(time);

            for (int c = 0; c < 4; c++)
            {
                if (kb.IsKeyDown(keys[c]) && !prevKb.IsKeyDown(keys[c]))
                {
                    if (editorMode)
                    {
                        notes.AddLast(new Note { Time = time, Column = c });
                    }
                    else
                    {
                        Note? nearest = null;
                        LinkedListNode<Note>? nearestNode = null;
                        float best = float.MaxValue;
                        for (var node = notes.First; node != null; node = node.Next)
                        {
                            var n = node.Value;
                            if (n.Column != c) continue;
                            float dt = Math.Abs(n.Time - adjTime);
                            if (dt <= GameConfig.GoodWindow && dt < best) { best = dt; nearest = n; nearestNode = node; }
                        }
                        if (nearestNode != null)
                        {
                            notes.Remove(nearestNode);
                            int pts; string jText; Color jColor;
                            if (best <= GameConfig.PerfectWindow)
                            {
                                pts = GameConfig.PerfectScore; jText = "PERFECT"; jColor = new Color(255, 220, 50);
                                hp = Math.Min(GameConfig.MaxHP, hp + GameConfig.HPGainPerfect); perfectCount++;
                            }
                            else if (best <= GameConfig.GreatWindow)
                            {
                                pts = GameConfig.GreatScore; jText = "GREAT"; jColor = new Color(80, 255, 120);
                                hp = Math.Min(GameConfig.MaxHP, hp + GameConfig.HPGainGreat); greatCount++;
                            }
                            else
                            {
                                pts = GameConfig.GoodScore; jText = "GOOD"; jColor = new Color(0, 200, 255);
                                hp = Math.Min(GameConfig.MaxHP, hp + GameConfig.HPGainGood); goodCount++;
                            }

                            score += pts; combo++; hitCount++;
                            if (combo > maxCombo) maxCombo = combo;
                            float sfxVol = (settingsManager?.Settings.SfxVolume ?? 0.8f);
                            sfxHit?.Play(Math.Clamp(0.5f + combo * 0.005f, 0.5f, 0.9f) * sfxVol, Math.Clamp(combo * 0.015f, 0f, 0.8f), 0f);
                            shakeTimer = 0.06f; shakeIntensity = Math.Clamp(1f + combo * 0.05f, 1f, 4f);
                            judgmentPopups.Add(new JudgmentPopup { Text = jText, Color = jColor, Timer = 0.6f,
                                Position = new Vector2(LaneLeft + c * LaneWidth + LaneWidth / 2, HitZoneY - 30) });
                            SpawnHitParticles(c);
                            replayManager?.RecordEvent(adjTime, c, jText, pts, combo);
                        }
                    }
                    int lx = LaneLeft + c * LaneWidth + 4;
                    if (keyFlashPool != null)
                    {
                        var k = keyFlashPool.Rent();
                        k.Reset(new Rectangle(lx, HitZoneY, LaneWidth - 8, HitZoneHeight),
                            TierNoteColors[ComboTier][c], GameConfig.KeyFlashDuration);
                        keyFlashes.Add(k);
                    }
                }
            }

            // Save in editor mode
            if (editorMode && kb.IsKeyDown(Keys.S) && !prevKb.IsKeyDown(Keys.S))
            {
                string songId = songs.Count > 0 ? songs[currentSongIndex].Id : "song";
                RcFileManager.WriteBeatmap(Path.Combine("Assets", songId + "_" + currentDifficulty + ".rcm"),
                    new Beatmap { Notes = new List<Note>(notes) });
            }

            // End detection (HP depleted or song finished)
            bool hpFail = hp <= 0 && !hpDepleted;
            if (hpFail) hpDepleted = true;

            if (!summaryShown && (hpDepleted || notes.Count == 0 || stopwatch.Elapsed.TotalSeconds >= songDurationSeconds + 0.1))
            {
                summaryShown = true; state = GameState.Result; songInstance?.Stop();
                videoPlayer?.Stop();
                var pct = maxScore > 0 ? (double)score / maxScore : 0.0;
                resultGrade = hpDepleted ? "F" : pct >= 0.95 ? "SS" : pct >= 0.85 ? "S" : pct >= 0.75 ? "A"
                            : pct >= 0.60 ? "B" : pct >= 0.40 ? "C" : "D";
                resultMenuIndex = 0;
                discordRpc?.SetResult(resultGrade, score);

                // Record stats
                int total = hitCount + missCount;
                double acc = total > 0 ? (double)hitCount / total * 100 : 0;
                string songId = songs.Count > 0 ? songs[currentSongIndex].Id : "unknown";
                statsDb?.RecordPlay(accountsManager?.LoggedInUser ?? "guest",
                    songId, currentDifficulty, score, maxCombo, hitCount, missCount, acc, resultGrade);

                // Save replay
                replayManager?.StopRecording(songId, currentDifficulty,
                    accountsManager?.LoggedInUser ?? "guest",
                    score, maxCombo, hitCount, missCount, acc, resultGrade);

                // Check achievements
                bool isFC = missCount == 0 && hitCount > 0;
                var summary = statsDb?.GetSummary(accountsManager?.LoggedInUser);
                int totalPlays = summary?.TotalPlays ?? 1;
                int uniqueSongs = GetUniqueSongsPlayed();
                achievementManager?.CheckAfterPlay(totalPlays, maxCombo, resultGrade, acc, isFC, uniqueSongs, songs.Count);

                // Cloud sync: upload play + achievements
                if (cloudSync != null && accountsManager?.LoggedInUser != null)
                {
                    var syncUser = accountsManager.LoggedInUser;
                    var playedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await cloudSync.UploadPlayAsync(syncUser, songId, currentDifficulty,
                                score, maxCombo, hitCount, missCount, acc, resultGrade, playedAt);
                            if (achievementManager != null)
                                await cloudSync.UploadAchievementsAsync(syncUser, achievementManager.GetAll());
                        }
                        catch { }
                    });
                }

                // Show achievement popups
                if (achievementManager != null && achievementManager.PendingPopups.Count > 0)
                {
                    var ach = achievementManager.PendingPopups.Dequeue();
                    achievementPopupText = Localization.Get(ach.NameKey);
                    achievementPopupTimer = 4f;
                }
            }

            // Shake, pulse, particles, judgments, miss detection
            if (shakeTimer > 0) shakeTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            { float bi = 0.5f; float ct = (float)stopwatch.Elapsed.TotalSeconds; float bp = (ct % bi) / bi;
              float tgt = bp < 0.1f ? (1f - bp / 0.1f) : 0f; tgt *= Math.Clamp(combo / 10f, 0.15f, 1f);
              beatPulseAlpha = MathHelper.Lerp(beatPulseAlpha, tgt, 0.3f); }

            float dt2 = (float)gameTime.ElapsedGameTime.TotalSeconds;
            for (int i = particles.Count - 1; i >= 0; i--)
            { var p = particles[i]; p.Pos += p.Vel * dt2; p.Vel.Y += 500f * dt2; p.Life -= dt2; if (p.Life <= 0) particles.RemoveAt(i); }
            for (int i = judgmentPopups.Count - 1; i >= 0; i--)
            { var j = judgmentPopups[i]; j.Timer -= dt2; j.Position = new Vector2(j.Position.X, j.Position.Y - 45f * dt2); if (j.Timer <= 0) judgmentPopups.RemoveAt(i); }

            // Fast miss detection - notes are missed shortly after passing the hit zone
            float pTime = (float)stopwatch.Elapsed.TotalSeconds + offset;
            for (var mN = notes.First; mN != null;)
            {
                var nxt = mN.Next;
                if (pTime - mN.Value.Time > GameConfig.MissWindow)
                {
                    int col = mN.Value.Column; notes.Remove(mN); combo = 0; missCount++;
                    hp = Math.Max(0, hp - GameConfig.HPDrainMiss);
                    float sfxVol = (settingsManager?.Settings.SfxVolume ?? 0.8f);
                    sfxMiss?.Play(0.35f * sfxVol, 0f, 0f);
                    replayManager?.RecordEvent(pTime, col, "MISS", 0, 0);
                    judgmentPopups.Add(new JudgmentPopup { Text = "MISS", Color = new Color(255, 80, 80), Timer = 0.5f,
                        Position = new Vector2(LaneLeft + col * LaneWidth + LaneWidth / 2, HitZoneY - 30) });
                    if (keyFlashPool != null)
                    { var k = keyFlashPool.Rent(); k.Reset(new Rectangle(LaneLeft + col * LaneWidth + 4, HitZoneY, LaneWidth - 8, HitZoneHeight), Color.Red, GameConfig.MissFlashDuration); keyFlashes.Add(k); }
                }
                mN = nxt;
            }
        }

        // ═══════════ Beatmap Editor Update ═══════════

        void InitEditor()
        {
            edSongName = ""; edAuthor = ""; edAudioPath = ""; edBpm = "120";
            edNotes = new(); edScrollTime = 0; edTotalTime = 10;
            edFieldFocus = 0; edMessage = ""; edMessageTimer = 0;
            edDragging = null; edPreviewing = false;
        }

        void UpdateEditor(GameTime gt)
        {
            float dt = (float)gt.ElapsedGameTime.TotalSeconds;
            if (edMessageTimer > 0) edMessageTimer -= dt;

            // Preview playback
            if (edPreviewing)
            {
                edScrollTime = (float)edPreviewWatch.Elapsed.TotalSeconds;
                if (edScrollTime >= edTotalTime) { edPreviewing = false; edPreviewInstance?.Stop(); }
            }

            // Scroll
            int scrollDelta = mouseState.ScrollWheelValue - prevScrollValue;
            if (scrollDelta != 0 && !edPreviewing)
            {
                edScrollTime -= scrollDelta / 120f * 0.5f;
                edScrollTime = Math.Clamp(edScrollTime, 0, Math.Max(edTotalTime - EdVisibleSeconds, 0));
            }

            // Tab / field switching
            if (kb.IsKeyDown(Keys.Tab) && !prevKb.IsKeyDown(Keys.Tab))
            {
                edFieldFocus = (edFieldFocus + 1) % 5; // -1→0→1→2→3→back to timeline
                if (edFieldFocus == 4) edFieldFocus = -1;
            }

            // Text input for focused field
            if (edFieldFocus >= 0)
            {
                bool shift = kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift);
                if (kb.IsKeyDown(Keys.Back) && !prevKb.IsKeyDown(Keys.Back))
                {
                    ref string field = ref GetEdField(edFieldFocus);
                    if (field.Length > 0) field = field[..^1];
                }
                else
                {
                    foreach (Keys k in Enum.GetValues(typeof(Keys)))
                    {
                        if (k == Keys.None || k == Keys.Tab || k == Keys.Escape || k == Keys.Enter) continue;
                        if (kb.IsKeyDown(k) && !prevKb.IsKeyDown(k))
                        {
                            char ch = KeyToChar(k, shift);
                            if (ch != '\0') { ref string field = ref GetEdField(edFieldFocus); field += ch; }
                        }
                    }
                }
            }

            // Space = preview toggle
            if (kb.IsKeyDown(Keys.Space) && !prevKb.IsKeyDown(Keys.Space) && edFieldFocus < 0)
            {
                if (edPreviewing) { edPreviewing = false; edPreviewInstance?.Stop(); }
                else if (!string.IsNullOrEmpty(edAudioPath) && File.Exists(edAudioPath))
                {
                    try
                    {
                        edPreviewInstance?.Stop(); edPreviewEffect?.Dispose();
                        using var fs = File.OpenRead(edAudioPath);
                        edPreviewEffect = SoundEffect.FromStream(fs);
                        edPreviewInstance = edPreviewEffect.CreateInstance();
                        edPreviewInstance.Play();
                        edPreviewWatch.Restart(); edPreviewing = true; edScrollTime = 0;
                    }
                    catch { }
                }
            }

            // Ctrl+S = save
            bool ctrl = kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl);
            if (ctrl && kb.IsKeyDown(Keys.S) && !prevKb.IsKeyDown(Keys.S))
                SaveEditorBeatmap();

            // Timeline mouse interaction
            int tlLeft = 180; int tlRight = width - 20;
            int tlTop = 60; int tlBottom = height - 60;
            int tlW = tlRight - tlLeft;
            int laneW = tlW / 4;
            int mx = mouseState.X, my = mouseState.Y;

            bool inTimeline = mx >= tlLeft && mx < tlRight && my >= tlTop && my < tlBottom;

            // Left click = place or start drag
            if (mouseState.LeftButton == ButtonState.Pressed && prevMouseState.LeftButton == ButtonState.Released && inTimeline && edFieldFocus < 0)
            {
                int col = (mx - tlLeft) / laneW;
                col = Math.Clamp(col, 0, 3);
                float t = edScrollTime + (my - tlTop) / EdPixelsPerSecond;

                // Check if clicking existing note (for drag)
                Note? hit = null;
                foreach (var n in edNotes)
                {
                    float ny = tlTop + (n.Time - edScrollTime) * EdPixelsPerSecond;
                    if (Math.Abs(ny - my) < 12 && n.Column == col) { hit = n; break; }
                }

                if (hit != null) { edDragging = hit; }
                else
                {
                    edNotes.Add(new Note { Time = (float)Math.Round(t, 3), Column = col });
                    edNotes.Sort((a, b) => a.Time.CompareTo(b.Time));
                }
            }

            // Dragging
            if (edDragging != null && mouseState.LeftButton == ButtonState.Pressed)
            {
                int col = Math.Clamp((mx - tlLeft) / laneW, 0, 3);
                float t = edScrollTime + (my - tlTop) / EdPixelsPerSecond;
                edDragging.Column = col;
                edDragging.Time = (float)Math.Round(Math.Max(0, t), 3);
            }
            if (mouseState.LeftButton == ButtonState.Released) edDragging = null;

            // Right click = delete
            if (mouseState.RightButton == ButtonState.Pressed && prevMouseState.RightButton == ButtonState.Released && inTimeline)
            {
                int col = Math.Clamp((mx - tlLeft) / laneW, 0, 3);
                edNotes.RemoveAll(n =>
                {
                    float ny = tlTop + (n.Time - edScrollTime) * EdPixelsPerSecond;
                    return Math.Abs(ny - my) < 12 && n.Column == col;
                });
            }

            // Click on left panel fields
            if (mouseState.LeftButton == ButtonState.Pressed && prevMouseState.LeftButton == ButtonState.Released)
            {
                if (mx < 170 && my >= 90 && my < 250)
                {
                    int fi = (my - 90) / 50;
                    if (fi >= 0 && fi <= 3) edFieldFocus = fi;
                }
                else if (inTimeline) edFieldFocus = -1;
            }
        }

        ref string GetEdField(int idx)
        {
            switch (idx)
            {
                case 0: return ref edSongName;
                case 1: return ref edAuthor;
                case 2: return ref edAudioPath;
                default: return ref edBpm;
            }
        }

        void SaveEditorBeatmap()
        {
            // Validate
            if (string.IsNullOrWhiteSpace(edAudioPath) || !File.Exists(edAudioPath))
            { edMessage = Localization.Get("editor_err_audio"); edMessageTimer = 3; return; }
            if (edNotes.Count == 0)
            { edMessage = Localization.Get("editor_err_notes"); edMessageTimer = 3; return; }
            if (string.IsNullOrWhiteSpace(edSongName))
            { edMessage = Localization.Get("editor_err_name"); edMessageTimer = 3; return; }
            if (string.IsNullOrWhiteSpace(edAuthor))
            { edMessage = Localization.Get("editor_err_author"); edMessageTimer = 3; return; }

            float.TryParse(edBpm, out float bpm);
            if (bpm <= 0) bpm = 120;

            // Copy audio to Assets
            string audioName = Path.GetFileName(edAudioPath);
            string destAudio = Path.Combine("Assets", audioName);
            if (!File.Exists(destAudio) && File.Exists(edAudioPath))
                File.Copy(edAudioPath, destAudio, true);

            var bm = new Beatmap
            {
                Name = edSongName.Trim(),
                Author = edAuthor.Trim(),
                AudioFile = audioName,
                Bpm = bpm,
                Notes = new List<Note>(edNotes),
            };

            // Save as .rcm
            string safeId = edSongName.Trim().ToLowerInvariant().Replace(' ', '_');
            string rcmPath = Path.Combine("Assets", safeId + "_easy.rcm");
            RcFileManager.WriteBeatmap(rcmPath, bm);

            // Add to songs.json if not exists
            if (!songs.Any(s => s.Id == safeId))
            {
                songs.Add(new SongInfo { Id = safeId, Title = edSongName.Trim(), File = audioName,
                    Difficulties = new List<string> { "easy" } });
                var opts = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText("Assets/songs.json", System.Text.Json.JsonSerializer.Serialize(songs, opts));
            }

            edMessage = Localization.Get("editor_saved"); edMessageTimer = 3;
        }

        void StartPlaying(bool editor)
        {
            editorMode = editor;
            state = GameState.Playing;
            score = 0; combo = 0; maxCombo = 0; hitCount = 0; missCount = 0;
            perfectCount = 0; greatCount = 0; goodCount = 0;
            hp = GameConfig.InitialHP; hpDepleted = false;
            summaryShown = false; keyFlashes.Clear(); particles.Clear(); judgmentPopups.Clear();
            shakeTimer = 0; beatPulseAlpha = 0;
            LoadCurrentSong();

            // Load video/background for current beatmap
            LoadBeatmapMedia();

            EnterBorderlessFullscreen();
            menuMusicInstance?.Stop();
            replayManager?.StartRecording();
            stopwatch.Restart(); songInstance?.Stop();
            if (songInstance != null)
            {
                songInstance.Volume = settingsManager?.Settings.MusicVolume ?? 0.7f;
                songInstance.Play();
            }

            // Start video playback
            if (videoPlayer != null && videoPlayer.HasVideo)
                videoPlayer.Play();

            string title = songs.Count > 0 ? songs[currentSongIndex].Title : "Unknown";
            discordRpc?.SetPlaying(title, DiffShort(currentDifficulty));
        }

        void LoadBeatmapMedia()
        {
            // Stop previous video
            videoPlayer?.Stop();
            videoPlayer?.Dispose();
            videoPlayer = new VideoBackgroundPlayer(GraphicsDevice);

            bgImageTexture?.Dispose();
            bgImageTexture = null;

            if (beatmap == null) return;

            // Try loading video
            if (!string.IsNullOrEmpty(beatmap.VideoFile))
            {
                string videoPath = Path.Combine("Assets", beatmap.VideoFile);
                if (File.Exists(videoPath))
                    videoPlayer.Open(videoPath);
            }

            // Try loading background image
            if (!string.IsNullOrEmpty(beatmap.BackgroundImage))
            {
                string bgPath = Path.Combine("Assets", beatmap.BackgroundImage);
                if (File.Exists(bgPath))
                {
                    try
                    {
                        using var fs = File.OpenRead(bgPath);
                        bgImageTexture = Texture2D.FromStream(GraphicsDevice, fs);
                    }
                    catch { bgImageTexture = null; }
                }
            }
        }

        void EnterBorderlessFullscreen()
        {
            windowedWidth = graphics!.PreferredBackBufferWidth;
            windowedHeight = graphics.PreferredBackBufferHeight;
            var d = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            graphics.PreferredBackBufferWidth = d.Width;
            graphics.PreferredBackBufferHeight = d.Height;
            graphics.IsFullScreen = false; graphics.ApplyChanges();
            Window.IsBorderless = true; Window.Position = Point.Zero;
            width = d.Width; height = d.Height;
            isFullscreen = true;
            renderCache?.Dispose(); renderCache = new RenderCache(GraphicsDevice);
        }

        void ExitBorderlessFullscreen()
        {
            Window.IsBorderless = false;
            graphics!.PreferredBackBufferWidth = windowedWidth;
            graphics.PreferredBackBufferHeight = windowedHeight;
            graphics.IsFullScreen = false; graphics.ApplyChanges();
            width = windowedWidth; height = windowedHeight;
            isFullscreen = false;
            renderCache?.Dispose(); renderCache = new RenderCache(GraphicsDevice);
        }

        // ═══════════════════════════════════════════════════════════════
        // DRAW
        // ═══════════════════════════════════════════════════════════════

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(10, 10, 25));

            spriteBatch!.Begin();
            DrawBackground();
            if (state == GameState.Playing)
            {
                if (ComboTier > 0)
                    spriteBatch.Draw(pixel!, new Rectangle(0, 0, width, height), TierGlowColor[ComboTier] * (0.03f + ComboTier * 0.018f));
                if (beatPulseAlpha > 0.01f)
                    spriteBatch.Draw(pixel!, new Rectangle(0, 0, width, height), TierGlowColor[ComboTier] * (beatPulseAlpha * 0.08f));
            }
            spriteBatch.End();

            Matrix xform = Matrix.Identity;
            if (state == GameState.Playing && shakeTimer > 0)
                xform = Matrix.CreateTranslation((float)(rng.NextDouble() - 0.5) * shakeIntensity * 4f,
                    (float)(rng.NextDouble() - 0.5) * shakeIntensity * 4f, 0f);

            spriteBatch.Begin(transformMatrix: state == GameState.Playing ? xform : Matrix.Identity);
            switch (state)
            {
                case GameState.Playing: DrawGameplay(gameTime); break;
                case GameState.Menu: DrawMenu(); break;
                case GameState.Result: DrawResult(); break;
                case GameState.Account: DrawAccount(); break;
                case GameState.Language: DrawLanguage(); break;
                case GameState.Stats: DrawStats(); break;
                case GameState.BeatmapEditor: DrawEditor(); break;
                case GameState.Settings: DrawSettings(); break;
                case GameState.Achievements: DrawAchievements(); break;
                case GameState.ReplayView: DrawReplayView(); break;
                case GameState.Profile: DrawProfile(); break;
                case GameState.SearchPlayer: DrawSearchPlayer(); break;
                case GameState.EditProfile: DrawEditProfile(); break;
            }

            // Achievement popup overlay
            if (achievementPopupTimer > 0 && !string.IsNullOrEmpty(achievementPopupText))
            {
                float alpha = Math.Min(achievementPopupTimer, 1f);
                int popW = 320, popH = 50;
                int popX = (width - popW) / 2, popY = 20;
                spriteBatch.Draw(pixel!, new Rectangle(popX, popY, popW, popH), new Color(255, 220, 50) * (0.15f * alpha));
                DrawRectBorder(new Rectangle(popX, popY, popW, popH), new Color(255, 220, 50) * (0.5f * alpha));
                var achLabel = textRenderer!.GetTexture("🏆 " + Localization.Get("achievement_unlocked"), "Segoe UI", 11, new Color(255, 220, 50));
                spriteBatch.Draw(achLabel, new Vector2(popX + (popW - achLabel.Width) / 2, popY + 6), Color.White * alpha);
                var achName = textRenderer!.GetTexture(achievementPopupText, "Segoe UI", 15, Color.White);
                spriteBatch.Draw(achName, new Vector2(popX + (popW - achName.Width) / 2, popY + 24), Color.White * alpha);
            }

            // Cloud sync status overlay
            if (syncStatusTimer > 0 && !string.IsNullOrEmpty(syncStatusText))
            {
                float alpha = Math.Min(syncStatusTimer, 1f);
                var syncTex = textRenderer!.GetTexture("☁ " + syncStatusText, "Segoe UI", 11, new Color(120, 200, 255));
                spriteBatch.Draw(syncTex, new Vector2(width - syncTex.Width - 12, height - 30), Color.White * alpha);
            }

            spriteBatch.End();
            base.Draw(gameTime);
        }

        void DrawBackground()
        {
            if (renderCache != null)
                spriteBatch!.Draw(renderCache.GetBackground(width, height), new Rectangle(0, 0, width, height), Color.White);
        }

        // ═══════════ Gameplay ═══════════

        void DrawGameplay(GameTime gameTime)
        {
            float time = (float)stopwatch.Elapsed.TotalSeconds;
            int ll = LaneLeft, hz = HitZoneY;

            // Video / background image behind lanes
            var videoFrame = videoPlayer?.CurrentFrame;
            if (videoFrame != null)
            {
                // Draw video frame scaled to fill screen, semi-transparent
                spriteBatch!.Draw(videoFrame, new Rectangle(0, 0, width, height), Color.White * 0.35f);
            }
            else if (bgImageTexture != null)
            {
                spriteBatch!.Draw(bgImageTexture, new Rectangle(0, 0, width, height), Color.White * 0.25f);
            }

            for (int i = 0; i < LaneCount; i++)
                spriteBatch!.Draw(pixel!, new Rectangle(ll + i * LaneWidth, 0, LaneWidth, height), Color.White * (i % 2 == 0 ? 0.02f : 0.04f));
            for (int i = 0; i <= LaneCount; i++)
                spriteBatch!.Draw(pixel!, new Rectangle(ll + i * LaneWidth, 0, 1, height), Color.White * 0.08f);

            // Hit zone
            Color glow = TierGlowColor[ComboTier];
            spriteBatch!.Draw(pixel!, new Rectangle(ll, hz, TotalLaneWidth, 2), glow * 0.8f);
            spriteBatch.Draw(pixel!, new Rectangle(ll, hz, TotalLaneWidth, HitZoneHeight), Color.White * 0.02f);

            for (int i = 0; i < LaneCount; i++)
            {
                var lkLabels = GetLaneKeyLabels();
                var label = textRenderer!.GetTexture(lkLabels[i], "Segoe UI", 18, new Color(180, 180, 200));
                spriteBatch.Draw(label, new Vector2(ll + i * LaneWidth + (LaneWidth - label.Width) / 2, hz + HitZoneHeight + 6), Color.White);
            }

            // Notes
            for (var n = notes.Last; n != null; n = n.Previous)
            {
                float dt = n.Value.Time - time;
                if (time - n.Value.Time > GameConfig.MissWindow) continue;
                float prog = (GameConfig.ApproachTime - dt) / (GameConfig.ApproachTime + 0.01f);
                int nx = ll + n.Value.Column * LaneWidth + 6;
                int ny = (int)(MathHelper.Clamp(prog, 0f, 1f) * (hz - NoteHeight));
                var nr = new Rectangle(nx, ny, LaneWidth - 12, NoteHeight);
                Color nc = editorMode ? Color.Yellow : TierNoteColors[ComboTier][n.Value.Column % 4];
                spriteBatch.Draw(pixel!, new Rectangle(nr.X - 1, nr.Y - 1, nr.Width + 2, nr.Height + 2), nc * 0.2f);
                spriteBatch.Draw(pixel!, nr, nc * 0.9f);
                spriteBatch.Draw(pixel!, new Rectangle(nr.X, nr.Y, nr.Width, 2), Color.White * 0.35f);
            }

            // Flashes, particles, judgments
            for (int i = keyFlashes.Count - 1; i >= 0; i--)
            {
                var k = keyFlashes[i];
                spriteBatch!.Draw(pixel!, k.Rect, k.Color * (Math.Clamp(k.TimeToLive / GameConfig.KeyFlashDuration, 0, 1) * 0.3f));
                k.TimeToLive -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (k.TimeToLive <= 0) { keyFlashes.RemoveAt(i); keyFlashPool?.Return(k); }
            }
            foreach (var p in particles)
            {
                float pa = p.Life / p.MaxLife; float ps = p.Size * (0.5f + pa * 0.5f);
                spriteBatch!.Draw(pixel!, new Rectangle((int)(p.Pos.X - ps / 2), (int)(p.Pos.Y - ps / 2), (int)ps + 1, (int)ps + 1), p.Color * pa);
            }
            foreach (var j in judgmentPopups)
            {
                float ja = Math.Clamp(j.Timer / 0.3f, 0, 1);
                var jt = textRenderer!.GetTexture(j.Text, "Segoe UI", 22, j.Color);
                spriteBatch!.Draw(jt, new Vector2(j.Position.X - jt.Width / 2, j.Position.Y), Color.White * ja);
            }

            // HUD - top
            string st = songs.Count > 0 ? songs[currentSongIndex].Title : Localization.Get("unknown");
            var tt = textRenderer!.GetTexture(st, "Segoe UI", 18, Color.White);
            spriteBatch.Draw(tt, new Vector2(16, 14), Color.White);
            var dft = textRenderer!.GetTexture(DiffShort(currentDifficulty), "Segoe UI", 14, glow);
            spriteBatch.Draw(dft, new Vector2(16, 38), Color.White);

            var sct = textRenderer!.GetTexture($"{Localization.Get("score")}: {score}", "Segoe UI", 18, Color.White);
            spriteBatch.Draw(sct, new Vector2(width - sct.Width - 16, 14), Color.White);

            if (combo > 1)
            {
                int cs = Math.Min(36 + ComboTier * 4, 52);
                var ct = textRenderer!.GetTexture($"{combo}x", "Segoe UI", cs, TierGlowColor[ComboTier]);
                spriteBatch.Draw(ct, new Vector2((width - ct.Width) / 2, hz - 56 - ComboTier * 4), Color.White);
            }

            if (songDurationSeconds > 0)
            {
                float pr = Math.Clamp((float)(stopwatch.Elapsed.TotalSeconds / songDurationSeconds), 0, 1);
                spriteBatch.Draw(pixel!, new Rectangle(0, 0, width, 3), Color.White * 0.06f);
                spriteBatch.Draw(pixel!, new Rectangle(0, 0, (int)(width * pr), 3), glow);
            }

            // ═══ SAO-style HP bar (right side) ═══
            DrawHPBar();

            // ═══ Judgment counter (left side) ═══
            DrawJudgmentCounter();

            if (editorMode)
            {
                var et = textRenderer!.GetTexture(Localization.Get("editor_hint"), "Segoe UI", 14, Color.Yellow);
                spriteBatch.Draw(et, new Vector2((width - et.Width) / 2, height - 22), Color.White);
            }
        }

        void DrawHPBar()
        {
            // SAO-style HP bar: vertical bar on the right with gradient coloring
            int barW = 12, barH = height - 160;
            int barX = width - barW - 20, barY = 80;

            // Background
            spriteBatch!.Draw(pixel!, new Rectangle(barX - 1, barY - 1, barW + 2, barH + 2), new Color(20, 20, 40) * 0.8f);
            DrawRectBorder(new Rectangle(barX - 1, barY - 1, barW + 2, barH + 2), Color.White * 0.1f);

            // HP fill from bottom
            float hpRatio = Math.Clamp(hp / GameConfig.MaxHP, 0, 1);
            int fillH = (int)(barH * hpRatio);
            int fillY = barY + barH - fillH;

            // Color gradient: green→yellow→red based on HP
            Color hpColor;
            if (hpRatio > 0.6f)
                hpColor = Color.Lerp(new Color(80, 255, 120), new Color(255, 220, 50), (1f - hpRatio) / 0.4f * 0.5f);
            else if (hpRatio > 0.3f)
                hpColor = Color.Lerp(new Color(255, 220, 50), new Color(255, 120, 0), (0.6f - hpRatio) / 0.3f);
            else
                hpColor = Color.Lerp(new Color(255, 120, 0), new Color(255, 40, 40), (0.3f - hpRatio) / 0.3f);

            // Glow effect
            spriteBatch.Draw(pixel!, new Rectangle(barX - 2, fillY - 1, barW + 4, fillH + 2), hpColor * 0.15f);
            spriteBatch.Draw(pixel!, new Rectangle(barX, fillY, barW, fillH), hpColor * 0.85f);
            // Bright top edge
            if (fillH > 2)
                spriteBatch.Draw(pixel!, new Rectangle(barX, fillY, barW, 2), Color.White * 0.4f);

            // HP text label
            var hpLabel = textRenderer!.GetTexture("HP", "Segoe UI", 11, hpColor);
            spriteBatch.Draw(hpLabel, new Vector2(barX + (barW - hpLabel.Width) / 2, barY - 18), Color.White);

            // Critical HP warning flash
            if (hpRatio < 0.2f && hpRatio > 0)
            {
                float flash = (float)(0.5 + 0.5 * Math.Sin(stopwatch.Elapsed.TotalSeconds * 8));
                spriteBatch.Draw(pixel!, new Rectangle(0, 0, width, height), new Color(255, 0, 0) * (flash * 0.04f));
            }
        }

        void DrawJudgmentCounter()
        {
            // Judgment counter on the left side
            int cx = 16, cy = height / 2 - 60;
            int w = 100, lh = 22;

            // Semi-transparent panel
            spriteBatch!.Draw(pixel!, new Rectangle(cx - 4, cy - 4, w + 8, lh * 4 + 12), new Color(10, 10, 25) * 0.6f);
            DrawRectBorder(new Rectangle(cx - 4, cy - 4, w + 8, lh * 4 + 12), Color.White * 0.06f);

            var pT = textRenderer!.GetTexture($"P  {perfectCount}", "Segoe UI", 13, new Color(255, 220, 50));
            spriteBatch.Draw(pT, new Vector2(cx, cy), Color.White);

            var grT = textRenderer!.GetTexture($"G  {greatCount}", "Segoe UI", 13, new Color(80, 255, 120));
            spriteBatch.Draw(grT, new Vector2(cx, cy + lh), Color.White);

            var gdT = textRenderer!.GetTexture($"OK {goodCount}", "Segoe UI", 13, new Color(0, 200, 255));
            spriteBatch.Draw(gdT, new Vector2(cx, cy + lh * 2), Color.White);

            var mT = textRenderer!.GetTexture($"X  {missCount}", "Segoe UI", 13, new Color(255, 80, 80));
            spriteBatch.Draw(mT, new Vector2(cx, cy + lh * 3), Color.White);
        }

        // ═══════════ Modern Menu ═══════════

        void DrawMenu()
        {
            int cx = width / 2;
            float pulse = (float)(0.8 + 0.2 * Math.Sin(menuTimer * 2.0));
            int scroll = -menuScrollOffset;

            // Title
            int titleY = 80 + scroll;
            var titleTex = textRenderer!.GetTexture("CLICK", "Segoe UI", 48, Color.White);
            spriteBatch!.Draw(titleTex, new Vector2(cx - titleTex.Width / 2, titleY), Color.White);

            // Thin accent line under title
            int lineW = 120;
            spriteBatch.Draw(pixel!, new Rectangle(cx - lineW / 2, titleY + titleTex.Height + 4, lineW, 2), new Color(0, 200, 255) * pulse);

            // Song selector card
            int cardY = titleY + titleTex.Height + 24;
            if (songs.Count > 0)
            {
                var s = songs[currentSongIndex];
                var songTex = textRenderer!.GetTexture(s.Title, "Segoe UI", 20, Color.White);
                spriteBatch.Draw(songTex, new Vector2(cx - songTex.Width / 2, cardY), Color.White);

                // Difficulty pills
                int pillY = cardY + 30;
                int pillGap = 8;
                var diffs = s.Difficulties;
                int totalW = 0;
                var pillWidths = new int[diffs.Count];
                for (int i = 0; i < diffs.Count; i++)
                {
                    var dt = textRenderer!.GetTexture(DiffShort(diffs[i]), "Segoe UI", 13, Color.White);
                    pillWidths[i] = dt.Width + 20;
                    totalW += pillWidths[i] + (i > 0 ? pillGap : 0);
                }
                int px = cx - totalW / 2;
                for (int i = 0; i < diffs.Count; i++)
                {
                    bool active = diffs[i] == currentDifficulty;
                    var pillRect = new Rectangle(px, pillY, pillWidths[i], 26);
                    Color pillBg = active ? new Color(0, 200, 255) * 0.2f : Color.White * 0.04f;
                    Color pillBorder = active ? new Color(0, 200, 255) * 0.5f : Color.White * 0.08f;
                    spriteBatch.Draw(pixel!, pillRect, pillBg);
                    DrawRectBorder(pillRect, pillBorder);
                    Color tc = active ? new Color(0, 200, 255) : new Color(140, 140, 160);
                    var dt = textRenderer!.GetTexture(DiffShort(diffs[i]), "Segoe UI", 13, tc);
                    spriteBatch.Draw(dt, new Vector2(pillRect.X + (pillRect.Width - dt.Width) / 2, pillRect.Y + 4), Color.White);
                    px += pillWidths[i] + pillGap;
                }

                // Song navigation hint
                var tabHint = textRenderer!.GetTexture("Tab \u25B6", "Segoe UI", 11, new Color(80, 80, 100));
                spriteBatch.Draw(tabHint, new Vector2(cx - tabHint.Width / 2, pillY + 32), Color.White);
            }

            // Menu buttons - modern flat style
            int optW = 240;
            int optH = 38;
            int gap = 6;
            int optX = cx - optW / 2;
            int optY = cardY + 98;

            // Auto-scroll to keep selected item visible
            int selBtnTop = optY + currentMenuIndex * (optH + gap);
            int selBtnBot = selBtnTop + optH;
            if (selBtnBot > height - 40)
                menuScrollOffset += selBtnBot - (height - 40);
            if (selBtnTop < 60)
                menuScrollOffset = Math.Max(0, menuScrollOffset + selBtnTop - 60);

            for (int i = 0; i < menuKeys.Length; i++)
            {
                bool sel = i == currentMenuIndex;
                var btn = new Rectangle(optX, optY + i * (optH + gap), optW, optH);

                // Skip drawing if completely off-screen
                if (btn.Bottom < 0 || btn.Top > height) continue;

                if (sel)
                {
                    spriteBatch.Draw(pixel!, btn, new Color(0, 200, 255) * 0.1f);
                    spriteBatch.Draw(pixel!, new Rectangle(btn.X, btn.Y, 3, btn.Height), new Color(0, 200, 255));
                }
                else
                    spriteBatch.Draw(pixel!, btn, Color.White * 0.03f);

                DrawRectBorder(btn, sel ? new Color(0, 200, 255) * 0.2f : Color.White * 0.04f);
                var tc2 = sel ? Color.White : new Color(160, 160, 180);
                var tex = textRenderer!.GetTexture(Localization.Get(menuKeys[i]), "Segoe UI", 17, tc2);
                spriteBatch.Draw(tex, new Vector2(btn.X + 18, btn.Y + (btn.Height - tex.Height) / 2), Color.White);
            }

            // Scroll indicator
            int totalMenuH = menuKeys.Length * (optH + gap);
            int contentH = optY - scroll + totalMenuH;
            if (contentH > height)
            {
                float scrollRatio = (float)menuScrollOffset / Math.Max(1, contentH - height);
                int barH = Math.Max(20, height * height / contentH);
                int barY = (int)(scrollRatio * (height - barH));
                spriteBatch.Draw(pixel!, new Rectangle(width - 6, barY, 4, barH), Color.White * 0.15f);
            }

            // Hint bar
            var hint = textRenderer!.GetTexture(Localization.Get("hint_menu"), "Segoe UI", 11, new Color(80, 80, 110));
            spriteBatch.Draw(hint, new Vector2(cx - hint.Width / 2, height - 28), Color.White);

            // Logged-in display
            if (accountsManager?.LoggedInUser != null)
            {
                var ut = textRenderer!.GetTexture($"{Localization.Get("logged_in_as")}: {accountsManager.LoggedInUser}", "Segoe UI", 12, new Color(80, 255, 120));
                spriteBatch.Draw(ut, new Vector2(width - ut.Width - 14, 12), Color.White);
            }
        }

        // ═══════════ Result ═══════════

        void DrawResult()
        {
            spriteBatch!.Draw(pixel!, new Rectangle(0, 0, width, height), Color.Black * 0.7f);
            int cx = width / 2;
            int cardW = 320, cardH = 340;
            int cardX = cx - cardW / 2, cardY = (height - cardH) / 2;

            var cr = new Rectangle(cardX, cardY, cardW, cardH);
            spriteBatch.Draw(pixel!, cr, new Color(14, 14, 32) * 0.96f);
            DrawRectBorder(cr, new Color(0, 200, 255) * 0.15f);

            var title = textRenderer!.GetTexture(Localization.Get("result"), "Segoe UI", 24, Color.White);
            spriteBatch.Draw(title, new Vector2(cx - title.Width / 2, cardY + 14), Color.White);
            spriteBatch.Draw(pixel!, new Rectangle(cardX + 20, cardY + 48, cardW - 40, 1), Color.White * 0.08f);

            Color gc = resultGrade switch
            { "SS" => new Color(255, 220, 50), "S" => new Color(255, 180, 0), "A" => new Color(80, 255, 120),
              "B" => new Color(0, 200, 255), "C" => new Color(180, 140, 255), _ => new Color(255, 80, 80) };
            var gt = textRenderer!.GetTexture(resultGrade, "Segoe UI", 48, gc);
            spriteBatch.Draw(gt, new Vector2(cx - gt.Width / 2, cardY + 56), Color.White);

            int sy = cardY + 120, sx = cardX + 24, sw = cardW - 48;
            int lh = 22;
            DrawStatLine(Localization.Get("score"), $"{score}/{maxScore}", sy, sx, sw);
            DrawStatLine(Localization.Get("max_combo"), $"{maxCombo}x", sy + lh, sx, sw);
            DrawStatLine(Localization.Get("hit"), $"{hitCount}", sy + lh * 2, sx, sw);
            DrawStatLine(Localization.Get("miss"), $"{missCount}", sy + lh * 3, sx, sw);
            int tn = hitCount + missCount;
            DrawStatLine(Localization.Get("accuracy"), tn > 0 ? $"{(double)hitCount / tn * 100:F1}%" : "--", sy + lh * 4, sx, sw);

            spriteBatch.Draw(pixel!, new Rectangle(cardX + 20, sy + lh * 5 + 6, cardW - 40, 1), Color.White * 0.08f);

            string[] opts = { Localization.Get("retry"), Localization.Get("watch_replay"), Localization.Get("menu") };
            int oy = sy + lh * 5 + 16;
            for (int i = 0; i < 3; i++)
            {
                bool sel = i == resultMenuIndex;
                int bx = cx - 70, by = oy + i * 32;
                if (sel)
                {
                    spriteBatch.Draw(pixel!, new Rectangle(bx, by, 140, 26), new Color(0, 200, 255) * 0.1f);
                    spriteBatch.Draw(pixel!, new Rectangle(bx, by, 3, 26), new Color(0, 200, 255));
                }
                var t = textRenderer!.GetTexture(opts[i], "Segoe UI", 15, sel ? Color.White : new Color(150, 150, 170));
                spriteBatch.Draw(t, new Vector2(bx + 12, by + (26 - t.Height) / 2), Color.White);
            }
        }

        // ═══════════ Account ═══════════

        void DrawAccount()
        {
            spriteBatch!.Draw(pixel!, new Rectangle(0, 0, width, height), Color.Black * 0.7f);
            int cx = width / 2;
            int cardW = 360, cardH = 320;
            int cardX = cx - cardW / 2, cardY = (height - cardH) / 2;

            spriteBatch.Draw(pixel!, new Rectangle(cardX, cardY, cardW, cardH), new Color(14, 14, 32) * 0.96f);
            DrawRectBorder(new Rectangle(cardX, cardY, cardW, cardH), new Color(160, 80, 255) * 0.15f);

            string titleKey = accountIsLoginMode ? "account_login" : "account_register";
            var title = textRenderer!.GetTexture(Localization.Get(titleKey), "Segoe UI", 20, Color.White);
            spriteBatch.Draw(title, new Vector2(cx - title.Width / 2, cardY + 16), Color.White);
            spriteBatch.Draw(pixel!, new Rectangle(cardX + 20, cardY + 46, cardW - 40, 1), Color.White * 0.08f);

            int fy = cardY + 56;
            if (accountsManager?.LoggedInUser != null)
            {
                var lt = textRenderer!.GetTexture($"{Localization.Get("logged_in_as")}: {accountsManager.LoggedInUser}", "Segoe UI", 12, new Color(80, 255, 120));
                spriteBatch.Draw(lt, new Vector2(cx - lt.Width / 2, fy), Color.White);
                fy += 24;
            }

            int fx = cardX + 24, fw = cardW - 48;
            DrawTextField(Localization.Get("username"), accountUsername, fx, fy, fw, accountFieldIndex == 0, false);
            DrawTextField(Localization.Get("password"), accountPassword, fx, fy + 64, fw, accountFieldIndex == 1, true);

            var hint = textRenderer!.GetTexture(Localization.Get("hint_account"), "Segoe UI", 11, new Color(80, 80, 110));
            spriteBatch.Draw(hint, new Vector2(cx - hint.Width / 2, cardY + cardH - 44), Color.White);

            if (accountShowMessage)
            {
                bool ok = accountMessage == Localization.Get("register_success") || accountMessage == Localization.Get("login_success");
                var mc = ok ? new Color(80, 255, 120) : new Color(255, 100, 100);
                var m = textRenderer!.GetTexture(accountMessage, "Segoe UI", 13, mc);
                spriteBatch.Draw(m, new Vector2(cx - m.Width / 2, cardY + cardH - 24), Color.White);
            }
        }

        void DrawTextField(string label, string value, int x, int y, int w, bool focused, bool masked)
        {
            var lb = textRenderer!.GetTexture(label, "Segoe UI", 12, new Color(120, 120, 150));
            spriteBatch!.Draw(lb, new Vector2(x, y), Color.White);
            var box = new Rectangle(x, y + 18, w, 32);
            spriteBatch.Draw(pixel!, box, focused ? Color.White * 0.06f : Color.White * 0.025f);
            DrawRectBorder(box, focused ? new Color(0, 200, 255) * 0.3f : Color.White * 0.06f);
            string disp = masked ? new string('\u2022', value.Length) : value;
            if (focused) disp += "|";
            var vt = textRenderer!.GetTexture(disp, "Segoe UI", 14, Color.White);
            spriteBatch.Draw(vt, new Vector2(box.X + 8, box.Y + 7), Color.White);
        }

        // ═══════════ Language ═══════════

        void DrawLanguage()
        {
            spriteBatch!.Draw(pixel!, new Rectangle(0, 0, width, height), Color.Black * 0.7f);
            int cx = width / 2, lc = Localization.All.Length;
            int cardW = 300, cardH = 60 + lc * 38;
            int cardX = cx - cardW / 2, cardY = (height - cardH) / 2;

            spriteBatch.Draw(pixel!, new Rectangle(cardX, cardY, cardW, cardH), new Color(14, 14, 32) * 0.96f);
            DrawRectBorder(new Rectangle(cardX, cardY, cardW, cardH), new Color(0, 200, 255) * 0.15f);

            var title = textRenderer!.GetTexture(Localization.Get("select_language"), "Segoe UI", 20, Color.White);
            spriteBatch.Draw(title, new Vector2(cx - title.Width / 2, cardY + 14), Color.White);
            spriteBatch.Draw(pixel!, new Rectangle(cardX + 20, cardY + 44, cardW - 40, 1), Color.White * 0.08f);

            for (int i = 0; i < lc; i++)
            {
                bool sel = i == languageMenuIndex;
                bool act = Localization.All[i] == Localization.Current;
                int by = cardY + 52 + i * 38;
                if (sel)
                {
                    spriteBatch.Draw(pixel!, new Rectangle(cardX + 16, by, cardW - 32, 30), new Color(0, 200, 255) * 0.1f);
                    spriteBatch.Draw(pixel!, new Rectangle(cardX + 16, by, 3, 30), new Color(0, 200, 255));
                }
                string dn = Localization.LanguageDisplayName(Localization.All[i]) + (act ? "  \u2713" : "");
                var t = textRenderer!.GetTexture(dn, "Segoe UI", 15, sel ? Color.White : new Color(160, 160, 180));
                spriteBatch.Draw(t, new Vector2(cardX + 30, by + (30 - t.Height) / 2), Color.White);
            }

            var hint = textRenderer!.GetTexture(Localization.Get("hint_language"), "Segoe UI", 11, new Color(80, 80, 110));
            spriteBatch.Draw(hint, new Vector2(cx - hint.Width / 2, cardY + cardH - 22), Color.White);
        }

        // ═══════════ Stats ═══════════

        void DrawStats()
        {
            spriteBatch!.Draw(pixel!, new Rectangle(0, 0, width, height), Color.Black * 0.7f);
            int cx = width / 2;
            int cardW = 460, cardH = 420;
            int cardX = cx - cardW / 2, cardY = (height - cardH) / 2;

            spriteBatch.Draw(pixel!, new Rectangle(cardX, cardY, cardW, cardH), new Color(14, 14, 32) * 0.96f);
            DrawRectBorder(new Rectangle(cardX, cardY, cardW, cardH), new Color(0, 200, 255) * 0.15f);

            var title = textRenderer!.GetTexture(Localization.Get("stats_title"), "Segoe UI", 22, Color.White);
            spriteBatch.Draw(title, new Vector2(cx - title.Width / 2, cardY + 14), Color.White);
            spriteBatch.Draw(pixel!, new Rectangle(cardX + 20, cardY + 46, cardW - 40, 1), Color.White * 0.08f);

            if (cachedStats == null || cachedStats.TotalPlays == 0)
            {
                var nd = textRenderer!.GetTexture(Localization.Get("no_data"), "Segoe UI", 16, new Color(100, 100, 130));
                spriteBatch.Draw(nd, new Vector2(cx - nd.Width / 2, cardY + 80), Color.White);
            }
            else
            {
                int sx = cardX + 24, sw = cardW - 48, lh = 22;
                int sy = cardY + 56;

                DrawStatLine(Localization.Get("total_plays"), $"{cachedStats.TotalPlays}", sy, sx, sw);
                DrawStatLine(Localization.Get("avg_accuracy"), $"{cachedStats.AvgAccuracy:F1}%", sy + lh, sx, sw);
                DrawStatLine(Localization.Get("best_score"), $"{cachedStats.BestScore}", sy + lh * 2, sx, sw);
                DrawStatLine(Localization.Get("best_combo"), $"{cachedStats.BestCombo}x", sy + lh * 3, sx, sw);
                DrawStatLine(Localization.Get("total_hit"), $"{cachedStats.TotalHit}", sy + lh * 4, sx, sw);
                DrawStatLine(Localization.Get("total_miss"), $"{cachedStats.TotalMiss}", sy + lh * 5, sx, sw);

                // Grade distribution bar
                int gy = sy + lh * 6 + 8;
                var gl = textRenderer!.GetTexture(Localization.Get("grade_dist"), "Segoe UI", 13, new Color(120, 120, 150));
                spriteBatch.Draw(gl, new Vector2(sx, gy), Color.White);
                gy += 20;

                string[] grades = { "SS", "S", "A", "B", "C", "D" };
                int[] counts = { cachedStats.CountSS, cachedStats.CountS, cachedStats.CountA, cachedStats.CountB, cachedStats.CountC, cachedStats.CountD };
                Color[] gColors = { new(255,220,50), new(255,180,0), new(80,255,120), new(0,200,255), new(180,140,255), new(255,80,80) };
                int maxC = counts.Max();
                int barMax = sw - 60;
                for (int i = 0; i < grades.Length; i++)
                {
                    var gn = textRenderer!.GetTexture(grades[i], "Segoe UI", 12, gColors[i]);
                    spriteBatch.Draw(gn, new Vector2(sx, gy + i * 18), Color.White);
                    int bw = maxC > 0 ? (int)((float)counts[i] / maxC * barMax) : 0;
                    if (bw < 2 && counts[i] > 0) bw = 2;
                    spriteBatch.Draw(pixel!, new Rectangle(sx + 36, gy + i * 18 + 2, bw, 12), gColors[i] * 0.5f);
                    var cv = textRenderer!.GetTexture($"{counts[i]}", "Segoe UI", 11, new Color(160, 160, 180));
                    spriteBatch.Draw(cv, new Vector2(sx + 40 + bw, gy + i * 18 + 1), Color.White);
                }

                // Recent plays
                int ry = gy + grades.Length * 18 + 12;
                spriteBatch.Draw(pixel!, new Rectangle(cardX + 20, ry - 4, cardW - 40, 1), Color.White * 0.08f);
                var rl = textRenderer!.GetTexture(Localization.Get("recent_plays"), "Segoe UI", 13, new Color(120, 120, 150));
                spriteBatch.Draw(rl, new Vector2(sx, ry), Color.White);
                ry += 20;

                if (cachedRecent != null)
                {
                    foreach (var rec in cachedRecent.Take(5))
                    {
                        string line = $"{rec.SongId}  {DiffShort(rec.Difficulty)}  {rec.Grade}  {rec.Score}";
                        var rt = textRenderer!.GetTexture(line, "Segoe UI", 11, new Color(140, 140, 160));
                        spriteBatch.Draw(rt, new Vector2(sx, ry), Color.White);
                        ry += 16;
                    }
                }
            }

            var hint = textRenderer!.GetTexture(Localization.Get("hint_stats"), "Segoe UI", 11, new Color(80, 80, 110));
            spriteBatch.Draw(hint, new Vector2(cx - hint.Width / 2, cardY + cardH - 22), Color.White);
        }

        // ═══════════ Beatmap Editor ═══════════

        void DrawEditor()
        {
            int cx = width / 2;

            // Title bar
            var title = textRenderer!.GetTexture(Localization.Get("editor_title"), "Segoe UI", 20, Color.White);
            spriteBatch!.Draw(title, new Vector2(cx - title.Width / 2, 10), Color.White);
            spriteBatch.Draw(pixel!, new Rectangle(0, 42, width, 1), Color.White * 0.08f);

            // Left panel - metadata fields
            int px = 12, py = 56;
            string[] labels = { "editor_name", "editor_author", "editor_audio", "editor_bpm" };
            string[] vals = { edSongName, edAuthor, edAudioPath, edBpm };
            for (int i = 0; i < 4; i++)
            {
                bool focused = edFieldFocus == i;
                var lb = textRenderer!.GetTexture(Localization.Get(labels[i]), "Segoe UI", 11, new Color(100, 100, 130));
                spriteBatch.Draw(lb, new Vector2(px, py + i * 50), Color.White);
                var box = new Rectangle(px, py + i * 50 + 16, 156, 28);
                spriteBatch.Draw(pixel!, box, focused ? Color.White * 0.06f : Color.White * 0.025f);
                DrawRectBorder(box, focused ? new Color(0, 200, 255) * 0.3f : Color.White * 0.04f);
                string v = vals[i] + (focused ? "|" : "");
                if (v.Length > 20) v = ".." + v[^18..];
                var vt = textRenderer!.GetTexture(v, "Segoe UI", 12, Color.White);
                spriteBatch.Draw(vt, new Vector2(box.X + 6, box.Y + 6), Color.White);
            }

            // Note count
            var nc = textRenderer!.GetTexture($"{Localization.Get("editor_notes")}: {edNotes.Count}", "Segoe UI", 13, new Color(0, 200, 255));
            spriteBatch.Draw(nc, new Vector2(px, py + 210), Color.White);

            // Save button
            int btnY = py + 240;
            var saveTex = textRenderer!.GetTexture(Localization.Get("editor_save"), "Segoe UI", 13, new Color(80, 255, 120));
            spriteBatch.Draw(saveTex, new Vector2(px, btnY), Color.White);

            // Preview button
            var prevTex = textRenderer!.GetTexture(Localization.Get("editor_play"), "Segoe UI", 13,
                edPreviewing ? new Color(255, 220, 50) : new Color(160, 160, 180));
            spriteBatch.Draw(prevTex, new Vector2(px, btnY + 22), Color.White);

            // Message
            if (edMessageTimer > 0 && !string.IsNullOrEmpty(edMessage))
            {
                bool isErr = edMessage.Contains("required") || edMessage.Contains("\u9700\u8981") || edMessage.Contains("\u81f3\u5c11");
                var mt = textRenderer!.GetTexture(edMessage, "Segoe UI", 13, isErr ? new Color(255, 100, 100) : new Color(80, 255, 120));
                spriteBatch.Draw(mt, new Vector2(px, btnY + 48), Color.White);
            }

            // Timeline area
            int tlLeft = 180, tlRight = width - 20;
            int tlTop = 56, tlBottom = height - 56;
            int tlW = tlRight - tlLeft;
            int laneW = tlW / 4;

            // Timeline background
            spriteBatch.Draw(pixel!, new Rectangle(tlLeft, tlTop, tlW, tlBottom - tlTop), new Color(8, 8, 20) * 0.8f);

            // Lane dividers
            for (int i = 0; i <= 4; i++)
                spriteBatch.Draw(pixel!, new Rectangle(tlLeft + i * laneW, tlTop, 1, tlBottom - tlTop), Color.White * 0.06f);

            // Lane labels
            for (int i = 0; i < 4; i++)
            {
                var ll = textRenderer!.GetTexture(LaneKeys[i], "Segoe UI", 14, new Color(80, 80, 110));
                spriteBatch.Draw(ll, new Vector2(tlLeft + i * laneW + (laneW - ll.Width) / 2, tlTop - 16), Color.White);
            }

            // Beat lines
            float bpm; float.TryParse(edBpm, out bpm); if (bpm <= 0) bpm = 120;
            float beatSec = 60f / bpm;
            float startBeat = (float)Math.Floor(edScrollTime / beatSec) * beatSec;
            for (float bt = startBeat; bt < edScrollTime + EdVisibleSeconds + beatSec; bt += beatSec)
            {
                float yp = tlTop + (bt - edScrollTime) * EdPixelsPerSecond;
                if (yp < tlTop || yp > tlBottom) continue;
                bool major = Math.Abs(bt % (beatSec * 4)) < 0.001f;
                spriteBatch.Draw(pixel!, new Rectangle(tlLeft, (int)yp, tlW, 1), Color.White * (major ? 0.12f : 0.04f));

                // Time label
                var tl2 = textRenderer!.GetTexture($"{bt:F1}s", "Segoe UI", 9, new Color(60, 60, 80));
                spriteBatch.Draw(tl2, new Vector2(tlLeft - tl2.Width - 4, (int)yp - 5), Color.White);
            }

            // Notes
            foreach (var n in edNotes)
            {
                float yp = tlTop + (n.Time - edScrollTime) * EdPixelsPerSecond;
                if (yp < tlTop - 20 || yp > tlBottom + 20) continue;

                int nx = tlLeft + n.Column * laneW + 4;
                int nw = laneW - 8;
                var nr = new Rectangle(nx, (int)yp - 8, nw, 16);
                Color nc2 = n == edDragging ? Color.Yellow : NoteColors[n.Column % 4];
                spriteBatch.Draw(pixel!, nr, nc2 * 0.8f);
                spriteBatch.Draw(pixel!, new Rectangle(nr.X, nr.Y, nr.Width, 2), Color.White * 0.4f);
                DrawRectBorder(nr, nc2 * 0.4f);
            }

            // Preview playhead
            if (edPreviewing)
            {
                float pyp = tlTop + (edScrollTime - edScrollTime) * EdPixelsPerSecond; // always at top since scroll follows
                // Actually the playhead is at current preview time
                float phY = tlTop; // scroll follows playhead
                spriteBatch.Draw(pixel!, new Rectangle(tlLeft, (int)phY, tlW, 2), new Color(255, 50, 50));
            }

            // Bottom hint
            spriteBatch.Draw(pixel!, new Rectangle(0, height - 46, width, 1), Color.White * 0.08f);
            var hint = textRenderer!.GetTexture(Localization.Get("editor_hint_main"), "Segoe UI", 11, new Color(80, 80, 110));
            spriteBatch.Draw(hint, new Vector2(cx - hint.Width / 2, height - 36), Color.White);

            var escHint = textRenderer!.GetTexture("Esc \u2190", "Segoe UI", 11, new Color(80, 80, 110));
            spriteBatch.Draw(escHint, new Vector2(width - escHint.Width - 12, height - 36), Color.White);
        }

        // ═══════════ Settings ═══════════

        Keys[] GetLaneKeys()
        {
            if (settingsManager == null) return new[] { Keys.D, Keys.F, Keys.J, Keys.K };
            var s = settingsManager.Settings;
            return new[]
            {
                Enum.TryParse<Keys>(s.Lane0Key, true, out var k0) ? k0 : Keys.D,
                Enum.TryParse<Keys>(s.Lane1Key, true, out var k1) ? k1 : Keys.F,
                Enum.TryParse<Keys>(s.Lane2Key, true, out var k2) ? k2 : Keys.J,
                Enum.TryParse<Keys>(s.Lane3Key, true, out var k3) ? k3 : Keys.K,
            };
        }

        string[] GetLaneKeyLabels()
        {
            var keys = GetLaneKeys();
            return keys.Select(k => k.ToString()).ToArray();
        }

        void UpdateSettings()
        {
            var s = settingsManager!.Settings;
            int itemCount = 8; // master, music, sfx, offset, lane0-3

            if (settingsBindingMode)
            {
                // Waiting for key press
                foreach (Keys k in Enum.GetValues(typeof(Keys)))
                {
                    if (k == Keys.None || k == Keys.Escape) continue;
                    if (kb.IsKeyDown(k) && !prevKb.IsKeyDown(k))
                    {
                        string kn = k.ToString();
                        switch (settingsBindingLane)
                        {
                            case 0: s.Lane0Key = kn; break;
                            case 1: s.Lane1Key = kn; break;
                            case 2: s.Lane2Key = kn; break;
                            case 3: s.Lane3Key = kn; break;
                        }
                        settingsBindingMode = false;
                        settingsManager.Save();
                        SyncSettingsToCloud();
                        return;
                    }
                }
                return;
            }

            if (kb.IsKeyDown(Keys.Up) && !prevKb.IsKeyDown(Keys.Up))
                settingsMenuIndex = (settingsMenuIndex - 1 + itemCount) % itemCount;
            if (kb.IsKeyDown(Keys.Down) && !prevKb.IsKeyDown(Keys.Down))
                settingsMenuIndex = (settingsMenuIndex + 1) % itemCount;

            if (kb.IsKeyDown(Keys.Left) && !prevKb.IsKeyDown(Keys.Left))
            {
                switch (settingsMenuIndex)
                {
                    case 0: s.MasterVolume = Math.Max(0, s.MasterVolume - 0.05f); break;
                    case 1: s.MusicVolume = Math.Max(0, s.MusicVolume - 0.05f); break;
                    case 2: s.SfxVolume = Math.Max(0, s.SfxVolume - 0.05f); break;
                    case 3: s.OffsetMs -= 5; break;
                }
                ApplyVolume();
                settingsManager.Save();
                SyncSettingsToCloud();
            }
            if (kb.IsKeyDown(Keys.Right) && !prevKb.IsKeyDown(Keys.Right))
            {
                switch (settingsMenuIndex)
                {
                    case 0: s.MasterVolume = Math.Min(1, s.MasterVolume + 0.05f); break;
                    case 1: s.MusicVolume = Math.Min(1, s.MusicVolume + 0.05f); break;
                    case 2: s.SfxVolume = Math.Min(1, s.SfxVolume + 0.05f); break;
                    case 3: s.OffsetMs += 5; break;
                }
                ApplyVolume();
                settingsManager.Save();
                SyncSettingsToCloud();
            }
            if (kb.IsKeyDown(Keys.Enter) && !prevKb.IsKeyDown(Keys.Enter))
            {
                if (settingsMenuIndex >= 4 && settingsMenuIndex <= 7)
                {
                    settingsBindingMode = true;
                    settingsBindingLane = settingsMenuIndex - 4;
                }
            }
        }

        void ApplyVolume()
        {
            if (menuMusicInstance != null && settingsManager != null)
                menuMusicInstance.Volume = settingsManager.Settings.MusicVolume * settingsManager.Settings.MasterVolume;
        }

        void DrawSettings()
        {
            spriteBatch!.Draw(pixel!, new Rectangle(0, 0, width, height), Color.Black * 0.7f);
            int cx = width / 2;
            int cardW = 420, cardH = 400;
            int cardX = cx - cardW / 2, cardY = (height - cardH) / 2;

            spriteBatch.Draw(pixel!, new Rectangle(cardX, cardY, cardW, cardH), new Color(14, 14, 32) * 0.96f);
            DrawRectBorder(new Rectangle(cardX, cardY, cardW, cardH), new Color(0, 200, 255) * 0.15f);

            var title = textRenderer!.GetTexture(Localization.Get("settings_title"), "Segoe UI", 22, Color.White);
            spriteBatch.Draw(title, new Vector2(cx - title.Width / 2, cardY + 14), Color.White);
            spriteBatch.Draw(pixel!, new Rectangle(cardX + 20, cardY + 46, cardW - 40, 1), Color.White * 0.08f);

            var s = settingsManager!.Settings;
            int sy = cardY + 56, sx = cardX + 24, sw = cardW - 48;
            int lh = 36;

            // Volume sliders
            DrawSettingsSlider(Localization.Get("master_volume"), s.MasterVolume, sy, sx, sw, settingsMenuIndex == 0);
            DrawSettingsSlider(Localization.Get("music_volume"), s.MusicVolume, sy + lh, sx, sw, settingsMenuIndex == 1);
            DrawSettingsSlider(Localization.Get("sfx_volume"), s.SfxVolume, sy + lh * 2, sx, sw, settingsMenuIndex == 2);

            // Offset
            bool selOffset = settingsMenuIndex == 3;
            var offLabel = textRenderer!.GetTexture(Localization.Get("offset_ms"), "Segoe UI", 13,
                selOffset ? Color.White : new Color(120, 120, 150));
            spriteBatch.Draw(offLabel, new Vector2(sx, sy + lh * 3), Color.White);
            var offVal = textRenderer!.GetTexture($"{s.OffsetMs}ms", "Segoe UI", 13,
                selOffset ? new Color(0, 200, 255) : Color.White);
            spriteBatch.Draw(offVal, new Vector2(sx + sw - offVal.Width, sy + lh * 3), Color.White);
            if (selOffset)
            {
                spriteBatch.Draw(pixel!, new Rectangle(sx, sy + lh * 3 - 2, sw, 20), new Color(0, 200, 255) * 0.05f);
                spriteBatch.Draw(pixel!, new Rectangle(sx, sy + lh * 3 - 2, 3, 20), new Color(0, 200, 255));
            }

            spriteBatch.Draw(pixel!, new Rectangle(cardX + 20, sy + lh * 4 - 4, cardW - 40, 1), Color.White * 0.08f);

            // Key bindings
            var bindTitle = textRenderer!.GetTexture(Localization.Get("key_bindings"), "Segoe UI", 14, new Color(0, 200, 255));
            spriteBatch.Draw(bindTitle, new Vector2(sx, sy + lh * 4 + 4), Color.White);

            string[] laneLabels = { "Lane 1", "Lane 2", "Lane 3", "Lane 4" };
            string[] laneKeys = { s.Lane0Key, s.Lane1Key, s.Lane2Key, s.Lane3Key };
            for (int i = 0; i < 4; i++)
            {
                int ky = sy + lh * 4 + 30 + i * 28;
                bool sel = settingsMenuIndex == 4 + i;
                bool binding = settingsBindingMode && settingsBindingLane == i;

                var ll = textRenderer!.GetTexture(laneLabels[i], "Segoe UI", 13,
                    sel ? Color.White : new Color(120, 120, 150));
                spriteBatch.Draw(ll, new Vector2(sx, ky), Color.White);

                string keyText = binding ? Localization.Get("press_key") : laneKeys[i];
                Color keyColor = binding ? new Color(255, 220, 50) : sel ? new Color(0, 200, 255) : Color.White;
                var kt = textRenderer!.GetTexture(keyText, "Segoe UI", 13, keyColor);
                spriteBatch.Draw(kt, new Vector2(sx + sw - kt.Width, ky), Color.White);

                if (sel)
                {
                    spriteBatch.Draw(pixel!, new Rectangle(sx, ky - 2, sw, 20), new Color(0, 200, 255) * 0.05f);
                    spriteBatch.Draw(pixel!, new Rectangle(sx, ky - 2, 3, 20), new Color(0, 200, 255));
                }
            }

            // Hint
            var hint = textRenderer!.GetTexture(Localization.Get("hint_settings"), "Segoe UI", 11, new Color(80, 80, 110));
            spriteBatch.Draw(hint, new Vector2(cx - hint.Width / 2, cardY + cardH - 22), Color.White);
        }

        void DrawSettingsSlider(string label, float value, int y, int x, int w, bool selected)
        {
            var lt = textRenderer!.GetTexture(label, "Segoe UI", 13,
                selected ? Color.White : new Color(120, 120, 150));
            spriteBatch!.Draw(lt, new Vector2(x, y), Color.White);

            int barX = x + 160, barW = w - 200, barY = y + 5, barH = 8;
            spriteBatch.Draw(pixel!, new Rectangle(barX, barY, barW, barH), Color.White * 0.08f);
            int fillW = (int)(barW * Math.Clamp(value, 0, 1));
            spriteBatch.Draw(pixel!, new Rectangle(barX, barY, fillW, barH),
                selected ? new Color(0, 200, 255) : new Color(80, 180, 220));

            var vt = textRenderer!.GetTexture($"{(int)(value * 100)}%", "Segoe UI", 12,
                selected ? new Color(0, 200, 255) : Color.White);
            spriteBatch.Draw(vt, new Vector2(x + w - vt.Width, y), Color.White);

            if (selected)
            {
                spriteBatch.Draw(pixel!, new Rectangle(x, y - 2, w, 20), new Color(0, 200, 255) * 0.05f);
                spriteBatch.Draw(pixel!, new Rectangle(x, y - 2, 3, 20), new Color(0, 200, 255));
            }
        }

        // ═══════════ Achievements ═══════════

        void UpdateAchievements()
        {
            var all = achievementManager?.GetAll();
            if (all == null) return;
            int count = all.Count;
            if (kb.IsKeyDown(Keys.Up) && !prevKb.IsKeyDown(Keys.Up))
                achievementsScrollIndex = Math.Max(0, achievementsScrollIndex - 1);
            if (kb.IsKeyDown(Keys.Down) && !prevKb.IsKeyDown(Keys.Down))
                achievementsScrollIndex = Math.Min(count - 1, achievementsScrollIndex + 1);
        }

        void DrawAchievements()
        {
            spriteBatch!.Draw(pixel!, new Rectangle(0, 0, width, height), Color.Black * 0.7f);
            int cx = width / 2;
            int cardW = 440, cardH = 420;
            int cardX = cx - cardW / 2, cardY = (height - cardH) / 2;

            spriteBatch.Draw(pixel!, new Rectangle(cardX, cardY, cardW, cardH), new Color(14, 14, 32) * 0.96f);
            DrawRectBorder(new Rectangle(cardX, cardY, cardW, cardH), new Color(255, 220, 50) * 0.15f);

            var title = textRenderer!.GetTexture(Localization.Get("achievements_title"), "Segoe UI", 22, Color.White);
            spriteBatch.Draw(title, new Vector2(cx - title.Width / 2, cardY + 14), Color.White);

            // Progress
            int unlocked = achievementManager?.UnlockedCount ?? 0;
            int total = achievementManager?.TotalCount ?? 0;
            var prog = textRenderer!.GetTexture($"{unlocked}/{total}", "Segoe UI", 14, new Color(255, 220, 50));
            spriteBatch.Draw(prog, new Vector2(cx - prog.Width / 2, cardY + 42), Color.White);

            spriteBatch.Draw(pixel!, new Rectangle(cardX + 20, cardY + 62, cardW - 40, 1), Color.White * 0.08f);

            var all = achievementManager?.GetAll() ?? new List<Achievement>();
            int sy = cardY + 72, sx = cardX + 24, sw = cardW - 48;
            int maxVisible = 8;
            int start = Math.Max(0, achievementsScrollIndex - maxVisible / 2);
            start = Math.Min(start, Math.Max(0, all.Count - maxVisible));

            for (int i = 0; i < maxVisible && start + i < all.Count; i++)
            {
                var ach = all[start + i];
                int ay = sy + i * 40;
                bool sel = start + i == achievementsScrollIndex;
                bool done = ach.Unlocked;

                Color bg = done ? new Color(255, 220, 50) * 0.06f : Color.White * 0.02f;
                Color border = done ? new Color(255, 220, 50) * 0.15f : Color.White * 0.04f;
                var rect = new Rectangle(sx, ay, sw, 34);
                spriteBatch.Draw(pixel!, rect, bg);
                DrawRectBorder(rect, sel ? new Color(0, 200, 255) * 0.3f : border);

                string icon = done ? "✓" : "✗";
                Color iconColor = done ? new Color(80, 255, 120) : new Color(120, 120, 150);
                var iconTex = textRenderer!.GetTexture(icon, "Segoe UI", 16, iconColor);
                spriteBatch.Draw(iconTex, new Vector2(sx + 8, ay + 4), Color.White);

                var nameTex = textRenderer!.GetTexture(Localization.Get(ach.NameKey), "Segoe UI", 13,
                    done ? Color.White : new Color(140, 140, 160));
                spriteBatch.Draw(nameTex, new Vector2(sx + 30, ay + 4), Color.White);

                var descTex = textRenderer!.GetTexture(Localization.Get(ach.DescKey), "Segoe UI", 10,
                    new Color(100, 100, 130));
                spriteBatch.Draw(descTex, new Vector2(sx + 30, ay + 20), Color.White);
            }

            var hint = textRenderer!.GetTexture(Localization.Get("hint_stats"), "Segoe UI", 11, new Color(80, 80, 110));
            spriteBatch.Draw(hint, new Vector2(cx - hint.Width / 2, cardY + cardH - 22), Color.White);
        }

        // ═══════════ Replay View ═══════════

        void StartReplayView(ReplayData replay)
        {
            replayData = replay;
            replayEventIndex = 0;
            state = GameState.ReplayView;
            replayJudgments.Clear();
            score = 0; combo = 0; maxCombo = 0; hitCount = 0; missCount = 0;
            keyFlashes.Clear(); particles.Clear(); judgmentPopups.Clear();
            shakeTimer = 0; beatPulseAlpha = 0;

            // Load song/beatmap for visual display
            LoadCurrentSong();
            EnterBorderlessFullscreen();
            menuMusicInstance?.Stop();
            stopwatch.Restart();
            if (songInstance != null)
            {
                songInstance.Volume = settingsManager?.Settings.MusicVolume ?? 0.7f;
                songInstance.Play();
            }
        }

        void UpdateReplayView(GameTime gameTime)
        {
            if (replayData == null) return;
            float time = (float)stopwatch.Elapsed.TotalSeconds;
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Process replay events
            while (replayEventIndex < replayData.Events.Count)
            {
                var ev = replayData.Events[replayEventIndex];
                if (ev.Time > time) break;
                replayEventIndex++;

                // Simulate the event visually
                Color jColor = ev.Judgment switch
                {
                    "PERFECT" => new Color(255, 220, 50),
                    "GREAT" => new Color(80, 255, 120),
                    "GOOD" => new Color(0, 200, 255),
                    _ => new Color(255, 80, 80)
                };

                if (ev.Judgment != "MISS")
                {
                    score += ev.ScoreGained;
                    combo = ev.ComboAt;
                    if (combo > maxCombo) maxCombo = combo;
                    hitCount++;
                    SpawnHitParticles(ev.Column);
                    sfxHit?.Play(0.6f * (settingsManager?.Settings.SfxVolume ?? 0.8f), 0, 0);

                    int lx = LaneLeft + ev.Column * LaneWidth + 4;
                    if (keyFlashPool != null)
                    {
                        var k = keyFlashPool.Rent();
                        k.Reset(new Rectangle(lx, HitZoneY, LaneWidth - 8, HitZoneHeight),
                            jColor, GameConfig.KeyFlashDuration);
                        keyFlashes.Add(k);
                    }
                }
                else
                {
                    combo = 0; missCount++;
                }

                judgmentPopups.Add(new JudgmentPopup { Text = ev.Judgment, Color = jColor, Timer = 0.6f,
                    Position = new Vector2(LaneLeft + ev.Column * LaneWidth + LaneWidth / 2, HitZoneY - 30) });
            }

            // Remove played notes
            for (var n = notes.First; n != null;)
            {
                var next = n.Next;
                if (time - n.Value.Time > GameConfig.ApproachTime + 0.5f) notes.Remove(n);
                n = next;
            }

            // Update effects
            if (shakeTimer > 0) shakeTimer -= dt;
            for (int i = particles.Count - 1; i >= 0; i--)
            { var p = particles[i]; p.Pos += p.Vel * dt; p.Vel.Y += 500f * dt; p.Life -= dt; if (p.Life <= 0) particles.RemoveAt(i); }
            for (int i = judgmentPopups.Count - 1; i >= 0; i--)
            { var j = judgmentPopups[i]; j.Timer -= dt; j.Position = new Vector2(j.Position.X, j.Position.Y - 45f * dt); if (j.Timer <= 0) judgmentPopups.RemoveAt(i); }
            for (int i = keyFlashes.Count - 1; i >= 0; i--)
            { var k = keyFlashes[i]; k.TimeToLive -= dt; if (k.TimeToLive <= 0) { keyFlashes.RemoveAt(i); keyFlashPool?.Return(k); } }

            // End of replay
            if (replayEventIndex >= replayData.Events.Count && notes.Count == 0)
            {
                songInstance?.Stop(); stopwatch.Stop();
                state = GameState.Result;
                score = replayData.FinalScore;
                maxCombo = replayData.MaxCombo;
                hitCount = replayData.Hit;
                missCount = replayData.Miss;
                resultGrade = replayData.Grade;
                maxScore = (hitCount + missCount) * 100;
                resultMenuIndex = 0;
            }
        }

        void DrawReplayView()
        {
            // Draw the same gameplay view
            float time = (float)stopwatch.Elapsed.TotalSeconds;
            int ll = LaneLeft, hz = HitZoneY;

            for (int i = 0; i < LaneCount; i++)
                spriteBatch!.Draw(pixel!, new Rectangle(ll + i * LaneWidth, 0, LaneWidth, height), Color.White * (i % 2 == 0 ? 0.02f : 0.04f));
            for (int i = 0; i <= LaneCount; i++)
                spriteBatch!.Draw(pixel!, new Rectangle(ll + i * LaneWidth, 0, 1, height), Color.White * 0.08f);

            int tier = combo >= GameConfig.ComboTier4 ? 4
                     : combo >= GameConfig.ComboTier3 ? 3
                     : combo >= GameConfig.ComboTier2 ? 2
                     : combo >= GameConfig.ComboTier1 ? 1 : 0;
            Color glow = TierGlowColor[tier];
            spriteBatch!.Draw(pixel!, new Rectangle(ll, hz, TotalLaneWidth, 2), glow * 0.8f);
            spriteBatch.Draw(pixel!, new Rectangle(ll, hz, TotalLaneWidth, HitZoneHeight), Color.White * 0.02f);

            // Notes
            for (var n = notes.Last; n != null; n = n.Previous)
            {
                float dt = n.Value.Time - time;
                float prog = (GameConfig.ApproachTime - dt) / (GameConfig.ApproachTime + 0.01f);
                if (prog < 0 || prog > 1.2f) continue;
                int nx = ll + n.Value.Column * LaneWidth + 6;
                int ny = (int)(MathHelper.Clamp(prog, 0f, 1f) * (hz - NoteHeight));
                var nr = new Rectangle(nx, ny, LaneWidth - 12, NoteHeight);
                Color nc = TierNoteColors[tier][n.Value.Column % 4];
                spriteBatch.Draw(pixel!, new Rectangle(nr.X - 1, nr.Y - 1, nr.Width + 2, nr.Height + 2), nc * 0.2f);
                spriteBatch.Draw(pixel!, nr, nc * 0.9f);
            }

            // Effects
            foreach (var k in keyFlashes)
                spriteBatch!.Draw(pixel!, k.Rect, k.Color * (Math.Clamp(k.TimeToLive / GameConfig.KeyFlashDuration, 0, 1) * 0.3f));
            foreach (var p in particles)
            { float pa = p.Life / p.MaxLife; float ps = p.Size * (0.5f + pa * 0.5f);
              spriteBatch!.Draw(pixel!, new Rectangle((int)(p.Pos.X - ps / 2), (int)(p.Pos.Y - ps / 2), (int)ps + 1, (int)ps + 1), p.Color * pa); }
            foreach (var j in judgmentPopups)
            { float ja = Math.Clamp(j.Timer / 0.3f, 0, 1);
              var jt = textRenderer!.GetTexture(j.Text, "Segoe UI", 22, j.Color);
              spriteBatch!.Draw(jt, new Vector2(j.Position.X - jt.Width / 2, j.Position.Y), Color.White * ja); }

            // HUD
            var sct = textRenderer!.GetTexture($"{Localization.Get("score")}: {score}", "Segoe UI", 18, Color.White);
            spriteBatch.Draw(sct, new Vector2(width - sct.Width - 16, 14), Color.White);

            if (combo > 1)
            {
                var ct = textRenderer!.GetTexture($"{combo}x", "Segoe UI", 36, glow);
                spriteBatch.Draw(ct, new Vector2((width - ct.Width) / 2, hz - 56), Color.White);
            }

            // REPLAY label
            var replayLabel = textRenderer!.GetTexture("▶ REPLAY", "Segoe UI", 20, new Color(255, 220, 50));
            spriteBatch.Draw(replayLabel, new Vector2(16, 14), Color.White);

            if (replayData != null)
            {
                var playerTex = textRenderer!.GetTexture(replayData.Player, "Segoe UI", 14, new Color(160, 160, 180));
                spriteBatch.Draw(playerTex, new Vector2(16, 40), Color.White);
            }
        }

        int GetUniqueSongsPlayed()
        {
            if (statsDb == null) return 0;
            var recent = statsDb.GetRecentPlays(accountsManager?.LoggedInUser, 1000);
            return recent.Select(r => r.SongId).Distinct().Count();
        }

        // ═══════════ Helpers ═══════════

        void DrawStatLine(string label, string value, int y, int x, int w)
        {
            var lt = textRenderer!.GetTexture(label, "Segoe UI", 13, new Color(120, 120, 150));
            var vt = textRenderer!.GetTexture(value, "Segoe UI", 13, Color.White);
            spriteBatch!.Draw(lt, new Vector2(x, y), Color.White);
            spriteBatch.Draw(vt, new Vector2(x + w - vt.Width, y), Color.White);
        }

        void DrawRectBorder(Rectangle r, Color c)
        {
            spriteBatch!.Draw(pixel!, new Rectangle(r.Left, r.Top, r.Width, 1), c);
            spriteBatch.Draw(pixel!, new Rectangle(r.Left, r.Bottom - 1, r.Width, 1), c);
            spriteBatch.Draw(pixel!, new Rectangle(r.Left, r.Top, 1, r.Height), c);
            spriteBatch.Draw(pixel!, new Rectangle(r.Right - 1, r.Top, 1, r.Height), c);
        }

        void SpawnHitParticles(int col)
        {
            Color[] c = TierNoteColors[ComboTier];
            Color bc = c[col % 4];
            int cnt = 8 + ComboTier * 5;
            int pcx = LaneLeft + col * LaneWidth + LaneWidth / 2;
            for (int i = 0; i < cnt; i++)
            {
                float a = (float)(rng.NextDouble() * Math.PI * 2);
                float spd = 120f + (float)rng.NextDouble() * 220f + ComboTier * 35f;
                particles.Add(new HitParticle
                {
                    Pos = new Vector2(pcx + (float)(rng.NextDouble() - 0.5) * 18, HitZoneY),
                    Vel = new Vector2((float)Math.Cos(a) * spd, (float)Math.Sin(a) * spd - 120f),
                    Color = Color.Lerp(bc, Color.White, (float)rng.NextDouble() * 0.4f),
                    Life = 0.3f + (float)rng.NextDouble() * 0.35f, MaxLife = 0.65f,
                    Size = 2f + (float)rng.NextDouble() * 4f,
                });
            }
        }

        // ═══════════ SFX Generation ═══════════

        SoundEffect GenerateHitSfx()
        {
            int sr = 44100; int len = (int)(0.08f * sr);
            byte[] pcm = new byte[len * 2];
            for (int i = 0; i < len; i++)
            { float t = (float)i / sr; float v = ((float)Math.Sin(2 * Math.PI * 800 * t) * (float)Math.Exp(-t * 60) * 0.5f + (float)Math.Sin(2 * Math.PI * 250 * t) * (float)Math.Exp(-t * 30) * 0.5f) * 0.7f;
              short s = (short)(Math.Clamp(v, -1f, 1f) * short.MaxValue); pcm[i * 2] = (byte)(s & 0xFF); pcm[i * 2 + 1] = (byte)((s >> 8) & 0xFF); }
            return new SoundEffect(pcm, sr, AudioChannels.Mono);
        }

        SoundEffect GenerateMissSfx()
        {
            int sr = 44100; int len = (int)(0.1f * sr);
            byte[] pcm = new byte[len * 2]; Random mr = new(99);
            for (int i = 0; i < len; i++)
            { float t = (float)i / sr; float v = ((float)Math.Sin(2 * Math.PI * 80 * t) * (float)Math.Exp(-t * 20) * 0.4f + ((float)mr.NextDouble() * 2 - 1) * (float)Math.Exp(-t * 30) * 0.2f) * 0.4f;
              short s = (short)(Math.Clamp(v, -1f, 1f) * short.MaxValue); pcm[i * 2] = (byte)(s & 0xFF); pcm[i * 2 + 1] = (byte)((s >> 8) & 0xFF); }
            return new SoundEffect(pcm, sr, AudioChannels.Mono);
        }

        // ═══════════ Music Generation ═══════════

        void GenerateMusicalWav(string path, float dur, float bpm = 120f, double bassNote = 110.0)
        {
            int sr = 44100; int n = (int)(sr * dur);
            float[] mix = new float[n]; float bs = 60f / bpm;
            Random wr = new((int)(bpm * 100 + dur * 10));

            // Kick on every beat
            for (float bt = 0; bt < dur; bt += bs)
            { int st = (int)(bt * sr); int ln = Math.Min((int)(0.18f * sr), n - st); float ph = 0;
              for (int i = 0; i < ln && st + i < n; i++)
              { float t = (float)i / sr; float fr = 150f * (float)Math.Exp(-t * 35) + 42f; ph += fr / sr;
                mix[st + i] += (float)Math.Sin(2 * Math.PI * ph) * (float)Math.Exp(-t * 7) * 0.45f; } }

            // Snare on beats 2,4
            for (float bt = bs; bt < dur; bt += bs * 2)
            { int st = (int)(bt * sr); int ln = Math.Min((int)(0.12f * sr), n - st);
              for (int i = 0; i < ln && st + i < n; i++)
              { float t = (float)i / sr; float bd = (float)Math.Sin(2 * Math.PI * 200 * t) * (float)Math.Exp(-t * 22);
                float ns = ((float)wr.NextDouble() * 2 - 1) * (float)Math.Exp(-t * 16);
                mix[st + i] += bd * 0.3f + ns * 0.25f; } }

            // Hi-hat on 8ths
            for (float ht = 0; ht < dur; ht += bs / 2)
            { int st = (int)(ht * sr); int ln = Math.Min((int)(0.035f * sr), n - st);
              for (int i = 0; i < ln && st + i < n; i++)
              { float t = (float)i / sr; mix[st + i] += ((float)wr.NextDouble() * 2 - 1) * (float)Math.Exp(-t * 90) * 0.13f; } }

            // Bass synth
            double[] bassNotes = { bassNote, bassNote * 1.333, bassNote * 1.5, bassNote * 1.25 };
            float md = bs * 4;
            for (float ms = 0; ms < dur; ms += md)
            { int ni = ((int)(ms / md)) % bassNotes.Length; double fr = bassNotes[ni];
              int st = (int)(ms * sr); int ln = Math.Min((int)(md * sr), n - st);
              for (int i = 0; i < ln && st + i < n; i++)
              { float t = (float)i / sr; float ev = Math.Min(t * 20, 1f) * Math.Max(1f - t / md, 0f);
                float ph = (float)((fr * t) % 1.0); mix[st + i] += (ph * 2 - 1) * ev * 0.14f; } }

            NormalizeAndWriteWav(path, mix, n, sr);
        }

        void GenerateMenuMusicWav(string path, float dur, float bpm, double bassNote)
        {
            int sr = 44100; int n = (int)(sr * dur);
            float[] mix = new float[n]; float bs = 60f / bpm;
            Random wr = new(42);

            for (float bt = 0; bt < dur; bt += bs)
            { int st = (int)(bt * sr); int ln = Math.Min((int)(0.15f * sr), n - st); float ph = 0;
              for (int i = 0; i < ln && st + i < n; i++)
              { float t = (float)i / sr; ph += (100f * (float)Math.Exp(-t * 25) + 35f) / sr;
                mix[st + i] += (float)Math.Sin(2 * Math.PI * ph) * (float)Math.Exp(-t * 9) * 0.25f; } }

            for (float ht = bs / 2; ht < dur; ht += bs)
            { int st = (int)(ht * sr); int ln = Math.Min((int)(0.025f * sr), n - st);
              for (int i = 0; i < ln && st + i < n; i++)
              { float t = (float)i / sr; mix[st + i] += ((float)wr.NextDouble() * 2 - 1) * (float)Math.Exp(-t * 120) * 0.06f; } }

            double[] cn = { bassNote * 2, bassNote * 2.5, bassNote * 3, bassNote * 2.67 }; float cd = bs * 8;
            for (float cs = 0; cs < dur; cs += cd)
            { int ni = ((int)(cs / cd)) % cn.Length; double[] tr = { cn[ni], cn[ni] * 1.25, cn[ni] * 1.5 };
              int st = (int)(cs * sr); int ln = Math.Min((int)(cd * sr), n - st);
              for (int i = 0; i < ln && st + i < n; i++)
              { float t = (float)i / sr; float ev = Math.Min(t * 4, 1f) * Math.Max(1f - t / cd * 0.3f, 0.5f);
                float v = 0; foreach (var f in tr) v += (float)Math.Sin(2 * Math.PI * f * t);
                mix[st + i] += v / 3f * ev * 0.1f; } }

            double[] bn = { bassNote, bassNote * 1.333, bassNote * 1.5, bassNote * 1.25 }; float md = bs * 4;
            for (float ms = 0; ms < dur; ms += md)
            { int ni = ((int)(ms / md)) % bn.Length; double fr = bn[ni];
              int st = (int)(ms * sr); int ln = Math.Min((int)(md * sr), n - st);
              for (int i = 0; i < ln && st + i < n; i++)
              { float t = (float)i / sr; mix[st + i] += (float)Math.Sin(2 * Math.PI * fr * t) * Math.Min(t * 10, 1f) * Math.Max(1f - t / md, 0f) * 0.12f; } }

            NormalizeAndWriteWav(path, mix, n, sr);
        }

        // Blue Archive style: bright piano+bell tone, upbeat pop drums, synth arpeggio, chord pads
        void GenerateBaStyleWav(string path, float dur, float bpm, double rootNote, int variation)
        {
            int sr = 44100; int n = (int)(sr * dur);
            float[] mix = new float[n]; float bs = 60f / bpm;
            Random wr = new(variation * 1000 + (int)(bpm * 10));

            // Major scale intervals: root, 2nd, 3rd, 5th, 6th, octave
            double[] scale = { 1.0, 9.0/8, 5.0/4, 3.0/2, 5.0/3, 2.0 };

            // Piano-bell lead melody (bright sine+harmonics with fast decay)
            double[] melody;
            switch (variation)
            {
                case 0: melody = new double[] { 1,5.0/4,3.0/2,2, 5.0/3,3.0/2,5.0/4,1, 9.0/8,5.0/4,3.0/2,5.0/3, 2,5.0/3,3.0/2,5.0/4 }; break;
                case 1: melody = new double[] { 2,5.0/3,3.0/2,5.0/4, 1,9.0/8,5.0/4,3.0/2, 5.0/3,2,5.0/3,3.0/2, 5.0/4,9.0/8,1,9.0/8 }; break;
                default: melody = new double[] { 3.0/2,2,5.0/3,3.0/2, 5.0/4,3.0/2,2,5.0/3, 1,5.0/4,3.0/2,5.0/4, 9.0/8,1,5.0/3,2 }; break;
            }

            // Lead melody - piano-like bell tones
            float noteLen = bs;
            for (int mi = 0; mi < (int)(dur / noteLen); mi++)
            {
                int idx = mi % melody.Length;
                double freq = rootNote * melody[idx];
                int st = (int)(mi * noteLen * sr);
                int ln = Math.Min((int)(noteLen * 0.9f * sr), n - st);
                for (int i = 0; i < ln && st + i < n; i++)
                {
                    float t = (float)i / sr;
                    float env = (float)Math.Exp(-t * 4.5) * Math.Min(t * 200, 1f);
                    // Fundamental + octave + 3rd harmonic for bell-like timbre
                    float v = (float)Math.Sin(2 * Math.PI * freq * t) * 0.5f
                            + (float)Math.Sin(2 * Math.PI * freq * 2 * t) * 0.25f
                            + (float)Math.Sin(2 * Math.PI * freq * 3 * t) * 0.08f;
                    mix[st + i] += v * env * 0.22f;
                }
            }

            // Synth arpeggio (fast 16th note arpeggios on chord tones)
            double[][] chords = {
                new[] { 1.0, 5.0/4, 3.0/2 },
                new[] { 5.0/3/2, 1.0, 5.0/4 },
                new[] { 9.0/8, 3.0/2/1.2, 3.0/2 },
                new[] { 3.0/2, 15.0/8/1.0, 2.0 },
            };
            float arpLen = bs / 4;
            for (float at = 0; at < dur; at += arpLen)
            {
                int ci = ((int)(at / (bs * 4))) % chords.Length;
                int ai = ((int)(at / arpLen)) % chords[ci].Length;
                double freq = rootNote * 2 * chords[ci][ai];
                int st = (int)(at * sr);
                int ln = Math.Min((int)(arpLen * 0.7f * sr), n - st);
                for (int i = 0; i < ln && st + i < n; i++)
                {
                    float t = (float)i / sr;
                    float env = (float)Math.Exp(-t * 12) * Math.Min(t * 400, 1f);
                    mix[st + i] += (float)Math.Sin(2 * Math.PI * freq * t) * env * 0.08f;
                }
            }

            // Upbeat kick (4-on-the-floor, punchy)
            for (float bt = 0; bt < dur; bt += bs)
            {
                int st = (int)(bt * sr); int ln = Math.Min((int)(0.12f * sr), n - st); float ph = 0;
                for (int i = 0; i < ln && st + i < n; i++)
                { float t = (float)i / sr; float fr = 180f * (float)Math.Exp(-t * 40) + 50f; ph += fr / sr;
                  mix[st + i] += (float)Math.Sin(2 * Math.PI * ph) * (float)Math.Exp(-t * 8) * 0.38f; }
            }

            // Snappy snare on 2 and 4
            for (float bt = bs; bt < dur; bt += bs * 2)
            {
                int st = (int)(bt * sr); int ln = Math.Min((int)(0.08f * sr), n - st);
                for (int i = 0; i < ln && st + i < n; i++)
                { float t = (float)i / sr;
                  float body = (float)Math.Sin(2 * Math.PI * 240 * t) * (float)Math.Exp(-t * 28);
                  float noise = ((float)wr.NextDouble() * 2 - 1) * (float)Math.Exp(-t * 22);
                  mix[st + i] += (body * 0.2f + noise * 0.22f); }
            }

            // Bright hi-hat on 8ths with accented offbeats
            for (float ht = 0; ht < dur; ht += bs / 2)
            {
                bool offbeat = ((int)(ht / (bs / 2))) % 2 == 1;
                float vol = offbeat ? 0.14f : 0.08f;
                int st = (int)(ht * sr); int ln = Math.Min((int)(0.02f * sr), n - st);
                for (int i = 0; i < ln && st + i < n; i++)
                { float t = (float)i / sr;
                  mix[st + i] += ((float)wr.NextDouble() * 2 - 1) * (float)Math.Exp(-t * 100) * vol; }
            }

            // Warm bass synth (sub + saw)
            double[] bassPattern = { 1.0, 1.0, 5.0/3/2, 3.0/2/2 };
            float bassLen = bs * 2;
            for (float bt = 0; bt < dur; bt += bassLen)
            {
                int bi = ((int)(bt / bassLen)) % bassPattern.Length;
                double freq = rootNote / 2 * bassPattern[bi];
                int st = (int)(bt * sr); int ln = Math.Min((int)(bassLen * 0.9f * sr), n - st);
                for (int i = 0; i < ln && st + i < n; i++)
                { float t = (float)i / sr;
                  float env = Math.Min(t * 30, 1f) * Math.Max(1f - t / (bassLen * 0.9f), 0f);
                  float sub = (float)Math.Sin(2 * Math.PI * freq * t);
                  float saw = (float)((freq * t) % 1.0) * 2 - 1;
                  mix[st + i] += (sub * 0.7f + saw * 0.3f) * env * 0.14f; }
            }

            // Chord pad (warm synth pad on chord changes)
            float chordLen = bs * 4;
            for (float ct = 0; ct < dur; ct += chordLen)
            {
                int ci2 = ((int)(ct / chordLen)) % chords.Length;
                int st = (int)(ct * sr); int ln = Math.Min((int)(chordLen * sr), n - st);
                for (int i = 0; i < ln && st + i < n; i++)
                { float t = (float)i / sr;
                  float env = Math.Min(t * 3, 1f) * Math.Max(1f - t / chordLen * 0.4f, 0.3f);
                  float v = 0;
                  foreach (var c in chords[ci2])
                      v += (float)Math.Sin(2 * Math.PI * rootNote * c * t);
                  mix[st + i] += v / 3f * env * 0.06f; }
            }

            NormalizeAndWriteWav(path, mix, n, sr);
        }

        void NormalizeAndWriteWav(string path, float[] mix, int n, int sr)
        {
            float mx = 0; for (int i = 0; i < n; i++) mx = Math.Max(mx, Math.Abs(mix[i]));
            if (mx > 0.85f) { float sc = 0.8f / mx; for (int i = 0; i < n; i++) mix[i] *= sc; }

            using var fs = new FileStream(path, FileMode.Create);
            using var bw = new BinaryWriter(fs);
            int br = sr * 2; int sc2 = n * 2;
            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); bw.Write(36 + sc2);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt ")); bw.Write(16);
            bw.Write((short)1); bw.Write((short)1); bw.Write(sr); bw.Write(br);
            bw.Write((short)2); bw.Write((short)16);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data")); bw.Write(sc2);
            for (int i = 0; i < n; i++)
            { short s = (short)(Math.Clamp(mix[i], -1f, 1f) * short.MaxValue); bw.Write(s); }
        }

        // ═══════════ Beatmap Generation ═══════════

        Beatmap GenerateBeatmapObject(float duration, float bpm, string diff)
        {
            float bs = 60f / bpm;
            float interval; double dc, tc;
            switch (diff)
            {
                case "very_difficulty": interval = bs / 4; dc = 0.45; tc = 0.15; break;
                case "difficulty": interval = bs / 3; dc = 0.30; tc = 0.05; break;
                case "hard": interval = bs / 2; dc = 0.25; tc = 0.0; break;
                default: interval = bs; dc = 0.0; tc = 0.0; break;
            }
            var nl = new List<Note>();
            var r = new Random((int)(bpm * 100 + duration * 7 + diff.GetHashCode()));
            for (float t = bs; t < duration - 0.5f; t += interval)
            {
                int col = r.Next(4);
                nl.Add(new Note { Time = (float)Math.Round(t, 3), Column = col });
                if (r.NextDouble() < dc) nl.Add(new Note { Time = (float)Math.Round(t, 3), Column = (col + 1 + r.Next(3)) % 4 });
                if (r.NextDouble() < tc) nl.Add(new Note { Time = (float)Math.Round(t, 3), Column = (col + 2) % 4 });
            }
            return new Beatmap { Notes = nl };
        }

        // ═══════════ Account Input ═══════════

        void HandleAccountInput(KeyboardState kbState, KeyboardState prevKbState)
        {
            bool shift = kbState.IsKeyDown(Keys.LeftShift) || kbState.IsKeyDown(Keys.RightShift);
            if (kbState.IsKeyDown(Keys.F1) && !prevKbState.IsKeyDown(Keys.F1))
            { accountIsLoginMode = !accountIsLoginMode; accountShowMessage = false; return; }
            if (kbState.IsKeyDown(Keys.Tab) && !prevKbState.IsKeyDown(Keys.Tab))
            { accountFieldIndex = (accountFieldIndex + 1) % 2; return; }
            if (kbState.IsKeyDown(Keys.Back) && !prevKbState.IsKeyDown(Keys.Back))
            {
                if (accountFieldIndex == 0 && accountUsername.Length > 0) accountUsername = accountUsername[..^1];
                else if (accountFieldIndex == 1 && accountPassword.Length > 0) accountPassword = accountPassword[..^1];
                return;
            }
            if (kbState.IsKeyDown(Keys.Enter) && !prevKbState.IsKeyDown(Keys.Enter))
            {
                if (accountsManager != null)
                {
                    bool loginOk = false;
                    string pwHash = AccountsManager.HashPassword(accountPassword);
                    if (accountIsLoginMode)
                    {
                        if (accountsManager.Login(accountUsername, accountPassword, out _))
                        { accountShowMessage = true; accountMessage = Localization.Get("login_success"); loginOk = true; accountPassword = ""; }
                        else { accountShowMessage = true; accountMessage = "Invalid credentials"; }
                    }
                    else
                    {
                        if (accountsManager.Register(accountUsername, accountPassword, out _))
                        { accountShowMessage = true; accountMessage = Localization.Get("register_success"); loginOk = true; accountPassword = ""; }
                        else { accountShowMessage = true; accountMessage = "Username taken or invalid"; }
                    }
                    // Cloud sync after successful login/register
                    if (loginOk && cloudSync != null)
                    {
                        var user = accountsManager.LoggedInUser ?? accountUsername;
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                if (!accountIsLoginMode)
                                    await cloudSync.RegisterAsync(user, pwHash);
                                else
                                    await cloudSync.LoginAsync(user, pwHash);

                                var result = await cloudSync.FullSyncAsync(user, statsDb, achievementManager, settingsManager);
                                syncStatusText = result.Success ? Localization.Get("sync_ok") : result.Message;
                                syncStatusTimer = 3f;
                            }
                            catch { syncStatusText = "Sync failed"; syncStatusTimer = 3f; }
                        });
                    }
                }
                return;
            }
            foreach (Keys k in Enum.GetValues(typeof(Keys)))
            {
                if (k == Keys.None) continue;
                if (kbState.IsKeyDown(k) && !prevKbState.IsKeyDown(k))
                {
                    char ch = KeyToChar(k, shift);
                    if (ch != '\0') { if (accountFieldIndex == 0) accountUsername += ch; else accountPassword += ch; }
                }
            }
        }

        static char KeyToChar(Keys k, bool shift)
        {
            if (k >= Keys.A && k <= Keys.Z) { char c = (char)('a' + (k - Keys.A)); return shift ? char.ToUpper(c) : c; }
            if (k >= Keys.D0 && k <= Keys.D9) return (char)('0' + (k - Keys.D0));
            if (k >= Keys.NumPad0 && k <= Keys.NumPad9) return (char)('0' + (k - Keys.NumPad0));
            if (k == Keys.OemMinus) return '-';
            if (k == Keys.OemPeriod) return '.';
            if (k == Keys.Space) return ' ';
            return '\0';
        }

        void LoadLocalProfile()
        {
            var lp = LocalProfileData.Load();
            customAvatarPath = lp.CustomAvatarPath ?? "";
            // Pre-load custom avatar texture if exists
            LoadCustomAvatarTexture();
            // Apply to edit defaults
            editAvatarIndex = Math.Max(0, Array.FindIndex(AvatarPresets, a => a.id == lp.AvatarId));
            editBannerIndex = Math.Max(0, Array.FindIndex(BannerPresets, b => b.id == lp.BannerId));
            editBio = lp.Bio ?? "";
            editRegion = lp.Region ?? "";
        }

        void SaveLocalProfile(string avatarId, string bannerId, string bio, string region)
        {
            var lp = new LocalProfileData
            {
                AvatarId = avatarId,
                BannerId = bannerId,
                Bio = bio,
                Region = region,
                CustomAvatarPath = customAvatarPath
            };
            lp.Save();
        }

        void LoadCustomAvatarTexture()
        {
            customAvatarTexture?.Dispose();
            customAvatarTexture = null;
            if (!string.IsNullOrEmpty(customAvatarPath) && File.Exists(customAvatarPath))
            {
                try
                {
                    using var fs = File.OpenRead(customAvatarPath);
                    customAvatarTexture = Texture2D.FromStream(GraphicsDevice, fs);
                }
                catch { customAvatarTexture = null; }
            }
        }

        void OpenAvatarFilePicker()
        {
            // Run file dialog on STA thread (required for Windows Forms dialog)
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    Directory.CreateDirectory(AvatarsDir);
                    using var ofd = new System.Windows.Forms.OpenFileDialog();
                    ofd.Title = "Select Avatar Image";
                    ofd.Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp";
                    ofd.InitialDirectory = AvatarsDir;
                    if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        // Copy to Avatars folder if not already there
                        string destPath = Path.Combine(AvatarsDir, Path.GetFileName(ofd.FileName));
                        if (!string.Equals(Path.GetFullPath(ofd.FileName), Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase))
                            File.Copy(ofd.FileName, destPath, true);
                        customAvatarPath = destPath;
                        // Reload texture on next frame
                        _pendingAvatarReload = true;
                    }
                }
                catch { }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
        }
        bool _pendingAvatarReload = false;

        void DrawAvatarAt(int x, int y, int size, string avatarId, string username)
        {
            if (avatarId == "custom" && customAvatarTexture != null)
            {
                // Draw custom image scaled to fit
                spriteBatch!.Draw(customAvatarTexture, new Rectangle(x, y, size, size), Color.White);
            }
            else
            {
                int avIdx = Array.FindIndex(AvatarPresets, a => a.id == avatarId);
                var avStyle = avIdx >= 0 ? AvatarPresets[avIdx] : AvatarPresets[0];
                spriteBatch!.Draw(pixel!, new Rectangle(x, y, size, size), avStyle.bg);
                string icon = avStyle.icon != "" ? avStyle.icon : (username.Length > 0 ? username[0].ToString().ToUpper() : "?");
                int fontSize = size > 40 ? 28 : 18;
                var charT = textRenderer!.GetTexture(icon, "Segoe UI", fontSize, avStyle.fg);
                spriteBatch.Draw(charT, new Vector2(x + (size - charT.Width) / 2, y + (size - charT.Height) / 2), Color.White);
            }
        }

        void SyncSettingsToCloud()
        {
            if (cloudSync == null || settingsManager == null || accountsManager?.LoggedInUser == null) return;
            var user = accountsManager.LoggedInUser;
            var settings = settingsManager.Settings;
            _ = Task.Run(async () =>
            {
                try { await cloudSync.UploadSettingsAsync(user, settings); }
                catch { }
            });
        }

        /// <summary>Register .rcm / .rcp / .rc file associations with custom icons (HKCU, no admin).</summary>
        static void RegisterFileAssociations()
        {
            try
            {
                // For single-file publish, the actual exe is the host process
                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath)) exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath)) return;
                string baseDir = Path.GetDirectoryName(exePath)!;
                string iconsDir = Path.Combine(baseDir, "Icons");
                if (!Directory.Exists(iconsDir)) return;

                var associations = new (string ext, string progId, string desc, string icoFile)[]
                {
                    (".rcm", "RhythmClicker.Beatmap",  "RhythmClicker Beatmap",  "file_rcm.ico"),
                    (".rcp", "RhythmClicker.Replay",   "RhythmClicker Replay",   "file_rcp.ico"),
                    (".rc",  "RhythmClicker.Data",     "RhythmClicker Data",     "file_rc.ico"),
                };

                foreach (var (ext, progId, desc, icoFile) in associations)
                {
                    string icoPath = Path.Combine(iconsDir, icoFile);
                    if (!File.Exists(icoPath)) continue;

                    // HKCU\Software\Classes\.ext → ProgId
                    using var extKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ext}");
                    extKey?.SetValue("", progId);

                    // HKCU\Software\Classes\ProgId
                    using var progKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{progId}");
                    progKey?.SetValue("", desc);

                    // DefaultIcon
                    using var iconKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{progId}\DefaultIcon");
                    iconKey?.SetValue("", $"\"{icoPath}\",0");
                }

                // Notify shell of changes
                SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
            }
            catch { /* non-critical */ }
        }

        [System.Runtime.InteropServices.DllImport("shell32.dll")]
        static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);

        // ═══════════ Profile ═══════════
        void UpdateProfile()
        {
            if (kb.IsKeyDown(Keys.Escape) && !prevKb.IsKeyDown(Keys.Escape))
            { state = GameState.Menu; return; }
            if (kb.IsKeyDown(Keys.Up) && !prevKb.IsKeyDown(Keys.Up)) profileScrollIndex = Math.Max(0, profileScrollIndex - 1);
            if (kb.IsKeyDown(Keys.Down) && !prevKb.IsKeyDown(Keys.Down)) profileScrollIndex++;
            // E = Edit profile (only own profile)
            if (kb.IsKeyDown(Keys.E) && !prevKb.IsKeyDown(Keys.E))
            {
                var user = accountsManager?.LoggedInUser;
                if (user != null && viewingProfile != null && string.Equals(viewingProfile.User, user, StringComparison.OrdinalIgnoreCase))
                {
                    editProfileFieldIndex = 0;
                    editBio = viewingProfile.Bio ?? "";
                    editRegion = viewingProfile.Region ?? "";
                    editAvatarIndex = Math.Max(0, Array.FindIndex(AvatarPresets, a => a.id == viewingProfile.AvatarId));
                    editBannerIndex = Math.Max(0, Array.FindIndex(BannerPresets, b => b.id == viewingProfile.BannerId));
                    editProfileMessage = "";
                    editProfileMsgTimer = 0f;
                    editProfileSaving = false;
                    // Load custom avatar if applicable
                    var lp = LocalProfileData.Load();
                    if (!string.IsNullOrEmpty(lp.CustomAvatarPath))
                    {
                        customAvatarPath = lp.CustomAvatarPath;
                        LoadCustomAvatarTexture();
                    }
                    state = GameState.EditProfile;
                    return;
                }
            }
            if (kb.IsKeyDown(Keys.F5) && !prevKb.IsKeyDown(Keys.F5))
            {
                var user = accountsManager?.LoggedInUser;
                if (user != null && cloudSync != null)
                {
                    profileLoading = true;
                    _ = Task.Run(async () =>
                    {
                        try { viewingProfile = await cloudSync.GetProfileAsync(user); }
                        catch { }
                        finally { profileLoading = false; }
                    });
                }
            }
        }

        void DrawProfile()
        {
            spriteBatch!.Draw(pixel!, new Rectangle(0, 0, width, height), Color.Black * 0.7f);
            int cx = width / 2;
            int cardW = 520, cardH = height - 60;
            int cardX = cx - cardW / 2, cardY = 30;

            spriteBatch.Draw(pixel!, new Rectangle(cardX, cardY, cardW, cardH), new Color(14, 14, 32) * 0.96f);
            DrawRectBorder(new Rectangle(cardX, cardY, cardW, cardH), new Color(0, 200, 255) * 0.15f);

            // Banner area
            int bannerH = 80;
            Color bannerColor = new Color(30, 40, 80);
            // Apply banner preset if profile loaded
            if (viewingProfile != null)
            {
                int bnIdx = Array.FindIndex(BannerPresets, b => b.id == viewingProfile.BannerId);
                if (bnIdx >= 0) bannerColor = BannerPresets[bnIdx].color;
            }
            spriteBatch.Draw(pixel!, new Rectangle(cardX + 1, cardY + 1, cardW - 2, bannerH), bannerColor);

            if (profileLoading)
            {
                var loading = textRenderer!.GetTexture(Localization.Get("profile_loading"), "Segoe UI", 16, new Color(100, 100, 130));
                spriteBatch.Draw(loading, new Vector2(cx - loading.Width / 2, cardY + bannerH + 40), Color.White);
                return;
            }

            if (viewingProfile == null)
            {
                var noProf = textRenderer!.GetTexture(Localization.Get("profile_not_logged_in"), "Segoe UI", 16, new Color(100, 100, 130));
                spriteBatch.Draw(noProf, new Vector2(cx - noProf.Width / 2, cardY + bannerH + 40), Color.White);
                var hint = textRenderer!.GetTexture(Localization.Get("hint_profile"), "Segoe UI", 11, new Color(80, 80, 110));
                spriteBatch.Draw(hint, new Vector2(cx - hint.Width / 2, cardY + cardH - 22), Color.White);
                return;
            }

            var p = viewingProfile;
            int sy = cardY + bannerH + 12;
            int sx = cardX + 20;
            int sw = cardW - 40;

            // Avatar with preset style
            int avSize = 56;
            int avX = cardX + 24, avY = cardY + bannerH - avSize / 2;
            int avIdx = Array.FindIndex(AvatarPresets, a => a.id == p.AvatarId);
            var avStyle = avIdx >= 0 ? AvatarPresets[avIdx] : AvatarPresets[0];
            spriteBatch.Draw(pixel!, new Rectangle(avX - 1, avY - 1, avSize + 2, avSize + 2), avStyle.fg * 0.4f);
            DrawAvatarAt(avX, avY, avSize, p.AvatarId, p.User);

            // Username
            var nameT = textRenderer!.GetTexture(p.User, "Segoe UI", 22, Color.White);
            spriteBatch.Draw(nameT, new Vector2(avX + avSize + 12, avY + 4), Color.White);

            // Badges
            int badgeX = avX + avSize + 12;
            int badgeY = avY + 30;
            foreach (var badge in p.Badges)
            {
                Color bc = ParseHexColor(badge.BadgeColor);
                var bt = textRenderer!.GetTexture(badge.BadgeName, "Segoe UI", 11, Color.White);
                int bw = bt.Width + 12;
                spriteBatch.Draw(pixel!, new Rectangle(badgeX, badgeY, bw, 20), bc * 0.3f);
                DrawRectBorder(new Rectangle(badgeX, badgeY, bw, 20), bc * 0.6f);
                spriteBatch.Draw(bt, new Vector2(badgeX + 6, badgeY + 3), Color.White);
                badgeX += bw + 6;
            }

            // Region + join date
            sy = avY + avSize + 16;
            if (!string.IsNullOrEmpty(p.Region))
            {
                var regT = textRenderer!.GetTexture($"\ud83c\udf0d {p.Region}", "Segoe UI", 12, new Color(120, 120, 150));
                spriteBatch.Draw(regT, new Vector2(sx, sy), Color.White);
            }
            if (!string.IsNullOrEmpty(p.CreatedAt))
            {
                var joinT = textRenderer!.GetTexture($"{Localization.Get("profile_joined")}: {p.CreatedAt[..Math.Min(10, p.CreatedAt.Length)]}", "Segoe UI", 12, new Color(120, 120, 150));
                spriteBatch.Draw(joinT, new Vector2(sx + sw - 160, sy), Color.White);
            }
            sy += 22;

            // Bio
            if (!string.IsNullOrEmpty(p.Bio))
            {
                spriteBatch.Draw(pixel!, new Rectangle(sx, sy, sw, 1), Color.White * 0.06f);
                sy += 6;
                var bioT = textRenderer!.GetTexture(p.Bio.Length > 200 ? p.Bio[..200] + "..." : p.Bio, "Segoe UI", 12, new Color(180, 180, 200));
                spriteBatch.Draw(bioT, new Vector2(sx, sy), Color.White);
                sy += Math.Max(bioT.Height, 16) + 8;
            }

            // Stats section
            spriteBatch.Draw(pixel!, new Rectangle(sx, sy, sw, 1), Color.White * 0.08f);
            sy += 8;
            var statsLabel = textRenderer!.GetTexture(Localization.Get("profile_stats"), "Segoe UI", 14, new Color(0, 200, 255));
            spriteBatch.Draw(statsLabel, new Vector2(sx, sy), Color.White);
            sy += 22;

            DrawStatLine(Localization.Get("total_plays"), $"{p.TotalPlays}", sy, sx, sw); sy += 22;
            DrawStatLine(Localization.Get("best_combo"), $"{p.BestCombo}x", sy, sx, sw); sy += 22;
            DrawStatLine(Localization.Get("avg_accuracy"), $"{p.AvgAccuracy:F1}%", sy, sx, sw); sy += 22;

            // Best grades per map
            if (p.BestGrades.Count > 0)
            {
                sy += 4;
                spriteBatch.Draw(pixel!, new Rectangle(sx, sy, sw, 1), Color.White * 0.08f);
                sy += 8;
                var mapsLabel = textRenderer!.GetTexture(Localization.Get("profile_maps"), "Segoe UI", 14, new Color(0, 200, 255));
                spriteBatch.Draw(mapsLabel, new Vector2(sx, sy), Color.White);
                sy += 22;

                int maxVisible = Math.Min(p.BestGrades.Count, 12);
                int startIdx = Math.Min(profileScrollIndex, Math.Max(0, p.BestGrades.Count - maxVisible));
                for (int i = startIdx; i < Math.Min(startIdx + maxVisible, p.BestGrades.Count); i++)
                {
                    if (sy + 18 > cardY + cardH - 30) break;
                    var g = p.BestGrades[i];
                    Color gc = g.BestGrade switch
                    {
                        "SS" => new Color(255, 220, 50),
                        "S" => new Color(255, 180, 0),
                        "A" => new Color(80, 255, 120),
                        "B" => new Color(0, 200, 255),
                        "C" => new Color(180, 140, 255),
                        _ => new Color(255, 80, 80)
                    };
                    var songLine = textRenderer!.GetTexture($"{g.SongId}  {DiffShort(g.Difficulty)}", "Segoe UI", 11, new Color(160, 160, 180));
                    spriteBatch.Draw(songLine, new Vector2(sx, sy), Color.White);
                    var gradeT = textRenderer!.GetTexture(g.BestGrade, "Segoe UI", 12, gc);
                    spriteBatch.Draw(gradeT, new Vector2(sx + sw - 120, sy), Color.White);
                    var accT = textRenderer!.GetTexture($"{g.BestAccuracy:F1}%", "Segoe UI", 11, new Color(140, 140, 160));
                    spriteBatch.Draw(accT, new Vector2(sx + sw - accT.Width, sy), Color.White);
                    sy += 18;
                }
            }

            var hintP = textRenderer!.GetTexture(Localization.Get("hint_profile"), "Segoe UI", 11, new Color(80, 80, 110));
            spriteBatch.Draw(hintP, new Vector2(cx - hintP.Width / 2, cardY + cardH - 22), Color.White);
        }

        // ═══════════ Edit Profile ═══════════
        void UpdateEditProfile(GameTime gameTime)
        {
            if (editProfileMsgTimer > 0)
                editProfileMsgTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Reload custom avatar texture if file picker finished
            if (_pendingAvatarReload)
            {
                _pendingAvatarReload = false;
                LoadCustomAvatarTexture();
            }

            if (kb.IsKeyDown(Keys.Escape) && !prevKb.IsKeyDown(Keys.Escape))
            { state = GameState.Profile; return; }

            bool shift = kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift);

            if (kb.IsKeyDown(Keys.Up) && !prevKb.IsKeyDown(Keys.Up))
                editProfileFieldIndex = Math.Max(0, editProfileFieldIndex - 1);
            if (kb.IsKeyDown(Keys.Down) && !prevKb.IsKeyDown(Keys.Down))
                editProfileFieldIndex = Math.Min(4, editProfileFieldIndex + 1);
            if (kb.IsKeyDown(Keys.Tab) && !prevKb.IsKeyDown(Keys.Tab))
                editProfileFieldIndex = (editProfileFieldIndex + 1) % 5;

            // Avatar selection (Left/Right on field 0)
            if (editProfileFieldIndex == 0)
            {
                if (kb.IsKeyDown(Keys.Left) && !prevKb.IsKeyDown(Keys.Left))
                    editAvatarIndex = (editAvatarIndex - 1 + AvatarPresets.Length) % AvatarPresets.Length;
                if (kb.IsKeyDown(Keys.Right) && !prevKb.IsKeyDown(Keys.Right))
                    editAvatarIndex = (editAvatarIndex + 1) % AvatarPresets.Length;
                // Enter on Custom avatar → open file picker
                if (kb.IsKeyDown(Keys.Enter) && !prevKb.IsKeyDown(Keys.Enter) && AvatarPresets[editAvatarIndex].id == "custom")
                    OpenAvatarFilePicker();
            }
            // Banner selection (Left/Right on field 1)
            else if (editProfileFieldIndex == 1)
            {
                if (kb.IsKeyDown(Keys.Left) && !prevKb.IsKeyDown(Keys.Left))
                    editBannerIndex = (editBannerIndex - 1 + BannerPresets.Length) % BannerPresets.Length;
                if (kb.IsKeyDown(Keys.Right) && !prevKb.IsKeyDown(Keys.Right))
                    editBannerIndex = (editBannerIndex + 1) % BannerPresets.Length;
            }
            // Bio text input (field 2)
            else if (editProfileFieldIndex == 2)
            {
                if (kb.IsKeyDown(Keys.Back) && !prevKb.IsKeyDown(Keys.Back))
                { if (editBio.Length > 0) editBio = editBio[..^1]; }
                else
                {
                    foreach (var key in kb.GetPressedKeys())
                    {
                        if (prevKb.IsKeyDown(key)) continue;
                        char? c = KeyToChar(key, shift);
                        if (c.HasValue && editBio.Length < 120) editBio += c.Value;
                    }
                }
            }
            // Region text input (field 3)
            else if (editProfileFieldIndex == 3)
            {
                if (kb.IsKeyDown(Keys.Back) && !prevKb.IsKeyDown(Keys.Back))
                { if (editRegion.Length > 0) editRegion = editRegion[..^1]; }
                else
                {
                    foreach (var key in kb.GetPressedKeys())
                    {
                        if (prevKb.IsKeyDown(key)) continue;
                        char? c = KeyToChar(key, shift);
                        if (c.HasValue && editRegion.Length < 30) editRegion += c.Value;
                    }
                }
            }

            // Enter on Save (field 4) or Ctrl+S anywhere
            bool ctrlS = (kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl))
                         && kb.IsKeyDown(Keys.S) && !prevKb.IsKeyDown(Keys.S);
            if ((editProfileFieldIndex == 4 && kb.IsKeyDown(Keys.Enter) && !prevKb.IsKeyDown(Keys.Enter)) || ctrlS)
            {
                if (!editProfileSaving)
                {
                    var user = accountsManager?.LoggedInUser;
                    if (user != null)
                    {
                        var avatarId = AvatarPresets[editAvatarIndex].id;
                        var bannerId = BannerPresets[editBannerIndex].id;
                        var bio = editBio;
                        var region = editRegion;

                        // Update local viewingProfile immediately
                        if (viewingProfile != null)
                        {
                            viewingProfile.AvatarId = avatarId;
                            viewingProfile.BannerId = bannerId;
                            viewingProfile.Bio = bio;
                            viewingProfile.Region = region;
                        }

                        // Always save locally
                        SaveLocalProfile(avatarId, bannerId, bio, region);
                        editProfileMessage = Localization.Get("edit_profile_saved");
                        editProfileMsgTimer = 2.5f;

                        // Try cloud upload in background (non-blocking)
                        if (cloudSync != null)
                        {
                            _ = Task.Run(async () =>
                            {
                                try { await cloudSync.UploadProfileAsync(user, avatarId, bannerId, bio, region); }
                                catch { }
                            });
                        }
                    }
                }
            }
        }

        void DrawEditProfile()
        {
            spriteBatch!.Draw(pixel!, new Rectangle(0, 0, width, height), Color.Black * 0.8f);
            int cx = width / 2;
            int cardW = 480, cardH = 420;
            int cardX = cx - cardW / 2, cardY = (height - cardH) / 2;

            spriteBatch.Draw(pixel!, new Rectangle(cardX, cardY, cardW, cardH), new Color(14, 14, 32) * 0.98f);
            DrawRectBorder(new Rectangle(cardX, cardY, cardW, cardH), new Color(0, 200, 255) * 0.2f);

            int sx = cardX + 24, sy = cardY + 16;
            int fieldW = cardW - 48;

            // Title
            var title = textRenderer!.GetTexture(Localization.Get("edit_profile_title"), "Segoe UI", 18, new Color(0, 200, 255));
            spriteBatch.Draw(title, new Vector2(cx - title.Width / 2, sy), Color.White);
            sy += 36;

            // ── Field 0: Avatar ──
            bool sel0 = editProfileFieldIndex == 0;
            var avLabel = textRenderer!.GetTexture(Localization.Get("edit_avatar"), "Segoe UI", 13, sel0 ? new Color(0, 200, 255) : new Color(140, 140, 160));
            spriteBatch.Draw(avLabel, new Vector2(sx, sy), Color.White);
            sy += 20;
            // Draw avatar preview
            var avPreset = AvatarPresets[editAvatarIndex];
            int avPrevSize = 48;
            int avPrevX = sx + 20;
            spriteBatch.Draw(pixel!, new Rectangle(avPrevX - 1, sy - 1, avPrevSize + 2, avPrevSize + 2), sel0 ? new Color(0, 200, 255) * 0.6f : Color.White * 0.15f);
            DrawAvatarAt(avPrevX, sy, avPrevSize, avPreset.id, accountsManager?.LoggedInUser ?? "?");
            // Name + arrows + browse button for custom
            string avLabelText = $"\u25C0  {avPreset.label}  \u25B6";
            var avNameT = textRenderer!.GetTexture(avLabelText, "Segoe UI", 14, sel0 ? Color.White : new Color(180, 180, 200));
            spriteBatch.Draw(avNameT, new Vector2(avPrevX + avPrevSize + 20, sy + 6), Color.White);
            if (avPreset.id == "custom")
            {
                string browseHint = customAvatarTexture != null ? Localization.Get("edit_avatar_change") : Localization.Get("edit_avatar_browse");
                var browseT = textRenderer!.GetTexture($"[Enter] {browseHint}", "Segoe UI", 11, sel0 ? new Color(0, 200, 255) : new Color(100, 100, 130));
                spriteBatch.Draw(browseT, new Vector2(avPrevX + avPrevSize + 20, sy + 28), Color.White);
            }
            sy += avPrevSize + 12;

            // ── Field 1: Banner ──
            bool sel1 = editProfileFieldIndex == 1;
            var bnLabel = textRenderer!.GetTexture(Localization.Get("edit_banner"), "Segoe UI", 13, sel1 ? new Color(0, 200, 255) : new Color(140, 140, 160));
            spriteBatch.Draw(bnLabel, new Vector2(sx, sy), Color.White);
            sy += 20;
            var bnPreset = BannerPresets[editBannerIndex];
            int bnW = fieldW - 40, bnH = 28;
            int bnX = sx + 20;
            spriteBatch.Draw(pixel!, new Rectangle(bnX - 1, sy - 1, bnW + 2, bnH + 2), sel1 ? new Color(0, 200, 255) * 0.6f : Color.White * 0.1f);
            spriteBatch.Draw(pixel!, new Rectangle(bnX, sy, bnW, bnH), bnPreset.color);
            var bnNameT = textRenderer!.GetTexture($"\u25C0  {bnPreset.label}  \u25B6", "Segoe UI", 12, Color.White);
            spriteBatch.Draw(bnNameT, new Vector2(bnX + (bnW - bnNameT.Width) / 2, sy + 6), Color.White);
            sy += bnH + 14;

            // ── Field 2: Bio ──
            bool sel2 = editProfileFieldIndex == 2;
            var bioLabel = textRenderer!.GetTexture(Localization.Get("edit_bio"), "Segoe UI", 13, sel2 ? new Color(0, 200, 255) : new Color(140, 140, 160));
            spriteBatch.Draw(bioLabel, new Vector2(sx, sy), Color.White);
            sy += 20;
            spriteBatch.Draw(pixel!, new Rectangle(sx + 20, sy, fieldW - 40, 26), sel2 ? new Color(30, 40, 70) : new Color(20, 24, 40));
            DrawRectBorder(new Rectangle(sx + 20, sy, fieldW - 40, 26), sel2 ? new Color(0, 200, 255) * 0.5f : Color.White * 0.08f);
            string bioDisplay = editBio + (sel2 ? "_" : "");
            var bioT = textRenderer!.GetTexture(bioDisplay.Length > 50 ? "..." + bioDisplay[^47..] : bioDisplay, "Segoe UI", 12, new Color(200, 200, 220));
            spriteBatch.Draw(bioT, new Vector2(sx + 26, sy + 5), Color.White);
            sy += 36;

            // ── Field 3: Region ──
            bool sel3 = editProfileFieldIndex == 3;
            var regLabel = textRenderer!.GetTexture(Localization.Get("edit_region"), "Segoe UI", 13, sel3 ? new Color(0, 200, 255) : new Color(140, 140, 160));
            spriteBatch.Draw(regLabel, new Vector2(sx, sy), Color.White);
            sy += 20;
            spriteBatch.Draw(pixel!, new Rectangle(sx + 20, sy, fieldW - 40, 26), sel3 ? new Color(30, 40, 70) : new Color(20, 24, 40));
            DrawRectBorder(new Rectangle(sx + 20, sy, fieldW - 40, 26), sel3 ? new Color(0, 200, 255) * 0.5f : Color.White * 0.08f);
            string regDisplay = editRegion + (sel3 ? "_" : "");
            var regT = textRenderer!.GetTexture(regDisplay.Length > 30 ? "..." + regDisplay[^27..] : regDisplay, "Segoe UI", 12, new Color(200, 200, 220));
            spriteBatch.Draw(regT, new Vector2(sx + 26, sy + 5), Color.White);
            sy += 38;

            // ── Field 4: Save button ──
            bool sel4 = editProfileFieldIndex == 4;
            string saveBtnText = editProfileSaving ? Localization.Get("edit_profile_saving") : Localization.Get("edit_profile_save");
            var saveT = textRenderer!.GetTexture(saveBtnText, "Segoe UI", 15, sel4 ? Color.White : new Color(140, 140, 160));
            int btnW = saveT.Width + 40, btnH = 32;
            int btnX = cx - btnW / 2;
            spriteBatch.Draw(pixel!, new Rectangle(btnX, sy, btnW, btnH), sel4 ? new Color(0, 140, 200) * 0.4f : new Color(30, 30, 50));
            DrawRectBorder(new Rectangle(btnX, sy, btnW, btnH), sel4 ? new Color(0, 200, 255) * 0.6f : Color.White * 0.1f);
            spriteBatch.Draw(saveT, new Vector2(cx - saveT.Width / 2, sy + 7), Color.White);
            sy += btnH + 10;

            // Status message
            if (editProfileMsgTimer > 0 && !string.IsNullOrEmpty(editProfileMessage))
            {
                Color msgColor = editProfileMessage.Contains("!") || editProfileMessage.Contains("失敗") || editProfileMessage.Contains("failed")
                    ? new Color(255, 80, 80)
                    : new Color(80, 255, 120);
                var msgT = textRenderer!.GetTexture(editProfileMessage, "Segoe UI", 12, msgColor);
                spriteBatch.Draw(msgT, new Vector2(cx - msgT.Width / 2, sy), Color.White);
            }

            // Hint
            var hint = textRenderer!.GetTexture(Localization.Get("hint_edit_profile"), "Segoe UI", 11, new Color(80, 80, 110));
            spriteBatch.Draw(hint, new Vector2(cx - hint.Width / 2, cardY + cardH - 22), Color.White);
        }

        // ═══════════ Search Players ═══════════
        void UpdateSearchPlayer()
        {
            bool shift = kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift);
            if (kb.IsKeyDown(Keys.Escape) && !prevKb.IsKeyDown(Keys.Escape))
            { state = GameState.Menu; return; }
            if (kb.IsKeyDown(Keys.Back) && !prevKb.IsKeyDown(Keys.Back))
            { if (searchQuery.Length > 0) searchQuery = searchQuery[..^1]; return; }
            if (kb.IsKeyDown(Keys.Up) && !prevKb.IsKeyDown(Keys.Up))
                searchSelectedIndex = Math.Max(0, searchSelectedIndex - 1);
            if (kb.IsKeyDown(Keys.Down) && !prevKb.IsKeyDown(Keys.Down))
                searchSelectedIndex = Math.Min(searchResults.Count - 1, searchSelectedIndex + 1);
            if (kb.IsKeyDown(Keys.Enter) && !prevKb.IsKeyDown(Keys.Enter))
            {
                if (searchResults.Count > 0 && searchSelectedIndex >= 0 && searchSelectedIndex < searchResults.Count)
                {
                    // View selected player's profile
                    var sel = searchResults[searchSelectedIndex];
                    state = GameState.Profile;
                    profileScrollIndex = 0;
                    profileLoading = true;
                    viewingProfile = null;
                    if (cloudSync != null)
                    {
                        _ = Task.Run(async () =>
                        {
                            try { viewingProfile = await cloudSync.GetProfileAsync(sel.Username); }
                            catch { }
                            finally { profileLoading = false; }
                        });
                    }
                }
                else if (searchQuery.Length > 0 && cloudSync != null)
                {
                    // Search
                    searchLoading = true;
                    var q = searchQuery;
                    _ = Task.Run(async () =>
                    {
                        try { searchResults = await cloudSync.SearchPlayersAsync(q); searchSelectedIndex = 0; }
                        catch { searchResults.Clear(); }
                        finally { searchLoading = false; }
                    });
                }
                return;
            }
            foreach (Keys k in Enum.GetValues(typeof(Keys)))
            {
                if (k == Keys.None) continue;
                if (kb.IsKeyDown(k) && !prevKb.IsKeyDown(k))
                {
                    char ch = KeyToChar(k, shift);
                    if (ch != '\0') searchQuery += ch;
                }
            }
        }

        void DrawSearchPlayer()
        {
            spriteBatch!.Draw(pixel!, new Rectangle(0, 0, width, height), Color.Black * 0.7f);
            int cx = width / 2;
            int cardW = 520, cardH = height - 60;
            int cardX = cx - cardW / 2, cardY = 30;

            spriteBatch.Draw(pixel!, new Rectangle(cardX, cardY, cardW, cardH), new Color(14, 14, 32) * 0.96f);
            DrawRectBorder(new Rectangle(cardX, cardY, cardW, cardH), new Color(0, 200, 255) * 0.15f);

            var title = textRenderer!.GetTexture(Localization.Get("search_title"), "Segoe UI", 20, Color.White);
            spriteBatch.Draw(title, new Vector2(cx - title.Width / 2, cardY + 12), Color.White);

            // Search input
            int inputY = cardY + 48;
            int sx = cardX + 20, sw = cardW - 40;
            var inputBox = new Rectangle(sx, inputY, sw, 30);
            spriteBatch.Draw(pixel!, inputBox, Color.White * 0.04f);
            DrawRectBorder(inputBox, new Color(0, 200, 255) * 0.3f);
            var inputLabel = textRenderer!.GetTexture(Localization.Get("search_placeholder"), "Segoe UI", 11, new Color(80, 80, 110));
            if (searchQuery.Length == 0)
                spriteBatch.Draw(inputLabel, new Vector2(sx + 8, inputY + 7), Color.White);
            else
            {
                var qt = textRenderer!.GetTexture(searchQuery, "Segoe UI", 14, Color.White);
                spriteBatch.Draw(qt, new Vector2(sx + 8, inputY + 6), Color.White);
            }

            int ry = inputY + 40;

            if (searchLoading)
            {
                var loading = textRenderer!.GetTexture(Localization.Get("search_loading"), "Segoe UI", 14, new Color(100, 100, 130));
                spriteBatch.Draw(loading, new Vector2(cx - loading.Width / 2, ry), Color.White);
            }
            else if (searchResults.Count == 0 && searchQuery.Length > 0)
            {
                var noResult = textRenderer!.GetTexture(Localization.Get("search_no_results"), "Segoe UI", 14, new Color(100, 100, 130));
                spriteBatch.Draw(noResult, new Vector2(cx - noResult.Width / 2, ry), Color.White);
            }
            else
            {
                for (int i = 0; i < searchResults.Count; i++)
                {
                    if (ry + 60 > cardY + cardH - 30) break;
                    var r = searchResults[i];
                    bool sel = i == searchSelectedIndex;

                    var rowRect = new Rectangle(sx, ry, sw, 54);
                    if (sel)
                    {
                        spriteBatch.Draw(pixel!, rowRect, new Color(0, 200, 255) * 0.08f);
                        spriteBatch.Draw(pixel!, new Rectangle(sx, ry, 3, 54), new Color(0, 200, 255));
                    }
                    else
                        spriteBatch.Draw(pixel!, rowRect, Color.White * 0.02f);
                    DrawRectBorder(rowRect, sel ? new Color(0, 200, 255) * 0.15f : Color.White * 0.03f);

                    // Avatar with preset style
                    int avS = 36;
                    DrawAvatarAt(sx + 8, ry + 9, avS, r.AvatarId, r.Username);

                    // Name + badges
                    var nameT = textRenderer!.GetTexture(r.Username, "Segoe UI", 15, Color.White);
                    spriteBatch.Draw(nameT, new Vector2(sx + 52, ry + 6), Color.White);

                    int bx = sx + 52 + nameT.Width + 8;
                    foreach (var badge in r.Badges)
                    {
                        Color bc = ParseHexColor(badge.BadgeColor);
                        var bt = textRenderer!.GetTexture(badge.BadgeName, "Segoe UI", 9, Color.White);
                        int bw = bt.Width + 8;
                        spriteBatch.Draw(pixel!, new Rectangle(bx, ry + 9, bw, 16), bc * 0.3f);
                        DrawRectBorder(new Rectangle(bx, ry + 9, bw, 16), bc * 0.5f);
                        spriteBatch.Draw(bt, new Vector2(bx + 4, ry + 11), Color.White);
                        bx += bw + 4;
                    }

                    // Stats line
                    string statsLine = $"{Localization.Get("total_plays")}: {r.TotalPlays}   {Localization.Get("best_combo")}: {r.BestCombo}x   {Localization.Get("avg_accuracy")}: {r.AvgAccuracy:F1}%";
                    var statsT = textRenderer!.GetTexture(statsLine, "Segoe UI", 10, new Color(120, 120, 150));
                    spriteBatch.Draw(statsT, new Vector2(sx + 52, ry + 28), Color.White);

                    if (!string.IsNullOrEmpty(r.Region))
                    {
                        var regT = textRenderer!.GetTexture(r.Region, "Segoe UI", 10, new Color(100, 100, 130));
                        spriteBatch.Draw(regT, new Vector2(sx + sw - regT.Width - 8, ry + 8), Color.White);
                    }

                    ry += 58;
                }
            }

            var hint = textRenderer!.GetTexture(Localization.Get("hint_search"), "Segoe UI", 11, new Color(80, 80, 110));
            spriteBatch.Draw(hint, new Vector2(cx - hint.Width / 2, cardY + cardH - 22), Color.White);
        }

        static Color ParseHexColor(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex[0] != '#' || hex.Length < 7) return new Color(255, 215, 0);
            try
            {
                int r = Convert.ToInt32(hex.Substring(1, 2), 16);
                int g = Convert.ToInt32(hex.Substring(3, 2), 16);
                int b = Convert.ToInt32(hex.Substring(5, 2), 16);
                return new Color(r, g, b);
            }
            catch { return new Color(255, 215, 0); }
        }
    }
}
