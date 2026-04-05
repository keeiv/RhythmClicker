using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

string outDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "ClickerGame", "Icons");
Directory.CreateDirectory(outDir);

var fileTypes = new (string ext, string label, Color bg, Color accent, string symbol)[]
{
    (".rcm", "RCM", Color.FromArgb(14, 14, 32), Color.FromArgb(0, 200, 255), "♪"),
    (".rcp", "RCP", Color.FromArgb(14, 14, 32), Color.FromArgb(180, 100, 255), "▶"),
    (".rc",  "RC",  Color.FromArgb(14, 14, 32), Color.FromArgb(255, 80, 80), "⊕"),
};

foreach (var (ext, label, bg, accent, symbol) in fileTypes)
{
    var sizes = new[] { 256, 128, 64, 48, 32, 16 };
    var images = new List<Bitmap>();

    foreach (int size in sizes)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        int r = Math.Max(size / 6, 2);
        using var bgPath = RoundedRect(new Rectangle(0, 0, size, size), r);
        using var bgBrush = new SolidBrush(bg);
        g.FillPath(bgBrush, bgPath);

        // Glow border
        int bw = Math.Max(size / 24, 1);
        using var borderPen = new Pen(Color.FromArgb(200, accent), bw);
        using var borderPath = RoundedRect(new Rectangle(bw / 2, bw / 2, size - bw, size - bw), r);
        g.DrawPath(borderPen, borderPath);

        // Inner glow
        using var glowBrush = new SolidBrush(Color.FromArgb(25, accent));
        int glowH = size / 3;
        g.FillRectangle(glowBrush, bw, bw, size - bw * 2, glowH);

        // Document fold corner (top-right)
        int foldSize = Math.Max(size / 5, 4);
        var foldPoints = new PointF[]
        {
            new(size - foldSize - bw, bw),
            new(size - bw, bw),
            new(size - bw, foldSize + bw),
        };
        using var foldBrush = new SolidBrush(Color.FromArgb(40, accent));
        g.FillPolygon(foldBrush, foldPoints);
        using var foldPen = new Pen(Color.FromArgb(120, accent), Math.Max(bw * 0.7f, 0.5f));
        g.DrawLine(foldPen, foldPoints[0], foldPoints[2]);

        // Symbol
        float symFontSize = size * 0.32f;
        using var symFont = new Font("Segoe UI", Math.Max(symFontSize, 7), FontStyle.Bold, GraphicsUnit.Pixel);
        using var symBrush = new SolidBrush(accent);
        var symMeasure = g.MeasureString(symbol, symFont);
        float symX = (size - symMeasure.Width) / 2;
        float symY = size * 0.15f;
        using var shadowBrush = new SolidBrush(Color.FromArgb(80, 0, 0, 0));
        g.DrawString(symbol, symFont, shadowBrush, symX + 1, symY + 1);
        g.DrawString(symbol, symFont, symBrush, symX, symY);

        // Label pill at bottom
        float labelFontSize = size * 0.2f;
        using var labelFont = new Font("Consolas", Math.Max(labelFontSize, 6), FontStyle.Bold, GraphicsUnit.Pixel);
        using var labelBrush2 = new SolidBrush(Color.White);
        var labelMeasure = g.MeasureString(label, labelFont);
        float labelX = (size - labelMeasure.Width) / 2;
        float labelY = size - labelMeasure.Height - size * 0.1f;

        float px = size * 0.05f;
        float py = size * 0.03f;
        var pillRect = new RectangleF(labelX - px * 2, labelY - py, labelMeasure.Width + px * 4, labelMeasure.Height + py * 2);
        int pillR = Math.Max((int)(size * 0.08f), 2);
        using var pillPath = RoundedRect(Rectangle.Round(pillRect), pillR);
        using var pillBrush = new SolidBrush(Color.FromArgb(200, accent));
        g.FillPath(pillBrush, pillPath);
        using var pillBorderPen = new Pen(Color.FromArgb(255, Color.FromArgb(
            Math.Min(accent.R + 40, 255), Math.Min(accent.G + 40, 255), Math.Min(accent.B + 40, 255))), Math.Max(bw * 0.5f, 0.5f));
        g.DrawPath(pillBorderPen, pillPath);

        g.DrawString(label, labelFont, labelBrush2, labelX, labelY);
        images.Add(bmp);
    }

    string icoPath = Path.Combine(outDir, $"file{ext.Replace(".", "_")}.ico");
    WriteIco(icoPath, images);
    Console.WriteLine($"Created: {icoPath}");
    foreach (var img in images) img.Dispose();
}

Console.WriteLine("All icons generated!");

static GraphicsPath RoundedRect(Rectangle bounds, int radius)
{
    var path = new GraphicsPath();
    int d = radius * 2;
    if (d > bounds.Width) d = bounds.Width;
    if (d > bounds.Height) d = bounds.Height;
    path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
    path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
    path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
    path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
    path.CloseFigure();
    return path;
}

static void WriteIco(string path, List<Bitmap> images)
{
    using var ms = new MemoryStream();
    using var bw2 = new BinaryWriter(ms);
    bw2.Write((short)0);
    bw2.Write((short)1);
    bw2.Write((short)images.Count);

    int dataOffset = 6 + images.Count * 16;
    var pngDatas = new List<byte[]>();
    foreach (var img in images)
    {
        using var pngMs = new MemoryStream();
        img.Save(pngMs, ImageFormat.Png);
        pngDatas.Add(pngMs.ToArray());
    }

    for (int i = 0; i < images.Count; i++)
    {
        int w = images[i].Width >= 256 ? 0 : images[i].Width;
        int h = images[i].Height >= 256 ? 0 : images[i].Height;
        bw2.Write((byte)w);
        bw2.Write((byte)h);
        bw2.Write((byte)0);
        bw2.Write((byte)0);
        bw2.Write((short)1);
        bw2.Write((short)32);
        bw2.Write(pngDatas[i].Length);
        bw2.Write(dataOffset);
        dataOffset += pngDatas[i].Length;
    }
    foreach (var png in pngDatas) bw2.Write(png);
    File.WriteAllBytes(path, ms.ToArray());
}
