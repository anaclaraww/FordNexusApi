namespace FordNexus.Application.Common;

public abstract class AppException(string message) : Exception(message);

public sealed class NotFoundException(string message) : AppException(message);

public sealed class ConflictException(string message) : AppException(message);

public sealed class ForbiddenException(string message) : AppException(message);

public sealed class AuthenticationFailedException(string message) : AppException(message);

public sealed class BusinessRuleException(string message) : AppException(message);

public sealed class ValidationException(IDictionary<string, string[]> errors)
    : AppException("Um ou mais campos são inválidos.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;

    public ValidationException(string field, string error)
        : this(new Dictionary<string, string[]> { [field] = new[] { error } }) { }
}
