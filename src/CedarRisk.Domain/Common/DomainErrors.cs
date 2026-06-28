namespace CedarRisk.Domain.Common;

public abstract record DomainError(string Code, string Message, ResultStatus Status);

// 404
public record NotFoundError(string Code, string Message)
    : DomainError(Code, Message, ResultStatus.NotFound);

// 400 — bad input shape
public record InvalidError(string Code, string Message, IReadOnlyList<FieldError>? Fields = null)
    : DomainError(Code, Message, ResultStatus.Invalid);

// 422 — valid input, violated business rule
public record UnprocessableError(string Code, string Message)
    : DomainError(Code, Message, ResultStatus.UnprocessableEntity);

// 409 — duplicate entity, state machine clash
public record ConflictError(string Code, string Message)
    : DomainError(Code, Message, ResultStatus.Conflict);

// 403
public record ForbiddenError(string Code, string Message)
    : DomainError(Code, Message, ResultStatus.Forbidden);

// 401
public record UnauthorizedError(string Code, string Message)
    : DomainError(Code, Message, ResultStatus.Unauthorized);

// 500
public record UnexpectedError(string Code, string Message)
    : DomainError(Code, Message, ResultStatus.Error);

// 503 — transient infra failure, caller may retry
public record UnavailableError(string Code, string Message, int? RetryAfterSeconds = null)
    : DomainError(Code, Message, ResultStatus.Unavailable);

// 500 critical — CriticalError: unrecoverable, don't retry  
public record CriticalError(string Code, string Message)
    : DomainError(Code, Message, ResultStatus.CriticalError);

// Field-level detail for 400/422
public record FieldError(string Field, string Message);

