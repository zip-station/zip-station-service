using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using ZipStation.Business.Gateways;
using ZipStation.Business.Helpers;
using ZipStation.Business.Repositories;
using ZipStation.Business.Services;

namespace ZipStation.Api.Helpers;

public static class DependencyInjection
{
    public static void SetupAllDependencyInjection(WebApplicationBuilder builder)
    {
        var appConfig = builder.Configuration.Get<AppConfig>() ?? new AppConfig();

        SetupMongoDb(builder, appConfig);
        SetupRepositories(builder, appConfig);
        SetupGateways(builder);
        SetupServices(builder);
    }

    private static void SetupMongoDb(WebApplicationBuilder builder, AppConfig appConfig)
    {
        // Register camelCase convention so BSON field names match JSON serialization
        var camelCaseConventionPack = new ConventionPack { new CamelCaseElementNameConvention() };
        ConventionRegistry.Register("camelCase", camelCaseConventionPack, type => true);

        var mongoSettings = MongoClientSettings.FromConnectionString(appConfig.ZipStationMongoDb.ConnectionString);
        mongoSettings.MaxConnectionPoolSize = 200;
        mongoSettings.MinConnectionPoolSize = 10;

        var client = new MongoClient(mongoSettings);
        var database = client.GetDatabase(appConfig.ZipStationMongoDb.DatabaseName);

        builder.Services.AddSingleton<IMongoClient>(client);
        builder.Services.AddSingleton(database);
    }

    private static void SetupRepositories(WebApplicationBuilder builder, AppConfig appConfig)
    {
        var collections = appConfig.ZipStationMongoDb.Collections;

        builder.Services.AddScoped<ICompanyRepository>(sp =>
            new CompanyRepository(sp.GetRequiredService<IMongoDatabase>(), collections.Companies));

        builder.Services.AddScoped<IProjectRepository>(sp =>
            new ProjectRepository(sp.GetRequiredService<IMongoDatabase>(), collections.Projects));

        builder.Services.AddScoped<IUserRepository>(sp =>
            new UserRepository(sp.GetRequiredService<IMongoDatabase>(), collections.Users));

        builder.Services.AddScoped<ITicketRepository>(sp =>
            new TicketRepository(sp.GetRequiredService<IMongoDatabase>(), collections.Tickets));

        builder.Services.AddScoped<ITicketMessageRepository>(sp =>
            new TicketMessageRepository(sp.GetRequiredService<IMongoDatabase>(), collections.TicketMessages));

        builder.Services.AddScoped<ITicketIdCounterRepository>(sp =>
            new TicketIdCounterRepository(sp.GetRequiredService<IMongoDatabase>(), collections.TicketIdCounters));

        builder.Services.AddScoped<ICustomerRepository>(sp =>
            new CustomerRepository(sp.GetRequiredService<IMongoDatabase>(), collections.Customers));

        builder.Services.AddScoped<IIntakeEmailRepository>(sp =>
            new IntakeEmailRepository(sp.GetRequiredService<IMongoDatabase>(), collections.IntakeEmails));

        builder.Services.AddScoped<IIntakeRuleRepository>(sp =>
            new IntakeRuleRepository(sp.GetRequiredService<IMongoDatabase>(), collections.IntakeRules));

        builder.Services.AddScoped<ICannedResponseRepository>(sp =>
            new CannedResponseRepository(sp.GetRequiredService<IMongoDatabase>(), collections.CannedResponses));

        builder.Services.AddScoped<IAuditLogRepository>(sp =>
            new AuditLogRepository(sp.GetRequiredService<IMongoDatabase>(), collections.AuditLog));

        builder.Services.AddScoped<IProjectApiKeyRepository>(sp =>
            new ProjectApiKeyRepository(sp.GetRequiredService<IMongoDatabase>(), collections.ProjectApiKeys));

        builder.Services.AddScoped<ITicketDraftRepository>(sp =>
            new TicketDraftRepository(sp.GetRequiredService<IMongoDatabase>(), collections.TicketDrafts));

        builder.Services.AddScoped<IAlertRepository>(sp =>
            new AlertRepository(sp.GetRequiredService<IMongoDatabase>(), collections.Alerts));

        builder.Services.AddScoped<IReportRepository>(sp =>
            new ReportRepository(sp.GetRequiredService<IMongoDatabase>(), collections.Reports));

        builder.Services.AddScoped<IRoleRepository>(sp =>
            new RoleRepository(sp.GetRequiredService<IMongoDatabase>(), collections.Roles));
    }

    private static void SetupGateways(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<ICompanyGateway, CompanyGateway>();
        builder.Services.AddScoped<IProjectGateway, ProjectGateway>();
        builder.Services.AddScoped<IUserGateway, UserGateway>();
        builder.Services.AddScoped<ITicketGateway, TicketGateway>();
        builder.Services.AddScoped<ICustomerGateway, CustomerGateway>();
        builder.Services.AddScoped<IIntakeGateway, IntakeGateway>();
        builder.Services.AddScoped<IIntakeRuleGateway, IntakeRuleGateway>();
        builder.Services.AddScoped<ICannedResponseGateway, CannedResponseGateway>();
        builder.Services.AddScoped<IAuditLogGateway, AuditLogGateway>();
        builder.Services.AddScoped<IAlertGateway, AlertGateway>();
    }

    private static void SetupServices(WebApplicationBuilder builder)
    {
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IAppUser, AppUser>();
        builder.Services.AddSingleton<ZipStation.Business.Services.IEmailService, ZipStation.Business.Services.EmailService>();
        builder.Services.AddScoped<ZipStation.Business.Services.IAuditService, ZipStation.Business.Services.AuditService>();
        builder.Services.AddSingleton<ZipStation.Business.Services.IConnectionTestService, ZipStation.Business.Services.ConnectionTestService>();
        builder.Services.AddScoped<IAlertService, AlertService>();
        builder.Services.AddScoped<IPermissionService, PermissionService>();
        builder.Services.AddSingleton<IFileStorageService, FileStorageService>();
    }
}
