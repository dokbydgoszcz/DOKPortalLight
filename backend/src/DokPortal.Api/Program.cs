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
using DokPortal.Application.NameDays;
using DokPortal.Application.ParishNeeds;
using DokPortal.Application.Permissions;
using DokPortal.Application.Parishes;
using DokPortal.Application.PastoralNotes;
using DokPortal.Application.Reminders;
using DokPortal.Application.Supervisions;
using DokPortal.Application.People;
using DokPortal.Application.Users;
using DokPortal.Api.Authorization;
using DokPortal.Api.ErrorHandling;
using Microsoft.AspNetCore.Authorization;
using DokPortal.Api.Filters;
using DokPortal.Infrastructure.Auth;
using DokPortal.Infrastructure.Identity;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Seed;
using DokPortal.Infrastructure.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options => options.Filters.Add<ValidationActionFilter>())
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddValidatorsFromAssemblyContaining<DokPortal.Application.Auth.LoginRequestValidator>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

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
    .AddEntityFrameworkStores<AppDbContext>();

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("Missing Jwt configuration section.");
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IPersonService, PersonService>();
builder.Services.AddScoped<IParishService, ParishService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services.AddScoped<ICandidateService, CandidateService>();
builder.Services.AddScoped<IMissionService, MissionService>();
builder.Services.AddScoped<IFormatorService, FormatorService>();
builder.Services.AddScoped<IParishNeedService, ParishNeedService>();
builder.Services.AddScoped<IBudgetService, BudgetService>();
builder.Services.AddScoped<IDokCaseService, DokCaseService>();
builder.Services.AddScoped<ICaseDocumentService, CaseDocumentService>();
builder.Services.AddScoped<IPastoralNoteService, PastoralNoteService>();
builder.Services.AddScoped<IMeetingService, MeetingService>();
builder.Services.AddScoped<ISupervisionService, SupervisionService>();
builder.Services.AddScoped<INameDayService, NameDayService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IMailingService, MailingService>();
builder.Services.AddScoped<IReminderService, ReminderService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddMemoryCache();
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
    var smtpFromName = builder.Configuration["Smtp:FromName"] ?? "DOK Portal Light";
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
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
    await DbSeeder.SeedAsync(scope.ServiceProvider);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program
{
}
