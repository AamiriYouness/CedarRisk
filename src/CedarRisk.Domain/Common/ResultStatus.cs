namespace CedarRisk.Domain.Common;

public enum ResultStatus
{
    Ok,
    Created,
    NoContent,
    NotFound,           // 404
    Invalid,            // 400 — malformed input, missing required fields
    UnprocessableEntity,// 422 — structurally valid but business rule violated
    Conflict,           // 409 — duplicate, state clash
    Forbidden,          // 403 — authenticated but not authorized
    Unauthorized,       // 401
    Error,              // 500 — unexpected
    CriticalError,      // 500 — unrecoverable
    Unavailable         // 503 — transient, retry eligible
}