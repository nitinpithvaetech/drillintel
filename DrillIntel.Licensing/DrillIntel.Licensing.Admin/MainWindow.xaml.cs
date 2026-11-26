using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using DrillIntel.Licensing.Issuer.Register;
using DrillIntel.Licensing.Issuer.Requests;
using DrillIntel.Licensing.Issuer.DevelopmentOnly;
using DrillIntel.Licensing.Core.Models;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using DrillIntel.Licensing.Issuer.Signing;

namespace DrillIntel.Licensing.Admin;

public partial class MainWindow : Window
{
    private readonly string _settingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DrillIntel", "Licensing");
    private string _registerPath = string.Empty;
    private SqliteLicenseRegisterStore? _store;
    private bool _loadingCustomers;
    private readonly Dictionary<string, string> _gridFilters = new(StringComparer.Ordinal);

    public MainWindow()
    {
        InitializeComponent();
#if !DEBUG
        IssueDevTestBundleButton.Visibility = Visibility.Collapsed;
#endif
        Loaded += Window_Loaded;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_settingsDirectory);
            var pathFile = Path.Combine(_settingsDirectory, "admin-register-path.txt");
            _registerPath = File.Exists(pathFile)
                ? File.ReadAllText(pathFile).Trim()
                : Path.Combine(_settingsDirectory, "LicenseRegister.sqlite");
            if (string.IsNullOrWhiteSpace(_registerPath))
                _registerPath = Path.Combine(_settingsDirectory, "LicenseRegister.sqlite");

            await OpenRegisterAsync(_registerPath);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Register failed to open";
            MessageBox.Show(this, ex.Message, "License Register", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task OpenRegisterAsync(string path)
    {
        var database = new SqliteRegisterDatabase(path);
        await database.InitializeAsync();
        _store = new SqliteLicenseRegisterStore(database);
        _registerPath = Path.GetFullPath(path);
        Directory.CreateDirectory(_settingsDirectory);
        File.WriteAllText(Path.Combine(_settingsDirectory, "admin-register-path.txt"), _registerPath);
        RegisterPathText.Text = _registerPath;
        StatusText.Text = "Register ready";
        await RefreshCustomersAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshCustomersAsync(SelectedCustomerId);
    }

    private async void ChangeRegister_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Open or create the office license register",
            Filter = "SQLite register (*.sqlite;*.db)|*.sqlite;*.db|All files (*.*)|*.*",
            FileName = File.Exists(_registerPath) ? Path.GetFileName(_registerPath) : "LicenseRegister.sqlite",
            DefaultExt = ".sqlite",
            AddExtension = true,
            OverwritePrompt = false
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            await OpenRegisterAsync(dialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not open register", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void AddCustomer_Click(object sender, RoutedEventArgs e)
    {
        if (!EntryDialog.TryShow(this, "Add Customer", [
                new EntryField("Customer name", true),
                new EntryField("Contact (optional)"),
                new EntryField("Notes (optional)", false, true)], out var values))
            return;

        try
        {
            var id = await RequireStore().CreateCustomerAsync(values[0], values[1], values[2]);
            await RefreshCustomersAsync(id);
            StatusText.Text = "Customer added";
        }
        catch (Exception ex)
        {
            ShowSaveError(ex);
        }
    }

    private async void AddRig_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedCustomerId is not { } customerId)
        {
            MessageBox.Show(this, "Select a customer first.", "Add Rig", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!EntryDialog.TryShow(this, "Add Rig", [new EntryField("Rig name", true)], out var values))
            return;

        try
        {
            await RequireStore().CreateRigAsync(customerId, values[0]);
            await RefreshCustomersAsync(customerId);
            StatusText.Text = "Rig added";
        }
        catch (Exception ex)
        {
            ShowSaveError(ex);
        }
    }

    private async void AddLicense_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentWorkspace is null || CurrentWorkspace.Rigs.Count == 0)
        {
            MessageBox.Show(this, "Select a customer with at least one rig, or add a rig first.", "Add License", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new NewLicenseDialog(CurrentWorkspace.Rigs) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.SelectedRig is null)
            return;

        try
        {
            var expiry = dialog.ExpiryDate;
            await RequireStore().CreateLicenseAsync(
                dialog.SelectedRig.RigId,
                $"{dialog.TermAmount}-{dialog.TermUnit}",
                AtUtcMidnight(dialog.StartDate),
                AtUtcMidnight(expiry),
                dialog.MaxDevices);
            await RefreshCustomersAsync(CurrentWorkspace.Customer.CustomerId);
            DetailsTabs.SelectedIndex = 1;
            StatusText.Text = "License record added as suspended; no signed entitlement issued";
        }
        catch (Exception ex)
        {
            ShowSaveError(ex);
        }
    }

