using AutonomousStore.AdminApp;
using AutonomousStore.AdminApp.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Mesmo endere�o da AutonomousStore.WebApi usado no ClientApp.
var apiBaseAddress = "https://localhost:7167/";

builder.Services.AddSingleton<AppState>();
builder.Services.AddTransient<AuthHeaderHandler>();

builder.Services.AddHttpClient("AutonomousStoreApi", client => client.BaseAddress = new Uri(apiBaseAddress))
    .AddHttpMessageHandler<AuthHeaderHandler>();

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("AutonomousStoreApi"));

builder.Services.AddScoped<IAdminAuthApiService, AdminAuthApiService>();
builder.Services.AddScoped<ISessionApiService, SessionApiService>();
builder.Services.AddScoped<IVisionApiService, VisionApiService>();
builder.Services.AddScoped<IProductApiService, ProductApiService>();
builder.Services.AddScoped<ICatalogApiService, CatalogApiService>();

// --- o gerente de plantao -------------------------------------------------
//
// HttpClient PROPRIO, SEM o AuthHeaderHandler: o monitor so devolve dado
// anonimo (cameras, quantas pessoas ha no chao) e nao tem por que receber o
// token de admin. Ele vale para a WebApi e para mais nada.
//
// O endereco e local por natureza — o monitor roda na mesma maquina do
// SO-Espacial, ao lado das cameras. Se ele nao estiver no ar, o chat
// responde sem a parte espacial em vez de quebrar.
var monitorBaseAddress = "http://localhost:8760/";

builder.Services.AddHttpClient("MonitorGerente",
    client => client.BaseAddress = new Uri(monitorBaseAddress));

// O modelo treinado mora em wwwroot/modelos/. Este cliente aponta para a
// propria origem do app — nao ha servidor envolvido, so o arquivo servido
// junto com a pagina.
builder.Services.AddHttpClient("AdminAppEstatico",
    client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));

builder.Services.AddScoped<IGerenteEspacialService, GerenteEspacialService>();

// SINGLETON, e nao Scoped: o modelo tem 856 vetores e 14.429 parametros, e
// desserializar isso a cada navegacao seria desperdicio visivel. Carregado
// uma vez, vale para a sessao inteira do painel.
builder.Services.AddSingleton<IClassificadorDeIntencao, ClassificadorDeIntencao>();

builder.Services.AddScoped<IGerenteService, GerenteService>();

await builder.Build().RunAsync();
