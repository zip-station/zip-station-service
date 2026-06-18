using AutoMapper;
using ZipStation.Models.CommandModels;
using ZipStation.Models.Entities;
using ZipStation.Models.Responses;

namespace ZipStation.Mapping;

public class KanbanMappingProfile : Profile
{
    public KanbanMappingProfile()
    {
        CreateMap<KanbanBoard, KanbanBoardResponse>();
        CreateMap<KanbanColumn, KanbanColumnResponse>();
        CreateMap<KanbanCardTypeDefinition, KanbanCardTypeResponse>();

        CreateMap<KanbanCardCommandModel, KanbanCard>();
        CreateMap<KanbanCard, KanbanCardResponse>();
        CreateMap<KanbanCardExternalSource, KanbanCardExternalSourceResponse>();

        CreateMap<KanbanCardCommentCommandModel, KanbanCardComment>();
        CreateMap<KanbanCardComment, KanbanCardCommentResponse>();
    }
}
