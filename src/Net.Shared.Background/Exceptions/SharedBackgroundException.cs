using Shared.Exceptions.Abstractions;
using Shared.Exceptions.Models;

namespace Shared.Background.Exceptions;

public sealed class SharedBackgroundException : SharedException
{
    public SharedBackgroundException(string initiator, string action, ExceptionDescription description) : base(initiator, action, description) { }
}