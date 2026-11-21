namespace Dexter.Core.Keyboard;

public enum KeyKind { Char, Shift, Space, Decor, Spacer }

/// <summary>Одна клавиша. Finger: -1 большой палец, 0..3 левая рука (мизинец→указательный), 4..7 правая (указательный→мизинец).</summary>
public sealed record KeyDef(string Label, char Normal, char Shifted, int Finger, double Width = 1.0, KeyKind Kind = KeyKind.Char)
{
    public bool IsTypeable => Kind is KeyKind.Char or KeyKind.Space;

    /// <summary>Крупная надпись на клавише (для букв — заглавная).</summary>
    public string Main => Kind == KeyKind.Char
        ? (char.IsLetter(Normal) ? char.ToUpperInvariant(Normal).ToString() : Normal.ToString())
        : Label;

    /// <summary>Мелкая надпись (символ с Shift) для небуквенных клавиш.</summary>
    public string? Sub => Kind == KeyKind.Char && !char.IsLetter(Normal) && Shifted != '\0' && Shifted != Normal
        ? Shifted.ToString() : null;
}

/// <summary>Результат поиска символа на клавиатуре: клавиша, нужен ли Shift и какой именно.</summary>
public sealed record KeyHit(KeyDef Key, bool NeedsShift, KeyDef? ShiftKey);

public static class Fingers
{
    public static readonly string[] Names =
    {
        "левый мизинец", "левый безымянный палец", "левый средний палец", "левый указательный палец",
        "правый указательный палец", "правый средний палец", "правый безымянный палец", "правый мизинец"
    };

    public static string Name(int finger) => finger < 0 ? "большой палец" : Names[finger];
}

public sealed class KeyboardLayoutDef
{
    readonly Dictionary<char, KeyHit> _map = new();

    public string Id { get; }
    public string Name { get; }
    public IReadOnlyList<IReadOnlyList<KeyDef>> Rows { get; }

    public KeyboardLayoutDef(string id, string name, IReadOnlyList<IReadOnlyList<KeyDef>> rows)
    {
        Id = id; Name = name; Rows = rows;

        var shifts = rows.SelectMany(r => r).Where(k => k.Kind == KeyKind.Shift).ToList();
        KeyDef? leftShift = shifts.FirstOrDefault(k => k.Finger <= 3);
        KeyDef? rightShift = shifts.FirstOrDefault(k => k.Finger >= 4);

        foreach (var key in rows.SelectMany(r => r).Where(k => k.IsTypeable))
        {
            _map[key.Normal] = new KeyHit(key, false, null);
            if (key.Shifted != '\0' && key.Shifted != key.Normal)
            {
                // Буква левой руки — Shift правой рукой, и наоборот.
                var shift = key.Finger is >= 0 and <= 3 ? rightShift : leftShift;
                _map[key.Shifted] = new KeyHit(key, true, shift);
            }
        }
    }

    public KeyHit? Find(char c) => _map.TryGetValue(c, out var h) ? h : null;
    public bool Contains(char c) => _map.ContainsKey(c);
}

public static class KeyboardLayouts
{
    // Палец по физической колонке (общая схема для всех рядов).
    static readonly int[] F0 = { 0, 0, 1, 2, 3, 3, 4, 4, 5, 6, 7, 7, 7 };           // цифровой ряд (13 клавиш)
    static readonly int[] F1 = { 0, 1, 2, 3, 3, 4, 4, 5, 6, 7, 7, 7, 7 };           // верхний ряд (13)
    static readonly int[] F2 = { 0, 1, 2, 3, 3, 4, 4, 5, 6, 7, 7 };                 // домашний ряд (11)
    static readonly int[] F3 = { 0, 1, 2, 3, 3, 4, 4, 5, 6, 7 };                    // нижний ряд (10)

    static KeyDef[] Chars(string normal, string shifted, int[] fingers)
    {
        if (normal.Length != shifted.Length || normal.Length != fingers.Length)
            throw new InvalidOperationException($"Ряд клавиш задан неверно: {normal}");
        var keys = new KeyDef[normal.Length];
        for (int i = 0; i < keys.Length; i++)
            keys[i] = new KeyDef("", normal[i], shifted[i], fingers[i]);
        return keys;
    }

    static IReadOnlyList<KeyDef> Row(params object[] parts)
    {
        var list = new List<KeyDef>();
        foreach (var p in parts)
        {
            if (p is KeyDef k) list.Add(k);
            else if (p is KeyDef[] arr) list.AddRange(arr);
        }
        return list;
    }

    static KeyDef Decor(string label, double w, int finger) => new(label, '\0', '\0', finger, w, KeyKind.Decor);
    static KeyDef Shift(double w, int finger) => new("Shift", '\0', '\0', finger, w, KeyKind.Shift);
    static KeyDef Spacer(double w) => new("", '\0', '\0', -1, w, KeyKind.Spacer);
    static KeyDef Space() => new("", ' ', '\0', -1, 6.25, KeyKind.Space);

    static KeyDef[] WithLastWidth(KeyDef[] keys, double w)
    {
        keys[^1] = keys[^1] with { Width = w };
        return keys;
    }

    static KeyboardLayoutDef Build(string id, string name, string r0, string r0s, string r1, string r1s,
                                   string r2, string r2s, string r3, string r3s)
    {
        var rows = new List<IReadOnlyList<KeyDef>>
        {
            Row(Chars(r0, r0s, F0), Decor("⌫", 2.0, 7)),
            Row(Decor("Tab", 1.5, 0), WithLastWidth(Chars(r1, r1s, F1), 1.5)),
            Row(Decor("Caps", 1.75, 0), Chars(r2, r2s, F2), Decor("Enter", 2.25, 7)),
            Row(Shift(2.25, 0), Chars(r3, r3s, F3), Shift(2.75, 7)),
            Row(Spacer(3.75), Space()),
        };
        return new KeyboardLayoutDef(id, name, rows);
    }

    public static readonly KeyboardLayoutDef Ru = Build("ru", "Русская (ЙЦУКЕН)",
        "ё1234567890-=", "Ё!\"№;%:?*()_+",
        "йцукенгшщзхъ\\", "ЙЦУКЕНГШЩЗХЪ/",
        "фывапролджэ", "ФЫВАПРОЛДЖЭ",
        "ячсмитьбю.", "ЯЧСМИТЬБЮ,");

    public static readonly KeyboardLayoutDef En = Build("en", "English (QWERTY)",
        "`1234567890-=", "~!@#$%^&*()_+",
        "qwertyuiop[]\\", "QWERTYUIOP{}|",
        "asdfghjkl;'", "ASDFGHJKL:\"",
        "zxcvbnm,./", "ZXCVBNM<>?");

    public static readonly KeyboardLayoutDef[] All = { Ru, En };

    public static KeyboardLayoutDef ById(string id) =>
        All.FirstOrDefault(l => string.Equals(l.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? throw new KeyNotFoundException($"Неизвестная раскладка: {id}");

    public static KeyboardLayoutDef? TryById(string id) =>
        All.FirstOrDefault(l => string.Equals(l.Id, id, StringComparison.OrdinalIgnoreCase));
}
