using LeanAI.Application.WeightManagement.DTOs;
using MediatR;

namespace LeanAI.Application.WeightManagement.Queries.GetLogContext;

public sealed record GetLogContextQuery(DateOnly Date) : IRequest<LogContextDto>;
