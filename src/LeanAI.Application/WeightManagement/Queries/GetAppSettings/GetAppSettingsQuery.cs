using LeanAI.Application.WeightManagement.DTOs;
using MediatR;

namespace LeanAI.Application.WeightManagement.Queries.GetAppSettings;

public sealed record GetAppSettingsQuery : IRequest<AppSettingsDto>;
