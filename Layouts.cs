namespace DexterType;

public record Layout(string Name, string[] Rows, string[] Groups, string[] Words,
                     string[]? Names = null, string[]? Exercises = null)
{
    /// <summary>Название урока для списка в интерфейсе (иначе — набор клавиш).</summary>
    public string LessonName(int i) => Names is { } n && i >= 0 && i < n.Length ? n[i] : Groups[i];
}

public static class Layouts
{
    // Палец для каждой позиции в ряду: 0-3 левая рука, 4-7 правая
    public static readonly int[] Fingers = { 0, 1, 2, 3, 3, 4, 4, 5, 6, 7, 7, 7 };

    public static readonly string[] FingerNames =
    {
        "левый мизинец", "левый безымянный палец", "левый средний палец", "левый указательный палец",
        "правый указательный палец", "правый средний палец", "правый безымянный палец", "правый мизинец"
    };

    // Уроки построены по слоговому методу (мнемо-блоки частых сочетаний + ритм):
    // сначала домашний ряд, затем блоки коротких сочетаний, предлогов/окончаний,
    // трёхбуквенных ритмов и фраз. Только буквы, которые есть на клавиатуре тренажёра.

    static readonly string[] MnemoNames =
    {
        "домашний ряд и ритм", "блок 1: короткие сочетания", "блок 2: предлоги и окончания",
        "блок 3: трёхбуквенные ритмы", "блок 4: новые буквы в ритме", "фразы на закрепление"
    };

    public static readonly Layout Ru = new("Русский",
        new[] { "йцукенгшщзхъ", "фывапролджэ", "ячсмитьбю" },
        new[] { "фывапролджэ", "авылдфо", "тонаепоздикмтр", "проткансмирд",
                "сутьжилщпкецгрузмы", "тотпортрозакнведч" },
        ("и в не на что он я с это быть человек год работа время дело жизнь день рука ночь глаз " +
         "слово место вопрос мир дом город страна вода земля огонь свет путь дорога дверь окно стол " +
         "стул книга письмо друг семья мать отец сын дочь дети школа урок учитель ученик знание").Split(' '),
        MnemoNames,
        new[]
        {
            "фыва олдж фыва олдж ао вылд рп жэ олд фыв",
            "ав ва ад да фа аф ыл лы ол ло од до ав ва ад да",
            "то на не по от до за из ком ток дом рот то на не по",
            "про тра кар пар нос сон ток кот мир рим дар рад про тра",
            "суть жил щуп кедр цены груз мышь утро вода земля",
            "тот порт та роза то окно утро день вечер ночь"
        });

    public static readonly Layout En = new("English",
        new[] { "qwertyuiop", "asdfghjkl", "zxcvbnm" },
        new[] { "asdfjkl", "asdfilod", "toonoforatinitheand", "theandingionentest",
                "quickbrownjumpslazydogvex", "thequickbrownfoxjumps" },
        ("the of and to in is you that it he was for on are as with his they at be this have from or one " +
         "had by word but not what all were we when your can said there use an each which she do how their " +
         "if will up other about out many then them these so some her would make like him into time has look " +
         "two more write go see number no way could people my than first water been call who oil its now find").Split(' '),
        MnemoNames,
        new[]
        {
            "asdf jkl asdf jkl aaa sss ddd fff jjj kkk lll",
            "as sa ad da fa af is si lo ol od do as sa ad da",
            "to on no of or at in it the and for you to on no of",
            "the and for you ing ion ent est tion ate the and for",
            "quick brown jumps lazy dog vex nymph swift glow",
            "the quick brown fox jumps over the lazy dog she sells sea"
        });

    public static readonly Layout[] All = { Ru, En };
}
