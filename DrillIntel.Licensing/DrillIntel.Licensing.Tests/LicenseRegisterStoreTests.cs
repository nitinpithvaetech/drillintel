using DrillIntel.Licensing.Issuer.Register;
using DrillIntel.Licensing.Client;
using System.Security.Cryptography;

namespace DrillIntel.Licensing.Tests;

public sealed class LicenseRegisterStoreTests
{
    [Fact]
    public async Task ImportedSignedRequest_IsVerifiedAndRecordedAsPendingOrDuplicate()
    {
        var directory = Path.Combine(Path.GetTempPath(), "DrillIntelRequestInboxTests", Guid.NewGuid().ToString("N"));
        try
        {
            var database = new SqliteRegisterDatabase(Path.Combine(directory, "register.sqlite"));
            await database.InitializeAsync();
            var store = new SqliteLicenseRegisterStore(database);
            var customerId = await store.CreateCustomerAsync("Acme Drilling", null, null);
            var rigId = await store.CreateRigAsync(customerId, "Rig 14");
            var licenseId = await store.CreateLicenseAsync(rigId, "6-month",
                DateTimeOffset.Parse("2026-12-01T00:00:00Z"), DateTimeOffset.Parse("2027-06-01T00:00:00Z"));
            using var deviceKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var requestJws = ActivationRequestBuilder.CreateSignedRequest(licenseId, deviceKey, "software", DateTimeOffset.UtcNow, out var request);
            var source = Path.Combine(directory, "request.jws");
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(source, requestJws);

            var first = await store.ImportActivationRequestAsync(requestJws, source);
            var second = await store.ImportActivationRequestAsync(requestJws, source);
            var workspace = await store.GetWorkspaceAsync(customerId);

            Assert.Equal("pending", first.Status);
            Assert.Equal("duplicate", second.Status);
            Assert.Contains(workspace!.RequestInbox, item => item.RequestId == request.RequestId && item.Status == "pending");
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CustomerRigAndLicenseRecords_AppearInCustomerWorkspace()
    {
        var directory = Path.Combine(Path.GetTempPath(), "DrillIntelRegisterStoreTests", Guid.NewGuid().ToString("N"));
        try
        {
            var database = new SqliteRegisterDatabase(Path.Combine(directory, "register.sqlite"));
            await database.InitializeAsync();
            var store = new SqliteLicenseRegisterStore(database);

            var customerId = await store.CreateCustomerAsync("Acme Drilling", "ops@acme.example", "Account note");
            var rigId = await store.CreateRigAsync(customerId, "Rig 14");
            var licenseId = await store.CreateLicenseAsync(
                rigId,
                "6-month",
                DateTimeOffset.Parse("2026-12-01T00:00:00Z"),
                DateTimeOffset.Parse("2027-06-01T00:00:00Z"));

            var customers = await store.GetCustomersAsync();
            var workspace = await store.GetWorkspaceAsync(customerId);

            Assert.Single(customers);
            Assert.Equal(1, customers[0].RigCount);
            Assert.Equal(1, customers[0].LicenseCount);
            Assert.NotNull(workspace);
            Assert.Equal("Acme Drilling", workspace.Customer.Name);
            Assert.Equal("Rig 14", Assert.Single(workspace.Rigs).RigName);
            var license = Assert.Single(workspace.Licenses);
            Assert.Equal(licenseId, license.LicenseId);
            Assert.Equal("suspended", license.Status);
            Assert.Equal(0, license.CurrentRevision);
            Assert.Equal(3, license.MaxDevices);
            Assert.Equal(3, workspace.AuditLog.Count);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
