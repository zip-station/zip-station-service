namespace ZipStation.Business.Helpers;

public class AppConfig
{
    public string ApplicationId { get; set; } = "zipstation-service";
    public string AppName { get; set; } = "ZipStationService";
    public bool IsLocal { get; set; }
    public bool IsStage { get; set; }
    public bool IsProd { get; set; }
    public string AllowedOrigins { get; set; } = "http://localhost:3000";
    public string EncryptionKey { get; set; } = string.Empty;

    public ZipStationMongoDbConfiguration ZipStationMongoDb { get; set; } = new();
    public FirebaseConfiguration Firebase { get; set; } = new();
}

public class ZipStationMongoDbConfiguration
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string DatabaseName { get; set; } = "zipstation";
    public ZipStationMongoDbCollections Collections { get; set; } = new();
}

public class ZipStationMongoDbCollections
{
    public string Companies { get; set; } = "companies";
    public string Projects { get; set; } = "projects";
    public string ProjectConfigs { get; set; } = "projectConfigs";
    public string Users { get; set; } = "users";
    public string Customers { get; set; } = "customers";
    public string Tickets { get; set; } = "tickets";
    public string TicketMessages { get; set; } = "ticketMessages";
    public string IntakeEmails { get; set; } = "intakeEmails";
    public string IntakeRules { get; set; } = "intakeRules";
    public string TicketResources { get; set; } = "ticketResources";
    public string Alerts { get; set; } = "alerts";
    public string Reports { get; set; } = "reports";
    public string AuditLog { get; set; } = "auditLog";
    public string TicketIdCounters { get; set; } = "ticketIdCounters";
    public string ResponseTimeStats { get; set; } = "responseTimeStats";
    public string CannedResponses { get; set; } = "cannedResponses";
    public string WorkerTriggers { get; set; } = "workerTriggers";
    public string ProjectApiKeys { get; set; } = "projectApiKeys";
    public string TicketDrafts { get; set; } = "ticketDrafts";
    public string Roles { get; set; } = "roles";
}

public class FirebaseConfiguration
{
    public string BearerTokenAudience { get; set; } = string.Empty;
    public string BearerTokenIssuer { get; set; } = string.Empty;
    public FirebaseServiceAccounts ServiceAccounts { get; set; } = new();
}

public class FirebaseServiceAccounts
{
    public string FirebaseAdminSdk { get; set; } = string.Empty;
}
