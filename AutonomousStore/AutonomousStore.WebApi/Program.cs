using System.Text;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.WebApi;
using AutonomousStore.Infrastructure.Persistence;
using AutonomousStore.Infrastructure.Repositories;
using AutonomousStore.WebApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Registros padrão da WebApi
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Permite testar endpoints protegidos direto pelo Swagger, colando "Bearer {token}"
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Cole aqui: Bearer {seu token}"
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
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

// Adiciona o DbContext conectando com o SQL Server
builder.Services.AddDbContext<AutonomousDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("SqlServerConnection"),
        sqlOptions =>
        {
            sqlOptions.MigrationsHistoryTable("MigrationHistory");
        }));

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICompanyRepository, CompanyRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IStoreSessionRepository, StoreSessionRepository>();
builder.Services.AddScoped<IAdminUserRepository, AdminUserRepository>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddHttpClient("GeminiApi");
builder.Services.AddScoped<IGeminiChatService, GeminiChatService>();
builder.Services.AddScoped<IGeminiVisionService, GeminiVisionService>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();

// Autenticação por token JWT — quem loga (senha ou Google) recebe um token,
// e esse token é exigido nos endpoints marcados com [Authorize].
var jwtSection = builder.Configuration.GetSection("Jwt");
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
        ValidIssuer = jwtSection["Issuer"],
        ValidAudience = jwtSection["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!))
    };
});
builder.Services.AddAuthorization();

// Libera o app Blazor (ClientApp) a chamar esta API durante o desenvolvimento.
// Ajuste as portas se o seu ClientApp rodar em outras (confira em Properties/launchSettings.json dele).
const string ClientAppCorsPolicy = "ClientAppCorsPolicy";
builder.Services.AddCors(options =>
{
    options.AddPolicy(ClientAppCorsPolicy, policy =>
    {
        policy.WithOrigins("https://localhost:7280", "http://localhost:5280", "https://localhost:7290", "http://localhost:5290")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Seed de exemplo: só roda em dev e só se ainda não existir nenhuma empresa cadastrada —
    // idempotente, popula o catálogo do ClientApp pra não começar vazio.
    using (var scope = app.Services.CreateScope())
    {
        await SeedData.SeedIfEmptyAsync(scope.ServiceProvider);
    }
}

// app.UseHttpsRedirection(); // Comentado para permitir conexões HTTP do ESP32
app.UseCors(ClientAppCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
