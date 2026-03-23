using AutoMapper;
using ZipStation.Models.Entities;
using ZipStation.Models.Responses;

namespace ZipStation.Mapping;

public class IntakeEmailMappingProfile : Profile
{
    public IntakeEmailMappingProfile()
    {
        CreateMap<IntakeEmail, IntakeEmailResponse>();
    }
}
