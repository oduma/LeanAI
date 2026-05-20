using LeanAI.Domain.FoodTracking.Interfaces;
using MediatR;

namespace LeanAI.Application.WeightManagement.Queries.GetBmrForDate;

public sealed class GetBmrForDateQueryHandler(ICaloryLogRepository repo)
    : IRequestHandler<GetBmrForDateQuery, double?>
{
    public async Task<double?> Handle(GetBmrForDateQuery request, CancellationToken cancellationToken)
    {
        var log = await repo.GetBmrForDateAsync(request.Date, cancellationToken);
        return log?.Calories;
    }
}
