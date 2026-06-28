using CedarRisk.Domain.Common;

namespace CedarRisk.Application.Common;

public interface IRequestHandler<TQuery, TResponse>
{
    Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken ct = default);
}
