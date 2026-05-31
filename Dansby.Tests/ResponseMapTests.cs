using Pipes.Nlp.Mapping.Responses;
using Xunit;

namespace Dansby.Tests;

public sealed class ResponseMapTests
{
    [Fact]
    public async Task Pick_ZebraPrintFailed_ReturnsFailureGuidance()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "Dansby.Core.Api",
            "response_mappings.json");
        var map = new ResponseMap(path);

        await map.ReloadAsync();

        var response = map.Pick("zebra.print.failed");

        Assert.False(string.IsNullOrWhiteSpace(response));
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
