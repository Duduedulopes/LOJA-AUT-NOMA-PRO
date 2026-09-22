using AutonomousStore.Comum;
using AutonomousStore.CriadorApp;
using AutonomousStore.CriadorApp.Services;
using AutonomousStore.Gerente;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Mesmo endereço da AutonomousStore.WebApi usado nos outros três apps.
var apiBaseAddress = "https://localhost:7167/";

builder.Services.AddSingleton<AppState>();
builder.Services.AddTransient<AuthHeaderHandler>();

builder.Services.AddHttpClient("AutonomousStoreApi", client => client.BaseAddress = new Uri(apiBaseAddress))
    .AddHttpMessageHandler<AuthHeaderHandler>();

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("AutonomousStoreApi"));

builder.Services.AddScoped<ICriadorAuthApiService, CriadorAuthApiService>();
builder.Services.AddScoped<IPlatformApiService, PlatformApiService>();

// --- o gerente, o MESMO dos outros tres apps -------------------------------
//
// Uma linha registra o agente inteiro: o classificador, os servicos que ele
// le e os dois clientes HTTP proprios dele. A lista mora na biblioteca, em
// ServicosDoGerente — copiar as vinte linhas para ca faria o dia em que o
// gerente ganhasse um servico novo quebrar o chat so deste app.
builder.Services.AdicionarGerente(builder.HostEnvironment.BaseAddress);

// A conversa de suporte, a mesma dos outros três apps. O HttpClient continua
// sendo deste app: é ele que carrega o token do Criador, que não é o token
// de ninguém mais.
builder.Services.AdicionarChamados(sp => sp.GetRequiredService<AppState>().Token);

await builder.Build().RunAsync();
