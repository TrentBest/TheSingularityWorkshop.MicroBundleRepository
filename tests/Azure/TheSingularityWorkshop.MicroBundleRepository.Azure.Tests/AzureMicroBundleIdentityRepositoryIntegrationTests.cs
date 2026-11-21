using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using TheSingularityWorkshop.MicroBundleRepository.Azure;
using TheSingularityWorkshop.MicroBundleRepository.Core;

namespace TheSingularityWorkshop.MicroBundleRepository.Azure.Tests;

public sealed class AzureMicroBundleIdentityRepositoryIntegrationTests
{
    [Fact]
    public async Task Identity_ingestion_is_idempotent_and_resolves_both_directions()
    {
        var (container, repository) = await CreateRepositoryAsync();
        try
        {
            var content = "revision one"u8.ToArray();
            var first = await repository.PutAsync("sample.ontology", "1.0.0", content);
            var reopened = new AzureMicroBundleIdentityRepository(container);

            var repeated = await reopened.PutAsync("sample.ontology", "1.0.0", content);
            var byIdentity = await reopened.ResolveAsync(first.Identity);
            var byId = await reopened.ResolveAsync(first.Address.BundleId);

            Assert.Equal(first, repeated);
            Assert.Equal(first, byIdentity);
            Assert.Equal(first, byId);
        }
        finally
        {
            await container.DeleteIfExistsAsync();
        }
    }

    [Fact]
    public async Task Different_revisions_of_the_same_ontology_receive_distinct_ids()
    {
        var (container, repository) = await CreateRepositoryAsync();
        try
        {
            var first = await repository.PutAsync("sample.ontology", "1.0.0", "revision one"u8.ToArray());
            var second = await repository.PutAsync("sample.ontology", "2.0.0", "revision two"u8.ToArray());

            Assert.NotEqual(first.Address.BundleId, second.Address.BundleId);
            Assert.NotEqual(first.Identity, second.Identity);
            Assert.Equal(first, await repository.ResolveAsync(first.Identity));
            Assert.Equal(second, await repository.ResolveAsync(second.Identity));
        }
        finally
        {
            await container.DeleteIfExistsAsync();
        }
    }

    [Fact]
    public async Task Concurrent_repository_instances_commit_distinct_bindings_without_aliasing()
    {
        var (container, _) = await CreateRepositoryAsync();
        try
        {
            var repositories = Enumerable.Range(0, 8)
                .Select(_ => new AzureMicroBundleIdentityRepository(container))
                .ToArray();
            var tasks = repositories.Select((repository, index) =>
                repository.PutAsync(
                    $"sample.ontology.{index}",
                    "1.0.0",
                    System.Text.Encoding.UTF8.GetBytes($"revision-{index}")).AsTask());

            var bindings = await Task.WhenAll(tasks);

            Assert.Equal(bindings.Length, bindings.Select(binding => binding.Address.BundleId).Distinct().Count());
            foreach (var binding in bindings)
            {
                var resolved = await repositories[0].ResolveAsync(binding.Address.BundleId);
                Assert.Equal(binding, resolved);
            }
        }
        finally
        {
            await container.DeleteIfExistsAsync();
        }
    }

