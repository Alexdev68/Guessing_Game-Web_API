using GuessingGame.API.Authentication;
using GuessingGame.API.Data;
using GuessingGame.API.Models;
using GuessingGame.API.Repositories;
using GuessingGame.API.Repositories.Interfaces;
using GuessingGame.API.Services;
using GuessingGame.API.Services.Interfaces;
using GuessingGame.API.Exceptions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Microsoft.AspNetCore.HttpLogging;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    string xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    string xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Description = "Enter your JWT access token.",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        });

    options.AddSecurityDefinition("Basic", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Description = "Enter your registered email and password.",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "basic"
        });

    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
        {
            Name = "X-API-Key",
            Description = "Enter your API key.",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "ApiKey"
        });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Basic", document)] = []
        });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("ApiKey", document)] = []
        });
});

string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Database connection string is missing.");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(
        connectionString,
        npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null);
        });
    if (builder.Environment.IsDevelopment())
    {
        options.EnableDetailedErrors();
        options.EnableSensitiveDataLogging();
    }
});


builder.Services.AddScoped<IGameRepository, GameRepository>();
builder.Services.AddScoped<IPlayerRepository, PlayerRepository>();
builder.Services.AddScoped<IGameService, GameService>();
builder.Services.AddScoped<IRoundService, RoundService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IAuthService, AuthService>();

string jwtToken = builder.Configuration["Jwt:Token"] ?? throw new InvalidOperationException("JWT token key is missing.");

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = AuthSchemes.Any;

        options.DefaultChallengeScheme = AuthSchemes.Any;
    })

    .AddPolicyScheme(AuthSchemes.Any, "JWT, API key, or Basic authentication", options =>
        {
            options.ForwardDefaultSelector = context =>
                {
                    string authorization = context.Request.Headers.Authorization.FirstOrDefault() ?? string.Empty;

                    if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        return AuthSchemes.Jwt;
                    }

                    if (authorization.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
                    {
                        return AuthSchemes.Basic;
                    }

                    if (context.Request.Headers.ContainsKey("X-API-Key"))
                    {
                        return AuthSchemes.ApiKey;
                    }

                    return AuthSchemes.Jwt;
                };
        })

    .AddJwtBearer(AuthSchemes.Jwt, options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = builder.Configuration["Jwt:Issuer"],
                    ValidAudience = builder.Configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey( Encoding.UTF8.GetBytes(jwtToken)),
                    RoleClaimType = ClaimTypes.Role,
                    NameClaimType = ClaimTypes.Name,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
        })

    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(AuthSchemes.ApiKey, options => { })

    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>(AuthSchemes.Basic, options => { });

builder.Services.AddAuthorization();

builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields =
        HttpLoggingFields.RequestProtocol |
        HttpLoggingFields.RequestMethod |
        HttpLoggingFields.RequestPath |
        HttpLoggingFields.ResponseStatusCode |
        HttpLoggingFields.Duration;

    options.CombineLogs = true;
});

builder.Services.AddProblemDetails();

builder.Services.AddMemoryCache();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI();

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseHttpLogging();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();