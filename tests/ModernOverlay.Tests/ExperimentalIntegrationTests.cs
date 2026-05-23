using ModernOverlay.Integration;
using ModernOverlay.Integration.Experimental;

namespace ModernOverlay.Tests;

[TestClass]
public sealed class ExperimentalIntegrationTests
{
    [TestMethod]
    public void ExperimentalContractsExposeSpecNamedProviderInterfaces()
    {
        Type[] contractTypes =
        [
            typeof(IOverlayIntegrationProvider),
            typeof(IRenderBridge),
            typeof(IOverlayCommandTransport),
            typeof(IWindowTargetProvider),
        ];

        Assert.IsTrue(contractTypes.Contains(typeof(IOverlayIntegrationProvider)));
        Assert.IsTrue(contractTypes.Contains(typeof(IRenderBridge)));
        Assert.IsTrue(contractTypes.Contains(typeof(IOverlayCommandTransport)));
        Assert.IsTrue(contractTypes.Contains(typeof(IWindowTargetProvider)));
    }

    [TestMethod]
    public async Task IsolatedProviderConvertsProviderExceptionsToFailureResult()
    {
        var provider = new IsolatedOverlayIntegrationProvider(new ThrowingProvider());
        OverlayIntegrationProviderResult result = await provider.InitializeAsync(new RejectingTransport());

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.Error, "provider failed");

        await provider.ShutdownAsync();
    }

    private sealed class ThrowingProvider : IOverlayIntegrationProvider
    {
        public OverlayIntegrationProviderDescriptor Descriptor { get; } = new("Throwing test provider");

        public ValueTask<OverlayIntegrationProviderResult> InitializeAsync(
            IOverlayCommandTransport transport,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("provider failed");

        public ValueTask ShutdownAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("shutdown failed");
    }

    private sealed class RejectingTransport : IOverlayCommandTransport
    {
        public ValueTask<OverlayCommandResult> SendAsync(OverlayCommandMessage message, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(OverlayCommandResult.Rejected(message.Sequence, "not used"));
    }
}
