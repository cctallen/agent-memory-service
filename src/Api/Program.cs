using AgentMemoryService.Agents.Application.Commands;
using AgentMemoryService.Memories.Application.Commands;
using AgentMemoryService.Memories.Application.Queries;
using AgentMemoryService.Namespaces.Application.Commands;
using AgentMemoryService.Namespaces.Application.Queries;
using AgentMemoryService.Secrets.Application.Commands;
using AgentMemoryService.Secrets.Application.Queries;
using AgentMemoryService.Shared.Core;
using AgentMemoryService.Shared.Infrastructure;
using AgentMemoryService.Users.Application.Commands;
using AgentMemoryService.Users.Core.Entities;
using AgentMemoryService.Users.Infrastructure.Handlers;
using AgentMemoryService.Users.Infrastructure.Services;
using AgentMemoryService.Api;
using AgentMemoryService.Api.Auth;
using AgentMemoryService.Shared.Infrastructure.Caching;
using AgentMemoryService.Api.Endpoints;
using AgentMemoryService.Mcp.Core;
using Microsoft.AspNetCore.Authentication;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Server;
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
.AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, _ => { })
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
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(
        JwtBearerDefaults.AuthenticationScheme,
        ApiKeyAuthenticationHandler.SchemeName)
        .RequireAuthenticatedUser()
        .Build();
});

// ── MediatR ───────────────────────────────────────────────────
// Register all handler assemblies — Application layer has commands/queries,
// Infrastructure layers have the actual handlers that depend on EF/Identity/etc.
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblies(
        typeof(RegisterCommand).Assembly,       // Users.Application
        typeof(LoginHandler).Assembly,          // Users.Infrastructure
        typeof(CreateNamespaceCommand).Assembly,// Namespaces.Application
        typeof(AddMemberCommand).Assembly,      // Namespaces.Application (members)
        typeof(GetNamespacesQuery).Assembly,    // Namespaces.Application
        typeof(AddMemoryCommand).Assembly,      // Memories.Application
        typeof(GetMemoriesQuery).Assembly,      // Memories.Application
        typeof(CreateAgentCommand).Assembly,    // Agents.Application
        typeof(CreateSecretCommand).Assembly,   // Secrets.Application
        typeof(GetSecretsQuery).Assembly        // Secrets.Application queries
    ));

// ── Secrets encryption ────────────────────────────────────────
builder.Services.AddSingleton<SecretEncryptionService>();

// ── Validation ────────────────────────────────────────────────
builder.Services.AddValidatorsFromAssemblies([
    typeof(RegisterCommand).Assembly,           // Users.Application validators
    typeof(CreateNamespaceCommand).Assembly,
    typeof(AddMemoryCommand).Assembly,
    typeof(CreateAgentCommand).Assembly,
    typeof(CreateSecretCommand).Assembly        // Secrets.Application validators
]);
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

// ── Cache ────────────────────────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IMemoryCacheService, HybridMemoryCacheService>();

// ── MCP (HTTP transport) ─────────────────────────────────────
// The MCP endpoint lives at /mcp and authenticates via X-Api-Key.
// MindVaultClient calls back into this same server on localhost,
// forwarding the caller's API key via ForwardApiKeyHandler.
{
    var selfUrl = builder.Configuration["Mcp:SelfUrl"] ?? "http://localhost:5228";
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddTransient<AgentMemoryService.Api.ForwardApiKeyHandler>();
    builder.Services.AddHttpClient<MindVaultClient>(client =>
    {
        client.BaseAddress = new Uri(selfUrl);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.Timeout = TimeSpan.FromSeconds(30);
    }).AddHttpMessageHandler<AgentMemoryService.Api.ForwardApiKeyHandler>();
    // MindVaultConfig is request-scoped — namespace ID comes from the X-Mcp-Namespace header
    builder.Services.AddScoped<MindVaultConfig>(sp =>
    {
        var http = sp.GetRequiredService<IHttpContextAccessor>();
        var ns = http.HttpContext?.Request.Headers["X-Mcp-Namespace"].FirstOrDefault()
            ?? builder.Configuration["Mcp:DefaultNamespaceId"]
            ?? string.Empty;
        return new MindVaultConfig(ns);
    });
    builder.Services
        .AddMcpServer()
        .WithHttpTransport()
        .WithToolsFromAssembly(typeof(MindVaultClient).Assembly);
}

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
    c.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Agent API key. Enter your key directly (no Bearer prefix).",
        Name = "X-Api-Key",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey
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
app.MapNamespaceMemberEndpoints();
app.MapMemoryEndpoints();
app.MapAgentEndpoints();
app.MapUserEndpoints();
app.MapSecretEndpoints();

// ── MCP endpoint ──────────────────────────────────────────────
// Secured by the existing ApiKey middleware — pass X-Api-Key + X-Mcp-Namespace
app.MapMcp("/mcp").RequireAuthorization();

// ── DB Migration on startup ───────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await RoleSeeder.SeedAsync(scope.ServiceProvider);
}

app.Run();
