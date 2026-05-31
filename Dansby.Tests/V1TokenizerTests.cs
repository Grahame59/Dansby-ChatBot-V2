using Pipes.Nlp.Mapping;
using Xunit;

namespace Dansby.Tests;

public sealed class V1TokenizerTests
{
    [Fact]
    public void Tokenize_NormalizesCaseAndStripsPunctuation()
    {
        var tokenizer = new V1Tokenizer();

        var tokens = tokenizer.Tokenize("  Hello, DANSBY!!!  ", filterStopWords: false);

        Assert.Equal(["hello", "dansby"], tokens);
    }

    [Fact]
    public void Tokenize_WithStopWordsEnabled_FiltersLowSignalWordsForLongInputs()
    {
        var tokenizer = new V1Tokenizer();

        var tokens = tokenizer.Tokenize("can you turn on the kitchen light");

        Assert.Equal(["turn", "kitchen", "light"], tokens);
    }

    [Fact]
    public void Tokenize_KeepsVeryShortQueriesIntact()
    {
        var tokenizer = new V1Tokenizer();

        var tokens = tokenizer.Tokenize("what time");

        Assert.Equal(["what", "time"], tokens);
    }
}
