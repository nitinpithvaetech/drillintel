using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DrillIntel.Models;
using DrillIntel.ViewModels;

namespace DrillIntel.Views
{
    public partial class ImportDataView : UserControl
    {
        public ImportDataView()
        {
            InitializeComponent();
        }

        private ImportDataViewModel? ViewModel => DataContext as ImportDataViewModel;

        private void DropZone_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0 && ViewModel != null)
                {
                    ViewModel.DropFileCommand.Execute(files[0]);
                }
            }
        }

        private void DropZone_Click(object sender, MouseButtonEventArgs e)
        {
            ViewModel?.UploadFileCommand.Execute(null);
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Do nothing, required for routing if we kept the handler in XAML, 
            // but we can just leave it empty or remove it from XAML.
        }
    }
}
