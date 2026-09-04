using Orbit.Core.Identification;
using Serilog.Core;

namespace Orbit.Tests.Identification;

public class AppIdentificationServiceTests
{
    private sealed class StubProvider : IIdentificationProvider
    {
        private readonly AppIdentification? _result;
        public StubProvider(int order, AppIdentification? result) { Order = order; _result = result; }
        public int Order { get; }
        public AppIdentification? Seen { get; private set; }
        public Task<AppIdentification?> IdentifyAsync(ExeSignals s, AppIdentification? current, CancellationToken ct)
        {
            Seen = current;
            return Task.FromResult(_result);
        }
    }

    private static AppIdentification Id(IdentificationKind kind, double confidence, string name) => new()
    {
        Kind = kind,
        Confidence = confidence,
        Name = name,
        SuggestedCategory = kind == IdentificationKind.Game ? "Jeux" : "Applications"
    };

    private static AppIdentificationService Service(params IIdentificationProvider[] providers) =>
        new(providers, Logger.None);

    [Fact]
    public async Task Highest_confidence_result_wins()
    {
        var service = Service(
            new StubProvider(0, Id(IdentificationKind.Application, 0.5, "Weak")),
            new StubProvider(1, Id(IdentificationKind.Game, 0.9, "Strong")));

        var result = await service.IdentifyAsync(@"C:\x\y.exe");

        Assert.Equal(IdentificationKind.Game, result.Kind);
        Assert.Equal("Strong", result.Name);
    }

    [Fact]
    public async Task Below_threshold_is_reported_as_unknown()
    {
        var service = Service(new StubProvider(0, Id(IdentificationKind.Game, 0.3, "Maybe")));

        var result = await service.IdentifyAsync(@"C:\x\y.exe");

        Assert.Equal(IdentificationKind.Unknown, result.Kind);
        Assert.False(result.IsReliable);
        Assert.Equal("Maybe", result.Name); // name is still surfaced for the manual form
    }

    [Fact]
    public async Task Later_providers_receive_the_running_best_guess()
    {
        var second = new StubProvider(1, null);
        var service = Service(
            new StubProvider(0, Id(IdentificationKind.Game, 0.7, "FromPath")),
            second);

        await service.IdentifyAsync(@"C:\x\y.exe");

        Assert.NotNull(second.Seen);
        Assert.Equal("FromPath", second.Seen!.Name);
    }

    [Fact]
    public async Task A_throwing_provider_does_not_break_identification()
    {
        var service = Service(
            new ThrowingProvider(),
            new StubProvider(1, Id(IdentificationKind.Application, 0.8, "OK")));

        var result = await service.IdentifyAsync(@"C:\x\y.exe");

        Assert.Equal("OK", result.Name);
    }

    private sealed class ThrowingProvider : IIdentificationProvider
    {
        public int Order => 0;
        public Task<AppIdentification?> IdentifyAsync(ExeSignals s, AppIdentification? c, CancellationToken ct) =>
            throw new InvalidOperationException("boom");
    }
}
