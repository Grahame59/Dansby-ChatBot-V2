namespace Dansby.Shared
{
    public readonly record struct HandlerResult(bool Ok, string? ErrorCode, string? Message, object? Data)
    {
        public static HandlerResult Success(object? data = null) => new(true, null, null, data);
        public static HandlerResult Fail(string code, string message) => new(false, code, message, null);
    }
}
