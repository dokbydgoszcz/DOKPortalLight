using DokPortal.Application.Attachments;
using DokPortal.Application.Auth;
using DokPortal.Application.AuditLog;
using DokPortal.Application.Budget;
using DokPortal.Application.Common;
using DokPortal.Application.CaseDocuments;
using DokPortal.Application.Candidates;
using DokPortal.Application.Dashboard;
using DokPortal.Application.Meetings;
using DokPortal.Application.DokCases;
using DokPortal.Application.Documents;
using DokPortal.Application.Export;
using DokPortal.Application.Formators;
using DokPortal.Application.Mailing;
using DokPortal.Application.Missions;
using DokPortal.Application.Resources;
using DokPortal.Application.NameDays;
using DokPortal.Application.ParishNeeds;
using DokPortal.Application.Permissions;
using DokPortal.Application.Parishes;
using DokPortal.Application.PastoralNotes;
using DokPortal.Application.Reminders;
using DokPortal.Application.Supervisions;
using DokPortal.Application.People;
using DokPortal.Application.Users;
using DokPortal.Api;
using DokPortal.Api.Authorization;
using DokPortal.Api.ErrorHandling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using DokPortal.Api.Filters;
using DokPortal.Infrastructure.Auth;
using DokPortal.Infrastructure.Identity;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Seed;
using DokPortal.Infrastructure.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddApplicationInsightsTelemetry();
    builder.Services.AddApplicationInsightsTelemetryProcessor<DokPortal.Api.Telemetry.HealthTelemetryFilter>();
}

builder.Services.AddControllers(options => options.Filters.Add<ValidationActionFilter>())
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddValidatorsFromAssemblyContaining<DokPortal.Application.Auth.LoginRequestValidator>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Darmowa baza Azure SQL usypia się po godzinie bez ruchu: dłuższy czas łączenia i ponawianie przejściowych błędów,
// żeby pierwsze żądanie po przerwie nie kończyło się błędem.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(DatabaseResilience.WithConnectTimeout(builder.Configuration.GetConnectionString("Default")), sql =>
    {
        sql.EnableRetryOnFailure(maxRetryCount: 6, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
        sql.CommandTimeout(60);
    }));

