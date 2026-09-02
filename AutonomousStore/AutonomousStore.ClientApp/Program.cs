using AutonomousStore.Comum;
using AutonomousStore.Gerente;
using AutonomousStore.ClientApp;
using AutonomousStore.ClientApp.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Endere�o da AutonomousStore.WebApi. Ajuste aqui se a porta da sua API for diferente
// (confira em Properties/launchSettings.json do projeto WebApi, ou na URL do Swagger).
// Endereco da AutonomousStore.WebApi.
//
// Por padrao e descoberto sozinho: o app assume que a API esta no MESMO
// computador que serviu esta pagina, na porta HTTP 5071.
//
//   Abriu em http://localhost:5280      -> chama http://localhost:5071
//   Abriu em http://172.20.10.2:5280    -> chama http://172.20.10.2:5071
//
// Ou seja: quando o IP do PC mudar de rede para rede, NADA precisa ser
// recompilado. Basta abrir o app pelo novo IP no celular.
//
// Para forcar um endereco fixo (o tunel ngrok, por exemplo), descomente e
// preencha a linha do override abaixo.
const string? apiBaseAddressOverride = null;
// const string? apiBaseAddressOverride = "https://reselect-headfirst-headless.ngrok-free.dev/";

var apiBaseAddress = apiBaseAddressOverride
    ?? $"http://{new Uri(builder.HostEnvironment.BaseAddress).Host}:5071/";

builder.Services.AddSingleton<AppState>();
builder.Services.AddTransient<AuthHeaderHandler>();

builder.Services.AddHttpClient("AutonomousStoreApi", client =>
    {
        client.BaseAddress = new Uri(apiBaseAddress);
        // Pula a pagina de aviso do ngrok na conta gratuita.
        // Sem isso o ngrok devolve HTML no lugar do JSON e o app quebra.
        client.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "true");
    })
    .AddHttpMessageHandler<AuthHeaderHandler>();

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("AutonomousStoreApi"));

builder.Services.AddScoped<IAuthApiService, AuthApiService>();
builder.Services.AddScoped<ICustomerApiService, CustomerApiService>();
builder.Services.AddScoped<ISessionApiService, SessionApiService>();
builder.Services.AddScoped<ICatalogApiService, CatalogApiService>();
builder.Services.AddScoped<IChatApiService, ChatApiService>();

// A conversa de suporte, nos três apps. O HttpClient continua sendo de cada
// um: é ele que carrega o token de quem está logado, e a rota de chamados
// devolve só o que essa pessoa pode ver.
builder.Services.AdicionarChamados();

// O gerente também atende o comprador. Quem decide o que ele pode dizer
// é o PerfilDeQuemFala passado ao <GerenteChat /> no MainLayout.
builder.Services.AdicionarGerente(builder.HostEnvironment.BaseAddress);

await builder.Build().RunAsync();