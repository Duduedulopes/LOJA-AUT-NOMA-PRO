using System.Text;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.WebApi;
using AutonomousStore.Infrastructure.Logging;
using AutonomousStore.Infrastructure.Persistence;
using AutonomousStore.Infrastructure.Repositories;
using AutonomousStore.WebApi.Controllers;
using AutonomousStore.WebApi.Middlewares;
using AutonomousStore.WebApi.Services;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Registros padrão da WebApi
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        // ENUM COMO TEXTO NO JSON. "tipo": "Roubo", nunca "tipo": 9. Um log
        // numerado obriga quem o lê a ter o código em mãos — e log serve
        // justamente para quando o código não está em mãos. Vale também
        // para o filtro da tela do suporte, que fica legível na URL.
        o.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
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

            // TENTA DE NOVO ANTES DE DESISTIR.
            //
            // O SQL Express acorda devagar. Na primeira conexão depois de
            // um tempo parado, o banco precisa ser trazido para online — e
            // isso acontece DEPOIS da autenticação, na fase de pós-login.
            // O padrão de 15 segundos não cobre esse acordar: já estourou
            // aqui com `[Post-Login] complete=12098`, ou seja, doze
            // segundos esperando um banco que estava só se levantando.
            //
            // Não é erro de conexão: o servidor foi achado, o handshake
            // passou e a autenticação passou. É lentidão momentânea, que é
            // exatamente o que uma política de repetição existe para
            // absorver. Sem ela, a API morre na partida por causa de uma
            // espera que teria terminado sozinha.
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        }));

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICompanyRepository, CompanyRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IStoreSessionRepository, StoreSessionRepository>();
builder.Services.AddScoped<IAdminUserRepository, AdminUserRepository>();
builder.Services.AddScoped<ISuporteUserRepository, SuporteUserRepository>();
builder.Services.AddScoped<IOcorrenciaRepository, OcorrenciaRepository>();
builder.Services.AddScoped<IRegistradorDeOcorrencia, RegistradorDeOcorrencia>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// ── TODA EXCEÇÃO VIRA OCORRÊNCIA ─────────────────────────────────────────
//
// Antes disto, um erro dentro da WebApi sumia: o cliente levava um 500 sem
// explicação e o histórico do suporte não sabia que tinha acontecido. O
// detector já existia e estava testado — só nunca tinha sido ligado em nada.
builder.Services.AddExceptionHandler<CapturaDeExcecao>();
builder.Services.AddProblemDetails();

// ── O FREIO DA ROTA ANÔNIMA ──────────────────────────────────────────────
//
// `POST /api/ocorrencias/navegador` não pede login, porque o erro que mais
// importa ver é o da tela de login — e ali ninguém tem token ainda. O preço
// é que a rota precisa de limite: sem ele, um laço encheria a tabela que o
// Eduardo usa para enxergar a loja.
//
// 30 por minuto por IP: uma tela quebrada em loop manda uns poucos por
// segundo, e 30 deixa passar a rajada inicial (que é o diagnóstico) e corta
// o resto. Fila zero de propósito — relato de erro atrasado não vale nada, e
// enfileirar seria segurar memória do servidor por causa de quem abusa.
builder.Services.AddRateLimiter(opcoes =>
{
    opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    opcoes.AddPolicy(OcorrenciasController.FreioDeRelato, contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: contexto.Connection.RemoteIpAddress?.ToString() ?? "sem-ip",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});
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

// Libera o app Blazor (ClientApp) a chamar esta API.
// Em vez de uma lista fixa de portas, a regra esta na funcao IsOriginAllowed,
// la no fim do arquivo: aceita localhost, qualquer IP de rede local e o tunel.
// Assim o IP do PC pode mudar de rede para rede sem quebrar nada.
const string ClientAppCorsPolicy = "ClientAppCorsPolicy";
builder.Services.AddCors(options =>
{
    options.AddPolicy(ClientAppCorsPolicy, policy =>
    {
        policy.SetIsOriginAllowed(IsOriginAllowed)
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
    //
    // O SEED NÃO PODE DERRUBAR A API. Ele é conveniência de desenvolvimento:
    // se o banco estiver frio, ou o SQL Express ainda subindo, a API tem de
    // levantar mesmo assim e responder o que não depende de banco — inclusive
    // o Swagger, que é por onde se descobre o que está errado. Morrer na
    // partida por causa de um catálogo de exemplo deixa quem está depurando
    // sem nenhuma superfície para depurar.
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            await SeedData.SeedIfEmptyAsync(scope.ServiceProvider);
        }
        catch (Exception e)
        {
            app.Logger.LogWarning(e,
                "Não consegui rodar o seed de exemplo. A API sobe assim mesmo; " +
                "confira se o SQL Server está no ar e se a migração foi aplicada.");
        }
    }
}

// PRIMEIRO DE TODOS. O que este middleware não envolver, ele não captura — e
// uma exceção no CORS ou na autenticação é exatamente o tipo de erro que hoje
// some sem deixar rastro.
app.UseExceptionHandler();

// app.UseHttpsRedirection(); // Comentado para permitir conexões HTTP do ESP32
app.UseCors(ClientAppCorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();


// Quem pode chamar esta API pelo navegador.
//
// Aceita tres casos:
//   1. localhost / 127.0.0.1  -> desenvolvimento no proprio PC
//   2. IP de rede privada     -> celular no mesmo wifi ou no hotspot
//   3. o dominio fixo do ngrok -> demonstracao fora da rede
//
// A faixa privada cobre 10.x.x.x, 192.168.x.x e 172.16-31.x.x (essa ultima
// inclui o 172.20.10.x que o hotspot do iPhone usa). Esses IPs so existem
// dentro de uma rede local, entao liberar a faixa nao expoe a API na internet.
static bool IsOriginAllowed(string origin)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        return false;

    var host = uri.Host;

    if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host == "127.0.0.1")
        return true;

    if (host.Equals("reselect-headfirst-headless.ngrok-free.dev", StringComparison.OrdinalIgnoreCase))
        return true;

    if (!System.Net.IPAddress.TryParse(host, out var ip) ||
        ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        return false;

    var b = ip.GetAddressBytes();

    return b[0] == 10
        || (b[0] == 192 && b[1] == 168)
        || (b[0] == 172 && b[1] >= 16 && b[1] <= 31);
}
