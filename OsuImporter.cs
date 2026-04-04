using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NAudio.Wave;
using NAudio.Vorbis;

namespace ClickerGame
{
    /// <summary>
    /// Imports osu! .osz packages and .osu beatmap files, converting them to the game's Beatmap format.
    /// Supports osu!mania (mode 3) natively and converts standard/taiko/catch modes to 4-lane.
    /// </summary>
    public static class OsuImporter
    {
        /// <summary>
        /// Import an .osz package (ZIP). Extracts audio + all .osu difficulties.
        /// Returns list of (Beatmap, difficultyLabel) for each .osu found.
        /// Audio file is extracted to assetsDir with a unique name based on songId.
        /// </summary>
        public static List<(Beatmap beatmap, string diffLabel)> ImportOsz(string oszPath, string assetsDir, string? songId = null)
        {
            var results = new List<(Beatmap, string)>();
            string tempDir = Path.Combine(Path.GetTempPath(), "rc_osz_" + Path.GetFileNameWithoutExtension(oszPath));
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);

            try
            {
                ZipFile.ExtractToDirectory(oszPath, tempDir);

                // Find all .osu files
                var osuFiles = Directory.GetFiles(tempDir, "*.osu");
                if (osuFiles.Length == 0) return results;

                string? audioFileCopied = null;

                foreach (var osuFile in osuFiles)
                {
                    var bm = Import(osuFile);

                    // Extract difficulty label from .osu filename: "Artist - Title (mapper) [DiffName].osu"
                    string fname = Path.GetFileNameWithoutExtension(osuFile);
                    string diffLabel = "easy";
                    int bracketStart = fname.LastIndexOf('[');
                    int bracketEnd = fname.LastIndexOf(']');
                    if (bracketStart >= 0 && bracketEnd > bracketStart)
                        diffLabel = fname.Substring(bracketStart + 1, bracketEnd - bracketStart - 1).Trim();

                    // Copy audio once (convert to WAV if needed), use unique name per song
                    if (audioFileCopied == null && !string.IsNullOrEmpty(bm.AudioFile))
                    {
                        string audioSrc = Path.Combine(tempDir, bm.AudioFile);
                        if (File.Exists(audioSrc))
                        {
                            // Use songId as prefix to avoid collision (e.g. multiple songs with "audio.mp3")
                            string prefix = !string.IsNullOrEmpty(songId) ? songId + "_" : "";
                            string wavName = prefix + Path.GetFileNameWithoutExtension(bm.AudioFile) + ".wav";
                            string audioDest = Path.Combine(assetsDir, wavName);
                            if (!File.Exists(audioDest))
                                ConvertToWav(audioSrc, audioDest);
                            audioFileCopied = wavName;
                        }
                    }
                    // Update AudioFile to the WAV name for all beatmaps
                    if (audioFileCopied != null) bm.AudioFile = audioFileCopied;

                    results.Add((bm, diffLabel));
                }
            }
            finally
            {
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
            }

            return results;
        }

