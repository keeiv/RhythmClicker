using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClickerGame
{
    // Simple runtime text renderer that caches generated textures.
    // Uses System.Drawing to render text into a bitmap and loads it into a Texture2D.
    public class TextRenderer : IDisposable
    {
        readonly GraphicsDevice _graphicsDevice;
        readonly Dictionary<string, Texture2D> _cache = new();

        public TextRenderer(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
        }

        string Key(string text, string fontName, int size, System.Drawing.Color color)
            => $"{fontName}|{size}|{color.ToArgb():X8}|{text}";

        public Texture2D GetTexture(string text, string fontName, int size, Microsoft.Xna.Framework.Color color)
        {
            var sysColor = System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
            var key = Key(text, fontName, size, sysColor);
            if (_cache.TryGetValue(key, out var tex)) return tex;

            using var bmp = new Bitmap(1, 1);
            using (var g = Graphics.FromImage(bmp))
            {
                var f = new Font(fontName, size, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
                var sz = g.MeasureString(text, f);
                int w = Math.Max(1, (int)Math.Ceiling(sz.Width));
                int h = Math.Max(1, (int)Math.Ceiling(sz.Height));
                using var real = new Bitmap(w, h);
                using var gr = Graphics.FromImage(real);
                gr.Clear(System.Drawing.Color.Transparent);
                gr.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                using var brush = new SolidBrush(sysColor);
                gr.DrawString(text, f, brush, 0f, 0f);
                using var ms = new MemoryStream();
                real.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Seek(0, SeekOrigin.Begin);
                tex = Texture2D.FromStream(_graphicsDevice, ms);
            }

            _cache[key] = tex;
            return tex;
        }

        // Ensure a texture is generated and cached for the given text.
        public void Precache(string text, string fontName, int size, Microsoft.Xna.Framework.Color color)
        {
            GetTexture(text, fontName, size, color);
        }

        public void Dispose()
        {
            foreach (var t in _cache.Values) t.Dispose();
            _cache.Clear();
        }
    }
}
