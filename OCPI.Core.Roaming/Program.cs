using OCPI.Core.Roaming.Services;
using OCPI.Core.Roaming.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// Register OCPI Services
builder.Services.AddScoped<IOcpiLocationService, OcpiLocationService>();
builder.Services.AddScoped<IOcpiSessionService, OcpiSessionService>();
builder.Services.AddScoped<IOcpiCredentialsService, OcpiCredentialsService>();
builder.Services.AddScoped<IOcpiVersionService, OcpiVersionService>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "OCPI Roaming API",
        Version = "v2.2.1",
        Description = "OCPI (Open Charge Point Interface) Roaming API for EV Charging Platform",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "EV Charging Platform",
            Email = "support@evcharging.com"
        }
    });

    // Add XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    // Add OCPI Authorization header
    c.AddSecurityDefinition("OCPI-Token", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "OCPI Authorization Token",
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
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "OCPI Roaming API v2.2.1");
        c.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
    });
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

// Log startup information
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("OCPI Roaming API started successfully");
logger.LogInformation($"Base URL: {builder.Configuration["OCPI:BaseUrl"]}");
logger.LogInformation($"Country Code: {builder.Configuration["OCPI:CountryCode"]}");
logger.LogInformation($"Party ID: {builder.Configuration["OCPI:PartyId"]}");

app.Run();
