using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;

namespace AudioSwitch.Linux;

public partial class App : Application
{
    private TrayIcon? _trayIcon;
    private MainWindow? _mainWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _mainWindow = new MainWindow();
            desktop.MainWindow = _mainWindow;
            SetupTrayIcon(desktop);
            _mainWindow.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        WindowIcon? icon = null;
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://AudioSwitch/Assets/AudioSwitch.ico"));
            icon = new WindowIcon(stream);
        }
        catch { }

        var openItem = new NativeMenuItem("Open Audio Island");
        openItem.Click += (_, _) => _mainWindow?.ExpandToDeckFromTray();

        var swapItem = new NativeMenuItem("Quick Swap (AUX <-> Bluetooth)");
        swapItem.Click += (_, _) => _mainWindow?.QuickSwapAuxBt();

        var collapseItem = new NativeMenuItem("Collapse to Top Line");
        collapseItem.Click += (_, _) => _mainWindow?.CollapseToThinLine();

        var exitItem = new NativeMenuItem("Quit Audio Switch");
        exitItem.Click += (_, _) =>
        {
            _trayIcon?.Dispose();
            _mainWindow?.Close();
            desktop.Shutdown();
        };

        var menu = new NativeMenu();
        menu.Items.Add(openItem);
        menu.Items.Add(swapItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(collapseItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(exitItem);

        _trayIcon = new TrayIcon
        {
            Icon = icon,
            ToolTipText = "Audio Switch — Dynamic Island for Linux",
            Menu = menu,
            IsVisible = true
        };
        _trayIcon.Clicked += (_, _) => _mainWindow?.ToggleOpenOrCollapse();
    }
}
