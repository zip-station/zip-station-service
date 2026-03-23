namespace ZipStation.Models.CommandModels;

public class BaseCommandModel
{
    public string? Id { get; set; }

    public long CreatedOnDateTime { get; set; }

    public long UpdatedOnDateTime { get; set; }

    public bool IsVoid { get; set; }
}