        public static Beatmap Import(string osuFilePath)
        {
            var lines = File.ReadAllLines(osuFilePath);
            var bm = new Beatmap();
            string section = "";
            int mode = 0;
            int circleSize = 4; // mania key count
            float overallDifficulty = 5;
            var timingPoints = new List<(double offset, double beatLength, bool inherited)>();
            var hitObjects = new List<OsuHitObject>();

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("//")) continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line[1..^1];
                    continue;
                }

                switch (section)
                {
                    case "General":
                        if (line.StartsWith("Mode:"))
                            int.TryParse(line.Split(':').Last().Trim(), out mode);
                        if (line.StartsWith("AudioFilename:"))
                            bm.AudioFile = line.Substring("AudioFilename:".Length).Trim();
                        break;

                    case "Metadata":
                        if (line.StartsWith("Title:"))
                            bm.Name = line.Substring("Title:".Length).Trim();
                        if (line.StartsWith("Artist:"))
                            bm.Author = line.Substring("Artist:".Length).Trim();
                        break;

                    case "Difficulty":
                        if (line.StartsWith("CircleSize:"))
                            float.TryParse(line.Split(':').Last().Trim(), System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out var cs)
                                .ToString(); // just parse
                        if (line.StartsWith("CircleSize:"))
                            int.TryParse(line.Split(':').Last().Trim().Split('.')[0], out circleSize);
                        if (line.StartsWith("OverallDifficulty:"))
                            float.TryParse(line.Split(':').Last().Trim(), System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out overallDifficulty);
                        break;

                    case "TimingPoints":
                        ParseTimingPoint(line, timingPoints);
                        break;

                    case "HitObjects":
                        ParseHitObject(line, hitObjects);
                        break;
                }
            }

            // Determine BPM from first uninherited timing point
            var mainTiming = timingPoints.FirstOrDefault(tp => !tp.inherited);
            if (mainTiming.beatLength > 0)
                bm.Bpm = (float)(60000.0 / mainTiming.beatLength);
            else
                bm.Bpm = 120;

            // Convert hit objects to 4-lane notes
            if (mode == 3) // osu!mania
                bm.Notes = ConvertManiaObjects(hitObjects, circleSize);
            else // standard, taiko, catch -> convert to 4 lanes
                bm.Notes = ConvertStandardObjects(hitObjects);

            return bm;
        }

        static void ParseTimingPoint(string line, List<(double offset, double beatLength, bool inherited)> list)
        {
            var parts = line.Split(',');
            if (parts.Length < 2) return;
            if (!double.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double offset)) return;
            if (!double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double beatLength)) return;
            bool inherited = parts.Length > 6 && parts[6].Trim() == "0";
            list.Add((offset, beatLength, inherited));
        }

        static void ParseHitObject(string line, List<OsuHitObject> list)
        {
            var parts = line.Split(',');
            if (parts.Length < 4) return;

            var obj = new OsuHitObject();
            if (!int.TryParse(parts[0], out obj.X)) return;
            if (!int.TryParse(parts[1], out obj.Y)) return;
            if (!double.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out obj.Time)) return;
            if (!int.TryParse(parts[3], out obj.Type)) return;

            // Check for hold note end time (mania) - in extras after ':'
            if ((obj.Type & 128) != 0 && parts.Length >= 6)
            {
                // Format: x,y,time,type,hitSound,endTime:extras
                var endParts = parts[5].Split(':');
                if (endParts.Length > 0)
                    double.TryParse(endParts[0], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out obj.EndTime);
            }

            list.Add(obj);
        }

        static List<Note> ConvertManiaObjects(List<OsuHitObject> objects, int keyCount)
        {
            var notes = new List<Note>();
            if (keyCount <= 0) keyCount = 4;

            foreach (var obj in objects)
            {
                // osu!mania column = floor(x * keyCount / 512)
                int col = (int)(obj.X * keyCount / 512.0);
                col = Math.Clamp(col, 0, keyCount - 1);

                // Map to 4 lanes
                int lane;
                if (keyCount <= 4)
                    lane = col;
                else
                    lane = (int)((float)col / keyCount * 4);
                lane = Math.Clamp(lane, 0, 3);

                float timeSec = (float)(obj.Time / 1000.0);
                notes.Add(new Note { Time = (float)Math.Round(timeSec, 3), Column = lane });
            }

            notes.Sort((a, b) => a.Time.CompareTo(b.Time));
            return notes;
        }

        static List<Note> ConvertStandardObjects(List<OsuHitObject> objects)
        {
            var notes = new List<Note>();

            foreach (var obj in objects)
            {
                // Map x position (0-512) to 4 lanes
                int lane = (int)(obj.X / 128.0);
                lane = Math.Clamp(lane, 0, 3);

                float timeSec = (float)(obj.Time / 1000.0);
                notes.Add(new Note { Time = (float)Math.Round(timeSec, 3), Column = lane });
            }

            notes.Sort((a, b) => a.Time.CompareTo(b.Time));
            return notes;
        }

        /// <summary>Convert any audio format (MP3, OGG, WAV, etc.) to 16-bit PCM WAV.</summary>
        public static void ConvertToWavPublic(string inputPath, string outputPath) => ConvertToWav(inputPath, outputPath);

        static void ConvertToWav(string inputPath, string outputPath)
        {
            string ext = Path.GetExtension(inputPath).ToLowerInvariant();
            WaveStream reader;
            if (ext == ".ogg")
                reader = new VorbisWaveReader(inputPath);
            else if (ext == ".mp3")
                reader = new Mp3FileReader(inputPath);
            else if (ext == ".wav")
            {
                File.Copy(inputPath, outputPath, false);
                return;
            }
            else
                reader = new AudioFileReader(inputPath);

            using (reader)
            {
                // Convert to 16-bit PCM WAV (required by MonoGame SoundEffect.FromStream)
                var targetFormat = new WaveFormat(44100, 16, reader.WaveFormat.Channels);
                if (reader.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat ||
                    reader.WaveFormat.BitsPerSample != 16 ||
                    reader.WaveFormat.SampleRate != 44100)
                {
                    try
                    {
                        using var resampler = new MediaFoundationResampler(reader, targetFormat);
                        WaveFileWriter.CreateWaveFile(outputPath, resampler);
                    }
                    catch
                    {
                        // Fallback: manual sample conversion
                        reader.Position = 0;
                        var sampleProvider = reader.ToSampleProvider();
                        WaveFileWriter.CreateWaveFile16(outputPath, sampleProvider);
                    }
                }
                else
                {
                    WaveFileWriter.CreateWaveFile(outputPath, reader);
                }
            }
        }

        struct OsuHitObject
        {
            public int X, Y;
            public double Time;
            public int Type;
            public double EndTime;
        }
    }
}
