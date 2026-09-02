namespace AutonomousStore.Gerente.Models;

/// <summary>
/// O resumo espacial que vem do monitor do gerente (servidor local em Python).
/// </summary>
/// <remarks>
/// DE ONDE ISTO VEM, E POR QUE NAO VEM DA WEBAPI.
///
/// A WebApi sabe quem tem sessao aberta. Ela nao sabe quantos CORPOS estao no
/// chao da loja — isso e o SO Espacial, que le as cameras e escreve o
/// estado num arquivo.
///
/// Os dois numeros sao diferentes de proposito, e a diferenca entre eles e
/// informacao: pessoa rastreada sem sessao aberta e alguem que entrou sem
/// fazer o check-in.
/// </remarks>
public record EspacialResumoDto(
    bool Online,
    string? Erro,
    string? Loja,
    int Pessoas,
    List<RastroDto>? Rastros,
    CamerasDto? Cameras,
    List<ZonaDto>? Zonas);

public record RastroDto(int Id, double Velocidade, string? Acao, string? Postura, double Incerteza);

public record CamerasDto(int Total, int Online, List<CameraDetalheDto>? Detalhe);

public record CameraDetalheDto(string Papel, string Estado, double Fps);

public record ZonaDto(string Id, string? Nome, int Ocupacao, int Visitas);

/// <summary>Uma fala do chat: quem disse, o que disse, e quando.</summary>
public record FalaDoChat(bool DoGerente, string Texto, DateTime Quando)
{
    public static FalaDoChat Gerente(string texto) => new(true, texto, DateTime.Now);
    public static FalaDoChat Voce(string texto) => new(false, texto, DateTime.Now);
}
