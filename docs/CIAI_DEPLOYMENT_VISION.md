# CiAi-Assisted MicroBundleRepository Deployment

## Intent

Provide an easy-button path for a user to deploy a verified MicroBundleRepository instance to a platform they control, without requiring the user to clone, compile, and manually configure the repository.

CiAi is the evidence and orchestration layer for this path. It observes actual CI runs and produces traceable reports. A separate, explicitly authorized deployment job will eventually consume a verified build artifact and a user-selected destination configuration.

## Intended flow

1. **Choose destination** — select a supported platform and deployment mode; record non-secret configuration such as region, endpoint, service name, and account/tenant reference.
2. **Validate prerequisites** — check platform capabilities, permissions, target compatibility, and required settings before building or changing anything.
3. **Build and verify** — run restore, build, tests, package verification, and artifact identity checks. Produce an immutable, traceable deployment artifact.
4. **Authorize deployment** — require an explicit deployment input or protected environment approval. CI verification alone is never deployment consent.
5. **Deploy** — use a platform-managed identity, OIDC federation, or narrowly scoped short-lived credential where supported. Do not compile arbitrary destination-supplied scripts or execute untrusted manifest content with deployment privileges.
6. **Verify and report** — perform a health check, record the deployment version and artifact digest, and return the endpoint and evidence links without revealing secrets.
7. **Recover** — where supported, retain a known-good revision and provide a documented rollback path.

## Security boundary

- **Configuration is not a credential.** Endpoint, region, deployment mode, and account/tenant identifiers may be ordinary inputs if the platform permits. Access tokens, passwords, private keys, and connection strings are secrets.
- **Do not transport password hashes as generic credentials.** Use the destination platform's supported authentication flow. Prefer OIDC/workload identity or managed identity; otherwise use a short-lived, least-privilege secret stored in a protected environment.
- **Never echo secrets.** Do not pass secrets as command-line arguments, write them to reports, package them into artifacts, or expose them in debug logs. Masking is a fallback, not the security design.
- **Separate observation from deployment.** The CiAi observer is read-only and must not receive deployment permissions. The deployment job should have a separate environment, narrow permissions, and explicit approval.
- **Verify provenance.** Tie the deployment to a commit SHA, successful required checks, package digest, and artifact identity. Do not deploy a mutable branch name or a package selected only by a floating tag.
- **Least privilege by default.** Grant contents: read to observation/build jobs. Add cloud permissions only to the deployment job and only for the selected destination.
- **Local development remains independent.** The filesystem repository and its tests must continue to work without cloud accounts or deployment credentials.

## Implementation sequence

1. Keep the repository build/test and package-verification workflows as independent gates.
2. Use CiAi to observe the actual commit's workflow runs and preserve JSON/Markdown evidence as a workflow artifact.
3. Define a small destination-provider contract: validate configuration, establish identity, deploy an immutable artifact, health-check, and report status.
4. Implement one non-production deployment target first, using a protected environment and no long-lived credentials in repository files.
5. Add integration tests for missing permissions, invalid endpoints, failed health checks, cancellation, secret redaction, and rollback.
6. Add further providers only after the first end-to-end deployment is repeatable and auditable.

## Current scope

The repository currently contains artifact storage/delivery adapters and CI verification. The CiAi workflow added alongside this document is an on-demand, read-only observation of workflow evidence for a commit. It does not deploy anything, create accounts, provision infrastructure, or request credentials. Those actions require a separate implementation and explicit deployment authorization.
