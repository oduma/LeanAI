using LeanAI.Domain.WeightManagement.Entities;
using LeanAI.Domain.WeightManagement.Interfaces;
using MediatR;

namespace LeanAI.Application.WeightManagement.Commands.AppendActivityComment;

public sealed class AppendActivityCommentCommandHandler(IDailyActualWeightRepository repo)
    : IRequestHandler<AppendActivityCommentCommand>
{
    public async Task Handle(AppendActivityCommentCommand request, CancellationToken cancellationToken)
    {
        var entry = await repo.GetByDateAsync(request.Date, cancellationToken);

        if (entry is not null)
        {
            entry.Notes = string.IsNullOrEmpty(entry.Notes)
                ? request.Comment
                : entry.Notes + "\n" + request.Comment;
            await repo.UpsertAsync(entry, cancellationToken);
        }
        else
        {
            var newEntry = new DailyActualWeight
            {
                Date     = request.Date,
                WeightKg = 0,
                Notes    = request.Comment
            };
            await repo.UpsertAsync(newEntry, cancellationToken);
        }
    }
}
