using MediatR;

namespace LeanAI.Application.WeightManagement.Commands.RecalculateWeeklyAverage;

/// <param name="Date">Any date within the target Mon–Sun week.</param>
public sealed record RecalculateWeeklyAverageCommand(DateOnly Date) : IRequest<Unit>;
