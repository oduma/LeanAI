using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Enums;
using LeanAI.Domain.WeightManagement.Interfaces;
using MediatR;

namespace LeanAI.Application.WeightManagement.Commands.GenerateIdealPath;

public class GenerateIdealPathCommandHandler(IDailyIdealWeightRepository repository)
    : IRequestHandler<GenerateIdealPathCommand, Unit>
{
    public async Task<Unit> Handle(GenerateIdealPathCommand request, CancellationToken cancellationToken)
    {
        var totalDays = request.TargetPeriod.TotalDays();
        var dailyLoss = (request.StartingWeightKg - request.TargetWeightKg) / totalDays;

        var entries = Enumerable.Range(0, totalDays)
            .Select(i => new DailyIdealWeight
            {
                Date     = request.StartDate.AddDays(i),
                WeightKg = request.StartingWeightKg - dailyLoss * i
            })
            .ToList();

        await repository.DeleteAllAsync(cancellationToken);
        await repository.InsertBatchAsync(entries, cancellationToken);

        return Unit.Value;
    }
}
