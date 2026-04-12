using CombinedEffect.Services.Interfaces;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;

namespace CombinedEffect.Services;

internal sealed class WindowThemeService : IWindowThemeService
{
    public void Bind(Window window)
    {
        if (window is null) return;
        window.SourceInitialized += (_, _) => ApplyCurrentTheme(window);
        window.Loaded += (_, _) => ApplyCurrentTheme(window);
    }

    private static void ApplyCurrentTheme(Window window)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        var captionBrush = (window.TryFindResource(SystemColors.ControlBrushKey) as SolidColorBrush)
                           ?? Brushes.White;
        var textBrush = (window.TryFindResource(SystemColors.WindowTextBrushKey) as SolidColorBrush)
                        ?? Brushes.Black;

        SetDwmColor((HWND)hwnd, DWMWINDOWATTRIBUTE.DWMWA_CAPTION_COLOR, captionBrush.Color);
        SetDwmColor((HWND)hwnd, DWMWINDOWATTRIBUTE.DWMWA_BORDER_COLOR, captionBrush.Color);
        SetDwmColor((HWND)hwnd, DWMWINDOWATTRIBUTE.DWMWA_TEXT_COLOR, textBrush.Color);
    }

    private static void SetDwmColor(HWND hwnd, DWMWINDOWATTRIBUTE attribute, Color color)
    {
        uint colorRef = (uint)(color.R | (color.G << 8) | (color.B << 16));
        byte[] bytes = BitConverter.GetBytes(colorRef);
        ReadOnlySpan<byte> span = MemoryMarshal.CreateReadOnlySpan(in bytes[0], bytes.Length);
        PInvoke.DwmSetWindowAttribute(hwnd, attribute, span);
    }
}
