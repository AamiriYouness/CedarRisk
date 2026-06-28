namespace CedarRisk.Domain.Common;

/// <summary>
/// Monade Result — évite les exceptions pour les règles métier.
/// </summary>
public readonly record struct Result<T>
{
    private readonly T? _value;
    private readonly DomainError? _error;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Accès à Value sur un Result en échec : {_error!.Code}");

    public DomainError Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("Accès à Error sur un Result en succès.");

    public ResultStatus Status { get; init; }
    public string SuccessMessage { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;

    public bool IsSuccess => Status is
        ResultStatus.Ok or
        ResultStatus.Created or
        ResultStatus.NoContent;

    public bool IsFailure => !IsSuccess;

    private Result(T value, ResultStatus status = ResultStatus.Ok)
    {
        _value = value;
        Status = status;
    }

    private Result(DomainError error)
    {
        _error = error;
        Status = error.Status;
    }

    public static Result<T> Success(T value) =>
        new(value);

    public static Result<T> Success(T value, string successMessage) =>
        new(value) { SuccessMessage = successMessage };

    public static Result<T> Created(T value, string? location = null) =>
        new(value, ResultStatus.Created) { Location = location ?? string.Empty };

    public static Result<T> NoContent() =>
        new(default!, ResultStatus.NoContent);

    public static Result<T> Failure(DomainError error) =>
        new(error);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator T(Result<T> result) => result.Value;

    public Result<TOut> Map<TOut>(Func<T, TOut> map) =>
        IsSuccess ? Result<TOut>.Success(map(_value!)) : Result<TOut>.Failure(_error!);

    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> bind) =>
        IsSuccess ? bind(_value!) : Result<TOut>.Failure(_error!);

    public T GetOrDefault(T defaultValue) => IsSuccess ? _value! : defaultValue;


    public TOut Match<TOut>(
        Func<T, TOut> onSuccess,
        Func<NotFoundError, TOut> onNotFound,
        Func<InvalidError, TOut> onInvalid,
        Func<UnprocessableError, TOut> onUnprocessable,
        Func<ConflictError, TOut> onConflict,
        Func<ForbiddenError, TOut> onForbidden,
        Func<UnauthorizedError, TOut> onUnauthorized,
        Func<UnexpectedError, TOut> onError,
        Func<UnavailableError, TOut> onUnavailable)
    {
        if (IsSuccess) return onSuccess(_value!);
        return _error switch
        {
            NotFoundError e => onNotFound(e),
            InvalidError e => onInvalid(e),
            UnprocessableError e => onUnprocessable(e),
            ConflictError e => onConflict(e),
            ForbiddenError e => onForbidden(e),
            UnauthorizedError e => onUnauthorized(e),
            UnexpectedError e => onError(e),
            UnavailableError e => onUnavailable(e),
            // CriticalError → 500, unrecoverable
            _ => onError(new UnexpectedError(_error!.Code, _error.Message))
        };
    }
}
