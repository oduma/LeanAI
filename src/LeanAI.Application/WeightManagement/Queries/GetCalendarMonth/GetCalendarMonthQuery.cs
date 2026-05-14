using LeanAI.Application.WeightManagement.DTOs;
using MediatR;

namespace LeanAI.Application.WeightManagement.Queries.GetCalendarMonth;

public sealed record GetCalendarMonthQuery(int Year, int Month) : IRequest<CalendarMonthDto?>;
