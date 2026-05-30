namespace Forex.Wpf.Common.Services;

public static class TransliterationHelper
{
    private static readonly Dictionary<char, string> CyrillicToLatin = new()
    {
        {'а', "a"}, {'б', "b"}, {'в', "v"}, {'г', "g"}, {'д', "d"}, {'е', "e"}, {'ё', "yo"},
        {'ж', "j"}, {'з', "z"}, {'и', "i"}, {'й', "y"}, {'к', "k"}, {'л', "l"}, {'м', "m"},
        {'н', "n"}, {'о', "o"}, {'п', "p"}, {'р', "r"}, {'с', "s"}, {'т', "t"}, {'у', "u"},
        {'ф', "f"}, {'х', "x"}, {'ц', "ts"}, {'ч', "ch"}, {'ш', "sh"}, {'щ', "sh"},
        {'ъ', "'"}, {'ы', "i"}, {'ь', "'"}, {'э', "e"}, {'ю', "yu"}, {'я', "ya"},
        {'ў', "o'"}, {'қ', "q"}, {'ғ', "g'"}, {'ҳ', "h"},
        {'А', "A"}, {'Б', "B"}, {'В', "V"}, {'Г', "G"}, {'Д', "D"}, {'Е', "E"}, {'Ё', "Yo"},
        {'Ж', "J"}, {'З', "Z"}, {'И', "I"}, {'Й', "Y"}, {'К', "K"}, {'Л', "L"}, {'М', "M"},
        {'Н', "N"}, {'О', "O"}, {'П', "P"}, {'Р', "R"}, {'С', "S"}, {'Т', "T"}, {'У', "U"},
        {'Ф', "F"}, {'Х', "X"}, {'Ц', "Ts"}, {'Ч', "Ch"}, {'Ш', "Sh"}, {'Щ', "Sh"},
        {'Ъ', "'"}, {'Ы', "I"}, {'Ь', "'"}, {'Э', "E"}, {'Ю', "Yu"}, {'Я', "Ya"},
        {'Ў', "O'"}, {'Қ', "Q"}, {'Ғ', "G'"}, {'Ҳ', "H"}
    };

    private static readonly Dictionary<string, string> LatinToCyrillic = new()
    {
        {"yo", "ё"}, {"yu", "ю"}, {"ya", "я"}, {"ch", "ч"}, {"sh", "ш"}, {"ts", "ц"},
        {"o'", "ў"}, {"g'", "ғ"}, {"a", "а"}, {"b", "б"}, {"v", "в"}, {"g", "г"},
        {"d", "д"}, {"e", "е"}, {"j", "ж"}, {"z", "з"}, {"i", "и"}, {"y", "й"},
        {"k", "к"}, {"l", "л"}, {"m", "м"}, {"n", "н"}, {"o", "о"}, {"p", "п"},
        {"r", "р"}, {"s", "с"}, {"t", "т"}, {"u", "у"}, {"f", "ф"}, {"x", "х"},
        {"q", "қ"}, {"h", "ҳ"}, {"'", "ъ"}
    };

    public static string ToLatin(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var result = new System.Text.StringBuilder();
        foreach (var c in text)
        {
            result.Append(CyrillicToLatin.TryGetValue(c, out var latin) ? latin : c.ToString());
        }
        return result.ToString();
    }

    public static string ToCyrillic(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var result = text.ToLower();
        foreach (var pair in LatinToCyrillic.OrderByDescending(p => p.Key.Length))
        {
            result = result.Replace(pair.Key, pair.Value);
        }
        return result;
    }

    public static bool ContainsIgnoreScript(string source, string search)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(search))
            return false;

        var sourceLower = source.ToLowerInvariant();
        var searchLower = search.ToLowerInvariant();

        if (sourceLower.Contains(searchLower))
            return true;

        var sourceLatin = ToLatin(sourceLower).ToLowerInvariant();
        var searchLatin = ToLatin(searchLower).ToLowerInvariant();

        if (sourceLatin.Contains(searchLatin))
            return true;

        var sourceCyrillic = ToCyrillic(sourceLower);
        var searchCyrillic = ToCyrillic(searchLower);

        return sourceCyrillic.Contains(searchCyrillic);
    }
}
