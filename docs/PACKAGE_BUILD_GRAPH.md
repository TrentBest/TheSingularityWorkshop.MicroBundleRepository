# NuGet Package Build Graph

A package ecosystem is a directed graph, not merely a collection of repositories.

If package A depends on package B, the release state of B constrains the release state of A. A stable 1.0.0 package should not depend on a prerelease package merely because the compiler can resolve it.

## The rule

> **A package may be released as stable only when every first-party package in its dependency closure is itself stable.**

This is a release-governance rule, not a NuGet technical restriction. NuGet can represent prerelease dependencies; our release process should not treat such a dependency closure as a stable foundation.

## Current graph

~~~text
                         +----------------------+
                         | FSM_API 1.0.13       |
                         | standalone root       |
                         +----------+-----------+
                                    |
                                    v
                         +----------------------+
                         | FSM_COS 1.0.0 prep  |
                         | depends on stable   |
                         | FSM_API + Domain     |
                         +----------+-----------+
                                    |
                                    v
                         +----------------------+
                         | FSM_REST 1.0.0 prep  |
                         | depends on COS 1.0  |
                         +----------------------+

+----------------------+
| MicroBundleDomain    |
| 1.0.0 root           |
+----------------------+

+----------------------+
| FSM_Serialization    |
| 1.0.0 root           |
+----------+-----------+
           |
           v
+------------------------------+
| MicroBundleRepository.Core   |
| 1.0.0                        |
+----------+-------------------+
           |
           v
+------------------------------+
| MicroBundleRepository.Azure  |
| alpha                        |
+------------------------------+

MicroBundleRepository.Rest 1.0.0 prep
    |
    +-- MicroBundleRepository.Core 1.0.0
    +-- FSM_REST 1.0.0
    +-- FSM_COS 1.0.0
~~~

## Release layers

### Layer 0 — independent roots

- TheSingularityWorkshop.FSM_API — currently 1.0.13
- TheSingularityWorkshop.FSM_Serialization — currently 1.0.0
- TheSingularityWorkshop.MicroBundleDomain — currently 1.0.0

These may proceed independently, subject to their own tests and release gates.

### Layer 1 — direct consumers of stable roots

- TheSingularityWorkshop.MicroBundleRepository.Core depends on FSM_Serialization 1.0.0.
- Its first-party dependency closure is therefore stable.
- Its current release-prep version is 1.0.0.

The Azure adapter is downstream of Core through a project reference and can become stable after Core is stable. Its Azure SDK dependencies are third-party dependencies evaluated by their own compatibility and security policy.

### Layer 2 — composition-dependent packages

TheSingularityWorkshop.FSM_COS release-prep PR #12 now targets 1.0.0 with FSM_API 1.0.13 and MicroBundleDomain 1.0.0. Its stable dependency closure is ready; the remaining work is release verification and human approval.

TheSingularityWorkshop.FSM_REST release-prep PR #4 now targets 1.0.0 and explicitly depends on FSM_COS 1.0.0. Its CI is expected to remain blocked until COS 1.0.0 is actually published.

### Layer 3 — repository REST adapter

TheSingularityWorkshop.MicroBundleRepository.Rest release-prep PR #13 now targets 1.0.0 and explicitly depends on Repository Core 1.0.0, FSM_REST 1.0.0, and FSM_COS 1.0.0. It is intentionally downstream of the two composition packages.

~~~text
Repository Core 1.0.0
       |
       +-- stable dependency closure

Repository REST 1.0.0 prep
       |
       +-- Core 1.0.0             OK
       +-- FSM_REST 1.0.0        WAITING
       +-- FSM_COS 1.0.0         WAITING
~~~

## The stable-release frontier

~~~text
FSM_API 1.0.13 -----------+
FSM_Serialization 1.0.0 --+--> Repository.Core 1.0.0
MicroBundleDomain 1.0.0 --+

FSM_API + Domain 1.0.0
             |
             v
        FSM_COS 1.0.0
             |
             v
        FSM_REST 1.0.0
             |
             v
     Repository.Rest 1.0.0
~~~

Repository Core does not have to wait for FSM_COS. The artifact boundary can stabilize independently of the runtime composition boundary.

## Build order is not publication order

The graph determines dependency order, but publication remains an explicit human-controlled action.

CI should restore the dependency graph, build and test each node, pack each node, inspect package metadata, reject a stable package whose first-party dependency closure contains prerelease packages, and produce packages as artifacts.

CI should not publish merely because a graph node passed. Publication remains a separate, explicitly authorized operation.

## Mechanical release gate

~~~text
stable package
     |
     v
direct dependency metadata
     |
     v
no prerelease first-party dependency
     |
     v
dependency graph remains inside stable frontier
     |
     v
eligible for human release review
~~~

A direct dependency check catches the immediate error. The graph document supplies the transitive context.

The long-term implementation should make the graph machine-readable so CI can calculate the transitive closure rather than relying on documentation. For this release pass, the graph is already being enforced mechanically at the packed-package boundary.

## Why this belongs in ecosystem theory

The build graph is not merely CI plumbing.

> **A package is only as stable as the first-party foundation required to consume it.**

This gives us distinct concepts:

- source independence — a repository can build by itself;
- package independence — its published package has no first-party package dependency;
- release independence — its complete first-party dependency closure is stable;
- publication authority — a human explicitly authorizes publishing the artifact.

Those are different properties and should remain separate.

## Future machine-readable graph

The natural next step is a small ecosystem manifest containing package identity, current version, dependency identities, release channel, and build prerequisites.

From that, tooling can calculate independent roots, topological build layers, dependency closures, the current stable frontier, packages blocked by prerelease dependencies, and packages eligible for stable release review.

That is preferable to manually maintaining a sequence of commands as the ecosystem grows.

## Boundary

The graph should describe build and release relationships, not become another runtime composition system.

~~~text
NuGet graph
    | build/release dependency
    v
package artifact
    | delivered artifact
    v
MicroBundle Repository
    | runtime composition
    v
FSM_COS
~~~

The repository remains a delivery boundary. FSM_COS remains the composition boundary. The package graph exists above both as release engineering metadata.

> **Build order is topology. Publication is authority. Runtime composition is a different graph.**