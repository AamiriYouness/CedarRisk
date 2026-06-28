using CedarRisk.Domain.Common;

namespace CedarRisk.Api.Extensions;

public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            return result.Status switch
            {
                ResultStatus.Created when !string.IsNullOrEmpty(result.Location)
                    => TypedResults.Created(result.Location, result.Value),
                ResultStatus.Created
                    => TypedResults.Created((string?)null, result.Value),
                ResultStatus.NoContent
                    => TypedResults.NoContent(),
                _ => TypedResults.Ok(result.Value)
            };
        }

        return result.Match(
            onSuccess: v => TypedResults.Ok(v),
            onNotFound: e => TypedResults.NotFound(Problem(e)),
            onInvalid: e => TypedResults.BadRequest(Problem(e, e.Fields)),
            onUnprocessable: e => TypedResults.UnprocessableEntity(Problem(e)),
            onConflict: e => TypedResults.Conflict(Problem(e)),
            onForbidden: _ => TypedResults.Forbid(),
            onUnauthorized: _ => TypedResults.Unauthorized(),
            onError: e => TypedResults.Problem(Problem(e)),
            onUnavailable: e =>
            {
                var retryAfter = e.RetryAfterSeconds ?? 60;
                var problem = Problem(e);
                problem.Extensions["retryAfter"] = retryAfter;
                return Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: problem.Title,
                    detail: problem.Detail,
                    extensions: new Dictionary<string, object?> { ["retryAfter"] = retryAfter });
            });
    }

    private static Microsoft.AspNetCore.Mvc.ProblemDetails Problem(DomainError e, IReadOnlyList<FieldError>? fields = null)
    {
        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Title = e.Code,
            Detail = e.Message,
            Status = StatusCodeFor(e.Status)
        };

        if (fields?.Count > 0)
            problem.Extensions["errors"] = fields
                .GroupBy(f => f.Field)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Message).ToArray());

        return problem;
    }

    private static int StatusCodeFor(ResultStatus status) => status switch
    {
        ResultStatus.Ok => 200,
        ResultStatus.Created => 201,
        ResultStatus.NoContent => 204,
        ResultStatus.NotFound => 404,
        ResultStatus.Invalid => 400,
        ResultStatus.UnprocessableEntity => 422,
        ResultStatus.Conflict => 409,
        ResultStatus.Forbidden => 403,
        ResultStatus.Unauthorized => 401,
        ResultStatus.Error => 500,
        _ => 500
    };
}
