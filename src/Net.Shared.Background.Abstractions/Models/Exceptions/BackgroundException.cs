using Net.Shared.Exceptions;

namespace Net.Shared.Background.Abstractions.Models.Exceptions;

public sealed class BackgroundException : NetSharedException
{
    public BackgroundException(string message) : base(message)
    {
    }

    public BackgroundException(Exception exception) : base(exception)
    {
    }
}