    private async void RecordPayment_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentWorkspace is null || CurrentWorkspace.Licenses.Count == 0)
        {
            MessageBox.Show(this, "Select a customer with at least one license first.", "Record Payment", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new RecordPaymentDialog(CurrentWorkspace.Licenses) { Owner = this };
        if (dialog.ShowDialog() != true)
            return;

        try
        {
            await RequireStore().CreatePaymentAsync(dialog.OrderReference, dialog.LicenseId, dialog.AmountMinor,
                dialog.Currency, dialog.Processor, dialog.Status, dialog.PaidAtUtc);
            await LoadWorkspaceAsync(SelectedCustomerId!.Value);
            DetailsTabs.SelectedIndex = 7;
            StatusText.Text = "Payment recorded";
        }
        catch (Exception ex)
        {
            ShowSaveError(ex);
        }
    }

    private void Help_Click(object sender, RoutedEventArgs e)
    {
        new LicenseHelpDialog { Owner = this }.ShowDialog();
    }

    private async void ImportRequest_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import signed DrillIntel activation request",
            Filter = "DrillIntel activation request (*.jws)|*.jws|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var compactJws = await File.ReadAllTextAsync(dialog.FileName);
            var request = await RequireStore().ImportActivationRequestAsync(compactJws, dialog.FileName);
            if (request.Status == "duplicate")
            {
                MessageBox.Show(this,
                    $"This device key already has a request for license {request.LicenseId}. The request was recorded as a duplicate and did not use another device slot.",
                    "Duplicate Activation Request", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            if (SelectedCustomerId is { } customerId)
                await RefreshCustomersAsync(customerId);
            DetailsTabs.SelectedIndex = 6;
            StatusText.Text = request.Status == "pending" ? "Signed activation request verified and imported" : "Duplicate request recorded";
            MessageBox.Show(this,
                $"Request signature verified and imported.\n\nLicense: {request.LicenseId}\nRequest ID: {request.RequestId}\nDevice key hash: {request.KeyHash}\nKey protection: {request.KeyProtection}\nStatus: {request.Status}\n\nSelect this pending row and choose Issue Activation Package to issue a customer license. The Dev Test Bundle action is for Debug testing only.",
                "Activation Request Imported", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could Not Import Activation Request", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SetupProductionKey_Click(object sender, RoutedEventArgs e)
    {
        if (File.Exists(ProductionIssuerKeyPaths.PrivateKeyPath))
        {
            MessageBox.Show(this,
                $"A production signing key already exists at:\n{ProductionIssuerKeyPaths.PrivateKeyPath}\n\nThe key will not be overwritten.",
                "Production Signing Key", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dialog = new ProductionIssuerKeySetupDialog { Owner = this };
        if (dialog.ShowDialog() == true)
            StatusText.Text = "Production signing key created; configure its public key in a Release DrillIntel build";
    }

    private async void RejectActivationRequest_Click(object sender, RoutedEventArgs e)
    {
        if (RequestInboxGrid.SelectedItem is not PendingRequestRecord pending || pending.Status != "pending")
        {
            MessageBox.Show(this, "Select a pending request in Request Inbox first.", "Reject / Archive Request",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new RejectActivationRequestDialog(pending.RequestId) { Owner = this };
        if (dialog.ShowDialog() != true)
            return;

        try
        {
            await RequireStore().RejectActivationRequestAsync(pending.RequestId, dialog.Reason);
            await RefreshCustomersAsync(SelectedCustomerId);
            DetailsTabs.SelectedIndex = 5;
            StatusText.Text = "Request rejected and archived in Requests history";
            MessageBox.Show(this,
                "The request was removed from the pending inbox and retained in Requests history and the Audit Log.",
                "Request Archived", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could Not Reject / Archive Request",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void IssueActivationPackage_Click(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(ProductionIssuerKeyPaths.PrivateKeyPath))
        {
            MessageBox.Show(this, "Set up the production signing key first.", "Issue Activation Package",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (RequestInboxGrid.SelectedItem is not PendingRequestRecord pending || pending.Status != "pending")
        {
            MessageBox.Show(this, "Select a pending request in Request Inbox first.", "Issue Activation Package",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (CurrentWorkspace is null)
            return;
        var license = CurrentWorkspace.Licenses.FirstOrDefault(x => x.LicenseId == pending.LicenseId);
        if (license is null)
        {
            MessageBox.Show(this, "Select the customer that owns this license before issuing the package.",
                "Issue Activation Package", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (CurrentWorkspace.Devices.Count(d => d.LicenseId == pending.LicenseId && d.Status == "active") >= license.MaxDevices)
        {
            MessageBox.Show(this, "This license has reached its maximum active PC count.", "Issue Activation Package",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var outputDialog = new SaveFileDialog
        {
            Title = "Save the activation package to send to the customer",
            Filter = "DrillIntel license activation package (*.dillicense)|*.dillicense",
            FileName = $"{pending.LicenseId}-{pending.KeyHash[..8]}.dillicense",
            DefaultExt = ".dillicense",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (outputDialog.ShowDialog(this) != true)
            return;

        var passwordDialog = new IssuerKeyPasswordDialog { Owner = this };
        if (passwordDialog.ShowDialog() != true)
            return;

        string? archivePath = null;
        var issuanceCommitted = false;
        try
        {
            var request = ActivationRequestVerifier.Verify(await File.ReadAllTextAsync(pending.SourceFile));
            if (request.RequestId != pending.RequestId || request.LicenseId != pending.LicenseId ||
                !StringComparer.OrdinalIgnoreCase.Equals(request.DevicePublicKeyHash, pending.KeyHash))
                throw new CryptographicException("The request file no longer matches the selected pending register row.");

            var now = DateTimeOffset.UtcNow;
            var startUtc = DateTimeOffset.Parse(license.StartUtc, CultureInfo.InvariantCulture).ToUniversalTime();
            var expiryUtc = DateTimeOffset.Parse(license.ExpiryUtc, CultureInfo.InvariantCulture).ToUniversalTime();
            if (now < startUtc || now >= expiryUtc)
                throw new InvalidOperationException("The license is outside its valid start and expiry dates.");
            if (license.Status == "revoked")
                throw new InvalidOperationException("This license is revoked and cannot activate another PC.");

            var keyPassword = passwordDialog.Password;
            passwordDialog.ClearPassword();
            using var issuerKey = ProductionIssuerKeyFile.Open(ProductionIssuerKeyPaths.PrivateKeyPath, keyPassword);
            var revision = Math.Max(1, license.CurrentRevision + 1);
            var entitlement = new Entitlement
            {
                LicenseId = license.LicenseId,
                Revision = revision,
                IssuedAtUtc = now,
                Customer = CurrentWorkspace.Customer.Name,
                Rig = license.RigName,
                Plan = license.Plan,
                StartUtc = startUtc,
                ExpiryUtc = expiryUtc,
                MaxDevices = license.MaxDevices
            };
            var activation = new Activation
            {
                LicenseId = license.LicenseId,
                DevicePublicKeyHash = request.DevicePublicKeyHash,
                ActivatedAtUtc = now,
                ActivationExpiryUtc = expiryUtc
            };
            var entitlementJws = issuerKey.Sign(entitlement);
            var activationJws = issuerKey.Sign(activation);
            var archiveDirectory = Path.Combine(ProductionIssuerKeyPaths.DirectoryPath, "IssuedPackages");
            Directory.CreateDirectory(archiveDirectory);
            archivePath = Path.Combine(archiveDirectory,
                $"{license.LicenseId}-{request.DevicePublicKeyHash[..8]}-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}.dillicense");
            using (var file = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
            {
                WriteZipEntry(archive, "entitlement.jws", entitlementJws);
                WriteZipEntry(archive, "activation.jws", activationJws);
                WriteZipEntry(archive, "package-info.json", System.Text.Json.JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    licenseId = license.LicenseId,
                    requestId = request.RequestId,
                    revision,
                    keyId = issuerKey.KeyId,
                    issuedAtUtc = now
                }));
            }

            var entitlementHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(entitlementJws)));
            var activationHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(activationJws)));
            await RequireStore().RecordProductionIssuanceAsync(request, revision, issuerKey.KeyId, now, expiryUtc,
                archivePath, entitlementHash, activationHash);
            issuanceCommitted = true;

            var outputPath = Path.GetFullPath(outputDialog.FileName);
            if (!StringComparer.OrdinalIgnoreCase.Equals(outputPath, archivePath))
                File.Copy(archivePath, outputPath, overwrite: true);
            await RefreshCustomersAsync(SelectedCustomerId);
            DetailsTabs.SelectedIndex = 6;
            StatusText.Text = "Production activation package issued and register updated";
            MessageBox.Show(this,
                $"Activation package created and recorded.\n\nLicense: {license.LicenseId}\nPC fingerprint: {request.DevicePublicKeyHash}\nRevision: {revision}\n\nSend this package to the customer:\n{outputPath}\n\nArchived copy:\n{archivePath}",
                "Activation Package Issued", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            if (!issuanceCommitted && archivePath is not null && File.Exists(archivePath))
                File.Delete(archivePath);
            MessageBox.Show(this, ex.Message, "Could Not Issue Activation Package", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void IssueDevelopmentBundle_Click(object sender, RoutedEventArgs e)
    {
#if !DEBUG
        MessageBox.Show(this, "Development signing is disabled in Release builds.", "Development Only",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
#else
        if (RequestInboxGrid.SelectedItem is not PendingRequestRecord pending || pending.Status != "pending")
        {
            MessageBox.Show(this, "Select a pending request in the Request Inbox first.", "Issue Test Bundle",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (CurrentWorkspace is null)
            return;

        var license = CurrentWorkspace.Licenses.FirstOrDefault(x => x.LicenseId == pending.LicenseId);
        if (license is null)
        {
            MessageBox.Show(this, "The request's license is not in the currently selected customer workspace.",
                "Issue Test Bundle", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        if (CurrentWorkspace.Devices.Count(d => d.LicenseId == pending.LicenseId && d.Status == "active") >= license.MaxDevices)
        {
            MessageBox.Show(this, "The license has reached its device limit.", "Issue Test Bundle",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Save development-only test bundle",
            Filter = "DrillIntel development test bundle (*.diltest)|*.diltest",
            FileName = $"{pending.LicenseId}-{pending.KeyHash[..8]}-DEV-TEST.diltest",
            DefaultExt = ".diltest",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var requestJws = File.ReadAllText(pending.SourceFile);
            var request = ActivationRequestVerifier.Verify(requestJws);
            if (request.RequestId != pending.RequestId || request.LicenseId != pending.LicenseId || request.DevicePublicKeyHash != pending.KeyHash)
                throw new CryptographicException("The source request no longer matches the pending register entry.");

            var now = DateTimeOffset.UtcNow;
            var licenseStart = DateTimeOffset.Parse(license.StartUtc, CultureInfo.InvariantCulture).ToUniversalTime();
            var licenseExpiry = DateTimeOffset.Parse(license.ExpiryUtc, CultureInfo.InvariantCulture).ToUniversalTime();
            var activationExpiry = now.AddDays(90) < licenseExpiry ? now.AddDays(90) : licenseExpiry;
            var entitlement = new Entitlement
            {
                LicenseId = license.LicenseId,
                Revision = Math.Max(1, license.CurrentRevision + 1),
                IssuedAtUtc = now,
                Customer = CurrentWorkspace.Customer.Name,
                Rig = license.RigName,
                Plan = license.Plan,
                StartUtc = licenseStart,
                ExpiryUtc = licenseExpiry,
                MaxDevices = license.MaxDevices
            };
            var activation = new Activation
            {
                LicenseId = license.LicenseId,
                DevicePublicKeyHash = request.DevicePublicKeyHash,
                ActivatedAtUtc = now,
                ActivationExpiryUtc = activationExpiry
            };

            using var issuerKey = DevelopmentTestIssuer.CreateEphemeralTestKey();
            var entitlementJws = DevelopmentTestIssuer.Sign(entitlement, issuerKey);
            var activationJws = DevelopmentTestIssuer.Sign(activation, issuerKey);
            using (var file = new FileStream(dialog.FileName, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
            {
                WriteZipEntry(archive, "entitlement.jws", entitlementJws);
                WriteZipEntry(archive, "activation.jws", activationJws);
                WriteZipEntry(archive, "issuer-public.spki", Convert.ToBase64String(issuerKey.ExportSubjectPublicKeyInfo()));
                WriteZipEntry(archive, "DEVELOPMENT-ONLY.txt",
                    "Signed with the public development test key. Never use this bundle or key for customer production licenses.");
            }

            StatusText.Text = "Development-only test bundle exported; production status unchanged";
            MessageBox.Show(this,
                $"Development test bundle exported:\n{dialog.FileName}\n\nThis uses public test signing material and does not activate a production license or update the register's production issuance records. In the Debug DrillIntel build, click Import Dev Test Bundle and select this file.",
                "Development Test Bundle", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could Not Issue Development Test Bundle", MessageBoxButton.OK, MessageBoxImage.Error);
        }
#endif
    }

    private static void WriteZipEntry(ZipArchive archive, string name, string contents)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(contents);
    }

    private async void CustomersGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_loadingCustomers || CustomersGrid.SelectedItem is not CustomerSummary selected || _store is null)
            return;

        try
        {
            await LoadWorkspaceAsync(selected.CustomerId);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Could not load customer details";
            MessageBox.Show(this, ex.Message, "License Register", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task RefreshCustomersAsync(long? selectCustomerId = null)
    {
        if (_store is null)
            return;

        var customers = await _store.GetCustomersAsync();
        var previousCustomerId = SelectedCustomerId;
        _loadingCustomers = true;
        try
        {
            CustomersGrid.ItemsSource = new ObservableCollection<CustomerSummary>(customers);
            ApplyGridFilter(nameof(CustomersGrid));
            CustomerCountText.Text = customers.Count == 1 ? "1 customer" : $"{customers.Count} customers";
            CustomersGrid.SelectedItem = selectCustomerId is { } id
                ? customers.FirstOrDefault(c => c.CustomerId == id)
                : customers.FirstOrDefault(c => c.CustomerId == previousCustomerId);
        }
        finally
        {
            _loadingCustomers = false;
        }

        if (CustomersGrid.SelectedItem is CustomerSummary selected)
            await LoadWorkspaceAsync(selected.CustomerId);
        else
            ClearWorkspace();
    }

    private async Task LoadWorkspaceAsync(long customerId)
    {
        if (_store is null)
            return;
        var workspace = await _store.GetWorkspaceAsync(customerId);
        CurrentWorkspace = workspace;
        if (workspace is null)
        {
            ClearWorkspace();
            return;
        }

        SelectedCustomerName.Text = workspace.Customer.Name;
        SelectedCustomerContact.Text = string.IsNullOrWhiteSpace(workspace.Customer.Contact)
            ? "No contact recorded"
            : workspace.Customer.Contact;
        SelectedCustomerNotes.Text = workspace.Customer.Notes ?? string.Empty;
        RigCountValue.Text = workspace.Rigs.Count.ToString();
        LicenseCountValue.Text = workspace.Licenses.Count.ToString();
        RigsGrid.ItemsSource = workspace.Rigs;
        LicensesGrid.ItemsSource = workspace.Licenses;
        DevicesGrid.ItemsSource = workspace.Devices;
        RevisionsGrid.ItemsSource = workspace.EntitlementRevisions;
        ActivationFilesGrid.ItemsSource = workspace.ActivationFiles;
        RequestsGrid.ItemsSource = workspace.Requests;
        RequestInboxGrid.ItemsSource = workspace.RequestInbox;
        PaymentsGrid.ItemsSource = workspace.Payments;
        AuditGrid.ItemsSource = workspace.AuditLog;
        ApplyAllGridFilters();
    }

    private void ClearWorkspace()
    {
        CurrentWorkspace = null;
        SelectedCustomerName.Text = "Select a customer";
        SelectedCustomerContact.Text = "Customer contact details appear here";
        SelectedCustomerNotes.Text = string.Empty;
        RigCountValue.Text = "0";
        LicenseCountValue.Text = "0";
        CopyLicenseIdButton.IsEnabled = false;
        RigsGrid.ItemsSource = null;
        LicensesGrid.ItemsSource = null;
        DevicesGrid.ItemsSource = null;
        RevisionsGrid.ItemsSource = null;
        ActivationFilesGrid.ItemsSource = null;
        RequestsGrid.ItemsSource = null;
        RequestInboxGrid.ItemsSource = null;
        PaymentsGrid.ItemsSource = null;
        AuditGrid.ItemsSource = null;
    }

    private void GridFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox filter || filter.Tag is not string gridName)
            return;
        _gridFilters[gridName] = filter.Text.Trim();
        ApplyGridFilter(gridName);
    }

    private void ApplyAllGridFilters()
    {
        ApplyGridFilter(nameof(CustomersGrid));
        ApplyGridFilter(nameof(RigsGrid));
        ApplyGridFilter(nameof(LicensesGrid));
        ApplyGridFilter(nameof(DevicesGrid));
        ApplyGridFilter(nameof(RevisionsGrid));
        ApplyGridFilter(nameof(ActivationFilesGrid));
        ApplyGridFilter(nameof(RequestsGrid));
        ApplyGridFilter(nameof(RequestInboxGrid));
        ApplyGridFilter(nameof(PaymentsGrid));
        ApplyGridFilter(nameof(AuditGrid));
    }

    private void ApplyGridFilter(string gridName)
    {
        if (FindName(gridName) is not DataGrid grid || grid.ItemsSource is null)
            return;

        var filterText = _gridFilters.GetValueOrDefault(gridName, string.Empty);
        var view = CollectionViewSource.GetDefaultView(grid.ItemsSource);
        if (view is null)
            return;
        view.Filter = item => MatchesFilter(item, filterText);
        view.Refresh();
    }

    private static bool MatchesFilter(object? item, string filterText)
    {
        if (string.IsNullOrWhiteSpace(filterText) || item is null)
            return true;

        foreach (var property in item.GetType().GetProperties())
        {
            var value = property.GetValue(item);
            if (value is not null && Convert.ToString(value, CultureInfo.CurrentCulture)
                    ?.Contains(filterText, StringComparison.CurrentCultureIgnoreCase) == true)
                return true;
        }
        return false;
    }

    private void LicensesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        CopyLicenseIdButton.IsEnabled = LicensesGrid.SelectedItem is LicenseRecord;
    }

    private void CopySelectedLicenseId_Click(object sender, RoutedEventArgs e)
    {
        if (LicensesGrid.SelectedItem is LicenseRecord record && !string.IsNullOrWhiteSpace(record.LicenseId))
        {
            SetClipboardText(record.LicenseId, "License ID");
        }
    }

    private void RowCopyLicenseId_Click(object sender, RoutedEventArgs e)
    {
        string? licenseId = null;
        if (sender is FrameworkElement fe)
        {
            licenseId = fe.Tag as string ?? (fe.DataContext as LicenseRecord)?.LicenseId;
        }

        if (!string.IsNullOrWhiteSpace(licenseId))
        {
            SetClipboardText(licenseId, "License ID");
        }
    }

    private void LicensesGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (LicensesGrid.SelectedItem is LicenseRecord record && !string.IsNullOrWhiteSpace(record.LicenseId))
            {
                SetClipboardText(record.LicenseId, "License ID");
                e.Handled = true; // Overrides default DataGrid row-copy (TSV) behavior!
            }
        }
    }

    private void LicensesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LicensesGrid.SelectedItem is LicenseRecord record && !string.IsNullOrWhiteSpace(record.LicenseId))
        {
            SetClipboardText(record.LicenseId, "License ID");
        }
    }

    private void GenericGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (TryCopyLicenseIdFromItem((sender as DataGrid)?.SelectedItem))
            {
                e.Handled = true;
            }
        }
    }

    private void CopyLicenseIdFromContextMenu_Click(object sender, RoutedEventArgs e)
    {
        var grid = GetCurrentTabActiveGrid();
        if (grid != null && TryCopyLicenseIdFromItem(grid.SelectedItem))
        {
            return;
        }
        if (LicensesGrid.SelectedItem is LicenseRecord rec && !string.IsNullOrWhiteSpace(rec.LicenseId))
        {
            SetClipboardText(rec.LicenseId, "License ID");
        }
    }

    private void CopyFullRowFromContextMenu_Click(object sender, RoutedEventArgs e)
    {
        var grid = GetCurrentTabActiveGrid();
        if (grid?.SelectedItem is object item)
        {
            var text = FormatRowAsTsv(item);
            if (!string.IsNullOrWhiteSpace(text))
            {
                SetClipboardText(text, "Row data");
            }
        }
    }

    private void DataGridRow_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow row)
        {
            row.IsSelected = true;
            row.Focus();
        }
    }

    private DataGrid? GetCurrentTabActiveGrid()
    {
        if (DetailsTabs.SelectedContent is DataGrid dg)
            return dg;
        if (DetailsTabs.SelectedContent is DockPanel dp)
            return dp.Children.OfType<DataGrid>().FirstOrDefault();
        return LicensesGrid;
    }

    private bool TryCopyLicenseIdFromItem(object? item)
    {
        if (item is null) return false;
        var licenseId = item switch
        {
            LicenseRecord l => l.LicenseId,
            DeviceRecord d => d.LicenseId,
            EntitlementRevisionRecord r => r.LicenseId,
            ActivationFileRecord a => a.LicenseId,
            RequestRecord req => req.LicenseId,
            PendingRequestRecord preq => preq.LicenseId,
            AuditRecord aud => aud.LicenseId,
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(licenseId))
        {
            SetClipboardText(licenseId, "License ID");
            return true;
        }
        return false;
    }

    private static string FormatRowAsTsv(object item)
    {
        if (item is LicenseRecord l)
            return $"{l.LicenseId}\t{l.RigName}\t{l.Plan}\t{l.Status}\t{l.MaxDevices}\t{l.StartUtc}\t{l.ExpiryUtc}\tRev {l.CurrentRevision}";
        var props = item.GetType().GetProperties();
        return string.Join("\t", props.Select(p => p.GetValue(item)?.ToString() ?? string.Empty));
    }

    private void SetClipboardText(string text, string description)
    {
        try
        {
            Clipboard.SetDataObject(text, true);
            StatusText.Text = $"Copied {description}: {text}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Could not copy to clipboard: {ex.Message}";
        }
    }

    private long? SelectedCustomerId => (CustomersGrid.SelectedItem as CustomerSummary)?.CustomerId;
    private CustomerWorkspace? CurrentWorkspace { get; set; }
    private SqliteLicenseRegisterStore RequireStore() => _store ?? throw new InvalidOperationException("The license register is not open.");

    private static DateTimeOffset AtUtcMidnight(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

    private void ShowSaveError(Exception exception) =>
        MessageBox.Show(this, exception.Message, "Could not save register record", MessageBoxButton.OK, MessageBoxImage.Error);
}
