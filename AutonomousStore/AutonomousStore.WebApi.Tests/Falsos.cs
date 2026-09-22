using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AutonomousStore.WebApi.Tests;

/// <summary>Um ambiente com o nome que o teste quiser: "Development", "Production"…</summary>
public sealed class AmbienteFalso : IHostEnvironment
{
    public AmbienteFalso(string nome) => EnvironmentName = nome;

    public string EnvironmentName { get; set; }
    public string ApplicationName { get; set; } = "Testes";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

/// <summary>Um log que guarda o que recebeu, para o teste conferir o que o serviço disse.</summary>
public sealed class LogFalso<T> : ILogger<T>
{
    public List<(LogLevel Nivel, string Texto)> Linhas { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel nivel, EventId id, TState estado, Exception? erro, Func<TState, Exception?, string> formatador)
        => Linhas.Add((nivel, formatador(estado, erro)));

    public bool Disse(LogLevel nivel, string trecho)
        => Linhas.Any(l => l.Nivel == nivel && l.Texto.Contains(trecho, StringComparison.Ordinal));
}

public static class Configuracao
{
    /// <summary>Uma configuração só com o que o teste disser, na forma "Email:Habilitado" = "true".</summary>
    public static IConfiguration De(params (string Chave, string? Valor)[] itens)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(itens.Select(i => new KeyValuePair<string, string?>(i.Chave, i.Valor)))
            .Build();

    /// <summary>
    /// Uma porta de localhost GARANTIDAMENTE fechada: o teste a reserva e a libera. É para onde vai qualquer e-mail
    /// que o teste PROVOQUE — a conexão é recusada na hora, e nada sai da máquina.
    /// </summary>
    public static int PortaLocalFechada()
    {
        var escuta = new TcpListener(IPAddress.Loopback, 0);
        escuta.Start();
        var porta = ((IPEndPoint)escuta.LocalEndpoint).Port;
        escuta.Stop();
        return porta;
    }
}
