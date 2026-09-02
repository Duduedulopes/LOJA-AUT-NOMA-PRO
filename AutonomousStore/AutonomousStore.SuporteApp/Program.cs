using AutonomousStore.Comum;
using AutonomousStore.SuporteApp;
using AutonomousStore.Gerente;
using AutonomousStore.SuporteApp.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseAddress = "https://localhost:7167/";

builder.Services.AddSingleton<AppState>();
builder.Services.AddTransient<AuthHeaderHandler>();

builder.Services.AddHttpClient("AutonomousStoreApi", client => client.BaseAddress = new Uri(apiBaseAddress))
    .AddHttpMessageHandler<AuthHeaderHandler>();

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("AutonomousStoreApi"));

builder.Services.AddScoped<ISuporteAuthApiService, SuporteAuthApiService>();

// --- o gerente, o MESMO do painel do admin --------------------------------
//
// Uma linha registra o agente inteiro: o classificador, os servicos que ele
// le (produtos, sessoes, ocorrencias, espacial) e os dois clientes HTTP
// proprios dele. A lista mora na biblioteca, em ServicosDoGerente — se ela
// fosse copiada para ca, no dia em que o gerente ganhasse um servico novo o
// chat quebraria so de um lado.
//
// O `IOcorrenciaApiService` que este arquivo registrava vem daqui agora. A
// versao da biblioteca e superconjunto da que estava aqui: tem tudo o que as
// telas do suporte chamam, mais o `EnviarAoSuporteAsync`.
//
// O cliente `AutonomousStoreApi` (acima) continua sendo deste aplicativo: e
// ele que carrega o token do TECNICO, que nao e o token do admin.
builder.Services.AdicionarGerente(builder.HostEnvironment.BaseAddress);
// A conversa de suporte, nos três apps. O HttpClient continua sendo de cada
// um: é ele que carrega o token de quem está logado, e a rota de chamados
// devolve só o que essa pessoa pode ver.
builder.Services.AdicionarChamados();


await builder.Build().RunAsync();
