namespace ZipStation.Models.CommandModels;

public class MaxApiKeyCommandModel
{
    public string ApiKey { get; set; } = string.Empty;
}

public class MaxTestConnectionCommandModel
{
    public string? ApiKey { get; set; }

    public string? Model { get; set; }
}

public class MaxToneAnalyzerCommandModel
{
    public int? ReplyCount { get; set; }
}
