using AutonomousStore.Gerente.Services;
using AutonomousStore.Gerente.Services.Aprendizado;
using Microsoft.Extensions.DependencyInjection;

namespace AutonomousStore.Gerente;

/// <summary>
/// Tudo que o gerente precisa para existir dentro de um aplicativo, numa
/// linha só.
/// </summary>
/// <remarks>
/// POR QUE ISTO EXISTE.
///
/// Tirar o gerente de dentro do AdminApp resolveu a cópia do CÓDIGO, mas
/// sobrava a cópia do REGISTRO: uns dez `AddScoped` que o painel do admin e o
/// ambiente do suporte teriam de repetir iguais. Duas listas iguais em dois
/// arquivos é a mesma doença de antes, só que mais silenciosa — no dia em que
/// o gerente ganhasse um serviço novo, um dos dois aplicativos ficaria para
/// trás e só se descobriria com o chat quebrado do lado esquecido.
///
/// O QUE ESTE MÉTODO NÃO REGISTRA, DE PROPÓSITO.
///
/// O cliente da WebApi (`AutonomousStoreApi`) fica com cada aplicativo. Ele
/// carrega o cabeçalho de autenticação, e o token do admin não é o token do
/// suporte — quem manda credencial é quem tem credencial.
/// </remarks>
public static class ServicosDoGerente
{
    /// <summary>O endereço padrão do monitor do SO-Espacial.</summary>
    /// <remarks>
    /// Local por natureza: o monitor roda na mesma máquina das câmeras. Se
    /// não estiver no ar, o chat responde sem a parte espacial em vez de
    /// quebrar — por isso ele nunca é obrigatório.
    /// </remarks>
    public const string MonitorPadrao = "http://localhost:8760/";

    /// <param name="enderecoDoApp">
    /// A própria origem do aplicativo — em Blazor WebAssembly,
    /// <c>builder.HostEnvironment.BaseAddress</c>. É por aqui que o modelo
    /// treinado é lido, e não há servidor envolvido: é arquivo servido junto
    /// com a página.
    /// </param>
    /// <param name="enderecoDoMonitor">
    /// O monitor do SO-Espacial. Sem cabeçalho de autenticação de propósito:
    /// ele só devolve dado anônimo (câmeras, quantas pessoas há no chão) e
    /// não tem por que receber o token de ninguém.
    /// </param>
    public static IServiceCollection AdicionarGerente(
        this IServiceCollection servicos,
        string enderecoDoApp,
        string enderecoDoMonitor = MonitorPadrao)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(enderecoDoApp);

        // --- os dois clientes HTTP que são do gerente, e de mais ninguém ---
        servicos.AddHttpClient(ClassificadorDeIntencao.ClienteEstatico,
            c => c.BaseAddress = new Uri(enderecoDoApp));

        servicos.AddHttpClient("MonitorGerente",
            c => c.BaseAddress = new Uri(enderecoDoMonitor));

        // --- o que ele lê da loja -----------------------------------------
        servicos.AddScoped<IProductApiService, ProductApiService>();
        servicos.AddScoped<ISessionApiService, SessionApiService>();
        servicos.AddScoped<IOcorrenciaApiService, OcorrenciaApiService>();
        servicos.AddScoped<IGerenteEspacialService, GerenteEspacialService>();

        // SINGLETON, e não Scoped: o modelo tem 856 vetores e 14.429
        // parâmetros, e desserializar isso a cada navegação seria desperdício
        // visível. Carregado uma vez, vale para a sessão inteira.
        servicos.AddSingleton<IClassificadorDeIntencao, ClassificadorDeIntencao>();

        servicos.AddScoped<IGerenteService, GerenteService>();

        // SINGLETON, e pelo mesmo motivo do classificador — só que aqui pesa
        // mais. O que a loja aprendeu vive DENTRO deste serviço: registrado
        // como Scoped, cada navegação criaria um aprendiz novo e o Chefe
        // veria o gerente esquecer tudo ao trocar de tela.
        servicos.AddSingleton<IServicoDeAprendizado, ServicoDeAprendizado>();

        return servicos;
    }
}
