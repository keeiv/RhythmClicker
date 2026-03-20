using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClickerGame
{
    // Caches textures that are expensive to generate per-frame.
    public class RenderCache
    {
        readonly GraphicsDevice _graphics;
        Texture2D? _background;

        public RenderCache(GraphicsDevice graphics)
        {
            _graphics = graphics;
        }

        public Texture2D GetBackground(int width, int height)
        {
            if (_background != null && _background.Width == width && _background.Height == height) return _background;
            _background?.Dispose();
            _background = CreateGradientTexture(width, height);
            return _background;
        }

        Texture2D CreateGradientTexture(int width, int height)
        {
            var tex = new Texture2D(_graphics, width, height);
            Color[] data = new Color[width * height];
            var cTop = new Color(180, 120, 255);
            var cBottom = new Color(80, 180, 255);
            for (int y = 0; y < height; y++)
            {
                float t = (float)y / Math.Max(1, height - 1);
                byte r = (byte)(cTop.R + (cBottom.R - cTop.R) * t);
                byte g = (byte)(cTop.G + (cBottom.G - cTop.G) * t);
                byte b = (byte)(cTop.B + (cBottom.B - cTop.B) * t);
                var rowColor = new Color(r, g, b);
                for (int x = 0; x < width; x++) data[y * width + x] = rowColor;
            }
            tex.SetData(data);
            return tex;
        }

        public void Dispose()
        {
            _background?.Dispose();
            _background = null;
        }
    }
}
