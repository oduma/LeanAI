using LeanAI.Domain.EnergyTracking.Interfaces;
using MediatR;

namespace LeanAI.Application.WeightManagement.Queries.GetBmrForDate;

public sealed class GetBmrForDateQueryHandler(IEnergyLogRepository repo)
    : IRequestHandler<GetBmrForDateQuery, double?>
{
    public async Task<double?> Handle(GetBmrForDateQuery request, CancellationToken cancellationToken)
    {
        var log = await repo.GetBmrForDateAsync(request.Date, cancellationToken);
        return log?.Calories;
    }
}
