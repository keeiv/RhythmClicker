using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
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
        Stopwatch stopwatch = new Stopwatch();
        int score = 0;
        KeyboardState kb, prevKb;

        // Song selection and editor
        List<SongInfo> songs = new();
        int currentSongIndex = 0;
        string currentDifficulty = "easy";
        bool editorMode = false;

        // Menu / scene
        enum GameState { Menu, Playing, Result, Account }
        GameState state = GameState.Menu;
        string[] menuOptions = new[] { "Start Game", "Editor", "Account", "Exit" };
        int currentMenuIndex = 0;

        TextRenderer? textRenderer;
        Texture2D? circleTexture;

        // input feedback
        public class KeyFlash
        {
            public Rectangle Rect;
            public Color Color;
            public float TimeToLive;
            public void Reset(Rectangle rect, Color color, float ttl) { Rect = rect; Color = color; TimeToLive = ttl; }
        }
        List<KeyFlash> keyFlashes = new();
        ObjectPool<KeyFlash>? keyFlashPool;

        // result / scoring
        int maxScore = 0;
        bool summaryShown = false;
        int resultMenuIndex = 0;

        double songDurationSeconds = 0.0;
        RenderCache? renderCache;

        // account manager
        AccountsManager? accountsManager;
        string accountUsername = string.Empty;
        string accountPassword = string.Empty;
        bool accountShowMessage = false;
        string accountMessage = string.Empty;
        int accountFieldIndex = 0; // 0=username,1=password

        // result grade
        string resultGrade = "";

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
            base.Initialize();
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);
            pixel = new Texture2D(GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });

            textRenderer = new TextRenderer(GraphicsDevice);
            circleTexture = CreateCircleTexture(256, Color.CornflowerBlue);

            renderCache = new RenderCache(GraphicsDevice);
            keyFlashPool = new ObjectPool<KeyFlash>(() => new KeyFlash(), 16);

            // Precache common UI text to avoid runtime System.Drawing overhead
            foreach (var m in menuOptions) textRenderer.Precache(m, "Arial", 22, Color.White);
            textRenderer.Precache("Result", "Arial", 36, Color.White);
            textRenderer.Precache("Score: 0/0", "Arial", 22, Color.LightGray);
            textRenderer.Precache("▶", "Arial", 64, Color.White);
            textRenderer.Precache("↑↓選擇 Enter確認 Esc離開", "Arial", 13, Color.White);

            Directory.CreateDirectory("Assets");

            // Ensure example songs exist (generated if missing)
            EnsureExampleSongs();

            // Load songs metadata
            string songsMeta = "Assets/songs.json";
            if (!File.Exists(songsMeta))
            {
                File.WriteAllText(songsMeta, DefaultSongsJson());
            }
            var metaJson = File.ReadAllText(songsMeta);
            songs = System.Text.Json.JsonSerializer.Deserialize<List<SongInfo>>(metaJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<SongInfo>();

            LoadCurrentSong();
            accountsManager = new AccountsManager();
            // don't start playing until entering the game
            // stopwatch will be restarted when switching to Playing state
        }

        protected override void UnloadContent()
        {
            textRenderer?.Dispose();
            base.UnloadContent();
        }

        string DefaultBeatmapJson()
        {
                        return @"{
    ""notes"": [
        { ""time"": 0.5, ""column"": 0 },
        { ""time"": 0.9, ""column"": 1 },
        { ""time"": 1.3, ""column"": 2 },
        { ""time"": 1.7, ""column"": 3 }
    ]
}";
        }

        string DefaultSongsJson()
        {
            return @"[
      { ""Id"": ""song1"", ""Title"": ""Example A"", ""File"": ""song1.wav"", ""Difficulties"": [""easy"", ""hard""] },
      { ""Id"": ""song2"", ""Title"": ""Example B"", ""File"": ""song2.wav"", ""Difficulties"": [""easy""] },
      { ""Id"": ""song3"", ""Title"": ""Example C"", ""File"": ""song3.wav"", ""Difficulties"": [""easy""] }
    ]";
        }

        void EnsureExampleSongs()
        {
            // create 3 example wavs if missing
            if (!File.Exists("Assets/song1.wav")) GenerateExampleWav("Assets/song1.wav", 8.0f, 440.0);
            if (!File.Exists("Assets/song2.wav")) GenerateExampleWav("Assets/song2.wav", 6.0f, 523.25);
            if (!File.Exists("Assets/song3.wav")) GenerateExampleWav("Assets/song3.wav", 10.0f, 349.23);

            // create a few beatmaps for them if missing
            if (!File.Exists("Assets/song1_easy.json")) File.WriteAllText("Assets/song1_easy.json", DefaultBeatmapJson());
                        if (!File.Exists("Assets/song1_hard.json")) File.WriteAllText("Assets/song1_hard.json", @"{
    ""notes"": [
        { ""time"": 0.4, ""column"": 0 },{ ""time"": 0.6, ""column"": 1 },{ ""time"": 0.8, ""column"": 2 },{ ""time"": 1.0, ""column"": 3 },{ ""time"": 1.2, ""column"": 0 },{ ""time"": 1.4, ""column"": 1 }
    ]
}");
            if (!File.Exists("Assets/song2_easy.json")) File.WriteAllText("Assets/song2_easy.json", DefaultBeatmapJson());
            if (!File.Exists("Assets/song3_easy.json")) File.WriteAllText("Assets/song3_easy.json", DefaultBeatmapJson());
        }

        void LoadCurrentSong()
        {
            if (songs.Count == 0)
            {
                // fallback single song
                if (!File.Exists("Assets/song.wav")) GenerateExampleWav("Assets/song.wav", 3.0f);
                LoadSong("Assets/song.wav", "Assets/beatmap.json");
                return;
            }
            var s = songs[Math.Clamp(currentSongIndex, 0, songs.Count - 1)];
            string songPath = Path.Combine("Assets", s.File);
            if (!File.Exists(songPath)) GenerateExampleWav(songPath, 6.0f);

            string beatmapPath = Path.Combine("Assets", s.Id + "_" + currentDifficulty + ".json");
            if (!File.Exists(beatmapPath))
            {
                // fallback to any available difficulty
                foreach (var d in s.Difficulties)
                {
                    var p = Path.Combine("Assets", s.Id + "_" + d + ".json");
                    if (File.Exists(p)) { beatmapPath = p; currentDifficulty = d; break; }
                }
                if (!File.Exists(beatmapPath)) File.WriteAllText(beatmapPath, DefaultBeatmapJson());
            }

            LoadSong(songPath, beatmapPath);
        }

        Texture2D CreateCircleTexture(int size, Color fill)
        {
            var tex = new Texture2D(GraphicsDevice, size, size);
            var data = new Color[size * size];
            float r = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r;
                    float dy = y + 0.5f - r;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    float alpha = d <= r ? 1f : 0f;
                    data[y * size + x] = new Color(fill.R, fill.G, fill.B, (byte)(fill.A * alpha));
                }
            }
            tex.SetData(data);
            return tex;
        }

        void LoadSong(string songFilePath, string beatmapPath)
        {
            var beatmapJson = File.ReadAllText(beatmapPath);
            beatmap = Beatmap.LoadFromString(beatmapJson);
            notes = new LinkedList<Note>(beatmap.Notes ?? new List<Note>());
            maxScore = (beatmap?.Notes?.Count ?? 0) * 100;

            songInstance?.Stop();
            songInstance?.Dispose();
            songEffect = null;
            using (var fs = File.OpenRead(songFilePath))
            {
                songEffect = SoundEffect.FromStream(fs);
                songInstance = songEffect.CreateInstance();
            }
            // song duration in seconds (if available)
            songDurationSeconds = songEffect?.Duration.TotalSeconds ?? 0.0;
            songInstance.Play();
        }

        void GenerateExampleWav(string path, float durationSeconds, double freq = 440.0)
        {
            int sampleRate = 44100;
            int samples = (int)(sampleRate * durationSeconds);
            using (var fs = new FileStream(path, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                int byteRate = sampleRate * 2;
                int subchunk2 = samples * 2;
                bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                bw.Write(36 + subchunk2);
                bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                bw.Write(16);
                bw.Write((short)1);
                bw.Write((short)1);
                bw.Write(sampleRate);
                bw.Write(byteRate);
                bw.Write((short)2);
                bw.Write((short)16);
                bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                bw.Write(subchunk2);
                for (int i = 0; i < samples; i++)
                {
                    short sample = (short)(Math.Sin(2 * Math.PI * freq * i / sampleRate) * short.MaxValue * 0.2);
                    bw.Write(sample);
                }
            }
        }

        protected override void Update(GameTime gameTime)
        {
            kb = Keyboard.GetState();
            // Escape: when playing, return to menu; when in menu, exit
            if (kb.IsKeyDown(Keys.Escape) && !prevKb.IsKeyDown(Keys.Escape))
            {
                if (state == GameState.Playing)
                {
                    // stop playback and return to menu
                    songInstance?.Stop();
                    stopwatch.Stop();
                    state = GameState.Menu;
                    // exit fullscreen
                    graphics!.IsFullScreen = false;
                    graphics!.ApplyChanges();
                    // clear input state to avoid immediate re-trigger
                    prevKb = Keyboard.GetState();
                    base.Update(gameTime);
                    return;
                }
                else
                {
                    Exit();
                }
            }

            if (state == GameState.Menu)
            {
                // navigate menu
                if (kb.IsKeyDown(Keys.Up) && !prevKb.IsKeyDown(Keys.Up))
                {
                    currentMenuIndex = (currentMenuIndex - 1 + menuOptions.Length) % menuOptions.Length;
                }
                if (kb.IsKeyDown(Keys.Down) && !prevKb.IsKeyDown(Keys.Down))
                {
                    currentMenuIndex = (currentMenuIndex + 1) % menuOptions.Length;
                }
                if (kb.IsKeyDown(Keys.Enter) && !prevKb.IsKeyDown(Keys.Enter))
                {
                    var choice = menuOptions[currentMenuIndex];
                    if (choice == "Start Game")
                    {
                        state = GameState.Playing;
                        editorMode = false;
                        // enter fullscreen for gameplay
                        graphics!.IsFullScreen = true;
                        graphics!.ApplyChanges();
                        stopwatch.Restart();
                        songInstance?.Stop();
                        songInstance?.Play();
                    }
                    else if (choice == "Editor")
                    {
                        state = GameState.Playing;
                        editorMode = true;
                        graphics!.IsFullScreen = true;
                        graphics!.ApplyChanges();
                        stopwatch.Restart();
                        songInstance?.Stop();
                        songInstance?.Play();
                    }
                    else if (choice == "Exit")
                    {
                        Exit();
                    }
                }

                prevKb = kb;
                base.Update(gameTime);
                return;
            }

            float time = (float)stopwatch.Elapsed.TotalSeconds;
            // Handle Result screen input
            if (state == GameState.Result)
            {
                if (kb.IsKeyDown(Keys.Up) && !prevKb.IsKeyDown(Keys.Up)) resultMenuIndex = (resultMenuIndex - 1 + 2) % 2;
                if (kb.IsKeyDown(Keys.Down) && !prevKb.IsKeyDown(Keys.Down)) resultMenuIndex = (resultMenuIndex + 1) % 2;
                if (kb.IsKeyDown(Keys.Enter) && !prevKb.IsKeyDown(Keys.Enter))
                {
                    if (resultMenuIndex == 0)
                    {
                        // Retry: reload current song
                        LoadCurrentSong();
                        summaryShown = false;
                        state = GameState.Playing;
                        stopwatch.Restart();
                        songInstance?.Stop();
                        songInstance?.Play();
                    }
                    else if (resultMenuIndex == 1)
                    {
                        // Menu
                        state = GameState.Menu;
                        graphics!.IsFullScreen = false;
                        graphics!.ApplyChanges();
                    }
                }
                prevKb = kb;
                base.Update(gameTime);
                return;
            }

            // Handle Account screen input
            if (state == GameState.Account)
            {
                // simple text input: letters, digits, backspace, tab to switch, enter to submit registration
                HandleAccountInput(kb, prevKb);
                prevKb = kb;
                base.Update(gameTime);
                return;
            }

            Keys[] keys = new[] { Keys.D, Keys.F, Keys.J, Keys.K };
            for (int c = 0; c < 4; c++)
            {
                if (kb.IsKeyDown(keys[c]) && !prevKb.IsKeyDown(keys[c]))
                {
                    if (editorMode)
                    {
                        // place a new note at current time
                        var n = new Note { Time = time, Column = c };
                        notes.AddLast(n);
                    }
                    else
                    {
                                // play mode: try hit nearest note (loosened window)
                                Note? nearest = null;
                                LinkedListNode<Note>? nearestNode = null;
                                float best = float.MaxValue;
                                const float hitWindow = 0.30f;
                                for (var node = notes.First; node != null; node = node.Next)
                                {
                                    var n = node.Value;
                                    if (n.Column != c) continue;
                                    float dt = Math.Abs(n.Time - time);
                                    if (dt <= hitWindow && dt < best)
                                    {
                                        best = dt;
                                        nearest = n;
                                        nearestNode = node;
                                    }
                                }
                                if (nearestNode != null)
                                {
                                    notes.Remove(nearestNode);
                                    score += 100;
                                }
                    }

                    // add a flash feedback for this column
                    int cols = 4;
                    int colW = width / cols;
                    int x = c * colW + 10;
                    int y = height - 120;
                    var rect = new Rectangle(x, y, colW - 20, 120);
                    if (keyFlashPool != null)
                    {
                        var k = keyFlashPool.Rent();
                        k.Reset(rect, Color.White, GameConfig.KeyFlashDuration);
                        keyFlashes.Add(k);
                    }
                }
            }

            // Save beatmap in editor: S
                if (editorMode && kb.IsKeyDown(Keys.S) && !prevKb.IsKeyDown(Keys.S))
            {
                var bm = new Beatmap { Notes = new List<Note>(notes) };
                string songId = songs.Count > 0 ? songs[currentSongIndex].Id : "song";
                string outPath = Path.Combine("Assets", songId + "_" + currentDifficulty + ".json");
                var opts = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(outPath, System.Text.Json.JsonSerializer.Serialize(bm, opts));
            }

            // Song switching: Left/Right change song
            if (kb.IsKeyDown(Keys.Left) && !prevKb.IsKeyDown(Keys.Left)) { currentSongIndex = Math.Max(0, currentSongIndex - 1); LoadCurrentSong(); }
            if (kb.IsKeyDown(Keys.Right) && !prevKb.IsKeyDown(Keys.Right)) { currentSongIndex = Math.Min(songs.Count - 1, currentSongIndex + 1); LoadCurrentSong(); }

            // check for end-of-song or all notes cleared -> show result
            if (state == GameState.Playing && !summaryShown)
            {
                if (notes.Count == 0 || stopwatch.Elapsed.TotalSeconds >= songDurationSeconds + 0.1)
                {
                    summaryShown = true;
                    state = GameState.Result;
                    songInstance?.Stop();
                    // compute grade
                    var pct = maxScore > 0 ? (double)score / maxScore : 0.0;
                    if (pct >= 0.95) resultGrade = "SS";
                    else if (pct >= 0.85) resultGrade = "S";
                    else if (pct >= 0.75) resultGrade = "A";
                    else if (pct >= 0.60) resultGrade = "B";
                    else if (pct >= 0.40) resultGrade = "C";
                    else resultGrade = "D";
                    resultMenuIndex = 0;
                }
            }

            prevKb = kb;
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.MediumPurple);

            spriteBatch!.Begin();

            // background (cached)
            if (renderCache != null)
            {
                var bg = renderCache.GetBackground(width, height);
                spriteBatch.Draw(bg, new Rectangle(0, 0, width, height), Color.White);
            }
            else
            {
                // fallback: simple gradient fill (rare)
                for (int y = 0; y < height; y += 4)
                {
                    float t = (float)y / (height - 1);
                    var cTop = new Color(180, 120, 255);
                    var cBottom = new Color(80, 180, 255);
                    var lerp = new Color(
                        (byte)(cTop.R + (cBottom.R - cTop.R) * t),
                        (byte)(cTop.G + (cBottom.G - cTop.G) * t),
                        (byte)(cTop.B + (cBottom.B - cTop.B) * t)
                    );
                    spriteBatch.Draw(pixel!, new Rectangle(0, y, width, 4), lerp);
                }
            }

            // Gameplay: notes + flashes + score
            if (state == GameState.Playing || editorMode)
            {
                int cols = 4;
                int colW = width / cols;
                float time = (float)stopwatch.Elapsed.TotalSeconds;

                for (int i = 0; i < cols; i++)
                {
                    Rectangle r = new Rectangle(i * colW, height - 120, colW - 4, 120);
                    spriteBatch.Draw(pixel!, r, Color.DarkSlateGray * 0.7f);
                }

                for (var node = notes.Last; node != null; )
                {
                    var prev = node.Previous;
                    var n = node.Value;
                    float t = n.Time - time;
                    if (time - n.Time > GameConfig.ApproachTime + 0.75f)
                    {
                        notes.Remove(node);
                        int mx = n.Column * colW + 10;
                        var r = new Rectangle(mx, height - 120, colW - 20, 120);
                        if (keyFlashPool != null)
                        {
                            var k = keyFlashPool.Rent();
                            k.Reset(r, Color.Red, GameConfig.MissFlashDuration);
                            keyFlashes.Add(k);
                        }
                        node = prev;
                        continue;
                    }
                    float progress = (GameConfig.ApproachTime - t) / (GameConfig.ApproachTime + 0.01f);
                    int x = n.Column * colW + 10;
                    int y = (int)(MathHelper.Clamp(progress, 0f, 1f) * (height - 160));
                    Rectangle nr = new Rectangle(x, y, colW - 20, 20);
                    spriteBatch.Draw(pixel!, nr, editorMode ? Color.Yellow : Color.OrangeRed);
                    node = prev;
                }

                // key flashes
                for (int i = keyFlashes.Count - 1; i >= 0; i--)
                {
                    var k = keyFlashes[i];
                    float alpha = Math.Clamp(k.TimeToLive / GameConfig.KeyFlashDuration, 0f, 1f);
                    spriteBatch.Draw(pixel!, k.Rect, k.Color * alpha);
                    k.TimeToLive -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                    if (k.TimeToLive <= 0f)
                    {
                        keyFlashes.RemoveAt(i);
                        keyFlashPool?.Return(k);
                    }
                }

                Rectangle scoreBar = new Rectangle(10, 10, Math.Min(600, score), 20);
                spriteBatch.Draw(pixel!, scoreBar, Color.LawnGreen);
            }

            // Menu
            if (state == GameState.Menu)
            {
                int centerX = width / 2;
                int centerY = height / 2 - 40;
                int circleSize = Math.Min(300, Math.Min(width, height) / 3);
                var circle = circleTexture!;
                int cx = centerX - circleSize / 2;
                int cy = centerY - circleSize / 2;
                for (int glow = 1; glow <= 6; glow++)
                {
                    float alpha = 0.08f * (7 - glow);
                    int size = circleSize + glow * 18;
                    spriteBatch.Draw(circle, new Rectangle(centerX - size / 2, centerY - size / 2, size, size), Color.White * alpha);
                }
                spriteBatch.Draw(circle, new Rectangle(cx, cy, circleSize, circleSize), new Color(255, 120, 255));
                spriteBatch.Draw(circle, new Rectangle(cx, cy, circleSize, circleSize), Color.White * 0.08f);
                var playTex = textRenderer!.GetTexture("▶", "Arial", 64, Color.White);
                spriteBatch.Draw(playTex, new Rectangle(centerX - playTex.Width / 2, centerY - playTex.Height / 2, playTex.Width, playTex.Height), Color.White);

                int optW = 240; int optH = 54; int gap = 22;
                int optX = centerX - optW / 2;
                int optY = centerY + circleSize / 2 + 24;
                for (int i = 0; i < menuOptions.Length; i++)
                {
                    bool sel = i == currentMenuIndex;
                    var bg = sel ? new Color(255, 120, 255) * 0.32f : Color.White * 0.10f;
                    Rectangle btnRect = new Rectangle(optX, optY + i * (optH + gap), optW, optH);
                    spriteBatch.Draw(pixel!, btnRect, bg);
                    int r = optH / 2;
                    spriteBatch.Draw(circle, new Rectangle(btnRect.Left - r, btnRect.Top, optH, optH), bg);
                    spriteBatch.Draw(circle, new Rectangle(btnRect.Right - r, btnRect.Top, optH, optH), bg);
                    var tex = textRenderer!.GetTexture(menuOptions[i], "Arial", 22, sel ? Color.White : Color.LightGray);
                    var shadow = textRenderer!.GetTexture(menuOptions[i], "Arial", 22, Color.Black * 0.4f);
                    spriteBatch.Draw(shadow, new Rectangle(optX + 22 + 2, optY + i * (optH + gap) + (optH - tex.Height) / 2 + 2, tex.Width, tex.Height), Color.White);
                    spriteBatch.Draw(tex, new Rectangle(optX + 22, optY + i * (optH + gap) + (optH - tex.Height) / 2, tex.Width, tex.Height), Color.White);
                }
                var hint = textRenderer!.GetTexture("↑↓選擇 Enter確認 Esc離開", "Arial", 13, Color.White);
                var hintShadow = textRenderer!.GetTexture("↑↓選擇 Enter確認 Esc離開", "Arial", 13, Color.Black * 0.4f);
                spriteBatch.Draw(hintShadow, new Rectangle((width - hint.Width) / 2 + 2, height - 48 + 2, hint.Width, hint.Height), Color.White);
                spriteBatch.Draw(hint, new Rectangle((width - hint.Width) / 2, height - 48, hint.Width, hint.Height), Color.White);
            }

            // Result overlay
            if (state == GameState.Result)
            {
                spriteBatch.Draw(pixel!, new Rectangle(0, 0, width, height), Color.Black * 0.6f);
                var title = textRenderer!.GetTexture("Result", "Arial", 36, Color.White);
                spriteBatch.Draw(title, new Rectangle((width - title.Width) / 2, 100, title.Width, title.Height), Color.White);
                var scoreTex = textRenderer!.GetTexture($"Score: {score}/{maxScore}", "Arial", 22, Color.LightGray);
                spriteBatch.Draw(scoreTex, new Rectangle((width - scoreTex.Width) / 2, 160, scoreTex.Width, scoreTex.Height), Color.White);
                var gradeTex = textRenderer!.GetTexture($"Grade: {resultGrade}", "Arial", 28, Color.Yellow);
                spriteBatch.Draw(gradeTex, new Rectangle((width - gradeTex.Width) / 2, 200, gradeTex.Width, gradeTex.Height), Color.White);
                string[] opts = new[] { "Retry", "Menu" };
                for (int i = 0; i < opts.Length; i++)
                {
                    var col = i == resultMenuIndex ? Color.White : Color.LightGray;
                    var t = textRenderer!.GetTexture(opts[i], "Arial", 20, col);
                    spriteBatch.Draw(t, new Rectangle((width - t.Width) / 2, 280 + i * 40, t.Width, t.Height), Color.White);
                }
            }

            // Account overlay
            if (state == GameState.Account)
            {
                spriteBatch.Draw(pixel!, new Rectangle(0, 0, width, height), Color.Black * 0.6f);
                var title = textRenderer!.GetTexture("Account Register", "Arial", 28, Color.White);
                spriteBatch.Draw(title, new Rectangle((width - title.Width) / 2, 100, title.Width, title.Height), Color.White);
                var uLabel = textRenderer!.GetTexture("Username:", "Arial", 18, Color.LightGray);
                spriteBatch.Draw(uLabel, new Rectangle((width - 400) / 2, 160, uLabel.Width, uLabel.Height), Color.White);
                var uBox = new Rectangle((width - 400) / 2, 190, 400, 36);
                spriteBatch.Draw(pixel!, uBox, accountFieldIndex == 0 ? Color.White * 0.12f : Color.Black * 0.1f);
                var uText = textRenderer!.GetTexture(accountUsername + (accountFieldIndex == 0 ? "|" : ""), "Arial", 18, Color.White);
                spriteBatch.Draw(uText, new Rectangle(uBox.X + 8, uBox.Y + 6, uText.Width, uText.Height), Color.White);
                var pLabel = textRenderer!.GetTexture("Password:", "Arial", 18, Color.LightGray);
                spriteBatch.Draw(pLabel, new Rectangle((width - 400) / 2, 240, pLabel.Width, pLabel.Height), Color.White);
                var pBox = new Rectangle((width - 400) / 2, 270, 400, 36);
                spriteBatch.Draw(pixel!, pBox, accountFieldIndex == 1 ? Color.White * 0.12f : Color.Black * 0.1f);
                var masked = new string('*', accountPassword.Length);
                var pText = textRenderer!.GetTexture(masked + (accountFieldIndex == 1 ? "|" : ""), "Arial", 18, Color.White);
                spriteBatch.Draw(pText, new Rectangle(pBox.X + 8, pBox.Y + 6, pText.Width, pText.Height), Color.White);
                var hint = textRenderer!.GetTexture("Type and press Enter to register (Tab to switch). Esc to cancel.", "Arial", 12, Color.LightGray);
                spriteBatch.Draw(hint, new Rectangle((width - hint.Width) / 2, pBox.Y + 56, hint.Width, hint.Height), Color.White);
                if (accountShowMessage)
                {
                    var m = textRenderer!.GetTexture(accountMessage, "Arial", 14, Color.Yellow);
                    spriteBatch.Draw(m, new Rectangle((width - m.Width) / 2, pBox.Y + 86, m.Width, m.Height), Color.White);
                }
            }

            spriteBatch.End();

            base.Draw(gameTime);
        }

            void HandleAccountInput(KeyboardState kbState, KeyboardState prevKbState)
            {
                bool shift = kbState.IsKeyDown(Keys.LeftShift) || kbState.IsKeyDown(Keys.RightShift);

                // navigation
                if (kbState.IsKeyDown(Keys.Tab) && !prevKbState.IsKeyDown(Keys.Tab))
                {
                    accountFieldIndex = (accountFieldIndex + 1) % 2;
                    return;
                }

                if (kbState.IsKeyDown(Keys.Escape) && !prevKbState.IsKeyDown(Keys.Escape))
                {
                    state = GameState.Menu;
                    return;
                }

                // handle backspace
                if (kbState.IsKeyDown(Keys.Back) && !prevKbState.IsKeyDown(Keys.Back))
                {
                    if (accountFieldIndex == 0 && accountUsername.Length > 0) accountUsername = accountUsername[..^1];
                    else if (accountFieldIndex == 1 && accountPassword.Length > 0) accountPassword = accountPassword[..^1];
                    return;
                }

                // submit
                if (kbState.IsKeyDown(Keys.Enter) && !prevKbState.IsKeyDown(Keys.Enter))
                {
                    if (accountsManager != null)
                    {
                        if (accountsManager.Register(accountUsername, accountPassword, out var msg))
                        {
                            accountShowMessage = true;
                            accountMessage = "Registered successfully";
                            accountPassword = string.Empty;
                        }
                        else
                        {
                            accountShowMessage = true;
                            accountMessage = msg;
                        }
                    }
                    return;
                }

                // character input (A-Z, 0-9, space, dash, dot)
                foreach (Keys k in Enum.GetValues(typeof(Keys)))
                {
                    if (k == Keys.None) continue;
                    if (kbState.IsKeyDown(k) && !prevKbState.IsKeyDown(k))
                    {
                        char ch = KeyToChar(k, shift);
                        if (ch != '\0')
                        {
                            if (accountFieldIndex == 0) accountUsername += ch;
                            else accountPassword += ch;
                        }
                    }
                }
            }

            static char KeyToChar(Keys k, bool shift)
            {
                if (k >= Keys.A && k <= Keys.Z)
                {
                    char c = (char)('a' + (k - Keys.A));
                    return shift ? char.ToUpper(c) : c;
                }
                if (k >= Keys.D0 && k <= Keys.D9)
                {
                    char c = (char)('0' + (k - Keys.D0));
                    return c;
                }
                if (k >= Keys.NumPad0 && k <= Keys.NumPad9)
                {
                    char c = (char)('0' + (k - Keys.NumPad0));
                    return c;
                }
                if (k == Keys.OemMinus) return '-';
                if (k == Keys.OemPeriod) return '.';
                if (k == Keys.Space) return ' ';
                return '\0';
            }
    }
}
