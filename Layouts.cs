namespace DexterType;

public record Layout(string Name, string[] Rows, string[] Groups, string[] Words);

public static class Layouts
{
    // Палец для каждой позиции в ряду: 0-3 левая рука, 4-7 правая
    public static readonly int[] Fingers = { 0, 1, 2, 3, 3, 4, 4, 5, 6, 7, 7, 7 };

    public static readonly string[] FingerNames =
    {
        "левый мизинец", "левый безымянный палец", "левый средний палец", "левый указательный палец",
        "правый указательный палец", "правый средний палец", "правый безымянный палец", "правый мизинец"
    };

    public static readonly Layout Ru = new("Русский",
        new[] { "йцукенгшщзхъ", "фывапролджэ", "ячсмитьбю" },
        new[] { "аоывлд", "пржфэ", "кенг", "шщзхъ", "мить", "чсяб", "ю", "йцу" },
        "и в не на я быть он с что а по это она этот к но они мы как из у который то за свой весь год от так о для ты же все тот мочь вы человек такой его только или бы себя один уже до время если сам когда другой вот говорить наш мой при дело жизнь кто первый очень два день новый рука даже".Split(' '));

    public static readonly Layout En = new("English",
        new[] { "qwertyuiop", "asdfghjkl", "zxcvbnm" },
        new[] { "fjdksla", "gh", "ei", "ru", "tyo", "wqp", "cvnm", "bzx" },
        "the be to of and in that have it for not on with he as you do at this but his by from they we say her she or an will my one all would there their what so up out if about who get which go me when make can like time no just him know take people into year your good some could them see other than then now look only come".Split(' '));

    public static readonly Layout[] All = { Ru, En };
}
