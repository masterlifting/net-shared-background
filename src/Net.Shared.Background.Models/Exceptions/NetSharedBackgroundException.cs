using Net.Shared.Exceptions;

namespace Net.Shared.Background.Models.Exceptions;

public sealed class NetSharedBackgroundException : NetSharedException
{
    public NetSharedBackgroundException(string message) : base(message)
    {
    }

    public NetSharedBackgroundException(Exception exception) : base(exception)
    {
    }
}