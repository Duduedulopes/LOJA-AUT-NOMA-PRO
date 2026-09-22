using AutonomousStore.Domain.Common;

namespace AutonomousStore.Domain.Entities;

/// <summary>
/// Uma loja física de uma empresa (a loja de comida, a de roupa). O limite de
/// lojas da assinatura conta estas linhas.
/// </summary>
public class Store : TenantEntity
{
    /// <summary>
    /// O tamanho das colunas Lojas.Nome e Lojas.Segmento. O que passar disso o banco recusa com erro 500; a regra mora aqui
    /// para chegar ao Admin como uma frase.
    /// </summary>
    public const int TamanhoMaximoDoNome = 200;
    public const int TamanhoMaximoDoSegmento = 100;

    public string Nome { get; private set; } = "";

    /// <summary>O tipo de negócio ("Comida", "Roupa"). Texto livre, só descreve.</summary>
    public string? Segmento { get; private set; }

    public bool IsActive { get; private set; }

    protected Store() { }

    public Store(string nome, string? segmento = null)
    {
        (Nome, Segmento) = Normalizar(nome, segmento);
        IsActive = true;
    }

    // Valida tudo ANTES de atribuir: um pedido recusado nao deixa a loja pela metade.
    public void AtualizarDados(string nome, string? segmento)
        => (Nome, Segmento) = Normalizar(nome, segmento);

    // A excecao sai SEM o nome do parametro de proposito: com ele, Message ganha " (Parameter 'nome')", que o controller
    // devolve ao Admin junto com a frase.
    private static (string Nome, string? Segmento) Normalizar(string? nome, string? segmento)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("O nome da loja não pode ser vazio.");

        var nomeLimpo = nome.Trim();
        var segmentoLimpo = string.IsNullOrWhiteSpace(segmento) ? null : segmento.Trim();

        if (nomeLimpo.Length > TamanhoMaximoDoNome)
            throw new ArgumentException($"O nome da loja pode ter no máximo {TamanhoMaximoDoNome} caracteres.");

        if (segmentoLimpo is { Length: > TamanhoMaximoDoSegmento })
            throw new ArgumentException($"O segmento pode ter no máximo {TamanhoMaximoDoSegmento} caracteres.");

        return (nomeLimpo, segmentoLimpo);
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
