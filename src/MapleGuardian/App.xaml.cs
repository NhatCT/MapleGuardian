using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.Configuration;
using MapleGuardian.Models;
using MapleGuardian.ViewModels;
using Application = System.Windows.Application;

namespace MapleGuardian;

/// <summary>
/// Application entry point.
/// Sets up configuration, creates the system tray icon, and manages app lifecycle.
/// The app runs in the background via system tray even when the window is closed.
/// </summary>
public partial class App : Application
{
    private TaskbarIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private MainViewModel? _viewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Load configuration
        var config = LoadConfiguration();

        // Create ViewModel (central coordinator)
        _viewModel = new MainViewModel(config);

        // Create main window
        _mainWindow = new MainWindow(_viewModel);

        // Setup system tray icon
        SetupTrayIcon();

        // Check if started with --minimized flag (auto-start with Windows)
        bool startMinimized = e.Args.Contains("--minimized");

        if (startMinimized)
        {
            // Don't show window, just sit in tray
            _mainWindow.Hide();
        }
        else
        {
            _mainWindow.Show();
        }
    }

    /// <summary>
    /// Load AppConfig from appsettings.json
    /// </summary>
    private static AppConfig LoadConfiguration()
    {
        var config = new AppConfig();

        try
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            var configuration = builder.Build();
            configuration.Bind(config);
        }
        catch
        {
            // Use defaults if config file is missing or invalid
        }

        return config;
    }

    /// <summary>
    /// Setup the system tray icon with context menu
    /// </summary>
    private void SetupTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Maple Guardian — Starting...",
            Visibility = Visibility.Visible
        };

        try
        {
            var iconUri = new Uri("pack://application:,,,/Resources/app_icon.ico", UriKind.Absolute);
            var streamInfo = System.Windows.Application.GetResourceStream(iconUri);
            if (streamInfo != null)
            {
                _trayIcon.Icon = new System.Drawing.Icon(streamInfo.Stream);
            }
            else if (!string.IsNullOrEmpty(Environment.ProcessPath))
            {
                _trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath);
            }
        }
        catch
        {
            if (!string.IsNullOrEmpty(Environment.ProcessPath))
            {
                try { _trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath); } catch { }
            }
        }

        // Create context menu
        var contextMenu = new System.Windows.Controls.ContextMenu();
        contextMenu.Style = CreateTrayMenuStyle();

        var showItem = new System.Windows.Controls.MenuItem { Header = "🍁 Show Dashboard" };
        showItem.Click += (_, _) => ShowMainWindow();

        var reconnectItem = new System.Windows.Controls.MenuItem { Header = "🔄 Reconnect VPN" };
        reconnectItem.Click += async (_, _) =>
        {
            if (_viewModel?.ReconnectVpnCommand.CanExecute(null) == true)
                await _viewModel.ReconnectVpn();
        };

        var logsItem = new System.Windows.Controls.MenuItem { Header = "📋 Open Logs" };
        logsItem.Click += (_, _) => _viewModel?.OpenLogsCommand.Execute(null);

        var separatorItem = new System.Windows.Controls.Separator();

        var exitItem = new System.Windows.Controls.MenuItem { Header = "❌ Exit" };
        exitItem.Click += (_, _) => ExitApplication();

        contextMenu.Items.Add(showItem);
        contextMenu.Items.Add(reconnectItem);
        contextMenu.Items.Add(logsItem);
        contextMenu.Items.Add(separatorItem);
        contextMenu.Items.Add(exitItem);

        _trayIcon.ContextMenu = contextMenu;
        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowMainWindow();

        // Update tooltip periodically
        var tooltipTimer = new System.Threading.Timer(_ =>
        {
            Dispatcher.Invoke(() =>
            {
                if (_trayIcon != null && _viewModel != null)
                    _trayIcon.ToolTipText = _viewModel.GetTrayToolTip();
            });
        }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Create dark-themed style for tray context menu
    /// </summary>
    private static System.Windows.Style CreateTrayMenuStyle()
    {
        var style = new System.Windows.Style(typeof(System.Windows.Controls.ContextMenu));
        style.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty,
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 27, 34))));
        style.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty,
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(230, 237, 243))));
        style.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty,
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(48, 54, 61))));
        style.Setters.Add(new Setter(System.Windows.Controls.Control.BorderThicknessProperty,
            new Thickness(1)));
        return style;
    }

    /// <summary>
    /// Show the main dashboard window
    /// </summary>
    private void ShowMainWindow()
    {
        if (_mainWindow == null) return;
        _mainWindow.ShowFromTray();
    }

    /// <summary>
    /// Clean exit
    /// </summary>
    private void ExitApplication()
    {
        _viewModel?.Dispose();
        _trayIcon?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _viewModel?.Dispose();
        base.OnExit(e);
    }
}
