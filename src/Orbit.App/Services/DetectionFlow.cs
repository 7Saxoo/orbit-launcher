using Orbit.App.ViewModels;

namespace Orbit.App.Services;

/// <summary>Shared "scan my PC and import" interaction, used from Settings and
/// from the Home empty state. Returns the number of entries imported.</summary>
public sealed class DetectionFlow
{
    private readonly Func<DetectionViewModel> _viewModelFactory;
    private readonly IDialogService _dialogs;

    public DetectionFlow(Func<DetectionViewModel> viewModelFactory, IDialogService dialogs)
    {
        _viewModelFactory = viewModelFactory;
        _dialogs = dialogs;
    }

    public int Run()
    {
        var vm = _viewModelFactory();
        _dialogs.ShowDetection(vm);
        return vm.ImportedCount;
    }
}
