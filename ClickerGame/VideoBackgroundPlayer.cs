using System;
using System.IO;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClickerGame
{
    /// <summary>
    /// Decodes video frames using FFMediaToolkit and provides them as Texture2D for background rendering.
    /// Falls back gracefully if FFmpeg DLLs are not available.
    /// </summary>
    public class VideoBackgroundPlayer : IDisposable
    {
        readonly GraphicsDevice _graphics;
        FFMediaToolkit.Decoding.MediaFile? _mediaFile;
        Texture2D? _currentFrame;
        Texture2D? _nextFrame;
        readonly object _frameLock = new();
        Thread? _decoderThread;
        volatile bool _running;
        volatile float _targetTime;
        double _videoDuration;
        bool _ffmpegAvailable;
        int _videoWidth, _videoHeight;

        public bool IsPlaying { get; private set; }
        public bool HasVideo => _mediaFile != null;
        public Texture2D? CurrentFrame
        {
            get { lock (_frameLock) return _currentFrame; }
        }

        public VideoBackgroundPlayer(GraphicsDevice graphics)
        {
            _graphics = graphics;
            TryInitFFmpeg();
        }

        void TryInitFFmpeg()
        {
            try
            {
                string? ffmpegDir = null;

                // 1. Check NATIVE_DLL_SEARCH_DIRECTORIES (works for single-file publish)
                var nativeDirs = AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") as string;
                if (!string.IsNullOrEmpty(nativeDirs))
                {
                    foreach (var dir in nativeDirs.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (Directory.Exists(dir) && File.Exists(Path.Combine(dir, "avcodec-61.dll")))
                        { ffmpegDir = dir; break; }
                    }
                }

                // 2. Check app directory and ffmpeg subfolder
                if (ffmpegDir == null)
                {
                    string appDir = AppDomain.CurrentDomain.BaseDirectory;
                    foreach (var candidate in new[] { appDir, Path.Combine(appDir, "ffmpeg") })
                    {
                        if (Directory.Exists(candidate) && (
                            File.Exists(Path.Combine(candidate, "avcodec-61.dll")) ||
                            File.Exists(Path.Combine(candidate, "avcodec-60.dll")) ||
                            File.Exists(Path.Combine(candidate, "avcodec-59.dll"))))
                        { ffmpegDir = candidate; break; }
                    }
                }

                // 3. Check PATH
                if (ffmpegDir == null)
                {
                    string? pathEnv = Environment.GetEnvironmentVariable("PATH");
                    if (pathEnv != null)
                    {
                        foreach (var dir in pathEnv.Split(';'))
                        {
                            if (Directory.Exists(dir) && (
                                File.Exists(Path.Combine(dir, "avcodec-61.dll")) ||
                                File.Exists(Path.Combine(dir, "avcodec-60.dll"))))
                            { ffmpegDir = dir; break; }
                        }
                    }
                }

                if (ffmpegDir != null)
                {
                    FFMediaToolkit.FFmpegLoader.FFmpegPath = ffmpegDir;
                    _ffmpegAvailable = true;
                }
            }
            catch
            {
                _ffmpegAvailable = false;
            }
        }

        public bool Open(string videoPath)
        {
            if (!_ffmpegAvailable || !File.Exists(videoPath)) return false;

            try
            {
                // Explicitly request BGR24 output for consistent pixel format across all containers (MP4, AVI, MKV, etc.)
                var options = new FFMediaToolkit.Decoding.MediaOptions
                {
                    VideoPixelFormat = FFMediaToolkit.Graphics.ImagePixelFormat.Bgr24
                };
                _mediaFile = FFMediaToolkit.Decoding.MediaFile.Open(videoPath, options);
                if (!_mediaFile.HasVideo) { _mediaFile.Dispose(); _mediaFile = null; return false; }

                var info = _mediaFile.Video.Info;
                _videoWidth = info.FrameSize.Width;
                _videoHeight = info.FrameSize.Height;
                _videoDuration = info.Duration.TotalSeconds;
                return true;
            }
            catch
            {
                _mediaFile = null;
                return false;
            }
        }

        public void Play()
        {
            if (_mediaFile == null) return;
            IsPlaying = true;
            _running = true;
            _decoderThread = new Thread(DecoderLoop) { IsBackground = true, Name = "VideoDecoder" };
            _decoderThread.Start();
        }

        public void UpdateTime(float seconds)
        {
            _targetTime = seconds;

            // Swap in the decoded frame if available
            lock (_frameLock)
            {
                if (_nextFrame != null)
                {
                    var old = _currentFrame;
                    _currentFrame = _nextFrame;
                    _nextFrame = null;
                    // We don't dispose old immediately because it might still be drawn
                }
            }
        }

        void DecoderLoop()
        {
            float lastDecodedTime = -1f;
            while (_running && _mediaFile != null)
            {
                float target = _targetTime;
                // Only decode if we need a new frame (roughly every ~33ms = 30fps)
                if (Math.Abs(target - lastDecodedTime) < 0.025f)
                {
                    Thread.Sleep(8);
                    continue;
                }

                try
                {
                    var ts = TimeSpan.FromSeconds(Math.Max(0, Math.Min(target, _videoDuration - 0.1)));

                    // Seek to the nearest position
                    if (Math.Abs(target - lastDecodedTime) > 1.0f || target < lastDecodedTime)
                        _mediaFile.Video.TryGetFrame(ts, out _); // seek

                    if (_mediaFile.Video.TryGetNextFrame(out var frame))
                    {
                        // Convert ImageData to Texture2D pixel data
                        int w = frame.ImageSize.Width;
                        int h = frame.ImageSize.Height;
                        var data = frame.Data;

                        // Scale down for performance if video is large
                        int targetW = Math.Min(w, 640);
                        int targetH = (int)((float)targetW / w * h);

                        var pixels = new Color[targetW * targetH];
                        float scaleX = (float)w / targetW;
                        float scaleY = (float)h / targetH;

                        // frame.Data is BGR24 format
                        int stride = frame.Stride;
                        for (int y = 0; y < targetH; y++)
                        {
                            int srcY = (int)(y * scaleY);
                            for (int x = 0; x < targetW; x++)
                            {
                                int srcX = (int)(x * scaleX);
                                int srcIdx = srcY * stride + srcX * 3;
                                if (srcIdx + 2 < data.Length)
                                {
                                    byte b = data[srcIdx];
                                    byte g = data[srcIdx + 1];
                                    byte r = data[srcIdx + 2];
                                    pixels[y * targetW + x] = new Color(r, g, b, (byte)180); // semi-transparent
                                }
                            }
                        }

                        // Create texture on decoder thread, set data
                        var tex = new Texture2D(_graphics, targetW, targetH);
                        tex.SetData(pixels);

                        lock (_frameLock)
                        {
                            _nextFrame?.Dispose();
                            _nextFrame = tex;
                        }
                        lastDecodedTime = target;
                    }
                }
                catch
                {
                    // Frame decode error, skip
                }
                Thread.Sleep(16); // ~60fps max decode rate
            }
        }

        public void Stop()
        {
            IsPlaying = false;
            _running = false;
            _decoderThread?.Join(500);
            _decoderThread = null;
        }

        public void Dispose()
        {
            Stop();
            _mediaFile?.Dispose();
            _mediaFile = null;
            lock (_frameLock)
            {
                _currentFrame?.Dispose();
                _currentFrame = null;
                _nextFrame?.Dispose();
                _nextFrame = null;
            }
        }
    }
}
