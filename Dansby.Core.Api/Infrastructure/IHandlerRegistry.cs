using Dansby.Shared;

namespace Dansby.Core.Api.Infrastructure;

internal interface IHandlerRegistry
{
    void Register(IIntentHandler handler);
    IIntentHandler? Resolve(string intent);
}
