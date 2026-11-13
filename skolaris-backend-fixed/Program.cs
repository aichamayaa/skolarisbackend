using Microsoft.EntityFrameworkCore;
using Skolaris.Data;
using Skolaris.Services;


// Npgsql 6+ : permet les DateTime "Unspecified/Local" (comportement identique a SQL Server)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Render fournit le port via la variable PORT
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Chaine de connexion : accepte "postgres://user:pass@host/db" (format Render) ou le format Npgsql
static string BuildConnectionString(string? raw)
{
    if (string.IsNullOrWhiteSpace(raw)) return "";
    if (!raw.StartsWith("postgres://") && !raw.StartsWith("postgresql://")) return raw;
    var uri = new Uri(raw);
    var userInfo = uri.UserInfo.Split(':', 2);
    var host = uri.Host;
    var dbPort = uri.Port > 0 ? uri.Port : 5432;
    return $"Host={host};Port={dbPort};Database={uri.AbsolutePath.TrimStart('/')};" +
           $"Username={Uri.UnescapeDataString(userInfo[0])};Password={Uri.UnescapeDataString(userInfo[1])};" +
           "SSL Mode=Require;Trust Server Certificate=true";
}
var connectionString = BuildConnectionString(
    Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("DefaultConnection"));

// Enum conversion
builder.Services.AddControllers(options =>
    {
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.Converters
        .Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        options.JsonSerializerOptions.ReferenceHandler =
            System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString)
    .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<EtablissementService>();
builder.Services.AddScoped<SessionAcademiqueService>();
builder.Services.AddScoped<DossierAcademiqueService>();
builder.Services.AddScoped<AbsenceService>();
builder.Services.AddScoped<InscriptionService>();
builder.Services.AddScoped<NoteService>();
builder.Services.AddScoped<BulletinService>();
builder.Services.AddScoped<GrilleEvaluationService>();
builder.Services.AddScoped<MessagerieService>();

builder.Services.AddScoped<SmsService>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<EmploiDuTempsService>();

builder.Services.AddScoped<NotificationService>();


// Limite upload: 10 Mo pour les pièces jointes de la messagerie
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 10_485_760; // 10 Mo
});
builder.WebHost.ConfigureKestrel(opts =>
{
    opts.Limits.MaxRequestBodySize = 10_485_760; // 10 Mo
});


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazor", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// mohammed ! création automatique de la BD + seed au démarrage ne le change pas svp !!!!!!
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated(); // crée toutes les tables depuis le DbContext, sans fichiers migration
    DataSeeder.Seed(db);
}

// HTTPS gere par Render (reverse proxy)
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowBlazor");

// Swagger actif aussi en production (pratique pour la demo CV)
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapGet("/health", () => Results.Ok("ok"));


app.UseAuthorization();
app.MapControllers();



app.Run();
