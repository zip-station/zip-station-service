namespace ZipStation.Models.CommandModels;

public class UpdateUserPreferencesCommandModel
{
    public string? PreferredLanguage { get; set; }
    public string? Timezone { get; set; }
}
