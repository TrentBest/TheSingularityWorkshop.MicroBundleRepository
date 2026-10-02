# Repository REST 1.0.0 Release Contract

## Purpose

This package is the REST delivery edge for the MicroBundle Repository. It is not the repository's composition engine and it is not a replacement for FSM_COS.

## Stable dependency boundary

The package requires:

- MicroBundleRepository.Core 1.0.0;
- FSM_REST 1.0.0;
- FSM_COS 1.0.0.

That closure makes the release order explicit.

## Responsibility

Repository REST owns the mapping between repository operations and REST transport.

It may expose:

- artifact retrieval;
- artifact publication;
- Experience publication/discovery where implemented;
- repository-facing request/response contracts.

It does not own:

- MicroBundle dependency closure;
- arbitration;
- RuntimeAssembly construction;
- storage-provider semantics;
- GUI manifestation;
- Experience execution.

## Theory

The repository answers a delivery question:

> Given a complete artifact address, can the artifact be obtained and verified?

FSM_COS answers a composition question:

> Given requested capabilities, how do they become a stable RuntimeAssembly?

REST is the transport surface between a client and the delivery boundary. It should not collapse those two responsibilities.

## Release review checklist

- [ ] Core 1.0.0 is stable.
- [ ] FSM_COS 1.0.0 is stable.
- [ ] FSM_REST 1.0.0 is stable.
- [ ] Release build succeeds.
- [ ] Complete tests succeed.
- [ ] Stable package dependency gate succeeds.
- [ ] README clearly separates delivery from composition.
- [ ] Publication selects exactly one package.
- [ ] Publication remains manual and explicit.

> **REST exposes the repository boundary; it does not become the repository's brain.**
