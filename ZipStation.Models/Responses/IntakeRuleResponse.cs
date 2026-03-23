using ZipStation.Models.Enums;

namespace ZipStation.Models.Responses;

public class IntakeRuleResponse
{
    public string Id { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<IntakeRuleConditionResponse> Conditions { get; set; } = new();
    public IntakeActionType Action { get; set; }
    public int Priority { get; set; }
    public bool IsEnabled { get; set; }
    public long CreatedOnDateTime { get; set; }
    public long UpdatedOnDateTime { get; set; }
}

public class IntakeRuleConditionResponse
{
    public IntakeConditionType Type { get; set; }
    public string Value { get; set; } = string.Empty;
}
