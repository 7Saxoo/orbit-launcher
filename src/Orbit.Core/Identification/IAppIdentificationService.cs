namespace Orbit.Core.Identification;

/// <summary>Recognises an executable as a game, an application, or Unknown, and
/// gathers whatever name / publisher / genre / cover it can.</summary>
public interface IAppIdentificationService
{
    Task<AppIdentification> IdentifyAsync(string executablePath, CancellationToken ct = default);
}
