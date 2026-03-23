namespace ZipStation.Models.CommandModels;

public class PatchEntityCommandModel
{
    public string Id { get; set; } = string.Empty;

    public List<PatchOperation> Operations { get; set; } = new();
}

public class PatchOperation
{
    public string PropertyName { get; set; } = string.Empty;

    public object? Value { get; set; }

    public PatchOperationTypes OperationType { get; set; }
}

public enum PatchOperationTypes
{
    Replace,
    Add,
    Remove,
    Clear
}
