using System.Windows;
using System.Windows.Input;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.ViewModels;

namespace DrillIntel.Views;

public partial class ExpressionEditorWindow : Window
{
    public ExpressionEditorWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BindCaretHandler();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is ExpressionEditorViewModel vm)
        {
            vm.RequestClose += result =>
            {
                try
                {
                    DialogResult = result;
                }
                catch
                {
                    // Fallback if window shown non-modally
                }
                Close();
            };

            BindCaretHandler();
        }
    }

    private void BindCaretHandler()
    {
        if (DataContext is ExpressionEditorViewModel vm)
        {
            vm.InsertTextAtCaretHandler = token =>
            {
                int caret = ExpressionTextBox.CaretIndex;
                string text = ExpressionTextBox.Text ?? string.Empty;

                if (caret >= 0 && caret <= text.Length)
                {
                    ExpressionTextBox.Text = text.Insert(caret, token);
                    ExpressionTextBox.CaretIndex = caret + token.Length;
                }
                else
                {
                    ExpressionTextBox.Text += token;
                    ExpressionTextBox.CaretIndex = ExpressionTextBox.Text.Length;
                }

                ExpressionTextBox.Focus();
            };
        }
    }

    private void OnChannelsListBoxMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ExpressionEditorViewModel vm && ChannelsListBox.SelectedItem is LogChannel ch)
        {
            vm.InsertChannelCommand.Execute(ch);
        }
    }

    private void OnFunctionsListBoxMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ExpressionEditorViewModel vm && FunctionsListBox.SelectedItem is string func)
        {
            vm.InsertFunctionCommand.Execute(func);
        }
    }

    private void OnOperatorsListBoxMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ExpressionEditorViewModel vm && OperatorsListBox.SelectedItem is string op)
        {
            vm.InsertOperatorCommand.Execute(op);
        }
    }
}

