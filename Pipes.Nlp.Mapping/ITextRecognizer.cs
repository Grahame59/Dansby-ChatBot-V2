
namespace Pipes.Nlp.Mapping;

public interface ITextRecognizer
{
    (string intent, double score, Dictionary<string, string> slots, string domain) Recognize(string text);
}
