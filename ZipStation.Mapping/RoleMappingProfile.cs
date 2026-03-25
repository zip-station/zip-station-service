using AutoMapper;
using ZipStation.Models.CommandModels;
using ZipStation.Models.Entities;
using ZipStation.Models.Responses;

namespace ZipStation.Mapping;

public class RoleMappingProfile : Profile
{
    public RoleMappingProfile()
    {
        CreateMap<RoleCommandModel, Role>();
        CreateMap<Role, RoleResponse>();
    }
}
