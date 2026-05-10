using MediatR;

namespace LeanAI.Application.WeightManagement.Commands.DeleteUserProfile;

public record DeleteUserProfileCommand : IRequest<Unit>;
