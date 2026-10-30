using System.Windows;

namespace DrillIntel.Views.Broomstick;

public partial class BroomstickPromptDialog : Window
{
    public string ResponseText
    {
        get => InputBox.Text;
        set => InputBox.Text = value;
    }

    public BroomstickPromptDialog(string title, string message, string defaultText = "")
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        InputBox.Text = defaultText;

        Loaded += (s, e) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

