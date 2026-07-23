using System.Windows;
using System.ComponentModel;
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
    private static System.Threading.Mutex? _appMutex;
    private TaskbarIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private MainViewModel? _viewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        const string mutexName = "Global\\MapleGuardianSingleInstanceMutex";
        _appMutex = new System.Threading.Mutex(true, mutexName, out bool createdNew);

        if (!createdNew)
        {
            System.Windows.MessageBox.Show("Maple Guardian is already running in the system tray.", "Maple Guardian", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // Load configuration
        var config = LoadConfiguration();

        // Create ViewModel (central coordinator)
        _viewModel = new MainViewModel(config);

        // Create main window
        _mainWindow = new MainWindow(_viewModel);

        // Setup system tray icon
        SetupTrayIcon();
        if (_trayIcon != null)
        {
            _viewModel.SetTaskbarIcon(_trayIcon);
        }

        // Subscribe to property changes to update the application and tray icons
        _viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.IsVpnConnected) || args.PropertyName == nameof(MainViewModel.VpnStatusText))
            {
                UpdateAppIconsBasedOnStatus();
            }
        };

        // Initialize icon state
        UpdateAppIconsBasedOnStatus();

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

        UpdateTrayIcon("app_icon.ico");

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
    /// Updates the application tray and window icons based on the current VPN connection status.
    /// </summary>
    private void UpdateAppIconsBasedOnStatus()
    {
        if (_viewModel == null) return;

        string iconName = "app_icon.ico";
        if (_viewModel.VpnStatusText == "Connected")
        {
            iconName = "icon_connected.ico";
        }
        else if (_viewModel.VpnStatusText == "LOST" || _viewModel.VpnStatusText == "Disconnected")
        {
            iconName = "icon_disconnected.ico";
        }

        // Update Tray Icon
        UpdateTrayIcon(iconName);

        // Update MainWindow Icon
        if (_mainWindow != null)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    var iconUri = new Uri($"pack://application:,,,/Resources/{iconName}", UriKind.Absolute);
                    _mainWindow.Icon = System.Windows.Media.Imaging.BitmapFrame.Create(iconUri);
                }
                catch
                {
                    // Ignore
                }
            });
        }
    }

    /// <summary>
    /// Updates the icon of the system tray icon.
    /// </summary>
    private void UpdateTrayIcon(string iconName)
    {
        if (_trayIcon == null) return;
        try
        {
            var iconUri = new Uri($"pack://application:,,,/Resources/{iconName}", UriKind.Absolute);
            var streamInfo = System.Windows.Application.GetResourceStream(iconUri);
            if (streamInfo != null)
            {
                _trayIcon.Icon = new System.Drawing.Icon(streamInfo.Stream);
            }
            else if (iconName == "app_icon.ico" && !string.IsNullOrEmpty(Environment.ProcessPath))
            {
                _trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath);
            }
        }
        catch
        {
            if (iconName == "app_icon.ico" && !string.IsNullOrEmpty(Environment.ProcessPath))
            {
                try { _trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath); } catch { }
            }
        }
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

        if (_appMutex != null)
        {
            try { _appMutex.ReleaseMutex(); } catch { }
            _appMutex.Dispose();
        }

        base.OnExit(e);
    }
}
