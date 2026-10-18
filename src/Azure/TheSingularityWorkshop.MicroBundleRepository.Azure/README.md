# TheSingularityWorkshop.MicroBundleRepository.Azure

Azure Blob Storage implementation of the platform-neutral MicroBundle repository contract.

Authentication uses Azure Identity's `DefaultAzureCredential`; storage account keys are not part of the repository configuration.


## Experience artifact limits

`AzureExperienceRepositoryOptions.MaxArtifactBytes` caps Experience artifact reads and writes. The default is 10 MiB. Reads reject blobs whose declared content length exceeds the limit before buffering and also enforce the limit while streaming in case the stream exceeds its declared length. The REST host configures this value with `Repository:MaxExperienceArtifactBytes`. Oversized artifacts raise `ExperienceArtifactTooLargeException`; the REST host translates this to HTTP 413. Adapter and host-level tests must pass before this behavior is considered release-verified.
