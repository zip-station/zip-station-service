using AutoMapper;
using ZipStation.Models.CommandModels;
using ZipStation.Models.Entities;
using ZipStation.Models.Responses;

namespace ZipStation.Mapping;

public class IntakeRuleMappingProfile : Profile
{
    public IntakeRuleMappingProfile()
    {
        CreateMap<IntakeRuleConditionInput, IntakeRuleCondition>();
        CreateMap<IntakeRuleCommandModel, IntakeRule>();
        CreateMap<IntakeRuleCondition, IntakeRuleConditionResponse>();
        CreateMap<IntakeRule, IntakeRuleResponse>();
    }
}
