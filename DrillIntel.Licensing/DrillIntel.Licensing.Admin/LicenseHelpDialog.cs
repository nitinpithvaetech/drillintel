using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DrillIntel.Licensing.Admin;

internal sealed class LicenseHelpDialog : Window
{
    public LicenseHelpDialog()
    {
        Title = "License Admin Help";
        Width = 660;
        Height = 690;
        MinWidth = 560;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brushes.White;

        var content = new StackPanel { Margin = new Thickness(24) };
        content.Children.Add(new TextBlock
        {
            Text = "How to provide a DrillIntel license",
            FontSize = 23,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(23, 32, 51)),
            Margin = new Thickness(0, 0, 0, 8)
        });
        content.Children.Add(new TextBlock
        {
            Text = "A license record is the office register entry. To activate a customer's PC, the customer device must also be enrolled using signed license and activation files.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
            Margin = new Thickness(0, 0, 0, 16)
        });

        AddStep(content, "1. Add the customer and rig", "Select Add Customer, then select that customer and choose Add Rig. Use the customer and rig names that should appear on the license.");
        AddStep(content, "2. Create a license record", "Select the customer, choose Add License, select the rig, enter the term in days or months, choose the UTC start date and maximum number of PCs, then create the record. The record starts as Suspended.");
        AddStep(content, "3. Receive the device request", "On the customer's PC, generate and send the signed activation request file (.jws) to your licensing administrator. In License Admin, choose Import .jws Request and select that file. Verify that the request belongs to the intended license and device.");
        AddStep(content, "4. Issue and return the files", "After the request is verified, the device needs a signed license entitlement and a device activation file. Return both files securely to the customer and have them import them in DrillIntel.");

        var note = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(255, 247, 237)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 4, 0, 12),
            Child = new TextBlock
            {
                Text = "Production issuance is not available in this build. Issue Dev Test Bundle is for Debug testing only; it uses a public test key and does not activate production licenses or update production issuance records. Do not send a development test bundle to a customer as a real license.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(124, 45, 18))
            }
        };
        content.Children.Add(note);

        content.Children.Add(new TextBlock
        {
            Text = "Security and administration",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 2, 0, 4)
        });
        content.Children.Add(new TextBlock
        {
            Text = "Keep the register and signing keys restricted to authorized staff. Verify the customer, rig, term, PC limit and request before issuing files. Send license files through an approved secure channel. Keep a protected backup of the register.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
            Margin = new Thickness(0, 0, 0, 12)
        });

        var close = new Button { Content = "Close", IsDefault = true, IsCancel = true, MinWidth = 90, Padding = new Thickness(12, 6, 12, 6), HorizontalAlignment = HorizontalAlignment.Right };
        content.Children.Add(close);

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content
        };
    }

    private static void AddStep(Panel panel, string heading, string body)
    {
        panel.Children.Add(new TextBlock
        {
            Text = heading,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(30, 79, 130)),
            Margin = new Thickness(0, 5, 0, 3)
        });
        panel.Children.Add(new TextBlock
        {
            Text = body,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
            Margin = new Thickness(0, 0, 0, 8)
        });
    }
}
