// dotnet-script: dotnet tool run dotnet-script GenerateIcons.csx
// Or: dotnet run this as a console app
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

string outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ClickerGame", "Icons");
Directory.CreateDirectory(outDir);

// Color schemes for each file type - matching game's neon aesthetic
var fileTypes = new[]
{
    // (extension, label, bgColor, accentColor, iconSymbol)
    (".rcm", "RCM", Color.FromArgb(20, 20, 50), Color.FromArgb(0, 200, 255), "♪"),   // Beatmap - cyan/music
    (".rcp", "RCP", Color.FromArgb(20, 20, 50), Color.FromArgb(180, 100, 255), "▶"),  // Replay - purple/play
    (".rc",  "RC",  Color.FromArgb(20, 20, 50), Color.FromArgb(255, 80, 80), "🔒"),   // Confidential - red/lock
};

foreach (var (ext, label, bg, accent, symbol) in fileTypes)
{
    // Generate multiple sizes for ICO
    var sizes = new[] { 256, 128, 64, 48, 32, 16 };
    var images = new List<Bitmap>();

    foreach (int size in sizes)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        // Rounded rectangle background
        int r = Math.Max(size / 8, 2);
        using var bgBrush = new SolidBrush(bg);
        using var path = RoundedRect(new Rectangle(0, 0, size, size), r);
        g.FillPath(bgBrush, path);

        // Accent border
        int borderW = Math.Max(size / 32, 1);
        using var borderPen = new Pen(Color.FromArgb(180, accent), borderW);
        using var borderPath = RoundedRect(new Rectangle(borderW / 2, borderW / 2, size - borderW, size - borderW), r);
        g.DrawPath(borderPen, borderPath);

        // Top accent stripe
        int stripeH = Math.Max(size / 5, 3);
        using var stripeBrush = new SolidBrush(Color.FromArgb(60, accent));
        g.FillRectangle(stripeBrush, r, borderW, size - r * 2, stripeH);

        // Symbol in center (larger)
        float symSize = size * 0.38f;
        using var symFont = new Font("Segoe UI Emoji", symSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using var symBrush = new SolidBrush(accent);
        var symMeasure = g.MeasureString(symbol, symFont);
        float symX = (size - symMeasure.Width) / 2;
        float symY = size * 0.18f;
        g.DrawString(symbol, symFont, symBrush, symX, symY);

        // Label text at bottom
        float labelSize = size * 0.22f;
        using var labelFont = new Font("Consolas", Math.Max(labelSize, 6), FontStyle.Bold, GraphicsUnit.Pixel);
        using var labelBrush = new SolidBrush(Color.FromArgb(220, Color.White));
        var labelMeasure = g.MeasureString(label, labelFont);
        float labelX = (size - labelMeasure.Width) / 2;
        float labelY = size - labelMeasure.Height - size * 0.08f;

        // Label background pill
        using var pillBrush = new SolidBrush(Color.FromArgb(140, accent));
        float pillPad = size * 0.04f;
        var pillRect = new RectangleF(labelX - pillPad * 2, labelY - pillPad, labelMeasure.Width + pillPad * 4, labelMeasure.Height + pillPad * 2);
        int pillR = Math.Max((int)(size * 0.06f), 2);
        using var pillPath = RoundedRect(Rectangle.Round(pillRect), pillR);
        g.FillPath(pillBrush, pillPath);

        g.DrawString(label, labelFont, labelBrush, labelX, labelY);

        images.Add(bmp);
    }

    // Write ICO file
    string icoPath = Path.Combine(outDir, $"file{ext.Replace(".", "_")}.ico");
    WriteIco(icoPath, images);
    Console.WriteLine($"Created: {icoPath}");

    foreach (var img in images) img.Dispose();
}

Console.WriteLine("Done!");

static GraphicsPath RoundedRect(Rectangle bounds, int radius)
{
    var path = new GraphicsPath();
    int d = radius * 2;
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
    using var bw = new BinaryWriter(ms);

    // ICO header
    bw.Write((short)0);     // reserved
    bw.Write((short)1);     // type: icon
    bw.Write((short)images.Count);

    int dataOffset = 6 + images.Count * 16;
    var pngDatas = new List<byte[]>();

    foreach (var img in images)
    {
        using var pngMs = new MemoryStream();
        img.Save(pngMs, ImageFormat.Png);
        pngDatas.Add(pngMs.ToArray());
    }

    // Directory entries
    for (int i = 0; i < images.Count; i++)
    {
        int w = images[i].Width >= 256 ? 0 : images[i].Width;
        int h = images[i].Height >= 256 ? 0 : images[i].Height;
        bw.Write((byte)w);
        bw.Write((byte)h);
        bw.Write((byte)0);    // palette
        bw.Write((byte)0);    // reserved
        bw.Write((short)1);   // planes
        bw.Write((short)32);  // bpp
        bw.Write(pngDatas[i].Length);
        bw.Write(dataOffset);
        dataOffset += pngDatas[i].Length;
    }

    // Image data
    foreach (var png in pngDatas)
        bw.Write(png);

    File.WriteAllBytes(path, ms.ToArray());
}
