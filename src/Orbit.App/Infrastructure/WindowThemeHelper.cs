using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Orbit.App.Infrastructure;

/// <summary>
/// Paints the OS title bar to match the current Orbit palette so the window
/// chrome is "in osmosis" with the app instead of a stray white strip. Uses the
/// Windows 11 DWM caption-colour attributes, with the immersive dark-mode flag
/// as the fallback on builds that don't honour explicit colours.
/// </summary>
public static class WindowThemeHelper
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>Raised by <see cref="ThemeManager"/> after a palette swap.</summary>
    public static event EventHandler? PaletteChanged;

    public static void RaisePaletteChanged() =>
        PaletteChanged?.Invoke(null, EventArgs.Empty);

    /// <summary>Keeps a window's title bar synced with the palette for its lifetime.</summary>
    public static void Attach(Window window)
    {
        void Apply() => ApplyTo(window);

        if (window.IsInitialized)
            Apply();
        else
            window.SourceInitialized += (_, _) => Apply();

        EventHandler handler = (_, _) => window.Dispatcher.BeginInvoke(Apply);
        PaletteChanged += handler;
        window.Closed += (_, _) => PaletteChanged -= handler;
    }

    private static void ApplyTo(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        var caption = ResourceColor(window, "Brush.Window.Background") ?? Colors.White;
        var text = ResourceColor(window, "Brush.Text.Primary") ?? Colors.Black;
        var border = ResourceColor(window, "Brush.Surface.Border") ?? caption;

        var dark = Luminance(caption) < 0.5 ? 1 : 0;
        Set(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark);

        var captionRef = ToColorRef(caption);
        var textRef = ToColorRef(text);
        var borderRef = ToColorRef(border);
        Set(hwnd, DWMWA_CAPTION_COLOR, ref captionRef);
        Set(hwnd, DWMWA_TEXT_COLOR, ref textRef);
        Set(hwnd, DWMWA_BORDER_COLOR, ref borderRef);
    }

    private static void Set(IntPtr hwnd, int attr, ref int value)
    {
        try
        {
            DwmSetWindowAttribute(hwnd, attr, ref value, sizeof(int));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Pre-Windows 10 / no DWM — nothing we can do, leave the default chrome.
        }
    }

    private static Color? ResourceColor(FrameworkElement scope, string key) =>
        scope.TryFindResource(key) is SolidColorBrush brush ? brush.Color : null;

    private static int ToColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

    private static double Luminance(Color c) =>
        (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
}
