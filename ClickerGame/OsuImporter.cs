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
                string? videoFileCopied = null;
                string? bgImageCopied = null;

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

                    // Copy video file once
                    if (videoFileCopied == null && !string.IsNullOrEmpty(bm.VideoFile))
                    {
                        string videoSrc = Path.Combine(tempDir, bm.VideoFile);
                        if (File.Exists(videoSrc))
                        {
                            string prefix = !string.IsNullOrEmpty(songId) ? songId + "_" : "";
                            string videoName = prefix + Path.GetFileName(bm.VideoFile);
                            string videoDest = Path.Combine(assetsDir, videoName);
                            if (!File.Exists(videoDest))
                                File.Copy(videoSrc, videoDest, false);
                            videoFileCopied = videoName;
                        }
                    }

                    // Copy background image once
                    if (bgImageCopied == null && !string.IsNullOrEmpty(bm.BackgroundImage))
                    {
                        string bgSrc = Path.Combine(tempDir, bm.BackgroundImage);
                        if (File.Exists(bgSrc))
                        {
                            string prefix = !string.IsNullOrEmpty(songId) ? songId + "_" : "";
                            string bgName = prefix + Path.GetFileName(bm.BackgroundImage);
                            string bgDest = Path.Combine(assetsDir, bgName);
                            if (!File.Exists(bgDest))
                                File.Copy(bgSrc, bgDest, false);
                            bgImageCopied = bgName;
                        }
                    }

                    // Update file paths for all beatmaps
                    if (audioFileCopied != null) bm.AudioFile = audioFileCopied;
                    if (videoFileCopied != null) bm.VideoFile = videoFileCopied;
                    if (bgImageCopied != null) bm.BackgroundImage = bgImageCopied;

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

                    case "Events":
                        ParseEventLine(line, bm);
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

            // Convert hit objects to 4-lane notes based on game mode
            bm.Notes = mode switch
            {
                0 => ConvertStandardObjects(hitObjects, timingPoints),
                1 => ConvertTaikoObjects(hitObjects, timingPoints),
                2 => ConvertCatchObjects(hitObjects, timingPoints),
                3 => ConvertManiaObjects(hitObjects, circleSize),
                _ => ConvertStandardObjects(hitObjects, timingPoints)
            };

            // Auto-detect break periods from note gaps (if none parsed from [Events])
            AutoDetectBreaks(bm);

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

        /// <summary>Parse [Events] section for video, background image, and break periods.</summary>
        static void ParseEventLine(string line, Beatmap bm)
        {
            // Break period: "2,startTime,endTime"
            if (line.StartsWith("2,"))
            {
                var parts = line.Split(',');
                if (parts.Length >= 3 &&
                    double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double startMs) &&
                    double.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double endMs))
                {
                    bm.Breaks.Add(new BreakPeriod
                    {
                        StartTime = (float)(startMs / 1000.0),
                        EndTime = (float)(endMs / 1000.0)
                    });
                }
                return;
            }
            // Video: "Video,offset,"filename"" or "1,offset,"filename""
            if (line.StartsWith("Video,", StringComparison.OrdinalIgnoreCase) || line.StartsWith("1,"))
            {
                string filename = ExtractQuotedFilename(line);
                if (!string.IsNullOrEmpty(filename))
                    bm.VideoFile = filename;
            }
            // Background image: "0,0,"filename.jpg",0,0"
            else if (line.StartsWith("0,0,"))
            {
                string filename = ExtractQuotedFilename(line);
                if (!string.IsNullOrEmpty(filename))
                {
                    string ext = Path.GetExtension(filename).ToLowerInvariant();
                    if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp")
                        bm.BackgroundImage = filename;
                }
            }
        }

        static string ExtractQuotedFilename(string line)
        {
            int q1 = line.IndexOf('"');
            if (q1 < 0) return "";
            int q2 = line.IndexOf('"', q1 + 1);
            if (q2 <= q1) return "";
            return line.Substring(q1 + 1, q2 - q1 - 1).Trim();
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

            // hitSound is at index 4
            if (parts.Length >= 5)
                int.TryParse(parts[4], out obj.HitSound);

            // Mania hold note: endTime in extras after ':'
            if (obj.IsManiaHold && parts.Length >= 6)
            {
                var endParts = parts[5].Split(':');
                if (endParts.Length > 0)
                    double.TryParse(endParts[0], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out obj.EndTime);
            }

            // Slider: objectParams = curveType|...,slides,length
            if (obj.IsSlider && parts.Length >= 8)
            {
                // parts[5] = curveType|curvePoints, parts[6] = slides, parts[7] = length
                int.TryParse(parts[6], out obj.RepeatCount);
                double.TryParse(parts[7], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out obj.SliderLength);
            }

            // Spinner: endTime at parts[5]
            if (obj.IsSpinner && parts.Length >= 6)
            {
                double.TryParse(parts[5], System.Globalization.NumberStyles.Float,
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

        /// <summary>
        /// osu! standard (mode 0): circles, sliders, spinners.
        /// Circles → single note at x-based lane.
        /// Sliders → note at start + additional notes along slider ticks.
        /// Spinners → burst of notes spread across lanes.
        /// </summary>
        static List<Note> ConvertStandardObjects(List<OsuHitObject> objects,
            List<(double offset, double beatLength, bool inherited)> timingPoints)
        {
            var notes = new List<Note>();

            foreach (var obj in objects)
            {
                float timeSec = (float)(obj.Time / 1000.0);
                int lane = Math.Clamp((int)(obj.X / 128.0), 0, 3);

                if (obj.IsSpinner)
                {
                    // Spinner: generate burst notes across all 4 lanes
                    double beatLen = GetBeatLength(timingPoints, obj.Time);
                    double interval = Math.Max(beatLen / 4.0, 80); // quarter-beat ticks
                    double endMs = obj.EndTime > obj.Time ? obj.EndTime : obj.Time + 1000;
                    int col = 0;
                    for (double t = obj.Time; t <= endMs; t += interval)
                    {
                        notes.Add(new Note { Time = (float)Math.Round(t / 1000.0, 3), Column = col % 4 });
                        col++;
                    }
                }
                else if (obj.IsSlider)
                {
                    // Slider head
                    notes.Add(new Note { Time = (float)Math.Round(timeSec, 3), Column = lane });

                    // Generate slider tick notes
                    double beatLen = GetBeatLength(timingPoints, obj.Time);
                    double sliderVelocity = GetSliderVelocity(timingPoints, obj.Time);
                    double pixelsPerBeat = sliderVelocity * 100.0; // base SV * 100
                    double sliderDuration = obj.SliderLength / pixelsPerBeat * beatLen;
                    double totalDuration = sliderDuration * Math.Max(obj.RepeatCount, 1);

                    // Add notes at each repeat point
                    int repeats = Math.Max(obj.RepeatCount, 1);
                    for (int r = 1; r <= repeats; r++)
                    {
                        double tickTime = obj.Time + sliderDuration * r;
                        int tickLane = (r % 2 == 0) ? lane : Math.Clamp(lane + 1, 0, 3);
                        notes.Add(new Note { Time = (float)Math.Round(tickTime / 1000.0, 3), Column = tickLane });
                    }
                }
                else
                {
                    // Circle: single note
                    notes.Add(new Note { Time = (float)Math.Round(timeSec, 3), Column = lane });
                }
            }

            notes.Sort((a, b) => a.Time.CompareTo(b.Time));
            return notes;
        }

        /// <summary>
        /// osu!taiko (mode 1): don (red) and kat (blue) drum hits.
        /// Don (centre) → lanes 1,2. Kat (rim) → lanes 0,3.
        /// Big notes → two simultaneous notes.
        /// Drumrolls → tick notes. Dendens → spinner burst.
        /// </summary>
        static List<Note> ConvertTaikoObjects(List<OsuHitObject> objects,
            List<(double offset, double beatLength, bool inherited)> timingPoints)
        {
            var notes = new List<Note>();
            var rng = new Random(42);

            foreach (var obj in objects)
            {
                float timeSec = (float)(obj.Time / 1000.0);
                bool isRim = (obj.HitSound & 2) != 0 || (obj.HitSound & 8) != 0; // whistle or clap = kat/rim
                bool isBig = (obj.HitSound & 4) != 0; // finish = big note

                if (obj.IsSpinner)
                {
                    // Denden (spinner): rapid alternating hits
                    double beatLen = GetBeatLength(timingPoints, obj.Time);
                    double interval = Math.Max(beatLen / 4.0, 100);
                    double endMs = obj.EndTime > obj.Time ? obj.EndTime : obj.Time + 1000;
                    bool alt = false;
                    for (double t = obj.Time; t <= endMs; t += interval)
                    {
                        int col = alt ? 0 : 3; // alternate rim lanes
                        notes.Add(new Note { Time = (float)Math.Round(t / 1000.0, 3), Column = col });
                        alt = !alt;
                    }
                }
                else if (obj.IsSlider)
                {
                    // Drumroll: tick notes at beat subdivisions
                    double beatLen = GetBeatLength(timingPoints, obj.Time);
                    double sliderVelocity = GetSliderVelocity(timingPoints, obj.Time);
                    double pixelsPerBeat = sliderVelocity * 100.0;
                    double sliderDuration = obj.SliderLength / pixelsPerBeat * beatLen;
                    double totalDuration = sliderDuration * Math.Max(obj.RepeatCount, 1);
                    double interval = Math.Max(beatLen / 4.0, 80);

                    for (double t = obj.Time; t <= obj.Time + totalDuration; t += interval)
                    {
                        int col = rng.Next(1, 3); // centre lanes 1-2
                        notes.Add(new Note { Time = (float)Math.Round(t / 1000.0, 3), Column = col });
                    }
                }
                else
                {
                    // Regular hit
                    if (isRim)
                    {
                        int col = rng.Next(0, 2) == 0 ? 0 : 3; // rim = outer lanes
                        notes.Add(new Note { Time = (float)Math.Round(timeSec, 3), Column = col });
                        if (isBig) // big kat: add second lane too
                            notes.Add(new Note { Time = (float)Math.Round(timeSec, 3), Column = col == 0 ? 3 : 0 });
                    }
                    else
                    {
                        int col = rng.Next(1, 3); // don = centre lanes 1-2
                        notes.Add(new Note { Time = (float)Math.Round(timeSec, 3), Column = col });
                        if (isBig) // big don: add both centre lanes
                            notes.Add(new Note { Time = (float)Math.Round(timeSec, 3), Column = col == 1 ? 2 : 1 });
                    }
                }
            }

            notes.Sort((a, b) => a.Time.CompareTo(b.Time));
            return notes;
        }

        /// <summary>
        /// osu!catch (mode 2): fruits at horizontal positions.
        /// Fruits → lane based on x. Juice streams → notes along slider path.
        /// Banana showers → random burst notes.
        /// </summary>
        static List<Note> ConvertCatchObjects(List<OsuHitObject> objects,
            List<(double offset, double beatLength, bool inherited)> timingPoints)
        {
            var notes = new List<Note>();
            var rng = new Random(42);

            foreach (var obj in objects)
            {
                float timeSec = (float)(obj.Time / 1000.0);
                int lane = Math.Clamp((int)(obj.X / 128.0), 0, 3);

                if (obj.IsSpinner)
                {
                    // Banana shower: random fruits across all lanes
                    double beatLen = GetBeatLength(timingPoints, obj.Time);
                    double interval = Math.Max(beatLen / 2.0, 120);
                    double endMs = obj.EndTime > obj.Time ? obj.EndTime : obj.Time + 1000;
                    for (double t = obj.Time; t <= endMs; t += interval)
                    {
                        notes.Add(new Note { Time = (float)Math.Round(t / 1000.0, 3), Column = rng.Next(0, 4) });
                    }
                }
                else if (obj.IsSlider)
                {
                    // Juice stream: note at start + droplets along duration
                    notes.Add(new Note { Time = (float)Math.Round(timeSec, 3), Column = lane });

                    double beatLen = GetBeatLength(timingPoints, obj.Time);
                    double sliderVelocity = GetSliderVelocity(timingPoints, obj.Time);
                    double pixelsPerBeat = sliderVelocity * 100.0;
                    double sliderDuration = obj.SliderLength / pixelsPerBeat * beatLen;
                    double totalDuration = sliderDuration * Math.Max(obj.RepeatCount, 1);
                    double interval = Math.Max(beatLen / 2.0, 100);

                    int prevLane = lane;
                    for (double t = obj.Time + interval; t <= obj.Time + totalDuration; t += interval)
                    {
                        // Droplets drift left/right
                        int drift = rng.Next(-1, 2);
                        int newLane = Math.Clamp(prevLane + drift, 0, 3);
                        notes.Add(new Note { Time = (float)Math.Round(t / 1000.0, 3), Column = newLane });
                        prevLane = newLane;
                    }
                }
                else
                {
                    // Fruit: single note
                    notes.Add(new Note { Time = (float)Math.Round(timeSec, 3), Column = lane });
                }
            }

            notes.Sort((a, b) => a.Time.CompareTo(b.Time));
            return notes;
        }

        /// <summary>Get the beat length (ms per beat) at a given time from uninherited timing points.</summary>
        static double GetBeatLength(List<(double offset, double beatLength, bool inherited)> timingPoints, double timeMs)
        {
            double result = 500; // default 120 BPM
            foreach (var tp in timingPoints)
            {
                if (tp.inherited) continue; // skip inherited (SV) points
                if (tp.offset <= timeMs) result = tp.beatLength;
                else break;
            }
            return Math.Max(result, 1);
        }

        /// <summary>Get the slider velocity multiplier at a given time from inherited timing points.</summary>
        static double GetSliderVelocity(List<(double offset, double beatLength, bool inherited)> timingPoints, double timeMs)
        {
            double sv = 1.0;
            foreach (var tp in timingPoints)
            {
                if (tp.offset > timeMs) break;
                if (tp.inherited && tp.beatLength < 0)
                    sv = -100.0 / tp.beatLength; // inherited point: SV = -100/beatLength
                else if (!tp.inherited)
                    sv = 1.0; // reset SV on uninherited point
            }
            return Math.Max(sv, 0.1);
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
            public int HitSound;
            public double EndTime;     // mania hold end, spinner end
            public int RepeatCount;    // slider repeats
            public double SliderLength; // slider pixel length

            public bool IsCircle => (Type & 1) != 0;
            public bool IsSlider => (Type & 2) != 0;
            public bool IsSpinner => (Type & 8) != 0;
            public bool IsManiaHold => (Type & 128) != 0;
        }

        /// <summary>
        /// Auto-detect break periods from gaps between notes.
        /// A gap of 3+ seconds with no notes is treated as a break.
        /// Merged with any explicitly parsed breaks from [Events].
        /// </summary>
        static void AutoDetectBreaks(Beatmap bm)
        {
            const float MinBreakGap = 3.0f; // minimum gap in seconds to be considered a break
            const float BreakPadding = 0.5f; // padding before/after the gap

            if (bm.Notes.Count < 2) return;

            var sorted = bm.Notes.OrderBy(n => n.Time).ToList();

            for (int i = 1; i < sorted.Count; i++)
            {
                float gap = sorted[i].Time - sorted[i - 1].Time;
                if (gap >= MinBreakGap)
                {
                    float bStart = sorted[i - 1].Time + BreakPadding;
                    float bEnd = sorted[i].Time - BreakPadding;
                    if (bEnd - bStart < 1.5f) continue; // too short to display

                    // Check if this gap overlaps with an existing explicit break
                    bool overlaps = bm.Breaks.Any(b =>
                        !(bEnd < b.StartTime || bStart > b.EndTime));
                    if (!overlaps)
                        bm.Breaks.Add(new BreakPeriod { StartTime = bStart, EndTime = bEnd });
                }
            }

            // Sort breaks by start time
            bm.Breaks.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
        }
    }
}
