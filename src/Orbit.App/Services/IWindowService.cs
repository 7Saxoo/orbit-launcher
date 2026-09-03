namespace Orbit.App.Services;

/// <summary>Lets the settings view-model resize the main window without holding
/// a reference to <c>System.Windows.Window</c>.</summary>
public interface IWindowService
{
    void ApplySize(int width, int height, bool maximized);
}
