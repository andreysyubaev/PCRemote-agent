using Avalonia.Platform.Storage;
using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using PCRemote.Desktop.ViewModels.Pages;
using Avalonia.Controls.Primitives;

namespace PCRemote.Desktop.Views.Pages;

public partial class LogsPage : UserControl
{
    private LogsPageViewModel? _viewModel;

    public LogsPage()
    {
        InitializeComponent();

        AttachedToVisualTree += (_, _) =>
        {
            if (DataContext is not LogsPageViewModel viewModel)
                return;

            _viewModel = viewModel;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            _ = _viewModel.LoadLogsCommand.ExecuteAsync(null);
        };

        DetachedFromVisualTree += (_, _) =>
        {
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                _viewModel = null;
            }
        };
    }

    private void OnViewModelPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LogsPageViewModel.IsLoading))
            return;

        if (sender is not LogsPageViewModel viewModel || viewModel.IsLoading)
            return;

        Dispatcher.UIThread.Post(ScrollToBottom);
    }

    private void ScrollToBottom()
    {
        var listBox = LogsList;

        if (listBox.ItemCount == 0)
            return;

        var lastItem = listBox.Items[listBox.ItemCount - 1];

        if (lastItem is not null)
            listBox.ScrollIntoView(lastItem);
    }
    
    private async void OnExportClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not LogsPageViewModel viewModel)
            return;

        var topLevel = TopLevel.GetTopLevel(this);

        if (topLevel is null)
            return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Export logs",
                SuggestedFileName = $"PCRemote-Logs-{DateTime.Now:yyyy-MM-dd}.txt",
                DefaultExtension = "txt",
                FileTypeChoices =
                [
                    new FilePickerFileType("Text files")
                    {
                        Patterns = ["*.txt"]
                    }
                ]
            });

        if (file is null)
            return;

        try
        {
            await using var stream = await file.OpenWriteAsync();

            await viewModel.ExportLogsAsync(stream);
        }
        catch (Exception)
        {
            viewModel.ErrorMessage = "Не удалось экспортировать журналы событий.";
        }
    }
}