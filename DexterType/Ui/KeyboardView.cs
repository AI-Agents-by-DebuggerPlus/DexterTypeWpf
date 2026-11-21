using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Dexter.Core.Keyboard;
using DexterType.Theming;

namespace DexterType.Ui;

/// <summary>
/// Экранная клавиатура: цвет клавиши зависит от пальца, следующая клавиша подсвечивается.
/// Для заглавных и знаков с Shift подсвечивается и нужный Shift (противоположной рукой).
/// Вписывается в доступное место (Viewbox), поэтому масштабируется вместе с окном.
/// </summary>
public sealed class KeyboardView : Viewbox
{
    const double Unit = 46, Gap = 5;

    readonly Canvas _canvas = new();
    readonly List<Border> _keys = new();
    readonly List<Border> _lit = new();
    readonly Dictionary<KeyDef, Border> _byKey = new(ReferenceEqualityComparer.Instance);
    KeyboardLayoutDef? _layout;

    public KeyboardView()
    {
        Stretch = Stretch.Uniform;
        Child = _canvas;
    }

    public void SetLayout(KeyboardLayoutDef layout)
    {
        _layout = layout;
        _canvas.Children.Clear();
        _keys.Clear(); _lit.Clear(); _byKey.Clear();

        double y = 0, maxX = 0;
        foreach (var row in layout.Rows)
        {
            double x = 0;
            foreach (var key in row)
            {
                if (key.Kind != KeyKind.Spacer)
                {
                    var border = MakeKey(key, key.Width * Unit - Gap);
                    Canvas.SetLeft(border, x);
                    Canvas.SetTop(border, y);
                    _canvas.Children.Add(border);
                    _keys.Add(border);
                    _byKey[key] = border;
                }
                x += key.Width * Unit;
            }
            maxX = Math.Max(maxX, x);
            y += Unit;
        }
        _canvas.Width = maxX - Gap;
        _canvas.Height = y - Gap;
        Repaint();
    }

    /// <summary>Подсветить клавишу для символа (null — снять подсветку).</summary>
    public void Highlight(char? c)
    {
        foreach (var b in _lit) Paint(b, false);
        _lit.Clear();
        if (c is null || _layout is null) return;

        var hit = _layout.Find(c.Value);
        if (hit is null) return;
        Light(hit.Key);
        if (hit.NeedsShift && hit.ShiftKey is not null) Light(hit.ShiftKey);
    }

    /// <summary>Перекрасить после смены темы.</summary>
    public void Repaint()
    {
        foreach (var b in _keys) Paint(b, _lit.Contains(b));
    }

    void Light(KeyDef key)
    {
        if (!_byKey.TryGetValue(key, out var b)) return;
        Paint(b, true);
        _lit.Add(b);
    }

    static Border MakeKey(KeyDef key, double width)
    {
        var grid = new Grid { IsHitTestVisible = false };
        bool hasSub = key.Sub is not null;

        var main = new TextBlock
        {
            Text = key.Main,
            FontSize = key.Kind == KeyKind.Char ? 17 : 12,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = hasSub ? VerticalAlignment.Bottom : VerticalAlignment.Center,
            Margin = hasSub ? new Thickness(0, 0, 0, 4) : new Thickness(0),
        };
        main.SetResourceReference(TextBlock.ForegroundProperty, "ThemeText");
        grid.Children.Add(main);

        if (key.Sub is string sub)
        {
            var small = new TextBlock
            {
                Text = sub, FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 5, 0),
            };
            small.SetResourceReference(TextBlock.ForegroundProperty, "ThemeSubText");
            grid.Children.Add(small);
        }

        return new Border
        {
            Width = width, Height = Unit - Gap,
            CornerRadius = new CornerRadius(7),
            BorderThickness = new Thickness(2),
            Child = grid, Tag = key,
        };
    }

    static void Paint(Border b, bool on)
    {
        var key = (KeyDef)b.Tag;
        if (key.Kind == KeyKind.Decor)
        {
            b.SetResourceReference(Border.BackgroundProperty, "ThemeSidebar");
            b.SetResourceReference(Border.BorderBrushProperty, "ThemeBorder");
            return;
        }

        var c = FingerColor(key.Finger);
        if (on)
        {
            b.Background = new SolidColorBrush(c);
            b.SetResourceReference(Border.BorderBrushProperty, "ThemeText");
        }
        else
        {
            b.Background = new SolidColorBrush(Color.FromArgb(125, c.R, c.G, c.B));
            b.BorderBrush = Brushes.Transparent;
        }
    }

    static Color FingerColor(int finger) => Themes.GetColor(finger switch
    {
        -1 => "ColorThumb",
        0 or 7 => "ColorPinky",
        1 or 6 => "ColorRing",
        2 or 5 => "ColorMiddle",
        _ => "ColorIndex",
    });
}
