using AutoMapper;
using ZipStation.Models.CommandModels;
using ZipStation.Models.Entities;
using ZipStation.Models.Responses;

namespace ZipStation.Mapping;

public class UserMappingProfile : Profile
{
    public UserMappingProfile()
    {
        CreateMap<UserCommandModel, User>()
            .ForMember(dest => dest.RoleAssignments, opt => opt.Ignore())
            .ForMember(dest => dest.EmailSignatureHtml, opt => opt.Ignore());

        CreateMap<User, UserResponse>()
            .ForMember(dest => dest.IsOwner, opt => opt.Ignore()); // Set manually in controller

        CreateMap<RoleAssignment, RoleAssignmentResponse>()
            .ForMember(dest => dest.RoleName, opt => opt.Ignore()); // Set manually when needed

        CreateMap<UserPreferences, UserPreferencesResponse>();
    }
}
