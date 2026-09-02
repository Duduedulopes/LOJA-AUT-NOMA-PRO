using System.Text.Json.Serialization;

namespace AutonomousStore.Gerente.Models;

// ══════════════════════════════════════════════════════════════════════
//  O GEMEO DIGITAL, COMO DADO.
//
//  O `gemeo.py` do Sistema Espacial SO diz, no proprio topo:
//
//      E: o modelo estruturado do ambiente num instante — pessoas, zonas,
//         cameras, ocupacao, com carimbo de tempo.
//      NAO e: uma imagem. O desenho e uma das saidas possiveis, nao o gemeo.
//
//  E o que torna esta tela possivel sem transmitir video: o painel recebe
//  DADO e desenha no navegador, em SVG, no tema certo. A janela do Python
//  nunca precisa atravessar a rede.
// ══════════════════════════════════════════════════════════════════════

/// <summary>A planta: o chao, os moveis e as zonas. Muda quando a loja muda.</summary>
/// <remarks>
/// BUSCADA UMA VEZ, e nao junto do estado. A planta muda talvez uma vez por
/// mes; o estado muda cinco vezes por segundo. Juntas, a mesma gondola
/// atravessaria a rede 432 mil vezes por dia sem mudar um numero.
/// </remarks>
public record PlantaDto(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("nome")] string? Nome,
    [property: JsonPropertyName("unidade")] string? Unidade,
    [property: JsonPropertyName("chao")] ChaoDto? Chao,

    /// <summary>
    /// O quadrilatero REAL do piso — quatro pontos [x, y] em metros.
    /// </summary>
    /// <remarks>
    /// E ELE QUE SE DESENHA, nao a caixa `Chao`. A propria planta avisa:
    /// "a caixa dele tem quase o dobro da area: projecao de retangulo nao
    /// volta a ser retangulo". Desenhar a caixa mostraria um piso maior que
    /// o calibrado, e a pessoa apareceria dentro de chao que nao existe.
    /// </remarks>
    [property: JsonPropertyName("contorno")] List<List<double>>? Contorno,

    [property: JsonPropertyName("moveis")] List<MovelDto>? Moveis,
    [property: JsonPropertyName("zonas")] List<ZonaPlantaDto>? Zonas);

/// <summary>A caixa que contem tudo. Serve para enquadrar, nao para desenhar o piso.</summary>
public record ChaoDto(
    [property: JsonPropertyName("xmin")] double XMin,
    [property: JsonPropertyName("xmax")] double XMax,
    [property: JsonPropertyName("ymin")] double YMin,
    [property: JsonPropertyName("ymax")] double YMax);

public record MovelDto(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("nome")] string? Nome,
    [property: JsonPropertyName("tipo")] string? Tipo,
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y,
    [property: JsonPropertyName("largura")] double Largura,
    [property: JsonPropertyName("profundidade")] double Profundidade,
    [property: JsonPropertyName("altura")] double Altura);

public record ZonaPlantaDto(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("nome")] string? Nome,
    [property: JsonPropertyName("x0")] double X0,
    [property: JsonPropertyName("x1")] double X1,
    [property: JsonPropertyName("y0")] double Y0,
    [property: JsonPropertyName("y1")] double Y1);

/// <summary>O estado do gemeo agora.</summary>
public record EspacialEstadoDto(
    [property: JsonPropertyName("online")] bool Online,
    [property: JsonPropertyName("erro")] string? Erro,
    [property: JsonPropertyName("dados")] EspacialDadosDto? Dados);

public record EspacialDadosDto(
    [property: JsonPropertyName("loja")] LojaDoGemeoDto? Loja,
    [property: JsonPropertyName("t")] double T,
    [property: JsonPropertyName("quadros")] int Quadros,
    [property: JsonPropertyName("pessoas")] List<PessoaDto>? Pessoas,
    [property: JsonPropertyName("zonas")] List<ZonaDto>? Zonas,
    [property: JsonPropertyName("cameras")] Dictionary<string, CameraEstadoDto>? Cameras);

public record LojaDoGemeoDto(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("nome")] string? Nome);

/// <summary>Uma pessoa rastreada, em metros no sistema da homografia.</summary>
public record PessoaDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y,
    [property: JsonPropertyName("velocidade")] double Velocidade,

    /// <summary>Raio de dúvida, em metros. Desenhado como halo.</summary>
    /// <remarks>
    /// MOSTRAR A INCERTEZA E O PONTO. Um ponto sólido diz "ela está AQUI" com
    /// uma confiança que o sistema não tem — e a certeza falsa é o que faz
    /// alguém agir sobre um número que ninguém mediu. O halo é o tamanho da
    /// dúvida, desenhado.
    /// </remarks>
    [property: JsonPropertyName("incerteza")] double Incerteza,

    /// <summary>Para onde o corpo aponta, em radianos.</summary>
    [property: JsonPropertyName("rumo")] double Rumo,

    [property: JsonPropertyName("prevendo")] int Prevendo,
    [property: JsonPropertyName("percorrido")] double Percorrido,

    /// <summary>
    /// QUAIS cameras enxergam esta pessoa agora.
    /// </summary>
    /// <remarks>
    /// Este campo responde na tela uma duvida que o Chefe levantou em 12/08 e
    /// que ficou registrada no `rodar.py`: "nao sei se as 3 cameras estao
    /// trabalhando juntas". Uma afirmacao de que trabalham nao vale nada;
    /// ver os tres nomes acesos ao lado da pessoa vale.
    /// </remarks>
    [property: JsonPropertyName("visto_por")] List<string>? VistoPor);

public record CameraEstadoDto(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("papel")] string? Papel,
    [property: JsonPropertyName("tipo")] string? Tipo,
    [property: JsonPropertyName("estado")] string? Estado,
    [property: JsonPropertyName("resolucao")] string? Resolucao,
    [property: JsonPropertyName("erro")] string? Erro,
    [property: JsonPropertyName("fps")] double Fps,
    [property: JsonPropertyName("latencia_ms")] double LatenciaMs);

/// <summary>Uma camera que esta publicando quadro, e ha quanto tempo.</summary>
public record CameraAoVivoDto(
    [property: JsonPropertyName("papel")] string Papel,
    [property: JsonPropertyName("idade_s")] double IdadeS);

public record CamerasAoVivoDto(
    [property: JsonPropertyName("cameras")] List<CameraAoVivoDto>? Cameras);
