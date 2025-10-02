using System.Collections.Immutable;

namespace Pipes.Nlp.Mapping;

public static class StopWords
{
    // Keep this small and high-signal; can be expanded later.
    public static readonly ImmutableHashSet<string> En = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // articles, pronouns, auxiliaries, preps, misc
        "a","an","the","and","or","but","so",
        "i","you","he","she","it","we","they","me","him","her","us","them",
        "my","his","her","its","our","their",
        "am","are","was","were","be","been","being",
        "do","does","did","doing",
        "have","has","had","having",
        "can","could","may","might","shall","should","will","would","must",
        "to","of","in","on","at","for","from","by","with","about","as","into","over","after","before","between","under","above","out","up","down",
        "that","this","these","those","there","here","then","than",
        "what","which","who","whom","whose","when","where","why","how",
        "not","no","nor","if","because","while","until","once",
        // common contractions (normalized)
        "im","i'm","ive","i've","id","i'd","ill","i'll",
        "youre","you're","youve","you've","youd","you'd","youll","you'll",
        "hes","he's","shes","she's","its","it's","were","we're","weve","we've","well","we'll",
        "theyre","they're","theyve","they've","theyd","they'd","theyll","they'll",
        "cant","can't","dont","don't","doesnt","doesn't","isnt","isn't","arent","aren't","wont","won't","shouldnt","shouldn't","couldnt","couldn't",
        "ya","yo","hey","hi","hello" // greetings (low-signal for intent disambiguation)
    }.ToImmutableHashSet();
}
