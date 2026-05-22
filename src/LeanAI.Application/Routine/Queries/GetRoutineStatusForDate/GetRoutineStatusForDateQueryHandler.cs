using LeanAI.Domain.Routine.Interfaces;
using MediatR;

namespace LeanAI.Application.Routine.Queries.GetRoutineStatusForDate;

public sealed class GetRoutineStatusForDateQueryHandler(IRoutineRepository repo)
    : IRequestHandler<GetRoutineStatusForDateQuery, bool>
{
    public Task<bool> Handle(GetRoutineStatusForDateQuery request, CancellationToken cancellationToken)
        => repo.GetIsActiveForDateAsync(request.Date, cancellationToken);
}
