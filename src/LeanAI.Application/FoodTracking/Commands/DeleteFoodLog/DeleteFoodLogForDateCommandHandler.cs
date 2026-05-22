using LeanAI.Domain.EnergyTracking.Interfaces;
using LeanAI.Domain.FoodTracking.Interfaces;
using MediatR;

namespace LeanAI.Application.FoodTracking.Commands.DeleteFoodLog;

public sealed class DeleteFoodLogForDateCommandHandler(IFoodLogRepository foodRepo, IEnergyLogRepository energyRepo)
    : IRequestHandler<DeleteFoodLogForDateCommand>
{
    public async Task Handle(DeleteFoodLogForDateCommand request, CancellationToken cancellationToken)
    {
        var existing  = await foodRepo.GetByDateAsync(request.Date, cancellationToken);
        var energyIds = existing.Select(fl => fl.EnergyLogId).ToList();
        await foodRepo.DeleteByDateAsync(request.Date, cancellationToken);
        await energyRepo.DeleteManyAsync(energyIds, cancellationToken);
    }
}
