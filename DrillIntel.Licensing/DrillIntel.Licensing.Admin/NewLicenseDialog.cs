using System.Windows;
using System.Windows.Controls;
using DrillIntel.Licensing.Issuer.Register;

namespace DrillIntel.Licensing.Admin;

internal sealed class NewLicenseDialog : Window
{
    private readonly ComboBox _rigCombo;
    private readonly ComboBox _planCombo;
    private readonly DatePicker _startPicker;
    private readonly TextBox _deviceLimit;

    public NewLicenseDialog(IReadOnlyList<RigRecord> rigs)
    {
        Title = "Add License Record";
        Width = 470;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = System.Windows.Media.Brushes.White;

        _rigCombo = new ComboBox { ItemsSource = rigs, DisplayMemberPath = nameof(RigRecord.RigName), SelectedValuePath = nameof(RigRecord.RigId), MinHeight = 34 };
        _rigCombo.SelectedIndex = 0;
        _planCombo = new ComboBox { ItemsSource = new[] { "4-month", "6-month", "12-month" }, MinHeight = 34, SelectedIndex = 1 };
        _startPicker = new DatePicker { SelectedDate = DateTime.Today, MinHeight = 34 };
        _deviceLimit = new TextBox { Text = "3", MinHeight = 34, Padding = new Thickness(8), VerticalContentAlignment = VerticalAlignment.Center };

        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock { Text = "Add License Record", FontSize = 21, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
        panel.Children.Add(new TextBlock
        {
            Text = "This creates a suspended register record. A signed entitlement and activation are still required to unlock a PC.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = System.Windows.Media.Brushes.DimGray,
            Margin = new Thickness(0, 0, 0, 12)
        });
        AddField(panel, "Rig", _rigCombo);
        AddField(panel, "Plan term", _planCombo);
        AddField(panel, "Start date (UTC date)", _startPicker);
        AddField(panel, "Maximum PCs", _deviceLimit);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        buttons.Children.Add(new Button { Content = "Cancel", MinWidth = 88, IsCancel = true });
        var save = new Button { Content = "Create Record", MinWidth = 116, IsDefault = true, Background = System.Windows.Media.Brushes.SteelBlue, Foreground = System.Windows.Media.Brushes.White };
        save.Click += Save_Click;
        buttons.Children.Add(save);
        panel.Children.Add(buttons);
        Content = panel;
    }

    public RigRecord? SelectedRig => _rigCombo.SelectedItem as RigRecord;
    public int TermMonths => _planCombo.SelectedIndex switch { 0 => 4, 1 => 6, 2 => 12, _ => 0 };
    public DateTime StartDate => _startPicker.SelectedDate?.Date ?? DateTime.Today;
    public int MaxDevices => int.TryParse(_deviceLimit.Text, out var value) ? value : 0;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRig is null || TermMonths == 0 || MaxDevices < 1 || _startPicker.SelectedDate is null)
        {
            MessageBox.Show(this, "Choose a rig and plan, then enter a valid start date and positive PC limit.", Title,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }

    private static void AddField(Panel panel, string label, UIElement input)
    {
        panel.Children.Add(new TextBlock { Text = label, Foreground = System.Windows.Media.Brushes.DimGray, Margin = new Thickness(0, 8, 0, 4) });
        panel.Children.Add(input);
    }
}
