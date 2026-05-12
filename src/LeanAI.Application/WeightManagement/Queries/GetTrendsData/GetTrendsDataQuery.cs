using LeanAI.Application.WeightManagement.DTOs;
using MediatR;

namespace LeanAI.Application.WeightManagement.Queries.GetTrendsData;

public sealed record GetTrendsDataQuery : IRequest<TrendsDataDto?>;
