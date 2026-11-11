using System;
using Avalonia.Controls;

namespace PCRemote.Desktop.Views;

public partial class MainWindow : Window
{
    public bool IsExiting { get; set; }

    public event Action? WindowHidden;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (!IsExiting && !e.Cancel)
        {
            e.Cancel = true;
            Hide();
            WindowHidden?.Invoke();
        }
    }
}