// Ograniczenie liczby prób logowania z jednego adresu (zgadywanie haseł, blokowanie cudzych kont, budzenie bazy)
// oraz ręcznego sprawdzania /health/ready (każde wywołanie budzi płatny limit darmowej bazy).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Zbyt wiele prób w krótkim czasie. Odczekaj minutę i spróbuj ponownie." }, ct);
    options.AddPolicy("login", http => RateLimitPartition.GetFixedWindowLimiter(
        DatabaseResilience.ClientAddress(http),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = http.RequestServices.GetRequiredService<IConfiguration>().GetValue("RateLimiting:LoginPerMinute", 10),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    options.AddPolicy("health-ready", http => RateLimitPartition.GetFixedWindowLimiter(
        "health-ready",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = http.RequestServices.GetRequiredService<IConfiguration>().GetValue("RateLimiting:HealthReadyPerMinute", 6),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: new[] { "ready" });

builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddErrorDescriber<PolishIdentityErrorDescriber>()
    .AddDefaultTokenProviders();
// Link „Nie pamiętam hasła” jest ważny 2 godziny (i jednorazowy – zmiana hasła zmienia znacznik bezpieczeństwa).
builder.Services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(2));
builder.Services.AddDataProtection();
builder.Services.AddSingleton(new PortalUrl(
    builder.Configuration["App:PublicUrl"]
    ?? builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()?.FirstOrDefault()
    ?? ""));
builder.Services.AddScoped<IAccountService, AccountService>();

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("Missing Jwt configuration section.");
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IPersonService, PersonService>();
builder.Services.AddScoped<IPersonOverviewService, PersonOverviewService>();
builder.Services.AddScoped<IPersonPhotoService, PersonPhotoService>();
builder.Services.AddScoped<IGdprService, GdprService>();
builder.Services.AddScoped<DokPortal.Application.Documents.ILetterTemplateService, LetterTemplateService>();
builder.Services.AddScoped<IPeopleImportService, PeopleImportService>();
builder.Services.AddScoped<ICalendarFeedService, CalendarFeedService>();
builder.Services.AddScoped<IParishService, ParishService>();
builder.Services.AddScoped<IParishImportService, ParishImportService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services.AddScoped<ICandidateService, CandidateService>();
builder.Services.AddScoped<DokPortal.Application.SkspClasses.ISkspClassService, SkspClassService>();
builder.Services.AddScoped<IMissionService, MissionService>();
builder.Services.AddScoped<IResourceService, ResourceService>();
builder.Services.AddScoped<IFormatorService, FormatorService>();
builder.Services.AddScoped<IParishNeedService, ParishNeedService>();
builder.Services.AddScoped<IBudgetService, BudgetService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICaseScopeProvider, HttpCaseScopeProvider>();
builder.Services.AddScoped<IDokCaseService, DokCaseService>();
builder.Services.AddScoped<ICaseDocumentService, CaseDocumentService>();
builder.Services.AddScoped<IPastoralNoteService, PastoralNoteService>();
builder.Services.AddScoped<IMeetingService, MeetingService>();
builder.Services.AddScoped<ISupervisionService, SupervisionService>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();
builder.Services.AddScoped<INameDayService, NameDayService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IMailingService, MailingService>();
builder.Services.AddSingleton(new MailingOptions
{
    DailyLimit = builder.Configuration.GetValue("Mailing:DailyLimit", 400),
    DelayBetweenMessages = TimeSpan.FromSeconds(builder.Configuration.GetValue("Mailing:DelaySeconds", 1.0))
});
builder.Services.AddSingleton<DokPortal.Api.Mailing.MailingQueueRunner>();
builder.Services.AddSingleton<IMailingQueue>(sp => sp.GetRequiredService<DokPortal.Api.Mailing.MailingQueueRunner>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<DokPortal.Api.Mailing.MailingQueueRunner>());
builder.Services.AddScoped<IReminderService, ReminderService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<SessionValidator>();
builder.Services.AddScoped<IPermissionService, PermissionService>();

var blobConnectionString = builder.Configuration["BlobStorage:ConnectionString"];
var blobContainerName = builder.Configuration["BlobStorage:ContainerName"] ?? "case-documents";
if (!string.IsNullOrWhiteSpace(blobConnectionString))
{
    builder.Services.AddSingleton<IFileStorageService>(
        new AzureBlobStorageService(blobConnectionString, blobContainerName));
}
else
{
    builder.Services.AddSingleton<IFileStorageService, NullFileStorageService>();
}

var smtpHost = builder.Configuration["Smtp:Host"];
if (!string.IsNullOrWhiteSpace(smtpHost))
{
    var smtpPort = builder.Configuration.GetValue<int?>("Smtp:Port") ?? 587;
    var smtpUsername = builder.Configuration["Smtp:Username"] ?? "";
    var smtpPassword = builder.Configuration["Smtp:Password"] ?? "";
    var smtpFromEmail = builder.Configuration["Smtp:FromEmail"] ?? smtpUsername;
    var smtpFromName = builder.Configuration["Smtp:FromName"] ?? "DOK Portal";
    var smtpEnableSsl = builder.Configuration.GetValue<bool?>("Smtp:EnableSsl") ?? true;
    builder.Services.AddSingleton<IEmailSender>(
        new SmtpEmailSender(smtpHost, smtpPort, smtpUsername, smtpPassword, smtpFromEmail, smtpFromName, smtpEnableSsl));
}
else
{
    builder.Services.AddSingleton<IEmailSender, NullEmailSender>();
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            RoleClaimType = "role"
        };
        // Token musi nadal pasować do konta: wyłączone konto albo zmienione role/hasło (nowy znacznik) kończą sesję od razu.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var validator = context.HttpContext.RequestServices.GetRequiredService<SessionValidator>();
                var principal = context.Principal!;
                var userId = principal.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
                var stamp = principal.FindFirst(JwtTokenGenerator.StampClaim)?.Value;
                if (!await validator.IsValidAsync(userId, stamp, context.HttpContext.RequestAborted))
                {
                    context.Fail("Sesja wygasła – zaloguj się ponownie.");
                }
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders("Content-Disposition", "X-Missing-Fields"));
});

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    await DatabaseResilience.MigrateWithRetryAsync(
        scope.ServiceProvider.GetRequiredService<AppDbContext>(),
        scope.ServiceProvider.GetRequiredService<ILogger<Program>>());
    await DbSeeder.SeedAsync(scope.ServiceProvider);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseRouting();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).RequireRateLimiting("health-ready");

app.Run();

public partial class Program
{
}
