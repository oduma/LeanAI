namespace LeanAI.Application.FoodTracking.DTOs;

public sealed record FoodLogEntryDto(Guid FoodLogId, Guid CaloryLogId, string FoodItem, string Quantity, double Calories);
