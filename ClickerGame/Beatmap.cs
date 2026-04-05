using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ClickerGame
{
    public class Beatmap
    {
        public string Name { get; set; } = "";
        public string Author { get; set; } = "";
        public string AudioFile { get; set; } = "";
        public string VideoFile { get; set; } = "";
        public string BackgroundImage { get; set; } = "";
        public float Bpm { get; set; }
        public List<Note> Notes { get; set; } = new();

        public static Beatmap LoadFromString(string s)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<Beatmap>(s, options) ?? new Beatmap();
        }
    }

    public class Note
    {
        public float Time { get; set; }
        public int Column { get; set; }
    }
}
