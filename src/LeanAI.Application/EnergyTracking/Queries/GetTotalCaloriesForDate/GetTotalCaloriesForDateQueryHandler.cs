using LeanAI.Domain.EnergyTracking.Interfaces;
using MediatR;

namespace LeanAI.Application.EnergyTracking.Queries.GetTotalCaloriesForDate;

public sealed class GetTotalCaloriesForDateQueryHandler(IEnergyLogRepository repo)
    : IRequestHandler<GetTotalCaloriesForDateQuery, double?>
{
    public Task<double?> Handle(GetTotalCaloriesForDateQuery request, CancellationToken cancellationToken)
        => repo.GetTotalCaloriesAsync(request.Date, cancellationToken);
}
