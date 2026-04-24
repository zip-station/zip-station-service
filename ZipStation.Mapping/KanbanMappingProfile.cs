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

        CreateMap<KanbanCardCommandModel, KanbanCard>();
        CreateMap<KanbanCard, KanbanCardResponse>();

        CreateMap<KanbanCardCommentCommandModel, KanbanCardComment>();
        CreateMap<KanbanCardComment, KanbanCardCommentResponse>();
    }
}
