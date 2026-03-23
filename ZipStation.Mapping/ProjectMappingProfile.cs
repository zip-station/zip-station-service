using AutoMapper;
using ZipStation.Models.CommandModels;
using ZipStation.Models.Entities;
using ZipStation.Models.Responses;

namespace ZipStation.Mapping;

public class ProjectMappingProfile : Profile
{
    public ProjectMappingProfile()
    {
        CreateMap<ProjectCommandModel, Project>();
        CreateMap<Project, ProjectResponse>();
        CreateMap<ProjectSettings, ProjectSettingsResponse>();
        CreateMap<SmtpSettings, SmtpSettingsResponse>()
            .ForMember(d => d.HasPassword, o => o.MapFrom(s => !string.IsNullOrEmpty(s.Password)));
        CreateMap<ImapSettings, ImapSettingsResponse>()
            .ForMember(d => d.HasPassword, o => o.MapFrom(s => !string.IsNullOrEmpty(s.Password)));
        CreateMap<TicketIdSettings, TicketIdSettingsResponse>();
        CreateMap<ContactFormSettings, ContactFormSettingsResponse>();
        CreateMap<EmailSignatureSettings, EmailSignatureSettingsResponse>();
        CreateMap<AutoReplySettings, AutoReplySettingsResponse>();
        CreateMap<SpamSettings, SpamSettingsResponse>();
    }
}
