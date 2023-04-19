namespace Net.Shared.Background.Models.Exceptions;

public sealed class BackgroundException : Net.Shared.Exception
{
    public BackgroundException(string message) : base(message)
    {
    }

    public BackgroundException(System.Exception exception) : base(exception)
    {
    }
}