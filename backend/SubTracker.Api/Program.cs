using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using SubTracker.Api.Auth;
using SubTracker.Api.Common;
using SubTracker.Api.Data;
using SubTracker.Api.Entities;
using SubTracker.Api.OpenApi;
using SubTracker.Api.Services;

var builder = WebApplication.CreateBuilder(args);


//-------------
//-----Database
//-------------

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' saknas. Lokalt sätts den med dotnet user-secrets (se README.md).");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));


//-------------
//-----Identity
//-------------

builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddErrorDescriber<SwedishIdentityErrorDescriber>();


//-------------------
//-----Authentication
//-------------------

builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

builder.Services.AddScoped<SecurityStampValidationEvents>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.EventsType = typeof(SecurityStampValidationEvents);
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = jwtOptions.CreateSigningKey(),
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization();


//---------
//-----CORS
//---------

const string FrontendCorsPolicy = "Frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));


//-------------
//-----Services
//-------------

var swedishTime = new SwedishTimeProvider();

builder.Services.AddSingleton<TimeProvider>(swedishTime);
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IForecastService, ForecastService>();

builder.Services.AddControllers();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddOperationTransformer<BearerSecuritySchemeTransformer>();
});


//------------
//-----Startup
//------------

var app = builder.Build();

if (allowedOrigins.Length == 0)
{
    app.Logger.LogWarning("Cors:AllowedOrigins är tom – ingen frontend kan anropa API:t.");
}

if (swedishTime.UsesFallbackZone)
{
    app.Logger.LogWarning("Tidszonen Europe/Stockholm saknas på servern – dagens datum räknas i UTC i stället för svensk tid.");
}

// Migrationerna är skrivna för SQL Server. Integrationstesterna kör mot SQLite och stänger därför av steget.
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    try
    {
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogCritical(ex, "Kunde inte migrera databasen. Kontrollera connection string och brandväggen i Azure SQL.");
    }
}


//---------------
//-----Middleware
//---------------

app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();


//--------------
//-----Endpoints
//--------------

app.MapOpenApi();
app.MapScalarApiReference(options => options.WithTitle("SubTracker API"));

app.MapControllers();
app.MapHealthChecks("/api/health");
app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();

app.Run();

// Gör Program synlig för WebApplicationFactory i testprojektet.
public partial class Program;
