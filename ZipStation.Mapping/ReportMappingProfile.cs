using AutoMapper;
using ZipStation.Models.CommandModels;
using ZipStation.Models.Entities;
using ZipStation.Models.Responses;

namespace ZipStation.Mapping;

public class ReportMappingProfile : Profile
{
    public ReportMappingProfile()
    {
        CreateMap<ReportCommandModel, Report>();
        CreateMap<Report, ReportResponse>();
    }
}
