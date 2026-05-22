namespace LeanAI.Application.FoodTracking.DTOs;

public sealed record FoodLogEntryDto(Guid FoodLogId, Guid EnergyLogId, string FoodItem, string Quantity, double Calories);
