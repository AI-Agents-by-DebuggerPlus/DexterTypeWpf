using System.Windows;
using System.Windows.Media;

namespace DexterType.Theming;

/// <summary>Цвета темы в виде hex-строк. Selected/Caret могут содержать альфа-канал (#AARRGGBB).</summary>
public sealed record ThemeDef(
    string Name, string Bg, string Panel, string Text, string Sub, string Accent, string OnAccent,
    string Border, string Sidebar, string Good, string Bad, string Selected, string Caret,
    string Pinky, string Ring, string Middle, string Index, string Thumb);

public static class Themes
{
    public static readonly ThemeDef Stamina = new("Stamina",
        "#EAF1F7", "#FFFFFF", "#1F2D3D", "#6B7C8F", "#2F80ED", "#FFFFFF",
        "#C9D6E2", "#F4F8FB", "#2E9E5B", "#D64545", "#332F80ED", "#FFE680",
        "#F4B6C2", "#B7E1A1", "#FFE08A", "#9CCBF5", "#D9D9E3");

    public static readonly ThemeDef Candy = new("Сладкая",
        "#F8F9FB", "#FFFFFF", "#2C3E50", "#7B8794", "#7E57C2", "#FFFFFF",
        "#E3E6EC", "#F1F3F8", "#43A047", "#E53935", "#337E57C2", "#FFF176",
        "#B5A2D8", "#A5D6A7", "#F48FB1", "#80DEEA", "#FFE082");

    public static readonly ThemeDef Dark = new("Тёмная",
        "#1B1F27", "#252B36", "#E6EAF0", "#97A3B6", "#4C9AFF", "#0B1220",
        "#394353", "#20252E", "#4CC38A", "#FF6B6B", "#444C9AFF", "#6B5A00",
        "#7B4B94", "#3E7B4F", "#9C3D5E", "#2C7A8C", "#8A6A1F");

    public static readonly ThemeDef Mono = new("Монохром",
        "#F2F2F2", "#FFFFFF", "#111111", "#6E6E6E", "#444444", "#FFFFFF",
        "#CFCFCF", "#EAEAEA", "#2E7D32", "#C62828", "#33444444", "#D0D0D0",
        "#CFCFCF", "#E0E0E0", "#BDBDBD", "#D6D6D6", "#EDEDED");

    public static readonly ThemeDef Matrix = new("Matrix",
        "#0D0208", "#001B1F", "#00FF41", "#008F11", "#00FF41", "#001B1F",
        "#008F11", "#07131A", "#00FF41", "#FF3B3B", "#4400FF41", "#005F1A",
        "#006B3C", "#007A33", "#00A650", "#00C060", "#004D26");

    public static readonly ThemeDef[] All = { Stamina, Candy, Dark, Mono, Matrix };

    public static ThemeDef ByName(string name) => All.FirstOrDefault(t => t.Name == name) ?? Stamina;

    public static void Apply(ThemeDef t)
    {
        var r = Application.Current.Resources;
        r["ThemeBackground"] = B(t.Bg);
        r["ThemePanel"] = B(t.Panel);
        r["ThemeText"] = B(t.Text);
        r["ThemeSubText"] = B(t.Sub);
        r["ThemeAccent"] = B(t.Accent);
        r["ThemeOnAccent"] = B(t.OnAccent);
        r["ThemeBorder"] = B(t.Border);
        r["ThemeSidebar"] = B(t.Sidebar);
        r["ThemeGood"] = B(t.Good);
        r["ThemeBad"] = B(t.Bad);
        r["ThemeSelected"] = B(t.Selected);
        r["ThemeCaret"] = B(t.Caret);
        r["ColorPinky"] = C(t.Pinky);
        r["ColorRing"] = C(t.Ring);
        r["ColorMiddle"] = C(t.Middle);
        r["ColorIndex"] = C(t.Index);
        r["ColorThumb"] = C(t.Thumb);
    }

    static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    static SolidColorBrush B(string hex)
    {
        var b = new SolidColorBrush(C(hex));
        b.Freeze();
        return b;
    }

    public static Color GetColor(string key) => (Color)Application.Current.Resources[key];
}
