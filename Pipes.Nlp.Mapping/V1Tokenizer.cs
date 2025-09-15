using System.Text.RegularExpressions;

namespace Pipes.Nlp.Mapping;

public sealed class V1Tokenizer
{
    public List<string> Tokenize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new();
        input = input.ToLowerInvariant();
        string[] words = Regex.Split(input, @"\W+");
        return words.Where(w => !string.IsNullOrWhiteSpace(w)).ToList();
    }
}
