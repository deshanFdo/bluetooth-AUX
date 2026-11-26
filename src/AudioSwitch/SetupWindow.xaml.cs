using System.Windows;
using System.Windows.Input;
using AudioSwitch.AudioCore;

namespace AudioSwitch;

public partial class SetupWindow : Window
{
    private bool _isInstalledDone;

    public SetupWindow()
    {
        InitializeComponent();
        InstallPathText.Text = InstallerService.DefaultInstallDir;

        if (InstallerService.IsAlreadyInstalled())
        {
            SubtitleText.Text = $"Update / Repair Installation • v{InstallerService.AppVersion}";
            PrimaryActionBtn.Content = "Update & Launch";
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            try { DragMove(); } catch { }
        }
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void PrimaryActionBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isInstalledDone)
        {
            InstallerService.LaunchInstalledApp();
            Close();
            return;
        }

        PrimaryActionBtn.IsEnabled = false;
        CancelBtn.IsEnabled = false;
        OptionsCard.IsEnabled = false;
        ProgressPanel.Visibility = Visibility.Visible;

        var options = new InstallOptions(
            CreateStartMenuShortcut: StartMenuCheck.IsChecked == true,
            CreateDesktopShortcut: DesktopCheck.IsChecked == true,
            LaunchOnStartup: StartupCheck.IsChecked == true,
            LaunchAfterInstall: true);

        var progress = new Progress<(int Percent, string Step)>(p =>
        {
            ProgressLabel.Text = p.Step;
            ProgressScale.ScaleX = Math.Clamp(p.Percent / 100.0, 0.05, 1.0);
        });

        try
        {
            await InstallerService.InstallAsync(options, progress);
            _isInstalledDone = true;
            PrimaryActionBtn.IsEnabled = true;
            PrimaryActionBtn.Content = "Launch Audio Switch";
            CancelBtn.IsEnabled = true;
            CancelBtn.Content = "Close";
            SubtitleText.Text = "Installed cleanly! Ready to run from Top Edge or System Tray.";
        }
        catch (Exception ex)
        {
            ProgressLabel.Text = $"Install error: {ex.Message}";
            PrimaryActionBtn.IsEnabled = true;
            CancelBtn.IsEnabled = true;
            OptionsCard.IsEnabled = true;
        }
    }
}
