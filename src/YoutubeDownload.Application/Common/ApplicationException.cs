using YoutubeDownload.Domain.Enums;

namespace YoutubeDownload.Application.Common;

/// <summary>Base for expected, caller-originated errors that map to 4xx responses.</summary>
public abstract class ApplicationException : Exception
{
    protected ApplicationException(string message) : base(message) { }
}

/// <summary>Raised when the caller supplies invalid input (maps to HTTP 400).</summary>
public sealed class ValidationException : ApplicationException
{
    public ValidationException(string message) : base(message) { }
}

/// <summary>Raised when a requested resource does not exist or is unavailable (maps to 404/410).</summary>
public sealed class NotFoundException : ApplicationException
{
    public NotFoundException(string message) : base(message) { }
}

/// <summary>Raised when an operation cannot be completed in the current job state (maps to 409).</summary>
public sealed class ConflictException : ApplicationException
{
    public ConflictException(string message) : base(message) { }
}

/// <summary>
/// A structured error raised when the underlying media operation fails in a way that
/// should be surfaced back to the caller with a stable error code.
/// </summary>
public sealed class MediaOperationException : ApplicationException
{
    public MediaOperationException(JobErrorCode code, string message, bool retryable) : base(message)
    {
        Code = code;
        Retryable = retryable;
    }

    public JobErrorCode Code { get; }
    public bool Retryable { get; }
}