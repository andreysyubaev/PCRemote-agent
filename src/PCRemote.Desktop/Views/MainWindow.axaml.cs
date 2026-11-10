using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace PCRemote.Desktop.Views;

public partial class MainWindow : Window
{
    public bool IsExiting { get; set; }

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
        }
    }
}