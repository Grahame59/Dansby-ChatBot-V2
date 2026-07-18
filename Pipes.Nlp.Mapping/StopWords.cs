using System.Collections.Immutable;

namespace Pipes.Nlp.Mapping;

public static class StopWords
{
    // Keep this small and high-signal; can be expanded later.
    public static readonly ImmutableHashSet<string> En = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // articles, pronouns, auxiliaries, preps, misc (A few that may interupt meaning or context)
        "a","an","or","but","so", //"is"
        "you","he","she","it","we","they","me","him","her","us","them", //"your",
        "his","her","its","our","their",
        "am","are","was","were","be","been","being",
        "can","could","may","might","shall","should","will","would","must",
        "not","no","nor","because","while","until","once",
        // common contractions (normalized)
        "youre","you're","youve","you've","youd","you'd","youll","you'll",
        "hes","he's","shes","she's","its","it's","were","we're","weve","we've","well","we'll",
        "ya","yo","hey","hi","hello" // greetings (low-signal for intent disambiguation)
    }.ToImmutableHashSet();
}
