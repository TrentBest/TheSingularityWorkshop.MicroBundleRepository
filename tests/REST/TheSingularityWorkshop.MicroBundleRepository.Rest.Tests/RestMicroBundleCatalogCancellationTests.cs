using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using TheSingularityWorkshop.FSM_COS;
using TheSingularityWorkshop.MicroBundleDomain;
using TheSingularityWorkshop.MicroBundleRepository.Core;
using TheSingularityWorkshop.MicroBundleRepository.Rest;
using TheSingularityWorkshop.FSM_REST;
using Xunit;

namespace TheSingularityWorkshop.MicroBundleRepository.Rest.Tests;

public sealed class RestMicroBundleCatalogCancellationTests
{
    [Fact]
    public async Task PreloadClosureAsync_does_not_publish_staged_bundle_when_cancelled_before_commit()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var address = Address(42, bytes);
        using var cancellation = new CancellationTokenSource();
        var repository = new RestMicroBundleRepository(
            new Uri("https://repository.test"),
            new CancellingTransport(address, bytes, cancellation));
        var catalog = new RestMicroBundleCatalog(
            repository,
            new Dictionary<ulong, MicroBundleArtifactAddress> { [42] = address },
            new Materializer());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => catalog.PreloadClosureAsync(new[] { 42UL }, cancellation.Token));

        Assert.False(catalog.TryResolve(42, out _));
        Assert.False(catalog.TryResolve(42, "1.0.0", out _));
    }

    [Fact]
    public async Task PreloadClosureAsync_observes_cancellation_while_waiting_for_commit_lock()
    {
        var bytes = new byte[] { 4, 5, 6 };
        var address = Address(43, bytes);
        using var cancellation = new CancellationTokenSource();
        var materializationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var materializerReturningThread = new TaskCompletionSource<Thread>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowMaterializerReturn = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new RestMicroBundleRepository(
            new Uri("https://repository.test"),
            new ImmediateTransport(address, bytes));
        var catalog = new RestMicroBundleCatalog(
            repository,
            new Dictionary<ulong, MicroBundleArtifactAddress> { [43] = address },
            new Materializer(materializationStarted, allowMaterializerReturn, materializerReturningThread));

        // Pause materialization before it returns, then acquire the catalog gate.
        // This lets the preload pass its initial loaded check before the gate is held.
        var gateField = typeof(RestMicroBundleCatalog).GetField(
            "_loadedGate",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(gateField);
        var gate = gateField!.GetValue(catalog);
        Assert.NotNull(gate);

        using var lockAcquired = new ManualResetEventSlim();
        using var releaseLock = new ManualResetEventSlim();
        var preload = Task.Run(() => catalog.PreloadClosureAsync(new[] { 43UL }, cancellation.Token));

        try
        {
            await materializationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var lockHolder = Task.Run(() =>
            {
                Monitor.Enter(gate!);
                try
                {
                    lockAcquired.Set();
                    if (!releaseLock.Wait(TimeSpan.FromSeconds(5)))
                        throw new TimeoutException("The test did not release the commit lock in time.");
                }
                finally
                {
                    Monitor.Exit(gate!);
                }
            });

            Assert.True(lockAcquired.Wait(TimeSpan.FromSeconds(5)), "The test lock was not acquired.");
            allowMaterializerReturn.TrySetResult();

            // Observe the actual thread that returned from materialization entering a
            // monitor wait. This replaces a blind delay with evidence that preload has
            // advanced past materialization and is blocked at the held commit gate.
            var materializerThread = await materializerReturningThread.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(
                SpinWait.SpinUntil(
                    () => (materializerThread.ThreadState & ThreadState.WaitSleepJoin) != 0,
                    TimeSpan.FromSeconds(5)),
                "The preload thread did not enter a wait while the commit lock was held.");
            cancellation.Cancel();
            releaseLock.Set();
            await lockHolder;
        }
        finally
        {
            allowMaterializerReturn.TrySetResult();
            releaseLock.Set();
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preload);
        Assert.False(catalog.TryResolve(43, out _));
        Assert.False(catalog.TryResolve(43, "1.0.0", out _));
    }

    private static MicroBundleArtifactAddress Address(ulong id, byte[] bytes) =>
        new(id, "1.0.0", Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());

    private sealed class Materializer(
        TaskCompletionSource? materialized = null,
        TaskCompletionSource? allowReturn = null,
        TaskCompletionSource<Thread>? materializerReturningThread = null) : IMicroBundleArtifactMaterializer
    {
        public IMicroBundle Materialize(MicroBundleArtifact artifact)
        {
            materialized?.TrySetResult();
            allowReturn?.Task.GetAwaiter().GetResult();
            materializerReturningThread?.TrySetResult(Thread.CurrentThread);
            return new TestBundle(artifact.Address.BundleId, artifact.Address.Version);
        }
    }

    private sealed class TestBundle(ulong id, string version) : IMicroBundle
    {
        public MicroBundleDescriptor Descriptor { get; } = new(id, version);
        public ulong Id => Descriptor.Id;
        public IReadOnlyList<MicroBundleDependencyRequest> Dependencies { get; } =
            Array.Empty<MicroBundleDependencyRequest>();

        public void Load(IMicroBundleLoadContext context) { }
        public bool Arbitrate(IMicroBundleArbitrationContext context, int roundIndex) => false;
    }

    private static RestResponse CreateResponse(MicroBundleArtifactAddress address, byte[] bytes)
    {
        var dto = new MicroBundleArtifactDto(
            address.BundleId,
            address.Version,
            address.ContentHash,
            Convert.ToBase64String(bytes));
        return new RestResponse(
            200,
            "OK",
            new Dictionary<string, string>(),
            JsonSerializer.Serialize(dto, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    private sealed class ImmediateTransport(
        MicroBundleArtifactAddress address,
        byte[] bytes) : IRestTransport
    {
        public Task<RestResponse> SendAsync(
            RestRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateResponse(address, bytes));
    }

    private sealed class CancellingTransport(
        MicroBundleArtifactAddress address,
        byte[] bytes,
        CancellationTokenSource cancellation) : IRestTransport
    {
        public Task<RestResponse> SendAsync(
            RestRequest request,
            CancellationToken cancellationToken = default)
        {
            // Simulate cancellation after the final artifact has been returned but
            // before the catalog's atomic commit point.
            cancellation.Cancel();
            return Task.FromResult(CreateResponse(address, bytes));
        }
    }
}
