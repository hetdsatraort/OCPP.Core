using OCPI;
using OCPP.Core.Database;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add all required OCPI services to the application
// This automatically registers all OCPI.Net services including:
// - IOcpiVersionService (auto-scans controllers and generates version endpoints)
// - Exception handling middleware
// - Validation services
// - Authorization services
builder.AddOcpi();

// Add Database Context (for future integration with OCPP.Core.Database)
builder.Services.AddDbContext<OCPPCoreContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer")));

// Register OCPI Services
builder.Services.AddHttpClient();
builder.Services.AddScoped<OCPI.Core.Roaming.Services.IOcpiCredentialsService, OCPI.Core.Roaming.Services.OcpiCredentialsService>();
builder.Services.AddScoped<OCPI.Core.Roaming.Services.IOcpiLocationService, OCPI.Core.Roaming.Services.OcpiLocationService>();
builder.Services.AddScoped<OCPI.Core.Roaming.Services.IOcpiSessionService, OCPI.Core.Roaming.Services.OcpiSessionService>();
builder.Services.AddScoped<OCPI.Core.Roaming.Services.IOcpiCdrService, OCPI.Core.Roaming.Services.OcpiCdrService>();
builder.Services.AddScoped<OCPI.Core.Roaming.Services.IOcpiTariffService, OCPI.Core.Roaming.Services.OcpiTariffService>();
builder.Services.AddScoped<OCPI.Core.Roaming.Services.IOcpiTokenService, OCPI.Core.Roaming.Services.OcpiTokenService>();
builder.Services.AddScoped<OCPI.Core.Roaming.Services.IOcpiCommandService, OCPI.Core.Roaming.Services.OcpiCommandService>();
builder.Services.AddScoped<OCPI.Core.Roaming.Services.IOcpiChargingProfileService, OCPI.Core.Roaming.Services.OcpiChargingProfileService>();
builder.Services.AddScoped<OCPI.Core.Roaming.Services.IChargingSessionService, OCPI.Core.Roaming.Services.ChargingSessionService>();

// Register OCPI Background Service
builder.Services.AddHostedService<OCPI.Core.Roaming.BackgroundServices.OcpiSyncBackgroundService>();
builder.Services.AddHostedService<OCPI.Core.Roaming.BackgroundServices.OcpiOrphanSessionService>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", builder =>
    {
        builder.WithOrigins("http://localhost:4200", "https://evc-admin.ortdemo.com", "https://admin.hycharge.in", "https://charge.hycharge.in", "https://app.hycharge.in")
               .AllowCredentials()
               .AllowAnyHeader()
               .AllowAnyMethod();
    });
});

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "OCPI Roaming API - HyCharge",
        Version = "v2.2.1",
        Description = "OCPI (Open Charge Point Interface) Roaming API for HyCharge",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "HyCharge",
            Email = "het.satra@onerooftech.com"
        }
    });

    // Add OCPI Authorization header
    c.AddSecurityDefinition("OCPI-Token", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "OCPI Authorization Token (format: 'Token <your-token>')",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Token"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "OCPI-Token"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || true)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "OCPI Roaming API v2.2.1");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "OCPI.Core.Roaming API Documentation";
        c.DisplayRequestDuration();
    });
}

//app.UseHttpsRedirection();

app.UseCors("AllowAngularApp");

app.UseAuthentication();
app.UseRouting();
app.UseAuthorization();

app.MapControllers();

// Log startup information
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("OCPI Roaming API started successfully");
logger.LogInformation($"Base URL: {builder.Configuration["OCPI:BaseUrl"]}");
logger.LogInformation($"Country Code: {builder.Configuration["OCPI:CountryCode"]}");
logger.LogInformation($"Party ID: {builder.Configuration["OCPI:PartyId"]}");

app.Run();
