using AutoMapper;
using ZipStation.Models.CommandModels;
using ZipStation.Models.Entities;
using ZipStation.Models.Responses;

namespace ZipStation.Mapping;

public class TicketMappingProfile : Profile
{
    public TicketMappingProfile()
    {
        CreateMap<TicketCommandModel, Ticket>();
        CreateMap<Ticket, TicketResponse>();
        CreateMap<TicketMessage, TicketMessageResponse>();
        CreateMap<MessageAttachment, MessageAttachmentResponse>();
    }
}
