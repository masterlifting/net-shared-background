namespace Net.Shared.Background.Models.Exceptions;

public sealed class BackgroundException : NetSharedException
{
    public BackgroundException(string message) : base(message)
    {
    }

    public BackgroundException(Exception exception) : base(exception)
    {
    }
}