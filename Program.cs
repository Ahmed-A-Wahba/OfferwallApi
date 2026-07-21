using System.Text.Json.Serialization;
using Carter;
using FluentValidation;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using OfferwallApi.Infrastructure.Behaviors;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Options;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Infrastructure.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddOpenApi();

builder.Services.AddCarter();

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IJwtClaimsFactory, JwtClaimsFactory>();

builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));

builder.Services.AddScoped<IJwtService, JwtService>();

builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly, ServiceLifetime.Scoped);

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
    // cfg.AddOpenBehavior(typeof(DeviceFingerprintBlacklistBehavior<,>));
    // cfg.AddOpenBehavior(typeof(CachingBehavior<,>));
    // cfg.AddOpenBehavior(typeof(CacheInvalidationBehavior<,>));
});

builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddSingleton<IApiKeyGenerator, ApiKeyGenerator>();

builder.Services.Configure<EmailOptions>(
    builder.Configuration.GetSection(EmailOptions.SectionName));

builder.Services.AddScoped<IEmailSender, EmailSender>();

builder.Services.AddScoped<IVerificationCodeService, VerificationCodeService>();



var app = builder.Build();

// Configure the HTTP request pipeline.

app.MapOpenApi();

app.MapScalarApiReference(options =>
{
    options.Title = "Offerwall API";
    options.Theme = ScalarTheme.DeepSpace;
    options.DefaultHttpClient = new(ScalarTarget.CSharp, ScalarClient.HttpClient);
});

app.MapCarter();

app.UseHttpsRedirection();

app.UseCors("CorsPolicy");

app.Run();
