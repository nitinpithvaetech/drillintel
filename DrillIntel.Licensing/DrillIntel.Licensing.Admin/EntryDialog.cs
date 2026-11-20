using System.Windows;
using System.Windows.Controls;

namespace DrillIntel.Licensing.Admin;

internal sealed record EntryField(string Label, bool Required = false, bool Multiline = false);

internal sealed class EntryDialog : Window
{
    private readonly TextBox[] _inputs;
    private readonly EntryField[] _fields;

    private EntryDialog(string title, EntryField[] fields)
    {
        Title = title;
        Width = 460;
        SizeToContent = SizeToContent.Height;
        MinHeight = 190;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = System.Windows.Media.Brushes.White;
        _fields = fields;
        _inputs = new TextBox[fields.Length];

        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 21,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 14)
        });

        for (var index = 0; index < fields.Length; index++)
        {
            var field = fields[index];
            panel.Children.Add(new TextBlock
            {
                Text = field.Label + (field.Required ? " *" : string.Empty),
                Foreground = System.Windows.Media.Brushes.DimGray,
                Margin = new Thickness(0, 6, 0, 4)
            });
            var input = new TextBox
            {
                MinHeight = field.Multiline ? 76 : 32,
                AcceptsReturn = field.Multiline,
                TextWrapping = field.Multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
                VerticalContentAlignment = field.Multiline ? VerticalAlignment.Top : VerticalAlignment.Center,
                Padding = new Thickness(8)
            };
            _inputs[index] = input;
            panel.Children.Add(input);
        }

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var cancel = new Button { Content = "Cancel", MinWidth = 88, IsCancel = true };
        var save = new Button { Content = "Save", MinWidth = 88, IsDefault = true, Background = System.Windows.Media.Brushes.SteelBlue, Foreground = System.Windows.Media.Brushes.White };
        save.Click += (_, _) =>
        {
            for (var i = 0; i < _fields.Length; i++)
            {
                if (_fields[i].Required && string.IsNullOrWhiteSpace(_inputs[i].Text))
                {
                    MessageBox.Show(this, $"{_fields[i].Label} is required.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                    _inputs[i].Focus();
                    return;
                }
            }
            DialogResult = true;
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        panel.Children.Add(buttons);
        Content = panel;
    }

    public static bool TryShow(Window owner, string title, EntryField[] fields, out string[] values)
    {
        var dialog = new EntryDialog(title, fields) { Owner = owner };
        var accepted = dialog.ShowDialog() == true;
        values = accepted ? dialog._inputs.Select(input => input.Text.Trim()).ToArray() : [];
        return accepted;
    }
}
