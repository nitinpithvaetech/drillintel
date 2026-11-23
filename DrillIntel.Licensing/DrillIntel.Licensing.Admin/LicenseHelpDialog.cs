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
        AddStep(content, "3. Create a request on the customer's installed PC", "Install DrillIntel on the customer's PC. In the ribbon, open the License group and choose PC Activation. Enter the License ID from the License tab, choose Check / Create Key, then choose Export Activation Request. Send the exported .jws file back to your office. The private device key stays on that PC.");
        AddStep(content, "4. Import the request in License Admin", "Choose Import .jws Request and select the customer's .jws file. Then open the Request Inbox tab. The app verifies the request and lists it there. Check the License ID and device fingerprint, select the pending request, then choose Issue Activation Package. Enter the signing-key password when asked.");
        AddStep(content, "5. Return and import the package", "Send the generated .dillicense package securely to the customer. On that same PC, open License → PC Activation, choose Check / Create Key if needed, then choose Import License Package. License Admin records the device, entitlement revision, activation file, request outcome, and audit event automatically.");

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
                Text = "In License Admin, open the Request Inbox tab to find Set Up Production Key and Issue Activation Package. Before the first production issue, choose Set Up Production Key. Keep the encrypted .dilkey file and password secure and back up the key file. Export the public JSON config and place it at DrillIntel.Licensing\\DrillIntel.Licensing.Client\\ProductionIssuerPublicKey.json (alongside DrillIntel.Licensing.Client.csproj). Then rebuild and redistribute the Release installer so it embeds the matching key. Keep the private .dilkey file and its password out of the project and installer. The installer you already have can export requests, but it must be rebuilt with this public key before it can import production packages. Issue Dev Test Bundle remains Debug-only test material; never send it as a customer license.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(124, 45, 18))
            }
        };
        content.Children.Add(note);

        content.Children.Add(new TextBlock
        {
            Text = "What each tab shows",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(23, 32, 51)),
            Margin = new Thickness(0, 10, 0, 4)
        });
        content.Children.Add(new TextBlock
        {
            Text = "These tabs show records for the customer selected in the Customers list. Select a customer first; some tabs may be empty until records exist. Each grid has a Filter box: type part of a value to narrow the visible rows, and clear the box to show all rows again. Most tabs are read-only history views populated by the workflow, not manual data-entry forms.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
            Margin = new Thickness(0, 0, 0, 8)
        });
        AddStep(content, "Rigs", "Rigs registered under the selected customer. Use Add Rig to register another rig.");
        AddStep(content, "Licenses", "License records for the customer's rigs, including plan term, status, maximum devices, UTC start and expiry dates, and the current entitlement revision. Select a row and use Copy License ID, the row's Copy button, Ctrl+C, or right-click to copy its License ID.");
        AddStep(content, "Devices", "One row per activated PC, showing its device key fingerprint, key protection type, activation and expiry times, status, and any replacement relationship. Issue Activation Package creates this row after verifying the signed request. Device and activation history is retained for traceability.");
        AddStep(content, "Entitlement Revisions", "The version history of signed license entitlements. Each issued package creates a revision record with its issue time, signing key ID, archive reference, and file hash.");
        AddStep(content, "Activation Files", "History of signed activation files issued for specific PCs, including the related request, revision, issue and expiry times, archive reference, and file hash.");
        AddStep(content, "Requests", "Processed and rejected/archived activation requests with their outcome and reason. Rejected or archived requests leave the pending inbox but remain here and in the Audit Log.");
        AddStep(content, "Request Inbox", "Pending activation requests only, with request time, device key hash, key protection, and source file. Use Import .jws Request to add a signed request. Select a pending request and choose Reject / Archive Request to remove it from this queue; enter a reason so the action is documented. This does not delete the record or revoke the license.");
        AddStep(content, "Payments", "Payment records associated with the customer's licenses. Choose Record Payment to enter the license, transaction reference, amount, currency, processor or method, status, and UTC date. Use the recorded status (paid, refunded, or chargeback) to represent the financial lifecycle; retain the payment history. This records an office entry; it does not charge a card or connect to a payment provider. Never enter card details.");
        AddStep(content, "Audit Log", "A chronological record of administrative actions, including the time, action, related license, and details where available. Issued files, entitlement revisions, and audit history are retained for traceability rather than deleted.");

        content.Children.Add(new TextBlock
        {
            Text = "What works with the current installer",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 10, 0, 4)
        });
        content.Children.Add(new TextBlock
        {
            Text = "Production issuance is available after the signing key is set up and the Release DrillIntel app is rebuilt with the matching public key. A Release app without that embedded key fails closed and rejects the production package. DrillIntel can verify and display an active license, but premium feature controls are not yet connected to license status. Issue Dev Test Bundle remains Debug-only and is not a customer license.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(124, 45, 18)),
            Margin = new Thickness(0, 0, 0, 12)
        });

        content.Children.Add(new TextBlock
        {
            Text = "Public key config example (sample only; not a real key)",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 2, 0, 4)
        });
        content.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 12),
            Child = new TextBlock
            {
                Text = "{\n  \"schemaVersion\": 1,\n  \"keyId\": \"rfc7515-test-vector-only\",\n  \"algorithm\": \"ES256\",\n  \"publicKeySpki\": \"MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEf83OJ3D2xF1Bg8vub9tLe1gHMzV76e8Tus9uPHvRVEXH8UTNG72bfocs3+257rn0s2ldbqkLJK2KRiMohYjlrQ==\"\n}",
                FontFamily = new FontFamily("Consolas"),
                TextWrapping = TextWrapping.Wrap
            }
        });
        content.Children.Add(new TextBlock
        {
            Text = "This is a publicly known test vector, not your company key. It has a publicly known matching private key and must never be installed as a production trust key.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(124, 45, 18)),
            Margin = new Thickness(0, 0, 0, 12)
        });
        content.Children.Add(new TextBlock
        {
            Text = "Security and administration",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 2, 0, 4)
        });
        content.Children.Add(new TextBlock
        {
            Text = "Keep the register and signing keys restricted to authorized staff. Verify the customer, rig, term, PC limit and request before issuing files. Send license packages through an approved secure channel. Back up the register, encrypted signing key file, and IssuedPackages archive; keep the key password separate from the backup.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
            Margin = new Thickness(0, 0, 0, 12)
        });

        var close = new Button { Content = "Close", IsDefault = true, IsCancel = true, MinWidth = 90, Padding = new Thickness(12, 6, 12, 6), HorizontalAlignment = HorizontalAlignment.Right };
        content.Children.Add(close);

        var helpTabs = new TabControl();
        helpTabs.Items.Add(new TabItem
        {
            Header = "Help",
            Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = content
            }
        });
        helpTabs.Items.Add(new TabItem
        {
            Header = "How to Test",
            Content = BuildHowToTestTab()
        });
        Content = helpTabs;
    }

    private static UIElement BuildHowToTestTab()
    {
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock
        {
            Text = "Test the request and verification path",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(23, 32, 51)),
            Margin = new Thickness(0, 0, 0, 8)
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Use Debug builds of both License Admin and DrillIntel on a test PC. The installed Release installer can export a request, but Release builds cannot import a Dev Test Bundle.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
            Margin = new Thickness(0, 0, 0, 12)
        });
        AddStep(panel, "1. Create a test license record", "In License Admin, add a customer, rig, and license record. Copy the License ID from the Licenses tab.");
        AddStep(panel, "2. Export a request from the test PC", "Run the Debug build of DrillIntel on the test PC. Open License → PC Activation, enter the License ID, choose Check / Create Key, then Export Activation Request. Send the .jws file to the License Admin PC.");
        AddStep(panel, "3. Import the request and create a test bundle", "In License Admin, choose Import .jws Request and select the .jws file. Select its row in Request Inbox, choose Issue Dev Test Bundle, and save the .diltest file.");
        AddStep(panel, "4. Verify the test bundle", "On the same test PC, in the Debug build, open License → PC Activation, choose Import Dev Test Bundle, and select the .diltest file. Check that verification succeeds and the displayed License ID and device fingerprint match.");

        panel.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(255, 247, 237)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 4, 0, 12),
            Child = new TextBlock
            {
                Text = "This is a signature and device-binding test only. It does not persist an activation, enable production features, or create production issuance records. The test signing key is public test material. Never send a Dev Test Bundle to a customer as a real license.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(124, 45, 18))
            }
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Test the production package flow separately",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 8, 0, 4)
        });
        panel.Children.Add(new TextBlock
        {
            Text = "For a Release-style test, use an isolated test License Admin profile, a separate test signing key, a test register, and a test Release DrillIntel build with that test public key embedded. Create a test license, export/import a .jws request, issue a .dillicense package, and import it. Confirm the license survives restarting the test app and that Devices, Entitlement Revisions, Activation Files, Requests, and Audit Log update in the test register. Do not use customer records or the production key during this check.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
            Margin = new Thickness(0, 0, 0, 12)
        });
        panel.Children.Add(new TextBlock
        {
            Text = "The Dev Test Bundle check verifies signatures and device binding only. The isolated Release check exercises the persistent production package path. Production deployment uses the separate production key and Release installer configured with its matching public key.",
            TextWrapping = TextWrapping.Wrap,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
            Margin = new Thickness(0, 0, 0, 12)
        });
        panel.Children.Add(new Button
        {
            Content = "Close",
            IsDefault = true,
            IsCancel = true,
            MinWidth = 90,
            Padding = new Thickness(12, 6, 12, 6),
            HorizontalAlignment = HorizontalAlignment.Right
        });
        return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = panel };
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
