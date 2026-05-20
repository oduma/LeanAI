using MediatR;

namespace LeanAI.Application.WeightManagement.Commands.CalculateAndSaveBmr;

public sealed record CalculateAndSaveBmrCommand(DateOnly Date, double WeightKg) : IRequest;
