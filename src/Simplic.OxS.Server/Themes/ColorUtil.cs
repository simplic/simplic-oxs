namespace Simplic.OxS.Server.Themes;

/// <summary>
/// Utility class for colors.
/// </summary>
public static class ColorUtil
{
    /// <summary>
    /// Returns the full ansi color code from a hex color code.
    /// </summary>
    public static string HexToAnsi(string hex) => $"\x1b[38;5;{HexToAnsi256(hex)}m";

    /// <summary>
    /// Returns the full ansi color code that consists of foreground and background from given hex color codes.
    /// </summary>
    /// <param name="fgHex">Foreground</param>
    /// <param name="bgHex">Background</param>
    public static string HexToAnsiWithBackground(string fgHex, string bgHex) =>
        $"\x1b[38;5;{HexToAnsi256(fgHex)}m\x1b[48;5;{HexToAnsi256(bgHex)}m";


    /// <summary>
    /// Returns the ANSI-256 integer value from a hex color code
    /// </summary>
    /// <param name="hex"></param>
    /// <returns></returns>
    public static int HexToAnsi256(string hex)
    {
        // Remove the '#' if it exists
        if (hex.StartsWith('#'))
        {
            hex = hex[1..];
        }

        // Parse hex string to RGB
        var r = Convert.ToInt32(hex.Substring(0, 2), 16);
        var g = Convert.ToInt32(hex.Substring(2, 2), 16);
        var b = Convert.ToInt32(hex.Substring(4, 2), 16);

        // Convert RGB to ANSI 256-color code
        return RgbToAnsi256(r, g, b);
    }

    /// <summary>
    /// Returns the ANSI-256 integer value from RGB values.
    /// </summary>
    public static int RgbToAnsi256(int r, int g, int b)
    {
        // Convert RGB to 256-color code
        var ansi = 16 + (36 * (r / 43)) + (6 * (g / 43)) + (b / 43);
        return Math.Min(ansi, 255); // Ensure the value is within the 0-255 range
    }
}