using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Enums;
using LeanAI.Domain.WeightManagement.Interfaces;
using MediatR;

namespace LeanAI.Application.WeightManagement.Commands.GenerateIdealPath;

public class GenerateIdealPathCommandHandler(
    IDailyIdealWeightRepository idealRepo,
    IUserProfileRepository      profileRepo)
    : IRequestHandler<GenerateIdealPathCommand, Unit>
{
    public async Task<Unit> Handle(GenerateIdealPathCommand request, CancellationToken cancellationToken)
    {
        var totalDays = request.ExactTotalDays ?? request.TargetPeriod.TotalDays();
        var dailyLoss = (request.StartingWeightKg - request.TargetWeightKg) / totalDays;

        var entries = Enumerable.Range(0, totalDays)
            .Select(i => new DailyIdealWeight
            {
                Date     = request.StartDate.AddDays(i),
                WeightKg = request.StartingWeightKg - dailyLoss * i
            })
            .ToList();

        await idealRepo.DeleteAllAsync(cancellationToken);
        await idealRepo.InsertBatchAsync(entries, cancellationToken);

        var profile = await profileRepo.GetAsync(cancellationToken);
        if (profile is not null)
        {
            profile.GoalStartDate = request.StartDate;
            profile.GoalEndDate   = request.StartDate.AddDays(totalDays - 1);
            await profileRepo.SaveAsync(profile, cancellationToken);
        }

        return Unit.Value;
    }
}
