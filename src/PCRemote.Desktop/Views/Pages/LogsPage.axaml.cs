using Avalonia.Platform.Storage;
using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PCRemote.Desktop.Localization;
using PCRemote.Desktop.ViewModels.Pages;

namespace PCRemote.Desktop.Views.Pages;

public partial class LogsPage : UserControl
{
    private LogsPageViewModel? _viewModel;

    public LogsPage()
    {
        InitializeComponent();

        SearchBox.AddHandler(
            InputElement.PointerPressedEvent,
            OnSearchPointerPressed,
            RoutingStrategies.Tunnel);
        SearchBox.AddHandler(
            InputElement.PointerReleasedEvent,
            OnSearchPointerReleased,
            RoutingStrategies.Tunnel);
        SearchBox.PointerCaptureLost += OnSearchPointerCaptureLost;

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

    private void OnSearchPointerPressed(
        object? sender,
        PointerPressedEventArgs e)
    {
        SearchBox.Classes.Add("pressed");
    }

    private void OnSearchPointerReleased(
        object? sender,
        PointerReleasedEventArgs e)
    {
        SearchBox.Classes.Remove("pressed");
    }

    private void OnSearchPointerCaptureLost(
        object? sender,
        PointerCaptureLostEventArgs e)
    {
        SearchBox.Classes.Remove("pressed");
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
                Title = LocalizationManager.GetString("Logs_ExportDialogTitle"),
                SuggestedFileName = $"PCRemote-Logs-{DateTime.Now:yyyy-MM-dd}.txt",
                DefaultExtension = "txt",
                FileTypeChoices =
                [
                    new FilePickerFileType(
                        LocalizationManager.GetString("Logs_ExportFileType"))
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
            viewModel.ErrorMessage =
                LocalizationManager.GetString("Logs_ExportError");
        }
    }
}