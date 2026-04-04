using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ClickerGame
{
    /// <summary>
    /// Stores user settings (volume, key bindings, offset) in an encrypted .rc file.
    /// </summary>
    public class SettingsManager
    {
        public GameSettings Settings { get; set; } = new();
        private readonly string _path;

        public SettingsManager(string path = "settings.rc")
        {
            _path = path;
            Load();
        }

        public void Load()
        {
            if (!File.Exists(_path))
            {
                Settings = GameSettings.Default();
                return;
            }
            try
            {
                Settings = RcFileManager.ReadEncrypted<GameSettings>(_path);
            }
            catch
            {
                Settings = GameSettings.Default();
            }
        }

        public void Save()
        {
            RcFileManager.WriteEncrypted(_path, Settings);
        }
    }

    public class GameSettings
    {
        public float MasterVolume { get; set; } = 0.8f;
        public float MusicVolume { get; set; } = 0.7f;
        public float SfxVolume { get; set; } = 0.8f;
        public int OffsetMs { get; set; } = 0;

        // Key bindings as string names (Keys enum)
        public string Lane0Key { get; set; } = "D";
        public string Lane1Key { get; set; } = "F";
        public string Lane2Key { get; set; } = "J";
        public string Lane3Key { get; set; } = "K";

        public static GameSettings Default() => new();
    }
}
