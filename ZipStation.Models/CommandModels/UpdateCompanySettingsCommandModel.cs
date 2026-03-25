namespace ZipStation.Models.CommandModels;

public class UpdateCompanySettingsCommandModel
{
    public string? DefaultTimezone { get; set; }
    public string? DefaultLanguage { get; set; }
    public UpdateCompanySmtpCommandModel? Smtp { get; set; }
}

public class UpdateCompanySmtpCommandModel
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool UseSsl { get; set; } = true;
    public string? FromName { get; set; }
    public string? FromEmail { get; set; }
}
