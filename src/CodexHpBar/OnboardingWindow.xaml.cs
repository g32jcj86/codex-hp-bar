using System.Windows;
using CodexHpBar.Core;

namespace CodexHpBar;

public partial class OnboardingWindow : Window
{
    public AppSettings Settings { get; private set; } = AppSettings.Default;

    public OnboardingWindow(AppSettings? current = null)
    {
        InitializeComponent();
        if (current is not null)
        {
            BackgroundBox.IsChecked = current.BackgroundMode;
            StartupBox.IsChecked = current.StartWithWindows;
        }
        UpdateStartupStatus();
    }

    private void StartupChecked(object sender, RoutedEventArgs e)
    {
        BackgroundBox.IsChecked = true;
        UpdateStartupStatus();
    }

    private void StartupUnchecked(object sender, RoutedEventArgs e) => UpdateStartupStatus();

    private void BackgroundChanged(object sender, RoutedEventArgs e)
    {
        if (BackgroundBox.IsChecked != true) StartupBox.IsChecked = false;
        UpdateStartupStatus();
    }

    private void ApplyClick(object sender, RoutedEventArgs e)
    {
        Settings = new AppSettings(BackgroundBox.IsChecked == true, StartupBox.IsChecked == true).Normalize();
        DialogResult = true;
    }

    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void UpdateStartupStatus()
    {
        if (!IsInitialized || StartupStatusText is null) return;
        StartupStatusText.Text = StartupBox.IsChecked == true
            ? "套用後：會隨 Windows 登入自動啟動"
            : "套用後：不會隨 Windows 登入自動啟動";
    }
}
