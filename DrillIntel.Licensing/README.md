# DrillIntel licensing (initial implementation)

This is a separate solution so issuer-only code and register storage do not enter the DrillIntel application by accident.

- `DrillIntel.Licensing.Core` contains license-file models and compact JWS ES256 verification only. It has no signing API and no database dependency.
- `DrillIntel.Licensing.Client` creates non-exportable machine CNG P-256 keys (TPM first, software fallback), signs activation requests, exports request files, and exposes an allow-list for premium UI features. It is referenced by DrillIntel. DPAPI state storage and production activation import are still to be implemented.
- `DrillIntel.Licensing.Issuer` is office-only; it owns the initial SQLite register schema and register operations, verifies device-signed requests, and records imported requests in the pending inbox. Production issuer signing and license issuance are still to be implemented. Never ship it with DrillIntel.
- `DrillIntel.Licensing.Admin` is the office WPF register editor. It can add customers, rigs and suspended license records, import and verify signed device requests, and create Debug-only `.diltest` bundles with an ephemeral test key. These bundles do not activate production licenses or update issuance records. It is never shipped to customers.

The register follows the supplied schema and archive rules: status expiry is derived from dates, revisions and device keys have uniqueness constraints, each issued file has an archive reference and SHA-256, and payment rows contain no card data. Backups must include a consistent database snapshot and the matching immutable archive. The SQLite WAL database is for one office machine, not a shared network/sync folder.

The device-key DACL is configured for authenticated Windows users so different crew profiles can use the same machine key. This must be verified on the actual rig PCs with a standard-user account before rollout; if the platform provider cannot apply the DACL, key creation reports/falls back rather than claiming TPM success.

Run the licensing test suite from the repository root with `dotnet test .\DrillIntel.Licensing\DrillIntel.Licensing.sln -m:1`. It covers the fixed ES256 vector, signature tampering, activation-request proof of possession, core license checks, and register constraints.

Run the office register UI from the repository root with `dotnet run --project .\DrillIntel.Licensing\DrillIntel.Licensing.Admin\DrillIntel.Licensing.Admin.csproj`. The first launch creates a local register under `%LOCALAPPDATA%\DrillIntel\Licensing`; use **Open / Change Register** to select another office register file. **Import .jws Request** verifies and records a pending request in the Request Inbox. In Debug, select that row and choose **Issue Dev Test Bundle** to create an ephemeral-key test package. In DrillIntel Debug, **Setup → PC Activation → Import Dev Test Bundle** verifies its signatures and device binding. This test does not persist activation state or enable premium features.

No production issuer key, trusted production public key, backup scheduler, or DrillIntel feature gate is configured yet. Those require key-custody and deployment decisions. No private key or customer data is checked in.