    [Fact]
    public async Task Numeric_write_cannot_reuse_an_identity_bound_id()
    {
        var (container, repository) = await CreateRepositoryAsync();
        try
        {
            var binding = await repository.PutAsync("sample.ontology", "1.0.0", "revision one"u8.ToArray());
            var hash = binding.Identity.ContentHash;
            var numericArtifact = new MicroBundleArtifact(
                new MicroBundleArtifactAddress(binding.Address.BundleId, binding.Address.Version, hash),
                "revision one"u8.ToArray());

            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await repository.PutAsync(numericArtifact));
        }
        finally
        {
            await container.DeleteIfExistsAsync();
        }
    }

    [Fact]
    public async Task Resolution_rejects_artifact_bytes_that_do_not_match_the_indexed_hash()
    {
        var (container, repository) = await CreateRepositoryAsync();
        try
        {
            var binding = await repository.PutAsync("sample.ontology", "1.0.0", "original bytes"u8.ToArray());
            var artifactBlob = container.GetBlobClient(AzureMicroBundleRepository.GetBlobName(binding.Address));
            await artifactBlob.UploadAsync(BinaryData.FromBytes("tampered bytes"u8.ToArray()), overwrite: true);

            await Assert.ThrowsAsync<ArgumentException>(
                async () => await repository.ResolveAsync(binding.Identity));
        }
        finally
        {
            await container.DeleteIfExistsAsync();
        }
    }

    [Fact]
    public async Task Identity_ingestion_rejects_preexisting_artifact_bytes_that_conflict_with_the_content_address()
    {
        var (container, _) = await CreateRepositoryAsync();
        try
        {
            const ulong candidateId = 7_500_001;
            var content = "expected immutable artifact bytes"u8.ToArray();
            var hash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(content)).ToLowerInvariant();
            var address = new MicroBundleArtifactAddress(candidateId, "1.0.0", hash);
            var artifactBlob = container.GetBlobClient(AzureMicroBundleRepository.GetBlobName(address));

            async Task CreateConflictingBlob(
                ulong bundleId,
                string owner,
                CancellationToken cancellationToken)
            {
                if (bundleId == candidateId && owner == "identity")
                {
                    await artifactBlob.UploadAsync(
                        BinaryData.FromBytes("conflicting preexisting bytes"u8.ToArray()),
                        cancellationToken);
                }
            }

            var repository = new AzureMicroBundleIdentityRepository(
                container,
                () => candidateId,
                CreateConflictingBlob);

            await Assert.ThrowsAsync<InvalidDataException>(
                async () => await repository.PutAsync("sample.preexisting-corruption", "1.0.0", content));

            var identity = new MicroBundleArtifactIdentity("sample.preexisting-corruption", "1.0.0", hash);
            Assert.Null(await repository.ResolveAsync(identity));
        }
        finally
        {
            await container.DeleteIfExistsAsync();
        }
    }

    [Fact]
    public async Task Corrupt_identity_index_fails_closed()
    {
        var (container, repository) = await CreateRepositoryAsync();
        try
        {
            var index = container.GetBlobClient("identity/identity-bindings.json");
            await index.UploadAsync(BinaryData.FromString(
                """[{"Ontology":"sample.ontology","Version":"1.0.0","ContentHash":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","BundleId":0}]"""));

            await Assert.ThrowsAsync<InvalidDataException>(
                async () => await repository.ResolveAsync(
                    new MicroBundleArtifactIdentity(
                        "sample.ontology",
                        "1.0.0",
                        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")));
        }
        finally
        {
            await container.DeleteIfExistsAsync();
        }
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[null]")]
    public async Task Null_identity_index_payloads_fail_closed(string json)
    {
        var (container, repository) = await CreateRepositoryAsync();
        try
        {
            var index = container.GetBlobClient("identity/identity-bindings.json");
            await index.UploadAsync(BinaryData.FromString(json));
            var content = "must not replace a null index"u8.ToArray();
            var hash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(content)).ToLowerInvariant();
            var identity = new MicroBundleArtifactIdentity("sample.null-index", "1.0.0", hash);

            await Assert.ThrowsAsync<InvalidDataException>(async () => await repository.ResolveAsync(identity));
            await Assert.ThrowsAsync<InvalidDataException>(async () => await repository.PutAsync(identity.Ontology, identity.Version, content));
        }
        finally
        {
            await container.DeleteIfExistsAsync();
        }
    }

    [Fact]
    public async Task Concurrent_identity_writers_retry_conditional_index_commit_without_losing_bindings()
    {
        var (container, _) = await CreateRepositoryAsync();
        try
        {
            var arrivals = 0;
            var bothReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            async Task BeforeIndexCommit(CancellationToken cancellationToken)
            {
                var arrival = Interlocked.Increment(ref arrivals);
                if (arrival == 2)
                    bothReady.TrySetResult(true);

                if (arrival <= 2)
                    await bothReady.Task.WaitAsync(cancellationToken);
            }

            var candidateSequence = 0;
            ulong CandidateId() => 7_400_000UL + (ulong)Interlocked.Increment(ref candidateSequence);
            var repository = new AzureMicroBundleIdentityRepository(
                container,
                CandidateId,
                beforeIndexCommit: BeforeIndexCommit);

            var firstBytes = "first concurrent revision"u8.ToArray();
            var secondBytes = "second concurrent revision"u8.ToArray();
            var bindings = await Task.WhenAll(
                repository.PutAsync("sample.index-race.first", "1.0.0", firstBytes).AsTask(),
                repository.PutAsync("sample.index-race.second", "1.0.0", secondBytes).AsTask());

            Assert.Equal(2, bindings.Select(binding => binding.Address.BundleId).Distinct().Count());
            foreach (var binding in bindings)
                Assert.Equal(binding, await repository.ResolveAsync(binding.Identity));
        }
        finally
        {
            await container.DeleteIfExistsAsync();
        }
    }

    [Fact]
    public async Task Identity_and_numeric_writers_cannot_claim_the_same_id_under_a_forced_reservation_race()
    {
        var (container, _) = await CreateRepositoryAsync();
        try
        {
            const ulong contestedId = 7_300_001;
            var arrivals = 0;
            var bothReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            async Task BeforeReservation(ulong bundleId, string owner, CancellationToken cancellationToken)
            {
                if (bundleId != contestedId)
                    return;

                if (Interlocked.Increment(ref arrivals) == 2)
                    bothReady.TrySetResult(true);

                await bothReady.Task.WaitAsync(cancellationToken);
            }

            var candidateCalls = 0;
            ulong CandidateId()
            {
                if (Interlocked.Increment(ref candidateCalls) == 1)
                    return contestedId;

                return BitConverter.ToUInt64(System.Security.Cryptography.RandomNumberGenerator.GetBytes(sizeof(ulong)));
            }

            var repository = new AzureMicroBundleIdentityRepository(
                container,
                CandidateId,
                BeforeReservation);

            var identityBytes = "identity writer"u8.ToArray();
            var numericBytes = "numeric writer"u8.ToArray();
            var numericHash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(numericBytes)).ToLowerInvariant();
            var numericArtifact = new MicroBundleArtifact(
                new MicroBundleArtifactAddress(contestedId, "1.0.0", numericHash),
                numericBytes);

            var identityTask = repository.PutAsync(
                "sample.reservation-race",
                "1.0.0",
                identityBytes).AsTask();
            var numericTask = repository.PutAsync(numericArtifact).AsTask();

            var identityBinding = await identityTask;
            Exception? numericFailure = null;
            try
            {
                await numericTask;
            }
            catch (Exception exception)
            {
                numericFailure = exception;
            }

            if (identityBinding.Address.BundleId == contestedId)
            {
                Assert.IsType<InvalidOperationException>(numericFailure);
                Assert.Equal(identityBinding, await repository.ResolveAsync(contestedId));
                Assert.Null(await repository.GetAsync(numericArtifact.Address));
            }
            else
            {
                Assert.Null(numericFailure);
                Assert.NotEqual(contestedId, identityBinding.Address.BundleId);
                Assert.Null(await repository.ResolveAsync(contestedId));
                var storedNumeric = await repository.GetAsync(numericArtifact.Address);
                Assert.NotNull(storedNumeric);
                Assert.Equal(numericBytes, storedNumeric.Content.ToArray());
            }
        }
        finally
        {
            await container.DeleteIfExistsAsync();
        }
    }

    [Fact]
    public async Task Failed_index_commit_leaves_an_unbound_orphan_and_reopen_can_recover()
    {
        var (container, _) = await CreateRepositoryAsync();
        try
        {
            var content = "artifact committed before injected index failure"u8.ToArray();
            var hash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(content)).ToLowerInvariant();
            var identity = new MicroBundleArtifactIdentity("sample.failed-index-commit", "1.0.0", hash);
            var failingRepository = new AzureMicroBundleIdentityRepository(
                container,
                beforeIndexCommit: _ => Task.FromException(new IOException("Injected index-commit failure.")));

            await Assert.ThrowsAsync<IOException>(
                async () => await failingRepository.PutAsync(identity.Ontology, identity.Version, content));

            Assert.Null(await failingRepository.ResolveAsync(identity));

            var artifactNames = new List<string>();
            await foreach (var blob in container.GetBlobsAsync(
                traits: BlobTraits.None,
                states: BlobStates.None,
                prefix: "artifacts/",
                cancellationToken: CancellationToken.None))
                artifactNames.Add(blob.Name);
            var orphanName = Assert.Single(artifactNames);
            var orphanId = ulong.Parse(
                orphanName.Split('/')[1],
                System.Globalization.CultureInfo.InvariantCulture);
            Assert.True((await container.GetBlobClient($"identity/reservations/{orphanId}").ExistsAsync()).Value);

            var reopened = new AzureMicroBundleIdentityRepository(container);
            var recovered = await reopened.PutAsync(identity.Ontology, identity.Version, content);

            Assert.NotEqual(orphanId, recovered.Address.BundleId);
            Assert.Equal(recovered, await reopened.ResolveAsync(identity));
        }
        finally
        {
            await container.DeleteIfExistsAsync();
        }
    }

    private static async Task<(BlobContainerClient Container, AzureMicroBundleIdentityRepository Repository)> CreateRepositoryAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("AZURITE_CONNECTION_STRING");
        Assert.False(string.IsNullOrWhiteSpace(connectionString),
            "AZURITE_CONNECTION_STRING must point to the Azurite blob service.");

        var service = new BlobServiceClient(connectionString);
        var container = service.GetBlobContainerClient("mbri" + Guid.NewGuid().ToString("N")[..20]);
        await container.CreateAsync();
        return (container, new AzureMicroBundleIdentityRepository(container));
    }
}
