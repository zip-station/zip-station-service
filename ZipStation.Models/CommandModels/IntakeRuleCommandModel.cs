using ZipStation.Models.Enums;

namespace ZipStation.Models.CommandModels;

public class IntakeRuleCommandModel : BaseCommandModel
{
    public string CompanyId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<IntakeRuleConditionInput> Conditions { get; set; } = new();
    public IntakeActionType Action { get; set; }
    public int Priority { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public class IntakeRuleConditionInput
{
    public IntakeConditionType Type { get; set; }
    public string Value { get; set; } = string.Empty;
}
