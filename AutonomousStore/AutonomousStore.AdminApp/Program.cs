using AutonomousStore.Comum;
using AutonomousStore.AdminApp;
using AutonomousStore.AdminApp.Services;
using AutonomousStore.Gerente;
using AutonomousStore.Gerente.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Mesmo endereço da AutonomousStore.WebApi usado no ClientApp.
var apiBaseAddress = "https://localhost:7167/";

builder.Services.AddSingleton<AppState>();
builder.Services.AddTransient<AuthHeaderHandler>();

builder.Services.AddHttpClient("AutonomousStoreApi", client => client.BaseAddress = new Uri(apiBaseAddress))
    .AddHttpMessageHandler<AuthHeaderHandler>();

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("AutonomousStoreApi"));

builder.Services.AddScoped<IAdminAuthApiService, AdminAuthApiService>();
builder.Services.AddScoped<IVisionApiService, VisionApiService>();
builder.Services.AddScoped<ICatalogApiService, CatalogApiService>();

// SCOPED, e nao Transient: o servico guarda a planta depois da primeira
// leitura. Transient jogaria fora o cache a cada injecao, e a planta voltaria
// pela rede cinco vezes por segundo.
builder.Services.AddScoped<IEspacialApiService, EspacialApiService>();

// --- o gerente de plantao -------------------------------------------------
//
// UMA LINHA REGISTRA O AGENTE INTEIRO: os servicos que ele le, o
// classificador e os dois clientes HTTP proprios dele. A lista mora na
// biblioteca, em ServicosDoGerente.
//
// Ela ficava aqui, com vinte linhas. O problema so apareceu quando o suporte
// tambem precisou do gerente: seriam DUAS listas iguais, e no dia em que ele
// ganhasse um servico novo uma das duas ficaria para tras — com o chat
// quebrado so de um lado, que e o jeito mais lento de descobrir.
//
// O cliente da WebApi (`AutonomousStoreApi`, acima) NAO entra nisso de
// proposito: ele carrega o cabecalho de autenticacao, e o token do admin nao
// e o token do suporte.
builder.Services.AdicionarGerente(builder.HostEnvironment.BaseAddress);
// A conversa de suporte, nos três apps. O HttpClient continua sendo de cada
// um: é ele que carrega o token de quem está logado, e a rota de chamados
// devolve só o que essa pessoa pode ver.
builder.Services.AdicionarChamados();


await builder.Build().RunAsync();
