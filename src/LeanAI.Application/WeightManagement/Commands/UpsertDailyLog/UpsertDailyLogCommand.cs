using MediatR;

namespace LeanAI.Application.WeightManagement.Commands.UpsertDailyLog;

public sealed record UpsertDailyLogCommand(DateOnly Date, double WeightKg, string? Notes) : IRequest;
