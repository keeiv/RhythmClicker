using System;
using DiscordRPC;
using DiscordRPC.Logging;

namespace ClickerGame
{
    public class DiscordRpcManager : IDisposable
    {
        private DiscordRpcClient? _client;
        private readonly string _appId;

        private const string DefaultAppId = "1489958225620111510";

        public DiscordRpcManager(string? appId = null)
        {
            _appId = appId ?? DefaultAppId;
            try
            {
                _client = new DiscordRpcClient(_appId);
                _client.Logger = new ConsoleLogger { Level = LogLevel.Warning };
                _client.Initialize();
                SetMenu();
            }
            catch
            {
                _client = null;
            }
        }

        public void SetMenu()
        {
            _client?.SetPresence(new RichPresence
            {
                Details = "In Menu",
                State = "Browsing songs",
                Assets = new Assets
                {
                    LargeImageKey = "icon",
                    LargeImageText = "RhythmClicker",
                },
                Timestamps = Timestamps.Now,
            });
        }

        public void SetPlaying(string songTitle, string difficulty)
        {
            _client?.SetPresence(new RichPresence
            {
                Details = $"Playing: {songTitle}",
                State = $"Difficulty: {difficulty}",
                Assets = new Assets
                {
                    LargeImageKey = "icon",
                    LargeImageText = "RhythmClicker",
                    SmallImageKey = "playing",
                    SmallImageText = "Playing",
                },
                Timestamps = Timestamps.Now,
            });
        }

        public void SetEditor(string songTitle)
        {
            _client?.SetPresence(new RichPresence
            {
                Details = "Beatmap Editor",
                State = $"Editing: {songTitle}",
                Assets = new Assets
                {
                    LargeImageKey = "icon",
                    LargeImageText = "RhythmClicker",
                    SmallImageKey = "editor",
                    SmallImageText = "Editor",
                },
                Timestamps = Timestamps.Now,
            });
        }

        public void SetResult(string grade, int score)
        {
            _client?.SetPresence(new RichPresence
            {
                Details = $"Result: Grade {grade}",
                State = $"Score: {score}",
                Assets = new Assets
                {
                    LargeImageKey = "icon",
                    LargeImageText = "RhythmClicker",
                },
            });
        }

        public void SetStats()
        {
            _client?.SetPresence(new RichPresence
            {
                Details = "Viewing Stats",
                State = "Player Statistics",
                Assets = new Assets
                {
                    LargeImageKey = "icon",
                    LargeImageText = "RhythmClicker",
                },
            });
        }

        public void Dispose()
        {
            _client?.ClearPresence();
            _client?.Dispose();
            _client = null;
        }
    }
}
