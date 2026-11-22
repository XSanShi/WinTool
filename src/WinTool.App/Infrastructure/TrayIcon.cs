using System.IO;

namespace WinTool.App.Infrastructure;

/// <summary>
/// 运行时生成托盘图标（不额外塞 .ico 资源文件）。
/// 注意：H.NotifyIcon 的 IconSource 只接受能转成 HICON 的位图源，传 DrawingImage 会抛
/// "ImageSource type: System.Windows.Media.DrawingImage is not supported"，
/// 所以这里直接拼一份 32×32 的 .ico 字节交给 System.Drawing.Icon。
/// </summary>
internal static class TrayIcon
{
    private const int Size = 32;

    public static System.Drawing.Icon Create()
    {
        // Icon(Stream) 会自己把数据读走，构造完即可丢掉流。
        using var stream = new MemoryStream(BuildIcoBytes());
        return new System.Drawing.Icon(stream);
    }

    private static byte[] BuildIcoBytes()
    {
        const int headerSize = 40;
        var pixelBytes = Size * Size * 4;
        var maskBytes = ((Size + 31) / 32) * 4 * Size; // 1bpp AND mask，行对齐到 4 字节
        var imageBytes = headerSize + pixelBytes + maskBytes;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // ICONDIR
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)1);

        // ICONDIRENTRY
        writer.Write((byte)Size);
        writer.Write((byte)Size);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(imageBytes);
        writer.Write(6 + 16);

        // BITMAPINFOHEADER（高度写两倍：颜色位图 + AND 掩码）
        writer.Write(headerSize);
        writer.Write(Size);
        writer.Write(Size * 2);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(0);
        writer.Write(pixelBytes + maskBytes);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);

        writer.Write(BuildPixels());
        writer.Write(new byte[maskBytes]);
        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>BGRA、自下而上：圆角方块 + 白色对勾，和界面主色一致。</summary>
    private static byte[] BuildPixels()
    {
        var pixels = new byte[Size * Size * 4];

        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var cx = x + 0.5;
                var cy = y + 0.5;
                var inside = InRoundedRect(cx, cy, 0.5, 0.5, Size - 0.5, Size - 0.5, 7.5);
                if (!inside)
                {
                    continue;
                }

                var onCheck = DistanceToSegment(cx, cy, 9.0, 16.5, 14.0, 21.5) <= 1.9
                              || DistanceToSegment(cx, cy, 14.0, 21.5, 23.0, 11.0) <= 1.9;

                var offset = ((Size - 1 - y) * Size + x) * 4;
                if (onCheck)
                {
                    pixels[offset] = 255;     // B
                    pixels[offset + 1] = 255; // G
                    pixels[offset + 2] = 255; // R
                    pixels[offset + 3] = 255; // A
                }
                else
                {
                    pixels[offset] = 0xEB;     // B  #2F6FEB
                    pixels[offset + 1] = 0x6F; // G
                    pixels[offset + 2] = 0x2F; // R
                    pixels[offset + 3] = 255;  // A
                }
            }
        }

        return pixels;
    }

    private static bool InRoundedRect(double x, double y, double left, double top, double right, double bottom, double radius)
    {
        if (x < left || x > right || y < top || y > bottom)
        {
            return false;
        }

        var innerLeft = left + radius;
        var innerRight = right - radius;
        var innerTop = top + radius;
        var innerBottom = bottom - radius;

        var cx = Math.Clamp(x, innerLeft, innerRight);
        var cy = Math.Clamp(y, innerTop, innerBottom);
        var dx = x - cx;
        var dy = y - cy;
        return (dx * dx) + (dy * dy) <= radius * radius;
    }

    private static double DistanceToSegment(double px, double py, double x1, double y1, double x2, double y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        var lengthSquared = (dx * dx) + (dy * dy);
        if (lengthSquared <= double.Epsilon)
        {
            return Math.Sqrt(((px - x1) * (px - x1)) + ((py - y1) * (py - y1)));
        }

        var t = Math.Clamp((((px - x1) * dx) + ((py - y1) * dy)) / lengthSquared, 0, 1);
        var projX = x1 + (t * dx);
        var projY = y1 + (t * dy);
        return Math.Sqrt(((px - projX) * (px - projX)) + ((py - projY) * (py - projY)));
    }
}
