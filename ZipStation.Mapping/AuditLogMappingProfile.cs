using AutoMapper;
using ZipStation.Models.Entities;
using ZipStation.Models.Responses;

namespace ZipStation.Mapping;

public class AuditLogMappingProfile : Profile
{
    public AuditLogMappingProfile()
    {
        CreateMap<AuditLogEntry, AuditLogEntryResponse>();
    }
}
