using AgentMemoryService.Agents.Application.Commands;
using AgentMemoryService.Memories.Application.Commands;
using AgentMemoryService.Memories.Application.Queries;
using AgentMemoryService.Namespaces.Application.Commands;
using AgentMemoryService.Namespaces.Application.Queries;
using AgentMemoryService.Shared.Core;
using AgentMemoryService.Shared.Infrastructure;
using AgentMemoryService.Users.Application.Commands;
using AgentMemoryService.Users.Core.Entities;
using AgentMemoryService.Users.Infrastructure.Handlers;
using AgentMemoryService.Users.Infrastructure.Services;
using AgentMemoryService.Api.Endpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Postgres"),
        o => o.UseVector()));

// ── Identity ──────────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = true;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// ── Redis ─────────────────────────────────────────────────────
builder.Services.AddStackExchangeRedisCache(options =>
    options.Configuration = builder.Configuration.GetConnectionString("Redis"));

// ── JWT ───────────────────────────────────────────────────────
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
        ClockSkew = TimeSpan.Zero // no grace period on token expiry
    };
});
builder.Services.AddAuthorization();

// ── MediatR ───────────────────────────────────────────────────
// Register all handler assemblies — Application layer has commands/queries,
// Infrastructure layers have the actual handlers that depend on EF/Identity/etc.
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblies(
        typeof(RegisterCommand).Assembly,       // Users.Application
        typeof(LoginHandler).Assembly,          // Users.Infrastructure
        typeof(CreateNamespaceCommand).Assembly,// Namespaces.Application
        typeof(GetNamespacesQuery).Assembly,    // Namespaces.Application
        typeof(AddMemoryCommand).Assembly,      // Memories.Application
        typeof(GetMemoriesQuery).Assembly,      // Memories.Application
        typeof(CreateAgentCommand).Assembly     // Agents.Application
    ));

// ── Validation ────────────────────────────────────────────────
builder.Services.AddValidatorsFromAssemblies([
    typeof(RegisterCommand).Assembly,           // Users.Application validators
    typeof(CreateNamespaceCommand).Assembly,
    typeof(AddMemoryCommand).Assembly,
    typeof(CreateAgentCommand).Assembly
]);
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

// ── Services ──────────────────────────────────────────────────
builder.Services.AddScoped<ITokenService, TokenService>();

// ── Swagger ───────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Agent Memory Service",
        Version = "v1",
        Description = "Cross-agent shared memory platform. One memory. Every agent. Your team."
    });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter: Bearer {your_access_token}",
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ── Middleware ────────────────────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ─────────────────────────────────────────────────
app.MapAuthEndpoints();
app.MapNamespaceEndpoints();
app.MapMemoryEndpoints();
app.MapAgentEndpoints();

// ── DB Migration on startup ───────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();
