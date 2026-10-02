using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data.Objects.DataObjects.Models;

namespace DrillIntel.ViewModels;

/// <summary>
/// ViewModel for the Expression Editor dialog.
/// Matches Timelog Editor theme and provides access to timelog LogChannels,
/// mathematical functions, operators, and expression syntax verification.
/// </summary>
public partial class ExpressionEditorViewModel : ObservableObject
{
    public event Action<bool?>? RequestClose;

    // Delegate allowing View to insert text at the active caret position
    public Action<string>? InsertTextAtCaretHandler { get; set; }

    [ObservableProperty]
    private string _title = "Expression Editor";

    [ObservableProperty]
    private string _subtitle = "Build calculation expressions using timelog channels, mathematical functions, and operators.";

    [ObservableProperty]
    private string _expression = string.Empty;

    [ObservableProperty]
    private LogChannel? _selectedChannel;

    [ObservableProperty]
    private string? _selectedFunction;

    [ObservableProperty]
    private string? _selectedOperator;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isStatusError;

    [ObservableProperty]
    private bool _isStatusVisible;

    public ObservableCollection<LogChannel> AvailableChannels { get; } = new();

    public ObservableCollection<string> AvailableFunctions { get; } = new()
    {
        "abs()",
        "round()",
        "sqrt()",
        "log()",
        "PI",
        "sin()",
        "cos()",
        "asin()",
        "acos()",
        "tan()",
        "atan()",
        "exp()",
        "min()",
        "max()",
        "IIF()"
    };

    public ObservableCollection<string> AvailableOperators { get; } = new()
    {
        "+",
        "-",
        "*",
        "/",
        "^",
        "(",
        ")",
        "<",
        ">",
        "<=",
        ">=",
        "=",
        "<>",
        "AND",
        "OR",
        "NOT"
    };

    public ExpressionEditorViewModel() : this(string.Empty, null)
    {
    }

    public ExpressionEditorViewModel(string? initialExpression, IEnumerable<LogChannel>? channels = null)
    {
        Expression = initialExpression ?? string.Empty;

        if (channels != null)
        {
            foreach (var ch in channels)
            {
                if (ch != null && !string.IsNullOrWhiteSpace(ch.Mnemonic))
                {
                    AvailableChannels.Add(ch);
                }
            }
        }

        if (AvailableChannels.Count > 0)
        {
            SelectedChannel = AvailableChannels[0];
        }
    }

    [RelayCommand]
    public void InsertChannel(LogChannel? channel)
    {
        var target = channel ?? SelectedChannel;
        if (target == null || string.IsNullOrWhiteSpace(target.Mnemonic)) return;

        string token = target.Mnemonic.Trim();
        InsertToken(token);
    }

    [RelayCommand]
    public void InsertFunction(string? func)
    {
        var target = func ?? SelectedFunction;
        if (string.IsNullOrWhiteSpace(target)) return;

        InsertToken(target.Trim());
    }

    [RelayCommand]
    public void InsertOperator(string? op)
    {
        var target = op ?? SelectedOperator;
        if (string.IsNullOrWhiteSpace(target)) return;

        string token = $" {target.Trim()} ";
        InsertToken(token);
    }

    private void InsertToken(string token)
    {
        if (InsertTextAtCaretHandler != null)
        {
            InsertTextAtCaretHandler(token);
        }
        else
        {
            if (string.IsNullOrEmpty(Expression))
            {
                Expression = token;
            }
            else
            {
                Expression += (Expression.EndsWith(" ") || token.StartsWith(" ") ? "" : " ") + token;
            }
        }
    }

    [RelayCommand]
    public void Verify()
    {
        string expr = (Expression ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(expr))
        {
            ShowStatus("Expression cannot be empty.", isError: true);
            return;
        }

        // 1. Check balanced parentheses
        int parenDepth = 0;
        foreach (char c in expr)
        {
            if (c == '(') parenDepth++;
            else if (c == ')')
            {
                parenDepth--;
                if (parenDepth < 0)
                {
                    ShowStatus("Syntax Error: Unexpected closing parenthesis ')' without matching opening '('.", isError: true);
                    return;
                }
            }
        }

        if (parenDepth > 0)
        {
            ShowStatus($"Syntax Error: Unbalanced parentheses. Missing {parenDepth} closing ')' parenthesis.", isError: true);
            return;
        }

        // 2. Check for invalid consecutive operators (e.g., ++, **, //, ^^, +*, /*)
        if (Regex.IsMatch(expr, @"[\+\-\*\/^\<\>=]{2,}"))
        {
            // Allow <=, >=, <>
            string cleaned = Regex.Replace(expr, @"<=|>=|<>|==", " ");
            if (Regex.IsMatch(cleaned, @"[\+\*\/^]{2,}|[\+\-\*\/^]\s*[\*\/^]"))
            {
                ShowStatus("Syntax Error: Consecutive operators detected.", isError: true);
                return;
            }
        }

        // 3. Check trailing operator
        if (Regex.IsMatch(expr, @"[\+\-\*\/^]$"))
        {
            ShowStatus("Syntax Error: Expression cannot end with an operator.", isError: true);
            return;
        }

        // 4. Check leading operator
        if (Regex.IsMatch(expr, @"^[\*\/^]"))
        {
            ShowStatus("Syntax Error: Expression cannot start with an operator like '*', '/', or '^'.", isError: true);
            return;
        }

        ShowStatus("Expression syntax is valid.", isError: false);
    }

    [RelayCommand]
    public void Ok()
    {
        string expr = (Expression ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(expr))
        {
            ShowStatus("Expression cannot be blank.", isError: true);
            return;
        }

        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    public void Cancel()
    {
        RequestClose?.Invoke(false);
    }

    [RelayCommand]
    public void Help()
    {
        ShowStatus("Tip: Double-click any channel, function, or operator to insert it into the formula.", isError: false);
    }

    public void ShowStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsStatusError = isError;
        IsStatusVisible = true;
    }
}

