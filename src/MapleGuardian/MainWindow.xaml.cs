using System.Windows;
using System.Windows.Input;
using MapleGuardian.ViewModels;

namespace MapleGuardian;

/// <summary>
/// MainWindow code-behind. Minimal — delegates to ViewModel.
/// Handles window chrome interactions (drag, minimize, close to tray).
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.LogEntries.CollectionChanged += (s, e) =>
        {
            if (_viewModel.IsLogViewerOpen)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    if (LogListBox.Items.Count > 0)
                    {
                        LogListBox.ScrollIntoView(LogListBox.Items[LogListBox.Items.Count - 1]);
                    }
                });
            }
        };

        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsLogViewerOpen) && _viewModel.IsLogViewerOpen)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    if (LogListBox.Items.Count > 0)
                    {
                        LogListBox.ScrollIntoView(LogListBox.Items[LogListBox.Items.Count - 1]);
                    }
                });
            }
        };

        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _viewModel.StartAllServices();
    }

    /// <summary>
    /// Allow dragging the borderless window
    /// </summary>
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    /// <summary>
    /// Minimize to taskbar
    /// </summary>
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    /// <summary>
    /// Close button = hide to system tray (app keeps running)
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    /// <summary>
    /// Show window from tray
    /// </summary>
    public void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Focus();
    }

    /// <summary>
    /// Override closing to hide to tray instead of exiting
    /// </summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true; // Prevent actual close
        Hide();          // Hide to tray
    }
}
