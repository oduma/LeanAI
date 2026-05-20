using MediatR;

namespace LeanAI.Application.WeightManagement.Queries.GetBmrForDate;

public sealed record GetBmrForDateQuery(DateOnly Date) : IRequest<double?>;
