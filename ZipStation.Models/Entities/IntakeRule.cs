using ZipStation.Models.Attributes;
using ZipStation.Models.Enums;

namespace ZipStation.Models.Entities;

public class IntakeRule : BaseEntity
{
    [DoNotChangeOnPatch]
    public string CompanyId { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string ProjectId { get; set; } = string.Empty;

    [DoNotClearOnPatch]
    public string Name { get; set; } = string.Empty;

    public List<IntakeRuleCondition> Conditions { get; set; } = new();

    public IntakeActionType Action { get; set; }

    public int Priority { get; set; }

    public bool IsEnabled { get; set; } = true;
}

public class IntakeRuleCondition
{
    public IntakeConditionType Type { get; set; }

    public string Value { get; set; } = string.Empty;
}
