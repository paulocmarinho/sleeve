using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Sleeve;

/// <summary>Decodifica a capa e extrai a cor marcante dela.</summary>
public static class Palette
{
    public static BitmapSource? Decode(byte[]? data)
    {
        if (data is null || data.Length == 0) return null;
        try
        {
            var bmp = new BitmapImage();
            using var ms = new MemoryStream(data);
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Agrupa os pixels por matiz e devolve a cor do grupo mais forte,
    /// dando mais peso a cores vivas. Capas em preto e branco viram cinza.
    /// </summary>
    public static Color Dominant(BitmapSource src)
    {
        BitmapSource small = src;
        if (src.PixelWidth > 48 || src.PixelHeight > 48)
            small = new TransformedBitmap(src, new ScaleTransform(48.0 / src.PixelWidth, 48.0 / src.PixelHeight));

        var bgra = new FormatConvertedBitmap(small, PixelFormats.Bgra32, null, 0);
        int w = bgra.PixelWidth, h = bgra.PixelHeight, stride = w * 4;
        var px = new byte[h * stride];
        bgra.CopyPixels(px, stride, 0);

        const int bins = 24;
        var weight = new double[bins];
        var rSum = new double[bins];
        var gSum = new double[bins];
        var bSum = new double[bins];
        double rAll = 0, gAll = 0, bAll = 0;
        int count = 0;

        for (int i = 0; i < px.Length; i += 4)
        {
            double b = px[i], g = px[i + 1], r = px[i + 2];
            rAll += r; gAll += g; bAll += b; count++;

            var (hue, sat, val) = ToHsv(r / 255, g / 255, b / 255);
            if (sat < 0.18 || val < 0.15) continue;

            double wgt = sat * val;
            int bin = (int)(hue / 360 * bins) % bins;
            weight[bin] += wgt;
            rSum[bin] += r * wgt;
            gSum[bin] += g * wgt;
            bSum[bin] += b * wgt;
        }

        int best = 0;
        for (int k = 1; k < bins; k++)
            if (weight[k] > weight[best]) best = k;

        if (count == 0) return Color.FromRgb(60, 60, 66);
        if (weight[best] < count * 0.02)
            return Color.FromRgb((byte)(rAll / count), (byte)(gAll / count), (byte)(bAll / count));

        return Color.FromRgb(
            (byte)(rSum[best] / weight[best]),
            (byte)(gSum[best] / weight[best]),
            (byte)(bSum[best] / weight[best]));
    }

    public static (double h, double s, double v) ToHsv(Color c) => ToHsv(c.R / 255.0, c.G / 255.0, c.B / 255.0);

    public static (double h, double s, double v) ToHsv(double r, double g, double b)
    {
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double d = max - min;

        double h = 0;
        if (d > 0)
        {
            if (max == r) h = 60 * (((g - b) / d) % 6);
            else if (max == g) h = 60 * (((b - r) / d) + 2);
            else h = 60 * (((r - g) / d) + 4);
        }
        if (h < 0) h += 360;

        double s = max == 0 ? 0 : d / max;
        return (h, s, max);
    }

    public static Color FromHsv(double h, double s, double v, byte alpha = 255)
    {
        double c = v * s;
        double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
        double m = v - c;

        (double r, double g, double b) = (h % 360) switch
        {
            < 60 => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _ => (c, 0d, x),
        };

        return Color.FromArgb(alpha,
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }
}
