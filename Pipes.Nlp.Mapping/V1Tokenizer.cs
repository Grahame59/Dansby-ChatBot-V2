using System.Text;
using System.Text.RegularExpressions;

namespace Pipes.Nlp.Mapping;

public interface ITokenizer
{
    IReadOnlyList<string> Tokenize(string text, bool filterStopWords = true);
}

public sealed class V1Tokenizer : ITokenizer
{
    // collapse whitespace, strip most punctuation (keep alphanumerics and basic symbols)
    private static readonly Regex Splitter = new(@"[^\p{L}\p{N}\+@#']+", RegexOptions.Compiled);

    public IReadOnlyList<string> Tokenize(string text, bool filterStopWords = true)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();

        // 1) normalize
        text = text.Trim().ToLowerInvariant();

        // 2) expand a few contractions (cheap)
        text = text
            .Replace("’", "'")       // smart apostrophes
            .Replace("what’s", "what's")
            .Replace("it’s", "it's")
            .Replace("i’m", "i'm");

        // 3) split
        var raw = Splitter.Split(text)
                          .Where(t => !string.IsNullOrWhiteSpace(t))
                          .ToArray();

        // 3.5) Keep very short queries intact
        if (!filterStopWords || raw.Length <= 3)
            return raw;

        // 4) filter stopwords
        var filtered = raw.Where(t => !StopWords.En.Contains(t)).ToArray();

        // 5) ensure not empty: if everything got filtered, fall back to raw
        return filtered.Length > 0 ? filtered : raw;
    }
}
