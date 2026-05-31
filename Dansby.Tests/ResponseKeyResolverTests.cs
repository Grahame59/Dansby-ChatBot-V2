using Pipes.Nlp.Mapping.Responses;
using Xunit;

namespace Dansby.Tests;

public sealed class ResponseKeyResolverTests
{
    [Fact]
    public void CandidatesFor_KnownCanonicalIntent_ReturnsCanonicalThenLegacyKeys()
    {
        var candidates = ResponseKeyResolver.CandidatesFor("chat.greet").ToArray();

        Assert.Equal(["chat.greet", "greetings"], candidates);
    }

    [Fact]
    public void CandidatesFor_KnownCanonicalIntent_IsCaseInsensitive()
    {
        var candidates = ResponseKeyResolver.CandidatesFor("CHAT.GREET").ToArray();

        Assert.Equal(["chat.greet", "greetings"], candidates);
    }

    [Fact]
    public void CandidatesFor_DynamicIntentWithNoLegacyKeys_ReturnsCanonicalIntent()
    {
        var candidates = ResponseKeyResolver.CandidatesFor("sys.time.date").ToArray();

        Assert.Equal(["sys.time.date"], candidates);
    }

    [Fact]
    public void CandidatesFor_UnknownIntent_ReturnsTheInputIntent()
    {
        var candidates = ResponseKeyResolver.CandidatesFor("plex.play").ToArray();

        Assert.Equal(["plex.play"], candidates);
    }
}
