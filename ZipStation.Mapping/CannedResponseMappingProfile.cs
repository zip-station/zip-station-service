using AutoMapper;
using ZipStation.Models.CommandModels;
using ZipStation.Models.Entities;
using ZipStation.Models.Responses;

namespace ZipStation.Mapping;

public class CannedResponseMappingProfile : Profile
{
    public CannedResponseMappingProfile()
    {
        CreateMap<CannedResponseCommandModel, CannedResponse>();
        CreateMap<CannedResponse, CannedResponseResponse>();
    }
}
