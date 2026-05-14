using System.ComponentModel.DataAnnotations;

namespace ZipStation.Models.CommandModels;

public class MaxInstructionCommandModel
{
    [Required]
    public string Instruction { get; set; } = string.Empty;

    public List<string> Contexts { get; set; } = new();
}
