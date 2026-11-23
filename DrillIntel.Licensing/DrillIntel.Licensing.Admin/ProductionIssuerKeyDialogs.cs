using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DrillIntel.Licensing.Issuer.Signing;
using Microsoft.Win32;

namespace DrillIntel.Licensing.Admin;

internal static class ProductionIssuerKeyPaths
{
    public static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DrillIntel", "Licensing", "Issuer");
    public static string PrivateKeyPath => Path.Combine(DirectoryPath, "production-issuer.dilkey");
    public static string PublicKeyPath => Path.Combine(DirectoryPath, "ProductionIssuerPublicKey.json");
}

internal sealed class ProductionIssuerKeySetupDialog : Window
{
    private readonly PasswordBox _password;
    private readonly PasswordBox _confirm;
    private readonly TextBlock _status;

    public ProductionIssuerKeySetupDialog()
    {
        Title = "Set Up Production Signing Key";
        Width = 650;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = Brushes.White;

        _password = new PasswordBox { MinHeight = 36, Padding = new Thickness(8), PasswordChar = '●' };
        _confirm = new PasswordBox { MinHeight = 36, Padding = new Thickness(8), PasswordChar = '●' };
        _status = new TextBlock { Foreground = Brushes.DimGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) };

        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock { Text = "One-time office setup", FontSize = 22, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
        panel.Children.Add(new TextBlock
        {
            Text = "This creates the production signing key used to issue customer licenses. It is saved encrypted in this Windows user's local application data and is never included in the installer or source repository. Keep the password and a secure backup of the encrypted key file. If the key is lost, previously issued licenses remain verifiable, but new files cannot be issued with this key.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.DimGray,
            Margin = new Thickness(0, 0, 0, 12)
        });
        panel.Children.Add(new TextBlock { Text = $"Private key file: {ProductionIssuerKeyPaths.PrivateKeyPath}", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.SlateGray, Margin = new Thickness(0, 0, 0, 10) });
        AddField(panel, "Password (at least 12 characters)", _password);
        AddField(panel, "Confirm password", _confirm);
        panel.Children.Add(_status);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        buttons.Children.Add(new Button { Content = "Cancel", IsCancel = true, MinWidth = 88, Margin = new Thickness(0, 0, 8, 0) });
        var create = new Button { Content = "Create Key & Export Public Key…", IsDefault = true, MinWidth = 220, Background = Brushes.SteelBlue, Foreground = Brushes.White };
        create.Click += Create_Click;
        buttons.Children.Add(create);
        panel.Children.Add(buttons);
        Content = panel;
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        if (_password.Password.Length < 12 || _password.Password != _confirm.Password)
        {
            MessageBox.Show(this, "Enter the same password in both fields. It must be at least 12 characters.", Title,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (File.Exists(ProductionIssuerKeyPaths.PrivateKeyPath))
        {
            MessageBox.Show(this, "A production signing key already exists here. It will not be overwritten.", Title,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            Directory.CreateDirectory(ProductionIssuerKeyPaths.DirectoryPath);
            var key = ProductionIssuerKeyFile.Create(ProductionIssuerKeyPaths.PrivateKeyPath, _password.Password);
            ProductionIssuerKeyFile.WritePublicKey(ProductionIssuerKeyPaths.PublicKeyPath, key);
            var saveDialog = new SaveFileDialog
            {
                Title = "Export public issuer key for the Release DrillIntel build",
                Filter = "Issuer public key config (*.json)|*.json",
                FileName = "ProductionIssuerPublicKey.json",
                InitialDirectory = ProductionIssuerKeyPaths.DirectoryPath,
                DefaultExt = ".json",
                AddExtension = true,
                OverwritePrompt = true
            };
            if (saveDialog.ShowDialog(this) != true)
            {
                _status.Text = $"Key created. Public key config saved here: {ProductionIssuerKeyPaths.PublicKeyPath}";
            }
            else
            {
                ProductionIssuerKeyFile.WritePublicKey(saveDialog.FileName, key);
                _status.Text = $"Key created. Public key config saved here: {saveDialog.FileName}";
            }

            MessageBox.Show(this,
                "The private key was created and saved encrypted. Back it up securely and keep its password separate. " +
                "Copy the public JSON config into DrillIntel.Licensing.Client as ProductionIssuerPublicKey.json, then rebuild and redistribute the Release installer. " +
                "The public key is safe to distribute; never distribute the .dilkey file or its password.",
                "Production Key Created", MessageBoxButton.OK, MessageBoxImage.Information);
            _password.Clear();
            _confirm.Clear();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could Not Create Signing Key", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void AddField(Panel panel, string label, UIElement input)
    {
        panel.Children.Add(new TextBlock { Text = label, Foreground = Brushes.DimGray, Margin = new Thickness(0, 7, 0, 4) });
        panel.Children.Add(input);
    }
}

internal sealed class IssuerKeyPasswordDialog : Window
{
    private readonly PasswordBox _password;

    public IssuerKeyPasswordDialog()
    {
        Title = "Unlock Production Signing Key";
        Width = 430;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = Brushes.White;
        _password = new PasswordBox { MinHeight = 36, Padding = new Thickness(8), PasswordChar = '●' };

        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock { Text = "Enter the signing key password", FontSize = 19, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) });
        panel.Children.Add(_password);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        buttons.Children.Add(new Button { Content = "Cancel", MinWidth = 88, IsCancel = true, Margin = new Thickness(0, 0, 8, 0) });
        var unlock = new Button { Content = "Continue", MinWidth = 100, IsDefault = true, Background = Brushes.SteelBlue, Foreground = Brushes.White };
        unlock.Click += (_, _) => { DialogResult = true; };
        buttons.Children.Add(unlock);
        panel.Children.Add(buttons);
        Content = panel;
    }

    public string Password => _password.Password;
    public void ClearPassword() => _password.Clear();
}
