using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal static class MakeWinCleanIcon
{
    private static void Main()
    {
        int[] sizes = { 256, 128, 64, 48, 32, 16 };
        List<byte[]> images = new List<byte[]>();

        for (int i = 0; i < sizes.Length; i++)
        {
            images.Add(RenderPng(sizes[i]));
        }

        using (FileStream stream = File.Create("WinClean.ico"))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write((short)0);
            writer.Write((short)1);
            writer.Write((short)sizes.Length);

            int offset = 6 + (16 * sizes.Length);
            for (int i = 0; i < sizes.Length; i++)
            {
                writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((short)1);
                writer.Write((short)32);
                writer.Write(images[i].Length);
                writer.Write(offset);
                offset += images[i].Length;
            }

            for (int i = 0; i < images.Count; i++)
            {
                writer.Write(images[i]);
            }
        }
    }

    private static byte[] RenderPng(int size)
    {
        using (Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            float scale = size / 256f;
            RectangleF tile = new RectangleF(13 * scale, 13 * scale, 230 * scale, 230 * scale);
            using (GraphicsPath tilePath = Rounded(tile, 52 * scale))
            using (LinearGradientBrush tileBrush = new LinearGradientBrush(tile, Color.FromArgb(10, 7, 5), Color.FromArgb(31, 14, 6), 45f))
            using (Pen border = new Pen(Color.FromArgb(249, 115, 22), Math.Max(2f, 6f * scale)))
            {
                graphics.FillPath(tileBrush, tilePath);
                graphics.DrawPath(border, tilePath);
            }

            PointF center = new PointF(128 * scale, 128 * scale);
            using (Pen ring = new Pen(Color.FromArgb(255, 154, 48), Math.Max(2f, 10f * scale)))
            {
                ring.StartCap = LineCap.Round;
                ring.EndCap = LineCap.Round;
                graphics.DrawArc(ring, 67 * scale, 70 * scale, 122 * scale, 122 * scale, 35, 290);
            }

            using (Pen nodeLine = new Pen(Color.FromArgb(255, 216, 166), Math.Max(1.4f, 5f * scale)))
            {
                nodeLine.StartCap = LineCap.Round;
                nodeLine.EndCap = LineCap.Round;
                graphics.DrawLine(nodeLine, 128 * scale, 69 * scale, 183 * scale, 151 * scale);
                graphics.DrawLine(nodeLine, 128 * scale, 69 * scale, 73 * scale, 151 * scale);
            }

            FillCircle(graphics, center.X, 66 * scale, 14 * scale, Color.FromArgb(255, 246, 235));
            FillCircle(graphics, 73 * scale, 151 * scale, 13 * scale, Color.FromArgb(249, 115, 22));
            FillCircle(graphics, 183 * scale, 151 * scale, 13 * scale, Color.FromArgb(249, 115, 22));

            using (Pen sparkle = new Pen(Color.FromArgb(255, 246, 235), Math.Max(1.3f, 5f * scale)))
            {
                sparkle.StartCap = LineCap.Round;
                sparkle.EndCap = LineCap.Round;
                graphics.DrawLine(sparkle, 181 * scale, 54 * scale, 181 * scale, 91 * scale);
                graphics.DrawLine(sparkle, 162 * scale, 73 * scale, 200 * scale, 73 * scale);
                graphics.DrawLine(sparkle, 55 * scale, 185 * scale, 55 * scale, 211 * scale);
                graphics.DrawLine(sparkle, 42 * scale, 198 * scale, 68 * scale, 198 * scale);
            }

            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Png);
                return memory.ToArray();
            }
        }
    }

    private static void FillCircle(Graphics graphics, float x, float y, float radius, Color color)
    {
        using (SolidBrush brush = new SolidBrush(color))
        {
            graphics.FillEllipse(brush, x - radius, y - radius, radius * 2, radius * 2);
        }
    }

    private static GraphicsPath Rounded(RectangleF rect, float radius)
    {
        float diameter = radius * 2;
        GraphicsPath path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
