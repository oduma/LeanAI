using AutoMapper;
using LeanAI.Application.WeightManagement.DTOs;
using LeanAI.Domain.WeightManagement.Interfaces;
using MediatR;

namespace LeanAI.Application.WeightManagement.Queries.GetUserProfile;

public class GetUserProfileQueryHandler(IUserProfileRepository repository, IMapper mapper)
    : IRequestHandler<GetUserProfileQuery, UserProfileDto?>
{
    public async Task<UserProfileDto?> Handle(GetUserProfileQuery request, CancellationToken cancellationToken)
    {
        var profile = await repository.GetAsync(cancellationToken);
        return profile is null ? null : mapper.Map<UserProfileDto>(profile);
    }
}
