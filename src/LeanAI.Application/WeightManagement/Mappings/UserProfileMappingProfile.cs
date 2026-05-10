using AutoMapper;
using LeanAI.Application.WeightManagement.DTOs;
using LeanAI.Domain.WeightManagement.Entities;

namespace LeanAI.Application.WeightManagement.Mappings;

public class UserProfileMappingProfile : Profile
{
    public UserProfileMappingProfile()
    {
        CreateMap<UserProfile, UserProfileDto>();
    }
}
