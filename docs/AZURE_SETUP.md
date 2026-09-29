# Azure Blob Storage Setup

This repository uses Azure Blob Storage as the first durable MicroBundle artifact store.

The target configuration is deliberately small:

- Storage account kind: **StorageV2**
- Performance: **Standard**
- Redundancy: **LRS**
- Access tier: **Hot**
- Container: **microbundles**
- Container access: **Private**
- Public blob access: **Disabled**
- Secure transfer: **Enabled**
- Minimum TLS: **1.2**
- Hierarchical namespace: **Disabled**
- SFTP: **Disabled**
- NFS: **Disabled**

No database, CDN, Front Door, geo-replication, or public artifact endpoint is required for the first vertical slice.

## Portal setup

Create one storage account in the Azure portal.

Choose **Storage accounts → Create** and configure the account with the settings above. Choose a region available to your subscription and reasonably close to your development environment.

After the account exists:

1. Open **Data storage → Containers**.
2. Create a container named `microbundles`.
3. Leave anonymous/public access disabled.
4. Open **Access control (IAM)** on the storage account.
5. Grant your development Microsoft Entra identity the **Storage Blob Data Contributor** role.

The repository uses `DefaultAzureCredential`, so no storage account key belongs in source control.

## Local authentication

The preferred local-development path is Microsoft Entra authentication.

With Azure CLI installed:

~~~powershell
az login
~~~

Confirm the signed-in identity:

~~~powershell
az account show
~~~

The account used by Visual Studio can also be used by `DefaultAzureCredential`.

## Storage account URI

The application needs only the storage account endpoint, for example:

~~~text
https://<storage-account-name>.blob.core.windows.net
~~~

Do not put an account key in appsettings, source code, GitHub, or a committed secrets file.

For local development, pass the URI through an environment variable:

~~~powershell
$env:MICROBUNDLE_STORAGE_ACCOUNT_URI = "https://<storage-account-name>.blob.core.windows.net"
~~~

## First repository initialization

The Azure adapter exposes:

~~~csharp
var repository = new AzureMicroBundleRepository(
    new AzureMicroBundleRepositoryOptions
    {
        StorageAccountUri = new Uri(storageAccountUri)
    });

await repository.InitializeAsync();
~~~

Initialization creates the private `microbundles` container if necessary.

Then an artifact can be stored:

~~~csharp
await repository.PutAsync(artifact);
~~~

and retrieved by its complete address:

~~~csharp
var artifact = await repository.GetAsync(address);
~~~

A missing blob returns `null`. A blob whose bytes do not match the requested SHA-256 address cannot become a `MicroBundleArtifact`.

## Optional Azure CLI bootstrap

The same configuration can be created from Azure CLI. Replace the placeholders before running:

~~~powershell
$resourceGroup = "<resource-group>"
$location = "<azure-region>"
$storageAccount = "<globally-unique-lowercase-name>"

az group create `
  --name $resourceGroup `
  --location $location

az storage account create `
  --name $storageAccount `
  --resource-group $resourceGroup `
  --location $location `
  --sku Standard_LRS `
  --kind StorageV2 `
  --access-tier Hot `
  --allow-blob-public-access false `
  --min-tls-version TLS1_2 `
  --https-only true

az storage container create `
  --name microbundles `
  --account-name $storageAccount `
  --auth-mode login

$accountId = az storage account show `
  --name $storageAccount `
  --resource-group $resourceGroup `
  --query id `
  --output tsv

$userId = az ad signed-in-user show --query id --output tsv

az role assignment create `
  --assignee-object-id $userId `
  --assignee-principal-type User `
  --role "Storage Blob Data Contributor" `
  --scope $accountId
~~~

Role assignment propagation can take a short time. If the container command or first repository operation receives an authorization error immediately after assigning the role, wait briefly and retry.

## Cost boundary

Azure's current free-services offer lists 5 GB of LRS hot block Blob Storage plus monthly operation allowances for qualifying free-account usage for 12 months. This is a prototype allowance, not a promise that arbitrary future traffic is free.

Keep the first repository deliberately small: one LRS/Hot storage account, one private container, no extra Azure services.

## Architecture rule

**Blob Storage is the substrate. The repository is the delivery boundary. FSM_COS is the composition boundary.**

Do not add a database merely to make blob artifacts queryable. If discovery later requires indexing, that should be designed as a purpose-built artifact/catalog structure rather than silently turning the repository into a CRUD database.
