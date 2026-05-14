using AutoMapper;
using ZipStation.Models.CommandModels;
using ZipStation.Models.Entities;
using ZipStation.Models.Responses;

namespace ZipStation.Mapping;

public class MaxMappingProfile : Profile
{
    public MaxMappingProfile()
    {
        CreateMap<MaxInstructionCommandModel, MaxInstruction>();
        CreateMap<MaxInstruction, MaxInstructionResponse>();

        CreateMap<MaxExampleReplyCommandModel, MaxExampleReply>();
        CreateMap<MaxExampleReply, MaxExampleReplyResponse>();

        CreateMap<MaxSettings, MaxSettingsResponse>()
            .ForMember(d => d.ApiKeySet, o => o.MapFrom(s => !string.IsNullOrEmpty(s.ApiKeyEncrypted)));
    }
}
