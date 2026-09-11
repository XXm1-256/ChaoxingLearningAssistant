using System.Windows.Media;
using ChaoxingLearningAssistant.Models;
using Microsoft.Win32;

namespace ChaoxingLearningAssistant.Services;

public sealed class ThemeService
{
    public void Apply(ThemeMode mode)
    {
        var actual = mode == ThemeMode.System
            ? (IsSystemLightTheme() ? ThemeMode.Light : ThemeMode.Dark)
            : mode;

        if (actual == ThemeMode.Dark)
        {
            SetBrush("WindowBackgroundBrush", "#081510");
            SetBrush("PanelBackgroundBrush", "#0F2119");
            SetBrush("SurfaceElevatedBrush", "#14291F");
            SetBrush("SurfaceMutedBrush", "#193326");
            SetBrush("InputBackgroundBrush", "#0B1A14");
            SetBrush("PrimaryTextBrush", "#F2FBF5");
            SetBrush("SecondaryTextBrush", "#C1D8CA");
            SetBrush("TertiaryTextBrush", "#91B09E");
            SetBrush("BorderBrush", "#284638");
            SetBrush("BorderStrongBrush", "#3B6851");
            SetBrush("AccentBrush", "#62D18E");
            SetBrush("AccentSecondaryBrush", "#8BDCB0");
            SetBrush("AccentSoftBrush", "#1B4832");
            SetBrush("HoverBrush", "#214434");
            SetBrush("SelectedBrush", "#28563F");
            SetBrush("SuccessBrush", "#62D18E");
            SetBrush("WarningBrush", "#F7C65F");
            SetBrush("DangerBrush", "#FF6B7A");
        }
        else
        {
            SetBrush("WindowBackgroundBrush", "#EEF6F0");
            SetBrush("PanelBackgroundBrush", "#FBFEFC");
            SetBrush("SurfaceElevatedBrush", "#F4FAF6");
            SetBrush("SurfaceMutedBrush", "#E5F3E9");
            SetBrush("InputBackgroundBrush", "#F7FCF8");
            SetBrush("PrimaryTextBrush", "#183126");
            SetBrush("SecondaryTextBrush", "#4E6C5C");
            SetBrush("TertiaryTextBrush", "#6E897A");
            SetBrush("BorderBrush", "#CDE1D4");
            SetBrush("BorderStrongBrush", "#9FC5AE");
            SetBrush("AccentBrush", "#3D9B67");
            SetBrush("AccentSecondaryBrush", "#73C998");
            SetBrush("AccentSoftBrush", "#D8F0E1");
            SetBrush("HoverBrush", "#E2F3E8");
            SetBrush("SelectedBrush", "#CFEDDA");
            SetBrush("SuccessBrush", "#27895A");
            SetBrush("WarningBrush", "#9A6B0D");
            SetBrush("DangerBrush", "#C9384E");
        }
    }

    private static bool IsSystemLightTheme()
    {
        try
        {
            var value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme",
                1);

            return Convert.ToInt32(value) != 0;
        }
        catch
        {
            return false;
        }
    }

    private static void SetBrush(string key, string color)
    {
        System.Windows.Application.Current.Resources[key] =
            new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color)!);
    }
}
