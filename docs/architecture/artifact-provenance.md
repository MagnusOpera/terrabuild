# Artifact provenance declarations

An invoked extension command returns `PreserveProvenance: ProvenanceEffect`
alongside `Batchable` and `Operations`. The discriminated union has three cases:
`Preserved` declares preservation, `Broken` declares a known break, and
`Unaffected` declares no effect on artifact provenance. Every extension command
must return one case explicitly. Omitted fields and option values are rejected.
Preservation is command output, never descriptor metadata. Extensions
own any computation behind it.

`Preserved` does not require byte-identical outputs: a traced transformation such
as Sentry Debug ID injection can preserve provenance when its tool version and
inputs are tracked. An upload-only command that leaves artifacts untouched can
declare `Unaffected`.

Terrabuild carries declarations with the invoked entrypoint, loaded extension
origin and script fingerprint. It does not analyze ecosystems or shell arguments,
compute provenance status, assemble a chain of evidence, or produce reports.

The existing .NET `restore` and Node `install` commands can declare preservation
from a readable, nonempty project-local lock and their existing locking switch.
Shared locks and unresolved implicit-restore/props settings cannot establish
preservation and return `Broken`.
Disabled locking or a missing local lock declares `Broken`. Extra arguments are
not parsed and conservatively return `Broken`. Arbitrary command dispatch, package
scripts, and dependency resolution without an enforced contract return `Broken`.
.NET build/pack/publish/test with restore disabled and no arbitrary arguments
return `Preserved`; their incoming dependency state remains unchanged. Sentry
Debug ID injection and upload return `Preserved`. No-op commands, Docker image
push/tag, and Terraform validation/workspace selection return `Unaffected` when
they have no arbitrary arguments. All commands retain their existing behavior. There are no special
`*_provenance` entrypoints.

## Production identity and cache reuse

Each execution produces a unique production identity and records the declarations
for its logical node and the original production references of content dependencies.
Phase-only ordering edges and synthetic batch nodes do not add artifact lineage.
Logical batch members retain separate production identities.

Cache summaries carry the original declaration and production references. Restoring
outputs does not recompute them under today's extension implementation. Legacy
summaries remain usable without declarations. Restore fallback creates a new
production. Declarations do not enter operation hash serialization or fail builds.
Debug diagnostics include the transported declarations; smoke normalization omits
volatile production identities.

## Insights reports

Insights stores immutable raw declarations and build associations, computes the
chain from original artifact production references and the reported graph, and
stores each computed report once. Status, causes, upstream evidence and complete
Timeline/Comparison state are server-owned business features.

Provenance computation and display are part of the standard Insights subscription
and require the workspace's Artifact provenance feature flag enabled in Admin.
Declaration ingestion remains available while the flag is disabled. Custom
preservation declarations retain their origin and remain untrusted in v1.

Deploy the compatible Insights schema/API before distributing updated Terrabuild
clients. Legacy artifacts do not acquire verification from artifact availability.

Insights treats `Broken` as `INCOMPLETE`; trusted `Unaffected` carries upstream
status without repairing gaps. Missing reports and custom declarations remain
`UNKNOWN` unless a known break already makes the chain incomplete.
