using MongoDB.Bson.Serialization.Attributes;
using ZipStation.Models.Attributes;

namespace ZipStation.Models.Entities;

public class MaxInstruction : BaseEntity
{
    [DoNotChangeOnPatch]
    public string CompanyId { get; set; } = string.Empty;

    [DoNotChangeOnPatch]
    public string ProjectId { get; set; } = string.Empty;

    [DoNotClearOnPatch]
    public string Instruction { get; set; } = string.Empty;

    public List<string> Contexts { get; set; } = new();

    [DoNotChangeOnPatch]
    public string Source { get; set; } = "manual";
}
