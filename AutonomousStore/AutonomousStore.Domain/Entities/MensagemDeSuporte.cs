using AutonomousStore.Domain.Common;
using AutonomousStore.Domain.Enums;

namespace AutonomousStore.Domain.Entities;

/// <summary>
/// Uma fala dentro de um chamado.
/// </summary>
/// <remarks>
/// POR QUE UMA TABELA, E NAO MAIS UM CAMPO NA OCORRENCIA.
///
/// O `EnviarAoSuporte` guardava o texto em `NotaDoAdmin` — UM campo. Mandar
/// de novo sobrescrevia o anterior, e o tecnico nao tinha onde responder. Na
/// pratica era um bilhete de mao unica que a proxima mensagem apagava.
///
/// Conversa e uma LISTA por natureza: tem ordem, tem quem falou, e a
/// terceira fala nao apaga a primeira. Qualquer tentativa de espremer isso
/// num campo so termina em texto concatenado que ninguem consegue filtrar
/// nem contar.
///
/// A MENSAGEM E IMUTAVEL DEPOIS DE ESCRITA. Nao ha `Editar`: num registro que
/// o suporte usa para entender o que aconteceu, poder reescrever o passado
/// vale menos que poder confiar nele.
/// </remarks>
public class MensagemDeSuporte : Entity
{
    /// <summary>O chamado a que esta fala pertence.</summary>
    public Guid OcorrenciaId { get; private set; }

    public DateTime QuandoUtc { get; private set; }

    public AutorDaMensagem Autor { get; private set; }

    /// <summary>O nome de quem escreveu, como aparece na conversa.</summary>
    public string AutorNome { get; private set; } = "";

    /// <summary>
    /// O e-mail de quem escreveu — é por ele que a pessoa reencontra o
    /// próprio chamado depois.
    /// </summary>
    public string? AutorEmail { get; private set; }

    public string Texto { get; private set; } = "";

    /// <summary>Para o EF.</summary>
    protected MensagemDeSuporte() { }

    public MensagemDeSuporte(
        Guid ocorrenciaId,
        AutorDaMensagem autor,
        string autorNome,
        string? autorEmail,
        string texto,
        DateTime quandoUtc)
    {
        if (string.IsNullOrWhiteSpace(texto))
            throw new ArgumentException("Mensagem vazia não é mensagem.", nameof(texto));
        if (string.IsNullOrWhiteSpace(autorNome))
            throw new ArgumentException("A mensagem precisa dizer quem escreveu.", nameof(autorNome));

        OcorrenciaId = ocorrenciaId;
        Autor = autor;
        AutorNome = Curto(autorNome.Trim(), 120);
        AutorEmail = string.IsNullOrWhiteSpace(autorEmail) ? null : Curto(autorEmail.Trim(), 200);

        // 4000 e o teto da coluna. Cortar aqui e melhor que estourar no banco
        // no meio de um pedido de ajuda.
        Texto = Curto(texto.Trim(), 4000);

        QuandoUtc = quandoUtc.Kind == DateTimeKind.Utc ? quandoUtc : quandoUtc.ToUniversalTime();
    }

    private static string Curto(string s, int limite) => s.Length <= limite ? s : s[..limite];
}
