using CommunityToolkit.Mvvm.ComponentModel;
using Orbit.Core.Detection;
using Orbit.Core.Models;

namespace Orbit.App.ViewModels;

/// <summary>One detected app in the auto-detection dialog, with its checkbox state.</summary>
public sealed partial class DetectionItemViewModel : ObservableObject
{
    public DetectionItemViewModel(DetectedApp app) => App = app;

    public DetectedApp App { get; }

    [ObservableProperty]
    private bool _isSelected = true;

    public string Name => App.Name;
    public string ExecutablePath => App.ExecutablePath;
    public string Source => App.Source;
    public string KindLabel => App.Kind == AppKind.Game ? "Jeu" : "Application";
    public string? Publisher => App.Publisher;
}
