using LeanAI.Application.ActivityTracking.DTOs;
using MediatR;

namespace LeanAI.Application.ActivityTracking.Commands.SaveRunActivities;

public sealed record SaveRunActivitiesCommand(
    DateOnly                         Date,
    IReadOnlyList<RunActivityRowDto> Rows,
    bool                             IsImportMode)
    : IRequest;
