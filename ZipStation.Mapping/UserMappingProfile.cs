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
            .ForMember(dest => dest.CompanyMemberships, opt => opt.Ignore())
            .ForMember(dest => dest.ProjectMemberships, opt => opt.Ignore())
            .ForMember(dest => dest.EmailSignatureHtml, opt => opt.Ignore());

        CreateMap<User, UserResponse>();
        CreateMap<CompanyMembership, CompanyMembershipResponse>();
        CreateMap<ProjectMembership, ProjectMembershipResponse>();
        CreateMap<UserPreferences, UserPreferencesResponse>();
    }
}
