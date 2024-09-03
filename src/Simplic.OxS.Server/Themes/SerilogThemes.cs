using Serilog.Sinks.SystemConsole.Themes;

namespace Simplic.OxS.Server.Themes;

/// <summary>
/// Themes for Serilog.
/// </summary>
internal static class SerilogThemes
{
    /// <summary>
    /// Theme for console logging.
    /// </summary>
    internal static readonly ConsoleTheme ConsoleTheme = new AnsiConsoleTheme(
        new Dictionary<ConsoleThemeStyle, string>
        {
            [ConsoleThemeStyle.Text] = ColorUtil.HexToAnsi("#f4f4f4"), // primary log message text
            [ConsoleThemeStyle.SecondaryText] = ColorUtil.HexToAnsi("#7d7d7d"), // exceptions, timestamps,..
            [ConsoleThemeStyle.TertiaryText] = ColorUtil.HexToAnsi("#2d2d2d"),
            [ConsoleThemeStyle.Invalid] = ColorUtil.HexToAnsi("#ab0003"),
            [ConsoleThemeStyle.Null] = ColorUtil.HexToAnsi("#0047ff"),
            [ConsoleThemeStyle.Name] = ColorUtil.HexToAnsi("#fff000"),
            [ConsoleThemeStyle.String] = ColorUtil.HexToAnsi("#6bb6e9"),
            [ConsoleThemeStyle.Boolean] = ColorUtil.HexToAnsi("#475cff"),
            [ConsoleThemeStyle.Number] = ColorUtil.HexToAnsi("#d3f263"),
            [ConsoleThemeStyle.Scalar] = ColorUtil.HexToAnsi("#f8ff9e"),
            [ConsoleThemeStyle.LevelVerbose] = ColorUtil.HexToAnsi("#e9e5ec"),
            [ConsoleThemeStyle.LevelDebug] = ColorUtil.HexToAnsi("#ec59ff"),
            [ConsoleThemeStyle.LevelInformation] = ColorUtil.HexToAnsi("#1ade00"),
            [ConsoleThemeStyle.LevelWarning] = ColorUtil.HexToAnsi("#ded300"),
            [ConsoleThemeStyle.LevelError] = ColorUtil.HexToAnsi("#de0700"),
            [ConsoleThemeStyle.LevelFatal] = ColorUtil.HexToAnsiWithBackground("#de0700", "#ffffff"),
        }
    );
}