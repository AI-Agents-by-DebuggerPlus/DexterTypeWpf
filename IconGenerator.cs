using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace DexterType;

public static class IconGenerator
{
    public static void Generate(string path)
    {
        using var bmp = new Bitmap(256, 256);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // Background circle
        using var brush = new SolidBrush(Color.FromArgb(255, 44, 62, 80));
        g.FillEllipse(brush, 10, 10, 236, 236);

        // Keyboard icon simplified
        using var keyBrush = new SolidBrush(Color.FromArgb(255, 181, 162, 216));
        g.FillRectangle(keyBrush, 60, 80, 40, 40);
        g.FillRectangle(keyBrush, 108, 80, 40, 40);
        g.FillRectangle(keyBrush, 156, 80, 40, 40);
        
        g.FillRectangle(keyBrush, 60, 130, 136, 40);

        // Letter D
        using (var font = new Font("Consolas", 80, FontStyle.Bold))
        {
            g.DrawString("D", font, Brushes.White, 85, 80);
        }

        bmp.Save(path, ImageFormat.Png);
    }
}
