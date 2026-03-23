using AutoMapper;
using ZipStation.Models.CommandModels;
using ZipStation.Models.Entities;
using ZipStation.Models.Responses;

namespace ZipStation.Mapping;

public class AlertMappingProfile : Profile
{
    public AlertMappingProfile()
    {
        CreateMap<AlertCommandModel, Alert>();
        CreateMap<Alert, AlertResponse>();
    }
}
