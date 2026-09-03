namespace Orbit.Core.Models;

/// <summary>
/// An <see cref="AppEntry"/> paired with its freshly evaluated file-system
/// availability. Produced by <c>ILibraryService.LoadAsync</c>.
/// </summary>
/// <param name="Entry">The persisted entry.</param>
/// <param name="Availability">Whether the backing executable is currently usable.</param>
public sealed record LibraryItem(AppEntry Entry, AppAvailability Availability);
