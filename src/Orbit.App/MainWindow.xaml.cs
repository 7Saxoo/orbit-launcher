using System.Windows;
using Orbit.App.Infrastructure;

namespace Orbit.App;

/// <summary>Interaction logic for MainWindow.xaml. Behaviour lives in
/// <see cref="ViewModels.MainViewModel"/>; this only sizes the window to the
/// current screen on launch. Manual resizing is allowed but not persisted, so
/// every launch fits the screen again.</summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        WindowThemeHelper.Attach(this);
        UiScaleManager.Track(this);

        SourceInitialized += (_, _) => FillWorkArea();
    }

    private void FillWorkArea()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Left;
        Top = area.Top;
        Width = Math.Max(MinWidth, area.Width);
        Height = Math.Max(MinHeight, area.Height);
    }
}
