using MediatR;

namespace LeanAI.Application.WeightManagement.Commands.AppendActivityComment;

public sealed record AppendActivityCommentCommand(DateOnly Date, string Comment) : IRequest;
