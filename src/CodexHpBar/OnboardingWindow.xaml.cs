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
    }

    private void StartupChecked(object sender, RoutedEventArgs e) => BackgroundBox.IsChecked = true;

    private void BackgroundChanged(object sender, RoutedEventArgs e)
    {
        if (BackgroundBox.IsChecked != true) StartupBox.IsChecked = false;
    }

    private void ApplyClick(object sender, RoutedEventArgs e)
    {
        Settings = new AppSettings(BackgroundBox.IsChecked == true, StartupBox.IsChecked == true).Normalize();
        DialogResult = true;
    }

    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
