using LeanAI.Application.FoodTracking.DTOs;
using MediatR;

namespace LeanAI.Application.FoodTracking.Commands.AnalyzeFoodImage;

public sealed record AnalyzeFoodImageCommand(byte[] ImageBytes, string MimeType, DateOnly Date)
    : IRequest<IReadOnlyList<FoodItemDto>>;
