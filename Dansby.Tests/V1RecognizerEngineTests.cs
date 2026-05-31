using Microsoft.Extensions.Logging.Abstractions;
using Pipes.Nlp.Mapping;
using Xunit;

namespace Dansby.Tests;

public sealed class V1RecognizerEngineTests
{
    [Theory]
    [InlineData("hello", "chat.greet")]
    [InlineData("what time is it", "sys.time.now")]
    [InlineData("what day is it?", "sys.time.dayofweek")]
    [InlineData("print label: Hello World", "zebra.print.simple")]
    public void RecognizeBest_ForKnownUtterance_ReturnsExpectedIntent(string text, string expectedIntent)
    {
        var engine = CreateLoadedEngine();

        var (intent, score) = engine.RecognizeBest(text);

        Assert.Equal(expectedIntent, intent);
        Assert.True(score > 0);
    }

    [Fact]
    public void RecognizeBest_ForBlankInput_ReturnsUnknown()
    {
        var engine = CreateLoadedEngine();

        var (intent, score) = engine.RecognizeBest("");

        Assert.Equal("unknown", intent);
        Assert.Equal(0.0, score);
    }

    [Fact]
    public void RecognizeBest_WhenUtteranceExactlyMatchesExample_ReturnsPerfectScore()
    {
        var engine = CreateLoadedEngine();

        var (intent, score) = engine.RecognizeBest("what day is it?");

        Assert.Equal("sys.time.dayofweek", intent);
        Assert.Equal(1.0, score);
    }

    private static V1RecognizerEngine CreateLoadedEngine()
    {
        var engine = new V1RecognizerEngine(
            NullLogger<V1RecognizerEngine>.Instance,
            new V1Tokenizer());

        engine.Load(Path.Combine(FindRepositoryRoot(), "Pipes.Nlp.Mapping", "intent_mappings.json"));
        return engine;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Dansby.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root containing Dansby.sln.");
    }
}
