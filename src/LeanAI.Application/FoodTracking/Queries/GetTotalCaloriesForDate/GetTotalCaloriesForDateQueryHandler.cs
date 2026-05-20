using LeanAI.Domain.FoodTracking.Interfaces;
using MediatR;

namespace LeanAI.Application.FoodTracking.Queries.GetTotalCaloriesForDate;

public sealed class GetTotalCaloriesForDateQueryHandler(ICaloryLogRepository repo)
    : IRequestHandler<GetTotalCaloriesForDateQuery, double?>
{
    public Task<double?> Handle(GetTotalCaloriesForDateQuery request, CancellationToken cancellationToken)
        => repo.GetTotalCaloriesAsync(request.Date, cancellationToken);
}
