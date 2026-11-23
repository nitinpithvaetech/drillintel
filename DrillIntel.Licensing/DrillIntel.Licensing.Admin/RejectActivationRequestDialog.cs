using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DrillIntel.Licensing.Admin;

internal sealed class RejectActivationRequestDialog : Window
{
    private readonly TextBox _reason;

    public RejectActivationRequestDialog(string requestId)
    {
        Title = "Reject / Archive Activation Request";
        Width = 520;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = Brushes.White;

        _reason = new TextBox
        {
            MinHeight = 92,
            MaxHeight = 140,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxLength = 500,
            Padding = new Thickness(8)
        };

        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock
        {
            Text = "Remove this request from the pending inbox?",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(23, 32, 51)),
            Margin = new Thickness(0, 0, 0, 8)
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"Request ID: {requestId}\nThe request will be marked rejected/archived, retained in Requests history, and recorded in the Audit Log. This does not revoke a license or affect other requests.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
            Margin = new Thickness(0, 0, 0, 12)
        });
        panel.Children.Add(new TextBlock { Text = "Reason (required, up to 500 characters)", Margin = new Thickness(0, 0, 0, 5) });
        panel.Children.Add(_reason);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        buttons.Children.Add(new Button { Content = "Cancel", IsCancel = true, MinWidth = 88, Margin = new Thickness(0, 0, 8, 0) });
        var reject = new Button
        {
            Content = "Reject / Archive",
            IsDefault = true,
            MinWidth = 130,
            Background = new SolidColorBrush(Color.FromRgb(185, 28, 28)),
            Foreground = Brushes.White
        };
        reject.Click += Reject_Click;
        buttons.Children.Add(reject);
        panel.Children.Add(buttons);
        Content = panel;
    }

    public string Reason => _reason.Text.Trim();

    private void Reject_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_reason.Text))
        {
            MessageBox.Show(this, "Enter a reason so the decision is clear in the register history.", Title,
                MessageBoxButton.OK, MessageBoxImage.Information);
            _reason.Focus();
            return;
        }
        DialogResult = true;
    }
}
