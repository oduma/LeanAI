using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Interfaces;
using MediatR;

namespace LeanAI.Application.WeightManagement.Commands.UpsertDailyLog;

public sealed class UpsertDailyLogCommandHandler(IDailyActualWeightRepository repository)
    : IRequestHandler<UpsertDailyLogCommand>
{
    public async Task Handle(UpsertDailyLogCommand request, CancellationToken cancellationToken)
    {
        var entry = await repository.GetByDateAsync(request.Date, cancellationToken);

        if (entry is null)
        {
            entry = new DailyActualWeight
            {
                Date     = request.Date,
                WeightKg = request.WeightKg,
                Notes    = request.Notes
            };
        }
        else
        {
            entry.WeightKg = request.WeightKg;
            entry.Notes    = request.Notes;
        }

        await repository.UpsertAsync(entry, cancellationToken);
    }
}
