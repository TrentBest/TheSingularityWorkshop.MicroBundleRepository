# MicroBundle Publisher

Repository-owned publication and consumption smoke test for MicroBundle artifacts.

## Local round-trip

This mode does not contact Azure and does not publish anything.

### PowerShell

Run this as a single command:

```powershell
dotnet run --project src/Tools/TheSingularityWorkshop.MicroBundleRepository.Publisher/TheSingularityWorkshop.MicroBundleRepository.Publisher.csproj --configuration Release -- --local 3101 1.0.0 path/to/3101.bundle
```

If you prefer multiple PowerShell lines, use the PowerShell backtick (`) for continuation:

```powershell
dotnet run --project src/Tools/TheSingularityWorkshop.MicroBundleRepository.Publisher/TheSingularityWorkshop.MicroBundleRepository.Publisher.csproj --configuration Release -- `
  --local 3101 1.0.0 path/to/3101.bundle
```

### Bash

```bash
dotnet run --project src/Tools/TheSingularityWorkshop.MicroBundleRepository.Publisher/TheSingularityWorkshop.MicroBundleRepository.Publisher.csproj --configuration Release -- \
  --local 3101 1.0.0 path/to/3101.bundle
```

A successful run proves:

```text
FSMB artifact
   -> MicroBundleArtifact
   -> IMicroBundleRepository.PutAsync
   -> IMicroBundleRepository.GetAsync
   -> byte-for-byte identity check
   -> FSM_COS AssemblyMicroBundleArtifactMaterializer
   -> IMicroBundle with ID 3101
```

The local repository is intentionally only a smoke-test implementation. It does not replace the Azure repository or the REST repository.

## Build a Moniker artifact

From the `TheSingularityWorkshop.Experiences` repository on the MicroBundle publication branch:

### PowerShell

Run these as separate commands:

```powershell
dotnet restore src/Moniker/TheSingularityWorkshop.Experiences.Moniker.csproj

dotnet build src/Moniker/TheSingularityWorkshop.Experiences.Moniker.csproj --configuration Release --no-restore /p:ContinuousIntegrationBuild=true /p:Deterministic=true

bash tools/publish-microbundle.sh src/Moniker/bin/Release/net8.0/TheSingularityWorkshop.Experiences.Moniker.dll 3101 1.0.0 artifacts/3101.bundle
```

The last command requires Bash (for example Git Bash) because `publish-microbundle.sh` is a Bash script. If you are already in Git Bash, the same command works there.

### Bash

```bash
dotnet restore src/Moniker/TheSingularityWorkshop.Experiences.Moniker.csproj
dotnet build src/Moniker/TheSingularityWorkshop.Experiences.Moniker.csproj --configuration Release --no-restore /p:ContinuousIntegrationBuild=true /p:Deterministic=true
bash tools/publish-microbundle.sh src/Moniker/bin/Release/net8.0/TheSingularityWorkshop.Experiences.Moniker.dll 3101 1.0.0 artifacts/3101.bundle
```

Then run the local round-trip command above from the MicroBundleRepository repository.

Azure publication remains a separate, explicitly invoked operation.
