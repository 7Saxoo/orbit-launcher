using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Orbit.App.Infrastructure;

/// <summary>
/// A <see cref="ContentControl"/> that cross-fades and gently slides its content
/// whenever it changes – used for the section transitions in the shell.
/// </summary>
public sealed class FadeContentControl : ContentControl
{
    private readonly TranslateTransform _slide = new();

    public FadeContentControl()
    {
        RenderTransform = _slide;
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);

        if (newContent is null)
            return;

        var fade = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(190)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        var rise = new DoubleAnimation(12.0, 0.0, new Duration(TimeSpan.FromMilliseconds(240)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        BeginAnimation(OpacityProperty, fade);
        _slide.BeginAnimation(TranslateTransform.YProperty, rise);
    }
}
