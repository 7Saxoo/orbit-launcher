using System.Windows;
using System.Windows.Media;

namespace Orbit.App.Infrastructure;

/// <summary>
/// Applies a single global interface scale by putting a <see cref="ScaleTransform"/>
/// on each window's content. Scaling down gives the layout proportionally more
/// logical room, so the whole UI just gets smaller/denser with no distortion.
/// </summary>
public static class UiScaleManager
{
    public const double Min = 0.7;
    public const double Max = 1.3;

    private static double _current = 1.0;

    public static event EventHandler? Changed;

    public static double Current => _current;

    public static void Set(double scale)
    {
        _current = Math.Clamp(scale, Min, Max);
        Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Keeps a window's content scaled to <see cref="Current"/> for its lifetime.</summary>
    public static void Track(Window window)
    {
        void Apply()
        {
            if (window.Content is FrameworkElement root)
                root.LayoutTransform = _current == 1.0
                    ? Transform.Identity
                    : new ScaleTransform(_current, _current);
        }

        if (window.IsLoaded)
            Apply();
        else
            window.Loaded += (_, _) => Apply();

        EventHandler handler = (_, _) => window.Dispatcher.BeginInvoke((Action)Apply);
        Changed += handler;
        window.Closed += (_, _) => Changed -= handler;
    }
}
