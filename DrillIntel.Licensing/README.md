# DrillIntel licensing (initial implementation)

This is a separate solution so issuer-only code and register storage do not enter the DrillIntel application by accident.

- `DrillIntel.Licensing.Core` contains license-file models and compact JWS ES256 verification only. It has no signing API and no database dependency.
- `DrillIntel.Licensing.Client` creates non-exportable machine CNG P-256 device keys (TPM first, software fallback), signs activation requests, verifies production packages against an embedded issuer public key, and persists verified state protected with Windows DPAPI. It is referenced by DrillIntel.
- `DrillIntel.Licensing.Issuer` is office-only; it owns the SQLite register, verifies device-signed requests, and creates ES256 signatures with the password-encrypted production issuer key file. Never ship it with DrillIntel.
- `DrillIntel.Licensing.Admin` is the office WPF register editor. It adds customers, rigs, licenses and payment records; imports requests; and issues one `.dillicense` package per PC. Issuing updates the device, entitlement revision, activation file, request outcome and audit records. Its Debug-only `.diltest` flow remains a separate test path. It is never shipped to customers.

The register follows the supplied schema and archive rules: status expiry is derived from dates, revisions and device keys have uniqueness constraints, each issued file has an archive reference and SHA-256, and payment rows contain no card data. Backups must include a consistent database snapshot, the issuer's encrypted `.dilkey` file, and the matching immutable `IssuedPackages` archive. The SQLite WAL database is for one office machine, not a shared network/sync folder.

The device-key DACL is configured for authenticated Windows users so different crew profiles can use the same machine key. This must be verified on the actual rig PCs with a standard-user account before rollout; if the platform provider cannot apply the DACL, key creation reports/falls back rather than claiming TPM success.

Run the licensing test suite from the repository root with `dotnet test .\DrillIntel.Licensing\DrillIntel.Licensing.sln -m:1`. It covers the fixed ES256 vector, signature tampering, activation-request proof of possession, core license checks, and register constraints.

Run the office register UI from the repository root with `dotnet run --project .\DrillIntel.Licensing\DrillIntel.Licensing.Admin\DrillIntel.Licensing.Admin.csproj`. The first launch creates a local register under `%LOCALAPPDATA%\DrillIntel\Licensing`; use **Open / Change Register** to select another office register file. Use **Set Up Production Key** once to create an encrypted key under `%LOCALAPPDATA%\DrillIntel\Licensing\Issuer`. Back up the `.dilkey` file securely and keep its password separately. Export the matching `ProductionIssuerPublicKey.json` and place it in `DrillIntel.Licensing.Client` before building the customer Release installer; the client embeds this public key and fails closed if it is absent. Create a customer license, import their `.jws` request, select its pending Request Inbox row, and choose **Issue Activation Package**. Send the resulting `.dillicense` file to the customer. In DrillIntel, open **License → PC Activation**, check/create the device key, and import the package.

For a Debug-only signature check, import a request and choose **Issue Dev Test Bundle**, then import it in DrillIntel Debug. It does not update production issuance records or enable premium features. `ProductionIssuerPublicKey.sample.json` contains a publicly known RFC test-vector key solely to show the file format; never embed it in a Release build.

No production private key or customer-specific trusted public key is checked into source control. The office must create the private key, export the public JSON, embed that public key in a Release build, and distribute the rebuilt installer before customer production activation can work. No private key or customer data is checked in.

The production activation flow validates and persists licenses, but the `FeatureGate` helper is not yet connected to premium UI actions. Define which product features a license should unlock before relying on this for feature-level access control.
