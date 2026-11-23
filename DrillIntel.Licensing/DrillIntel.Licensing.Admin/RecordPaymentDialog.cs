using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DrillIntel.Licensing.Issuer.Register;

namespace DrillIntel.Licensing.Admin;

internal sealed class RecordPaymentDialog : Window
{
    private sealed record LicenseChoice(string LicenseId, string Label);

    private readonly ComboBox _license;
    private readonly TextBox _orderRef;
    private readonly TextBox _amount;
    private readonly TextBox _currency;
    private readonly TextBox _processor;
    private readonly ComboBox _status;
    private readonly DatePicker _paidAt;

    public RecordPaymentDialog(IReadOnlyList<LicenseRecord> licenses)
    {
        Title = "Record Payment";
        Width = 500;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = Brushes.White;

        _license = new ComboBox
        {
            ItemsSource = licenses.Select(l => new LicenseChoice(l.LicenseId, $"{l.RigName} · {l.Plan} · {l.LicenseId}")),
            DisplayMemberPath = nameof(LicenseChoice.Label),
            SelectedIndex = 0,
            MinHeight = 34
        };
        _orderRef = MakeTextBox();
        _amount = MakeTextBox();
        _currency = MakeTextBox("INR");
        _processor = MakeTextBox("Manual");
        _status = new ComboBox { ItemsSource = new[] { "paid", "refunded", "chargeback" }, SelectedIndex = 0, MinHeight = 34 };
        _paidAt = new DatePicker { SelectedDate = DateTime.Today, MinHeight = 34 };

        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock { Text = "Record Payment", FontSize = 21, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
        panel.Children.Add(new TextBlock
        {
            Text = "Enter a payment reference and amount. Amounts are stored in the currency's minor units; card details must never be entered here.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.DimGray,
            Margin = new Thickness(0, 0, 0, 8)
        });
        AddField(panel, "License", _license);
        AddField(panel, "Order / transaction reference", _orderRef);
        AddField(panel, "Amount", _amount);
        AddField(panel, "Currency (3-letter code)", _currency);
        AddField(panel, "Processor / payment method", _processor);
        AddField(panel, "Status", _status);
        AddField(panel, "Paid date (UTC date)", _paidAt);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        buttons.Children.Add(new Button { Content = "Cancel", MinWidth = 88, IsCancel = true, Margin = new Thickness(0, 0, 8, 0) });
        var save = new Button { Content = "Save Payment", MinWidth = 116, IsDefault = true, Background = Brushes.SteelBlue, Foreground = Brushes.White };
        save.Click += Save_Click;
        buttons.Children.Add(save);
        panel.Children.Add(buttons);
        Content = panel;
    }

    public string LicenseId => (_license.SelectedItem as LicenseChoice)?.LicenseId ?? string.Empty;
    public string OrderReference => _orderRef.Text.Trim();
    public string Currency => _currency.Text.Trim().ToUpperInvariant();
    public string Processor => _processor.Text.Trim();
    public string Status => _status.SelectedItem?.ToString() ?? string.Empty;
    public DateTimeOffset? PaidAtUtc => _paidAt.SelectedDate is { } date
        ? new DateTimeOffset(DateTime.SpecifyKind(date.Date, DateTimeKind.Utc))
        : null;

    public long AmountMinor
    {
        get
        {
            if (!decimal.TryParse(_amount.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var value))
                return -1;
            try { return checked((long)decimal.Round(value * 100m, 0, MidpointRounding.AwayFromZero)); }
            catch (OverflowException) { return -1; }
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(LicenseId) || string.IsNullOrWhiteSpace(OrderReference) ||
            OrderReference.Length > 100 || AmountMinor < 0 || (Status == "paid" && AmountMinor == 0) ||
            Currency.Length != 3 || !Currency.All(char.IsAsciiLetter) ||
            string.IsNullOrWhiteSpace(Processor) || Processor.Length > 80 || _paidAt.SelectedDate is null)
        {
            MessageBox.Show(this, "Enter a license, unique reference, valid non-negative amount, three-letter currency, processor, status, and date. Paid amounts must be greater than zero.",
                Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }

    private static TextBox MakeTextBox(string? text = null) => new()
    {
        Text = text ?? string.Empty,
        MinHeight = 34,
        Padding = new Thickness(8),
        VerticalContentAlignment = VerticalAlignment.Center
    };

    private static void AddField(Panel panel, string label, UIElement input)
    {
        panel.Children.Add(new TextBlock { Text = label, Foreground = Brushes.DimGray, Margin = new Thickness(0, 7, 0, 4) });
        panel.Children.Add(input);
    }
}
