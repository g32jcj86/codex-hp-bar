using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CodexHpBar.Core;
using Microsoft.Win32;

namespace CodexHpBar;

public partial class OnboardingWindow : Window
{
    public AppSettings Settings { get; private set; } = AppSettings.Default;

    public OnboardingWindow(AppSettings? current = null)
    {
        InitializeComponent();
        var settings = (current ?? AppSettings.Default).Normalize();
        var mascot = settings.Mascot!;
        BackgroundBox.IsChecked = settings.BackgroundMode;
        StartupBox.IsChecked = settings.StartWithWindows;
        MascotModeBox.SelectedValue = mascot.Mode.ToString();
        MascotPathBox.Text = mascot.FilePath ?? string.Empty;
        FrameRateBox.Text = mascot.FramesPerSecond.ToString(CultureInfo.InvariantCulture);
        UpdateMascotControls();
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

    private void MascotModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MascotModeBox is null) return;
        UpdateMascotControls();
    }

    private void BrowseMascotClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Multiselect = false,
            Filter = GetFileFilter(SelectedMascotMode())
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            var selected = new MascotSettings(SelectedMascotMode(), dialog.FileName, 8);
            var copied = MascotAssetStorage.EnsureLocalCopy(selected);
            MascotPathBox.Text = copied.FilePath ?? string.Empty;
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "圖片檔案無法複製", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ApplyClick(object sender, RoutedEventArgs e)
    {
        var mascot = BuildMascotSettings(out var error);
        if (mascot is null)
        {
            MessageBox.Show(this, error, "圖片設定無法套用", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Settings = new AppSettings(BackgroundBox.IsChecked == true, StartupBox.IsChecked == true, mascot).Normalize();
        DialogResult = true;
    }

    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private MascotSettings? BuildMascotSettings(out string error)
    {
        var mode = SelectedMascotMode();
        if (!int.TryParse(FrameRateBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var framesPerSecond))
        {
            error = "播放速度必須是 1 到 30 之間的整數。";
            return null;
        }

        var settings = new MascotSettings(
            mode,
            mode == MascotAssetMode.BuiltInMushroom ? null : MascotPathBox.Text,
            framesPerSecond).Normalize();
        if (mode != MascotAssetMode.BuiltInMushroom)
        {
            try
            {
                settings = MascotAssetStorage.EnsureLocalCopy(settings);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return null;
            }

            var validator = new MascotAnimation();
            if (!validator.TryValidate(settings, out error)) return null;
        }

        error = string.Empty;
        return settings;
    }

    private MascotAssetMode SelectedMascotMode()
    {
        return Enum.TryParse<MascotAssetMode>(MascotModeBox.SelectedValue?.ToString(), out var mode)
            ? mode
            : MascotAssetMode.BuiltInMushroom;
    }

    private void UpdateMascotControls()
    {
        if (MascotModeBox is null || MascotPathBox is null || BrowseMascotButton is null || FrameRateBox is null || MascotRuleText is null) return;
        var mode = SelectedMascotMode();
        var usesFile = mode != MascotAssetMode.BuiltInMushroom;
        MascotPathBox.IsEnabled = usesFile;
        BrowseMascotButton.IsEnabled = usesFile;
        FrameRateBox.IsEnabled = mode == MascotAssetMode.SpriteSheet4x4;
        MascotRuleText.Text = mode switch
        {
            MascotAssetMode.BuiltInMushroom => "內建圖片會以像素風格顯示，作業系統停用動畫時仍保持靜態。",
            MascotAssetMode.StaticImage => "靜態圖片會固定顯示第一張畫面；建議使用帶透明背景的 PNG。",
            MascotAssetMode.AnimatedGif => "GIF 依檔案內建的影格時間循環播放；若 GIF 只有一張影格，就當作靜態圖片。",
            MascotAssetMode.SpriteSheet4x4 => "規則：圖片必須無間距等分為 4 欄×4 列，依左至右、上至下播放 16 影格，播完回到第一格循環；透明背景會依共同底線自動對齊，避免留白差異造成上下跳動。",
            _ => string.Empty
        };
    }

    private string GetFileFilter(MascotAssetMode mode) => mode switch
    {
        MascotAssetMode.StaticImage => "靜態圖片|*.png;*.jpg;*.jpeg;*.bmp;*.ico",
        MascotAssetMode.AnimatedGif => "GIF 動圖|*.gif",
        MascotAssetMode.SpriteSheet4x4 => "4×4 圖片|*.png;*.jpg;*.jpeg;*.bmp",
        _ => "圖片檔案|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico"
    };

    private void UpdateStartupStatus()
    {
        if (!IsInitialized || StartupStatusText is null) return;
        StartupStatusText.Text = StartupBox.IsChecked == true
            ? "套用後：會隨 Windows 登入自動啟動"
            : "套用後：不會隨 Windows 登入自動啟動";
    }
}
