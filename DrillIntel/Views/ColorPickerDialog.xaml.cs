using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DrillIntel.Views;

public partial class ColorPickerDialog : Window
{
    private bool _isUpdating;

    public string SelectedHex { get; private set; } = "#000000";
    public string OriginalHex { get; private set; } = "#000000";

    public ColorPickerDialog(string? initialHex = null)
    {
        InitializeComponent();

        string normalized = NormalizeHex(initialHex);
        OriginalHex = normalized;
        SelectedHex = normalized;

        TxtOriginalHex.Text = normalized;
        if (TryParseHex(normalized, out Color origColor))
        {
            BrushOriginal.Color = origColor;
        }

        SetColorFromHex(normalized);
    }

    /// <summary>
    /// Displays the ColorPickerDialog modal window centered on owner.
    /// Returns (Success: true, Hex: "#RRGGBB") if the user confirmed, or (Success: false, Hex: initialHex) if cancelled.
    /// </summary>
    public static (bool Success, string Hex) Show(Window? owner, string? initialHex)
    {
        var dlg = new ColorPickerDialog(initialHex);
        if (owner != null)
        {
            dlg.Owner = owner;
        }
        else if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsVisible)
        {
            dlg.Owner = Application.Current.MainWindow;
        }

        bool? result = dlg.ShowDialog();
        if (result == true)
        {
            return (true, dlg.SelectedHex);
        }

        return (false, dlg.OriginalHex);
    }

    private void SetColorFromHex(string hex)
    {
        if (TryParseHex(hex, out Color color))
        {
            _isUpdating = true;
            try
            {
                SliderR.Value = color.R;
                SliderG.Value = color.G;
                SliderB.Value = color.B;

                TxtR.Text = color.R.ToString();
                TxtG.Text = color.G.ToString();
                TxtB.Text = color.B.ToString();

                string formattedHex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                TxtHex.Text = formattedHex;
                TxtSelectedHex.Text = formattedHex;
                BrushSelected.Color = color;
                SelectedHex = formattedHex;
            }
            finally
            {
                _isUpdating = false;
            }
        }
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdating) return;

        _isUpdating = true;
        try
        {
            byte r = (byte)Math.Clamp(Math.Round(SliderR.Value), 0, 255);
            byte g = (byte)Math.Clamp(Math.Round(SliderG.Value), 0, 255);
            byte b = (byte)Math.Clamp(Math.Round(SliderB.Value), 0, 255);

            TxtR.Text = r.ToString();
            TxtG.Text = g.ToString();
            TxtB.Text = b.ToString();

            string hex = $"#{r:X2}{g:X2}{b:X2}";
            TxtHex.Text = hex;
            TxtSelectedHex.Text = hex;
            BrushSelected.Color = Color.FromRgb(r, g, b);
            SelectedHex = hex;
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void RgbText_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdating) return;

        if (byte.TryParse(TxtR.Text, out byte r) &&
            byte.TryParse(TxtG.Text, out byte g) &&
            byte.TryParse(TxtB.Text, out byte b))
        {
            _isUpdating = true;
            try
            {
                SliderR.Value = r;
                SliderG.Value = g;
                SliderB.Value = b;

                string hex = $"#{r:X2}{g:X2}{b:X2}";
                TxtHex.Text = hex;
                TxtSelectedHex.Text = hex;
                BrushSelected.Color = Color.FromRgb(r, g, b);
                SelectedHex = hex;
            }
            finally
            {
                _isUpdating = false;
            }
        }
    }

    private void TxtHex_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdating) return;

        string text = TxtHex.Text?.Trim() ?? string.Empty;
        if (TryParseHex(text, out Color color))
        {
            _isUpdating = true;
            try
            {
                SliderR.Value = color.R;
                SliderG.Value = color.G;
                SliderB.Value = color.B;

                TxtR.Text = color.R.ToString();
                TxtG.Text = color.G.ToString();
                TxtB.Text = color.B.ToString();

                string formattedHex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                TxtSelectedHex.Text = formattedHex;
                BrushSelected.Color = color;
                SelectedHex = formattedHex;
            }
            finally
            {
                _isUpdating = false;
            }
        }
    }

    private void PaletteSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex)
        {
            SetColorFromHex(hex);
        }
    }

    private void RevertToOriginal_Click(object sender, RoutedEventArgs e)
    {
        SetColorFromHex(OriginalHex);
    }

    private void Select_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static string NormalizeHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return "#000000";

        string clean = hex.Trim().TrimStart('#');
        if (clean.Length == 6)
        {
            return "#" + clean.ToUpperInvariant();
        }
        if (clean.Length == 8)
        {
            // Drop alpha channel if present
            return "#" + clean.Substring(2, 6).ToUpperInvariant();
        }
        return "#000000";
    }

    private static bool TryParseHex(string? hex, out Color color)
    {
        color = Colors.Black;
        if (string.IsNullOrWhiteSpace(hex)) return false;

        string clean = hex.Trim().TrimStart('#');
        if (clean.Length == 6 && int.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
        {
            byte r = (byte)((rgb >> 16) & 0xFF);
            byte g = (byte)((rgb >> 8) & 0xFF);
            byte b = (byte)(rgb & 0xFF);
            color = Color.FromRgb(r, g, b);
            return true;
        }

        return false;
    }
}

