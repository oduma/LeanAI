using LeanAI.Domain.WeightManagement.Interfaces;
using MediatR;

namespace LeanAI.Application.WeightManagement.Commands.DeleteUserProfile;

public class DeleteUserProfileCommandHandler(IUserProfileRepository repository)
    : IRequestHandler<DeleteUserProfileCommand, Unit>
{
    public async Task<Unit> Handle(DeleteUserProfileCommand request, CancellationToken cancellationToken)
    {
        await repository.DeleteAsync(cancellationToken);
        return Unit.Value;
    }
}
