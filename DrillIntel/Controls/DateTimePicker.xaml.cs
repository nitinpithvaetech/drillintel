using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DrillIntel.Controls;

public partial class DateTimePicker : UserControl
{
    private DateTime _tempDate = DateTime.Today;
    private int _tempHour = 12;
    private int _tempMinute;
    private bool _isCancelled;
    private bool _isInternalUpdating;

    public static readonly DependencyProperty SelectedDateTimeProperty =
        DependencyProperty.Register(
            nameof(SelectedDateTime),
            typeof(DateTime?),
            typeof(DateTimePicker),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedDateTimeChanged));

    public static readonly DependencyProperty DisplayDateStartProperty =
        DependencyProperty.Register(
            nameof(DisplayDateStart),
            typeof(DateTime?),
            typeof(DateTimePicker),
            new PropertyMetadata(null, OnDisplayDateBoundsChanged));

    public static readonly DependencyProperty DisplayDateEndProperty =
        DependencyProperty.Register(
            nameof(DisplayDateEnd),
            typeof(DateTime?),
            typeof(DateTimePicker),
            new PropertyMetadata(null, OnDisplayDateBoundsChanged));

    public static readonly DependencyProperty DateFormatProperty =
        DependencyProperty.Register(
            nameof(DateFormat),
            typeof(string),
            typeof(DateTimePicker),
            new PropertyMetadata("dd/MM/yyyy HH:mm", OnDateFormatChanged));

    public static readonly DependencyProperty WatermarkProperty =
        DependencyProperty.Register(
            nameof(Watermark),
            typeof(string),
            typeof(DateTimePicker),
            new PropertyMetadata("DD/MM/YYYY HH:MM", OnWatermarkChanged));

    public static readonly DependencyProperty IsDropDownOpenProperty =
        DependencyProperty.Register(
            nameof(IsDropDownOpen),
            typeof(bool),
            typeof(DateTimePicker),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsDropDownOpenChanged));

    public DateTime? SelectedDateTime
    {
        get => (DateTime?)GetValue(SelectedDateTimeProperty);
        set => SetValue(SelectedDateTimeProperty, value);
    }

    public DateTime? DisplayDateStart
    {
        get => (DateTime?)GetValue(DisplayDateStartProperty);
        set => SetValue(DisplayDateStartProperty, value);
    }

    public DateTime? DisplayDateEnd
    {
        get => (DateTime?)GetValue(DisplayDateEndProperty);
        set => SetValue(DisplayDateEndProperty, value);
    }

    public string DateFormat
    {
        get => (string)GetValue(DateFormatProperty);
        set => SetValue(DateFormatProperty, value);
    }

    public string Watermark
    {
        get => (string)GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    public bool IsDropDownOpen
    {
        get => (bool)GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    public DateTimePicker()
    {
        InitializeComponent();
        UpdateTextBoxFromValue();
    }

    private static void OnSelectedDateTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DateTimePicker picker && !picker._isInternalUpdating)
        {
            picker.UpdateTextBoxFromValue();
        }
    }

    private static void OnDisplayDateBoundsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DateTimePicker picker)
        {
            picker.UpdateCalendarBounds();
        }
    }

    private static void OnDateFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DateTimePicker picker)
        {
            picker.UpdateTextBoxFromValue();
        }
    }

    private static void OnWatermarkChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DateTimePicker picker)
        {
            picker.WatermarkTextBlock.Text = picker.Watermark;
        }
    }

    private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DateTimePicker picker)
        {
            picker.DropDownPopup.IsOpen = (bool)e.NewValue;
        }
    }

    private void UpdateCalendarBounds()
    {
        if (DisplayDateStart.HasValue && DisplayDateStart.Value != DateTime.MinValue)
        {
            PopupCalendar.DisplayDateStart = DisplayDateStart.Value.Date;
            MinDateButton.Visibility = Visibility.Visible;
        }
        else
        {
            PopupCalendar.DisplayDateStart = null;
            MinDateButton.Visibility = Visibility.Collapsed;
        }

        if (DisplayDateEnd.HasValue && DisplayDateEnd.Value != DateTime.MaxValue && DisplayDateEnd.Value != DateTime.MinValue)
        {
            PopupCalendar.DisplayDateEnd = DisplayDateEnd.Value.Date;
            MaxDateButton.Visibility = Visibility.Visible;
        }
        else
        {
            PopupCalendar.DisplayDateEnd = null;
            MaxDateButton.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateTextBoxFromValue()
    {
        if (SelectedDateTime.HasValue)
        {
            DateTimeTextBox.Text = SelectedDateTime.Value.ToString(DateFormat, CultureInfo.InvariantCulture);
            WatermarkTextBlock.Visibility = Visibility.Collapsed;
        }
        else
        {
            DateTimeTextBox.Text = string.Empty;
            WatermarkTextBlock.Visibility = Visibility.Visible;
        }
    }

    private void OnOpenDropDownClick(object sender, RoutedEventArgs e)
    {
        if (!IsEnabled) return;
        IsDropDownOpen = !IsDropDownOpen;
    }

    private void OnPopupOpened(object sender, EventArgs e)
    {
        _isCancelled = false;
        var initial = SelectedDateTime ?? (DisplayDateStart.HasValue && DisplayDateStart.Value > DateTime.MinValue ? DisplayDateStart.Value : DateTime.Now);

        _tempDate = initial.Date;
        _tempHour = initial.Hour;
        _tempMinute = initial.Minute;

        UpdateCalendarBounds();

        PopupCalendar.SelectedDate = _tempDate;
        PopupCalendar.DisplayDate = _tempDate;

        HourTextBox.Text = _tempHour.ToString("D2");
        MinuteTextBox.Text = _tempMinute.ToString("D2");

        UpdatePreview();
    }

    private void OnPopupClosed(object sender, EventArgs e)
    {
        if (IsDropDownOpen)
        {
            IsDropDownOpen = false;
        }

        if (!_isCancelled)
        {
            CommitSelection();
        }
    }

    private void UpdatePreview()
    {
        var previewDt = new DateTime(_tempDate.Year, _tempDate.Month, _tempDate.Day, _tempHour, _tempMinute, 0);
        PopupPreviewTextBlock.Text = previewDt.ToString(DateFormat, CultureInfo.InvariantCulture);
    }

    private void CommitSelection()
    {
        ParseHourMinuteInputs();
        var committed = new DateTime(_tempDate.Year, _tempDate.Month, _tempDate.Day, _tempHour, _tempMinute, 0);

        _isInternalUpdating = true;
        try
        {
            SelectedDateTime = committed;
            UpdateTextBoxFromValue();
        }
        finally
        {
            _isInternalUpdating = false;
        }
    }

    private void ParseHourMinuteInputs()
    {
        if (int.TryParse(HourTextBox.Text?.Trim(), out int h))
        {
            _tempHour = Math.Clamp(h, 0, 23);
        }
        if (int.TryParse(MinuteTextBox.Text?.Trim(), out int m))
        {
            _tempMinute = Math.Clamp(m, 0, 59);
        }

        HourTextBox.Text = _tempHour.ToString("D2");
        MinuteTextBox.Text = _tempMinute.ToString("D2");
    }

    private void OnCalendarSelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (PopupCalendar.SelectedDate.HasValue)
        {
            _tempDate = PopupCalendar.SelectedDate.Value.Date;
            UpdatePreview();
        }
    }

    private void OnHourUpClick(object sender, RoutedEventArgs e)
    {
        ParseHourMinuteInputs();
        _tempHour = (_tempHour + 1) % 24;
        HourTextBox.Text = _tempHour.ToString("D2");
        UpdatePreview();
    }

    private void OnHourDownClick(object sender, RoutedEventArgs e)
    {
        ParseHourMinuteInputs();
        _tempHour = (_tempHour - 1 + 24) % 24;
        HourTextBox.Text = _tempHour.ToString("D2");
        UpdatePreview();
    }

    private void OnMinuteUpClick(object sender, RoutedEventArgs e)
    {
        ParseHourMinuteInputs();
        _tempMinute = (_tempMinute + 1) % 60;
        MinuteTextBox.Text = _tempMinute.ToString("D2");
        UpdatePreview();
    }

    private void OnMinuteDownClick(object sender, RoutedEventArgs e)
    {
        ParseHourMinuteInputs();
        _tempMinute = (_tempMinute - 1 + 60) % 60;
        MinuteTextBox.Text = _tempMinute.ToString("D2");
        UpdatePreview();
    }

    private void OnHourTextBoxLostFocus(object sender, RoutedEventArgs e)
    {
        ParseHourMinuteInputs();
        UpdatePreview();
    }

    private void OnMinuteTextBoxLostFocus(object sender, RoutedEventArgs e)
    {
        ParseHourMinuteInputs();
        UpdatePreview();
    }

    private void OnTimeTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ParseHourMinuteInputs();
            UpdatePreview();
            e.Handled = true;
        }
    }

    private void OnPresetTimeClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            switch (tag)
            {
                case "00:00":
                    _tempHour = 0;
                    _tempMinute = 0;
                    break;
                case "06:00":
                    _tempHour = 6;
                    _tempMinute = 0;
                    break;
                case "12:00":
                    _tempHour = 12;
                    _tempMinute = 0;
                    break;
                case "18:00":
                    _tempHour = 18;
                    _tempMinute = 0;
                    break;
                case "23:59":
                    _tempHour = 23;
                    _tempMinute = 59;
                    break;
                case "Now":
                    var now = DateTime.Now;
                    _tempHour = now.Hour;
                    _tempMinute = now.Minute;
                    break;
            }

            HourTextBox.Text = _tempHour.ToString("D2");
            MinuteTextBox.Text = _tempMinute.ToString("D2");
            UpdatePreview();
        }
    }

    private void OnTodayClick(object sender, RoutedEventArgs e)
    {
        _tempDate = DateTime.Today;
        PopupCalendar.SelectedDate = _tempDate;
        PopupCalendar.DisplayDate = _tempDate;
        UpdatePreview();
    }

    private void OnMinDateClick(object sender, RoutedEventArgs e)
    {
        if (DisplayDateStart.HasValue && DisplayDateStart.Value != DateTime.MinValue)
        {
            _tempDate = DisplayDateStart.Value.Date;
            PopupCalendar.SelectedDate = _tempDate;
            PopupCalendar.DisplayDate = _tempDate;
            UpdatePreview();
        }
    }

    private void OnMaxDateClick(object sender, RoutedEventArgs e)
    {
        if (DisplayDateEnd.HasValue && DisplayDateEnd.Value != DateTime.MaxValue)
        {
            _tempDate = DisplayDateEnd.Value.Date;
            PopupCalendar.SelectedDate = _tempDate;
            PopupCalendar.DisplayDate = _tempDate;
            UpdatePreview();
        }
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        var target = SelectedDateTime ?? DateTime.Now;
        _tempDate = target.Date;
        _tempHour = target.Hour;
        _tempMinute = target.Minute;

        PopupCalendar.SelectedDate = _tempDate;
        PopupCalendar.DisplayDate = _tempDate;
        HourTextBox.Text = _tempHour.ToString("D2");
        MinuteTextBox.Text = _tempMinute.ToString("D2");
        UpdatePreview();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        _isCancelled = true;
        IsDropDownOpen = false;
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        _isCancelled = false;
        CommitSelection();
        IsDropDownOpen = false;
    }

    private void OnTextBoxLostFocus(object sender, RoutedEventArgs e)
    {
        ProcessTextEdit();
    }

    private void OnTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ProcessTextEdit();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            UpdateTextBoxFromValue();
            e.Handled = true;
        }
        else if (e.Key == Key.Down && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
        {
            IsDropDownOpen = true;
            e.Handled = true;
        }
    }

    private void ProcessTextEdit()
    {
        string raw = DateTimeTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            SelectedDateTime = null;
            UpdateTextBoxFromValue();
            return;
        }

        if (TryParseDateTime(raw, out var parsed))
        {
            SelectedDateTime = parsed;
            UpdateTextBoxFromValue();
        }
        else
        {
            // Revert back to valid state
            UpdateTextBoxFromValue();
        }
    }

    public static bool TryParseDateTime(string? text, out DateTime result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string[] formats = new[]
        {
            "dd/MM/yyyy HH:mm",
            "dd/MM/yyyy HH:mm:ss",
            "dd-MM-yyyy HH:mm",
            "dd-MM-yyyy HH:mm:ss",
            "dd.MM.yyyy HH:mm",
            "dd.MM.yyyy HH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy/MM/dd HH:mm",
            "yyyy/MM/dd HH:mm:ss",
            "dd/MM/yyyy",
            "dd-MM-yyyy",
            "dd.MM.yyyy",
            "yyyy-MM-dd",
            "yyyy/MM/dd",
            "M/d/yyyy h:mm tt",
            "M/d/yyyy h:mm:ss tt",
            "M/d/yyyy HH:mm",
            "MM/dd/yyyy HH:mm"
        };

        if (DateTime.TryParseExact(text.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            return true;

        if (DateTime.TryParse(text.Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out result))
            return true;

        return DateTime.TryParse(text.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }
}

