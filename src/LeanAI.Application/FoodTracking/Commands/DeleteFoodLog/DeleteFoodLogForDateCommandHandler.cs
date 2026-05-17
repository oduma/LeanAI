using LeanAI.Domain.FoodTracking.Interfaces;
using MediatR;

namespace LeanAI.Application.FoodTracking.Commands.DeleteFoodLog;

public sealed class DeleteFoodLogForDateCommandHandler(IFoodLogRepository repo)
    : IRequestHandler<DeleteFoodLogForDateCommand>
{
    public Task Handle(DeleteFoodLogForDateCommand request, CancellationToken cancellationToken)
        => repo.DeleteByDateAsync(request.Date, cancellationToken);
}
