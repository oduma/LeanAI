using LeanAI.Application.WeightManagement.DTOs;
using MediatR;

namespace LeanAI.Application.WeightManagement.Queries.GetUserProfile;

public record GetUserProfileQuery : IRequest<UserProfileDto?>;
