using System.Security.Cryptography;
using System.IO;
using System.IO.Compression;
using System.Windows;
using DrillIntel.Licensing.Client;
using DrillIntel.Licensing.Core.Verification;
using Microsoft.Win32;

namespace DrillIntel.Views;

public partial class LicenseActivationWindow : Window
{
    private DeviceKeySession? _deviceKey;

    public event Action<LicenseVerificationResult>? DevelopmentLicenseVerified;
    public event Action<LicenseVerificationResult>? ProductionLicenseVerified;

    public LicenseActivationWindow()
    {
        InitializeComponent();
#if !DEBUG
        ImportTestBundleButton.Visibility = Visibility.Collapsed;
#endif
        Closed += (_, _) => _deviceKey?.Dispose();
    }

    private async void ImportProductionPackage_Click(object sender, RoutedEventArgs e)
    {
        if (_deviceKey is null)
        {
            MessageBox.Show(this, "Choose Check / Create Key first so the package can be matched to this PC.",
                "License Package", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Import DrillIntel license activation package",
            Filter = "DrillIntel license package (*.dillicense)|*.dillicense|ZIP archive (*.zip)|*.zip|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        ImportProductionPackageButton.IsEnabled = false;
        FooterStatusText.Text = "Verifying license package";
        try
        {
            var result = await ProductionActivationStore.ImportAndVerifyAsync(
                dialog.FileName, _deviceKey.PublicKeyHash, DateTimeOffset.UtcNow);
            if (!result.IsValid)
            {
                FooterStatusText.Text = "License package rejected";
                MessageBox.Show(this, result.FailureReason ?? "The license package could not be verified.",
                    "License Package Rejected", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            LicenseIdTextBox.Text = result.LicenseId;
            ProductionLicenseVerified?.Invoke(result);
            FooterStatusText.Text = "Production license activated on this PC";
            MessageBox.Show(this,
                $"License activated successfully on this PC.\n\nLicense: {result.LicenseId}\nPlan: {result.Plan}\nValid until: {result.LicenseExpiryUtc?.ToLocalTime():yyyy-MM-dd}\nRevision: {result.AcceptedRevision}\nDevice fingerprint: {_deviceKey.PublicKeyHash}",
                "License Activated", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            FooterStatusText.Text = "Could not import license package";
            MessageBox.Show(this, ex.Message, "Could Not Import License Package", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ImportProductionPackageButton.IsEnabled = true;
        }
    }

    private async void CheckDeviceKey_Click(object sender, RoutedEventArgs e)
    {
        CheckDeviceKeyButtonEnabled(false);
        KeyStatusText.Text = "Checking TPM and software key providers…";
        FooterStatusText.Text = "Checking device key";
        try
        {
            _deviceKey?.Dispose();
            _deviceKey = await Task.Run(DeviceKeyStore.OpenOrCreate);
            KeyHashTextBox.Text = _deviceKey.PublicKeyHash;
            ExportRequestButton.IsEnabled = true;
            if (_deviceKey.KeyProtection == "tpm")
            {
                KeyStatusText.Text = "TPM-backed, non-exportable device key is ready.";
                FooterStatusText.Text = "TPM key ready";
            }
            else
            {
                KeyStatusText.Text = "Software CNG fallback is ready. " + _deviceKey.FallbackReason;
                FooterStatusText.Text = "Software fallback key ready";
                MessageBox.Show(this,
                    "A TPM key could not be used on this PC. A non-exportable software CNG key was created instead. " +
                    "The key protection value is informational; it does not provide TPM-equivalent protection.",
                    "Software Key Fallback", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex) when (ex is CryptographicException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            _deviceKey?.Dispose();
            _deviceKey = null;
            ExportRequestButton.IsEnabled = false;
            KeyHashTextBox.Clear();
            KeyStatusText.Text = "Could not open or create a device key.";
            FooterStatusText.Text = "Device key unavailable";
            MessageBox.Show(this,
                ex.Message + "\n\nMachine-wide CNG keys may require administrator permission for the initial creation. " +
                "If this is a new PC, close DrillIntel, run it once as Administrator, and then retry from the normal crew account.",
                "Device Key Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            CheckDeviceKeyButtonEnabled(true);
        }
    }

    private void ExportRequest_Click(object sender, RoutedEventArgs e)
    {
        if (_deviceKey is null)
        {
            MessageBox.Show(this, "Check or create a device key first.", "Activation Request", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (string.IsNullOrWhiteSpace(LicenseIdTextBox.Text))
        {
            MessageBox.Show(this, "Enter the License ID from your office before exporting the request.", "Activation Request", MessageBoxButton.OK, MessageBoxImage.Information);
            LicenseIdTextBox.Focus();
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export signed activation request",
            Filter = "DrillIntel activation request (*.jws)|*.jws|All files (*.*)|*.*",
            FileName = $"DrillIntel-activation-request-{DateTime.UtcNow:yyyyMMdd-HHmmss}.jws",
            DefaultExt = ".jws",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var request = ActivationRequestFileExporter.Export(
                dialog.FileName,
                LicenseIdTextBox.Text.Trim(),
                _deviceKey,
                DateTimeOffset.UtcNow);
            FooterStatusText.Text = $"Request exported · {request.RequestId}";
            MessageBox.Show(this,
                $"Activation request exported:\n{dialog.FileName}\n\nSend this file to your office licensing administrator.",
                "Request Exported", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (IOException ex)
        {
            MessageBox.Show(this, ex.Message, "Could Not Export Request", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (CryptographicException ex)
        {
            MessageBox.Show(this, ex.Message, "Could Not Sign Request", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ImportTestBundle_Click(object sender, RoutedEventArgs e)
    {
        if (_deviceKey is null)
        {
            MessageBox.Show(this, "Click Check / Create Key first so DrillIntel can match the activation to this PC.",
                "Development Bundle", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Import development test license bundle",
            Filter = "DrillIntel development bundle (*.diltest)|*.diltest|ZIP archive (*.zip)|*.zip|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            ImportTestBundleButton.IsEnabled = false;
            using var archive = ZipFile.OpenRead(dialog.FileName);
            var entitlement = await ReadArchiveTextAsync(archive, "entitlement.jws");
            var activation = await ReadArchiveTextAsync(archive, "activation.jws");
            var issuerPublicKey = Convert.FromBase64String(await ReadArchiveTextAsync(archive, "issuer-public.spki"));
            var result = await DevelopmentBundleVerifier.VerifyAsync(
                entitlement, activation, issuerPublicKey, _deviceKey.PublicKeyHash, DateTimeOffset.UtcNow);
            if (!result.IsValid)
            {
                FooterStatusText.Text = "Development bundle rejected";
                MessageBox.Show(this, result.FailureReason ?? "The bundle is invalid.",
                    "Development Bundle Rejected", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DevelopmentLicenseBundleStore.SaveVerifiedBundle(dialog.FileName);
            LicenseIdTextBox.Text = result.LicenseId;
            DevelopmentLicenseVerified?.Invoke(result);
            FooterStatusText.Text = "Test bundle signature and device binding verified";
            MessageBox.Show(this,
                $"Development test activation verified for this PC.\n\nLicense: {result.LicenseId}\nPlan: {result.Plan}\nLicense valid until: {result.LicenseExpiryUtc?.ToLocalTime():yyyy-MM-dd}\nThis PC activation valid until: {result.ActivationExpiryUtc?.ToLocalTime():yyyy-MM-dd}\nRevision: {result.AcceptedRevision}\nDevice fingerprint: {_deviceKey.PublicKeyHash}\n\nThis confirms the signing and verification path only. The dev key is public test material and must never be used for production licenses.",
                "Development Activation Verified", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or CryptographicException or FormatException)
        {
            FooterStatusText.Text = "Could not read development bundle";
            MessageBox.Show(this, ex.Message, "Could Not Import Development Bundle", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ImportTestBundleButton.IsEnabled = true;
        }
    }

    private static async Task<string> ReadArchiveTextAsync(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName) ?? throw new InvalidDataException($"Bundle is missing {entryName}.");
        if (entry.Length > 128 * 1024)
            throw new InvalidDataException($"Bundle entry {entryName} is unexpectedly large.");
        await using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void CheckDeviceKeyButtonEnabled(bool enabled) => CheckDeviceKeyButton.IsEnabled = enabled;
}
