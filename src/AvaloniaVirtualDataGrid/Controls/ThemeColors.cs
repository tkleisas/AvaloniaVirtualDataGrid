using Avalonia;
using Avalonia.Media;

namespace AvaloniaVirtualDataGrid.Controls;

/// <summary>
/// Theme-aware brushes for the grid: backgrounds stay transparent so the host's themed
/// background shows through, separators use a low-alpha neutral that reads on light and
/// dark, and selection/sort indicators follow the application's accent color.
/// </summary>
internal static class ThemeColors
{
    /// <summary>The application's accent color (falls back to a default blue).</summary>
    public static Color Accent()
    {
        if (Application.Current is { } app &&
            app.TryGetResource("SystemAccentColor", app.ActualThemeVariant, out var resource) &&
            resource is Color color)
        {
            return color;
        }

        return Color.FromRgb(0, 120, 215);
    }

    /// <summary>A translucent accent brush for selected rows.</summary>
    public static IBrush SelectionBrush() => new SolidColorBrush(Accent(), 0.28);

    /// <summary>A translucent accent brush for sort/focus indicators.</summary>
    public static IBrush AccentBrush() => new SolidColorBrush(Accent());

    /// <summary>A low-alpha neutral brush for grid lines/borders (works on light and dark).</summary>
    public static IBrush Separator() => new SolidColorBrush(Color.FromArgb(40, 128, 128, 128));

    /// <summary>A subtle translucent background for the header strip.</summary>
    public static IBrush HeaderBackground() => new SolidColorBrush(Color.FromArgb(28, 128, 128, 128));

    /// <summary>A mid-gray foreground for muted text, readable on light and dark.</summary>
    public static IBrush MutedForeground() => new SolidColorBrush(Color.FromRgb(150, 150, 150));
}
