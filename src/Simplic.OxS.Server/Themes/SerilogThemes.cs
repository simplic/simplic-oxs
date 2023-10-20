using Serilog.Sinks.SystemConsole.Themes;

namespace Simplic.OxS.Server.Themes;

/// <summary>
/// Themes for serilog.
/// </summary>
internal static class SerilogThemes
{
    /// <summary>
    /// Theme for console logging.
    /// </summary>
    internal static readonly ConsoleTheme ConsoleTheme = new AnsiConsoleTheme(
            new Dictionary<ConsoleThemeStyle, string>
            {
                [ConsoleThemeStyle.Text] = "\x1b[38;5;28m", // Green
                [ConsoleThemeStyle.SecondaryText] = "\x1b[38;5;245m", // Light grey
                [ConsoleThemeStyle.TertiaryText] = "\x1b[38;5;242m", // Dark grey
                [ConsoleThemeStyle.Invalid] = "\x1b[38;5;160m", // Red
                [ConsoleThemeStyle.Null] = "\x1b[38;5;242m", // Dark grey
                [ConsoleThemeStyle.Name] = "\x1b[38;5;45m", // Blue
                [ConsoleThemeStyle.String] = "\x1b[38;5;110m", // Light green
                [ConsoleThemeStyle.Number] = "\x1b[38;5;167m", // Orange
                [ConsoleThemeStyle.Boolean] = "\x1b[38;5;110m", // Light green
                [ConsoleThemeStyle.Scalar] = "\x1b[38;5;242m", // Dark grey
                [ConsoleThemeStyle.LevelVerbose] = "\x1b[38;5;242m", // Dark grey
                [ConsoleThemeStyle.LevelDebug] = "\x1b[38;5;200m", // Magenta
                [ConsoleThemeStyle.LevelInformation] = "\x1b[38;5;34m", // Green
                [ConsoleThemeStyle.LevelWarning] = "\x1b[38;5;220m", // Yellow
                [ConsoleThemeStyle.LevelError] = "\x1b[38;5;160m", // Red
                [ConsoleThemeStyle.LevelFatal] = "\x1b[48;5;9;38;5;16m", // White on red
            }
        );
}