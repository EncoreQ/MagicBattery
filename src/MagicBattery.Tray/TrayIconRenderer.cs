using System.Globalization;
using System.IO;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MagicBattery.Tray.Core;

namespace MagicBattery.Tray;

/// <summary>
/// 把 <see cref="TrayIconModel"/> 光栅化成托盘图标(数字 + 档位底色 + 充电闪电 + 置灰)。
/// 绘制用纯 WPF(DrawingVisual + RenderTargetBitmap),再自行把位图包成 ICO 交给
/// <c>TaskbarIcon.Icon</c>(其类型即 <see cref="System.Drawing.Icon"/>,由托盘库自身暴露)。
///
/// 不走 H.NotifyIcon 的 IconSource / ToIcon:它们的转换器只认 URI 背景的图片,
/// 对运行期生成的 RenderTargetBitmap 直接抛 NotImplemented(实测)。这里自己 PNG→ICO,
/// 不新增任何 NuGet 依赖(System.Drawing.Icon 随托盘库传递引入)。
/// 必须在 UI 线程调用(FormattedText / DrawingVisual 是 DispatcherObject)。
/// </summary>
[SupportedOSPlatform("windows")]
public static class TrayIconRenderer
{
    private const int Size = 32; // 基准像素;系统按 DPI 缩放
    private static readonly Typeface NumberFace = new(
        new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    public static System.Drawing.Icon Render(TrayIconModel model)
    {
        BitmapSource bitmap = RenderBitmap(model);
        byte[] png = EncodePng(bitmap);
        return WrapPngAsIcon(png, Size);
    }

    private static BitmapSource RenderBitmap(TrayIconModel model)
    {
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            double opacity = model.Dimmed ? 0.55 : 1.0;
            var bg = new SolidColorBrush(
                Color.FromRgb(model.Background.R, model.Background.G, model.Background.B))
            { Opacity = opacity };

            double radius = Size * 0.22;
            dc.DrawRoundedRectangle(bg, null, new Rect(0, 0, Size, Size), radius, radius);

            if (model.Kind == TrayIconKind.Bars)
            {
                DrawBars(dc, model.Bars);
            }
            else
            {
                DrawCenteredText(dc, model.Text);
            }

            if (model.ShowBolt)
            {
                DrawBolt(dc);
            }
        }

        var rtb = new RenderTargetBitmap(Size, Size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }

    private static void DrawCenteredText(DrawingContext dc, string text)
    {
        // 字号随字符数收缩,保证 "100" 也放得下
        double fontSize = text.Length switch
        {
            <= 1 => Size * 0.72,
            2 => Size * 0.60,
            _ => Size * 0.44,
        };

        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            NumberFace,
            fontSize,
            Brushes.White,
            pixelsPerDip: 1.0);

        double x = (Size - formatted.Width) / 2;
        double y = (Size - formatted.Height) / 2;
        dc.DrawText(formatted, new Point(x, y));
    }

    // 横置电池:白描边外框 + 右侧正极 + 4 格,点亮前 bars 格(粗档设备如手柄)
    private static void DrawBars(DrawingContext dc, int bars)
    {
        SolidColorBrush white = Brushes.White;
        var pen = new Pen(white, Size * 0.055);

        double bodyW = Size * 0.62, bodyH = Size * 0.40;
        double x = (Size - bodyW - Size * 0.08) / 2;
        double y = (Size - bodyH) / 2;
        dc.DrawRectangle(null, pen, new Rect(x, y, bodyW, bodyH));

        double tipW = Size * 0.06, tipH = bodyH * 0.45;
        dc.DrawRectangle(white, null, new Rect(x + bodyW + Size * 0.02, y + (bodyH - tipH) / 2, tipW, tipH));

        const int cells = 4;
        double pad = Size * 0.06;
        double innerX = x + pad, innerY = y + pad;
        double innerW = bodyW - 2 * pad, innerH = bodyH - 2 * pad;
        double gap = Size * 0.03;
        double cellW = (innerW - gap * (cells - 1)) / cells;
        for (int i = 0; i < bars && i < cells; i++)
        {
            dc.DrawRectangle(white, null, new Rect(innerX + i * (cellW + gap), innerY, cellW, innerH));
        }
    }

    // 右下角画一个小闪电(金填充 + 白描边),表示充电中
    private static void DrawBolt(DrawingContext dc)
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(22, 17), isFilled: true, isClosed: true);
            ctx.LineTo(new Point(17, 25), true, false);
            ctx.LineTo(new Point(20.5, 25), true, false);
            ctx.LineTo(new Point(18, 31), true, false);
            ctx.LineTo(new Point(26, 22), true, false);
            ctx.LineTo(new Point(22, 22), true, false);
            ctx.LineTo(new Point(25, 17), true, false);
        }

        geometry.Freeze();
        dc.DrawGeometry(Brushes.Gold, new Pen(Brushes.White, 1.2), geometry);
    }

    private static byte[] EncodePng(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    // 把一张 PNG 包成单图 ICO(Vista+ 支持 PNG 压缩的图标项),再交给 System.Drawing.Icon。
    private static System.Drawing.Icon WrapPngAsIcon(byte[] png, int size)
    {
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            // ICONDIR
            w.Write((short)0);              // reserved
            w.Write((short)1);              // type = 1 (icon)
            w.Write((short)1);              // image count

            // ICONDIRENTRY
            w.Write((byte)(size >= 256 ? 0 : size)); // width(0 表示 256)
            w.Write((byte)(size >= 256 ? 0 : size)); // height
            w.Write((byte)0);               // 调色板色数
            w.Write((byte)0);               // reserved
            w.Write((short)1);              // color planes
            w.Write((short)32);             // bits per pixel
            w.Write(png.Length);            // 图像字节数
            w.Write(6 + 16);                // 图像数据偏移 = ICONDIR(6) + ENTRY(16)
            w.Write(png);
        }

        ms.Position = 0;
        return new System.Drawing.Icon(ms, size, size);
    }
}
