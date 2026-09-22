using AutonomousStore.Domain.Common;

namespace AutonomousStore.Domain.Entities;

/// <summary>
/// Registro de uma ação de quem opera a plataforma: quem fez, quando, em qual
/// empresa e o quê. É o que alimenta o "tudo que o suporte fez e faz" no painel
/// do Criador — e o que responde, se alguém perguntar, quem viu um dado
/// pessoal e por quê.
///
/// Só se acrescenta: não há método que altere um registro. Um log que pode ser
/// reescrito não prova nada.
/// </summary>
public class Auditoria : Entity
{
    public DateTime QuandoUtc { get; private set; }

    /// <summary>Quem agiu. Nulo quando a ação veio de fora de uma sessão (ex.: a partida do sistema).</summary>
    public Guid? AtorId { get; private set; }

    public string AtorPapel { get; private set; } = "";

    public string AtorNome { get; private set; } = "";

    /// <summary>Em nome de qual empresa a ação foi feita. Nulo quando é da plataforma inteira.</summary>
    public Guid? TenantAlvoId { get; private set; }

    /// <summary>O verbo: "empresa.criada", "empresa.suspensa", "login.criador"…</summary>
    public string Acao { get; private set; } = "";

    /// <summary>Sobre o que: "Tenant", "Customer", "Ocorrencia"…</summary>
    public string Recurso { get; private set; } = "";

    public string? RecursoId { get; private set; }

    /// <summary>O motivo, quando a ação exige um — ex.: revelar um dado mascarado.</summary>
    public string? Motivo { get; private set; }

    public string? Ip { get; private set; }

    protected Auditoria() { }

    public Auditoria(
        DateTime quandoUtc,
        Guid? atorId,
        string atorPapel,
        string atorNome,
        string acao,
        string recurso,
        Guid? tenantAlvoId = null,
        string? recursoId = null,
        string? motivo = null,
        string? ip = null)
    {
        if (string.IsNullOrWhiteSpace(acao))
            throw new ArgumentException("A auditoria precisa dizer o que foi feito.", nameof(acao));

        if (string.IsNullOrWhiteSpace(recurso))
            throw new ArgumentException("A auditoria precisa dizer sobre o quê.", nameof(recurso));

        if (string.IsNullOrWhiteSpace(atorPapel))
            throw new ArgumentException("A auditoria precisa dizer o papel de quem agiu.", nameof(atorPapel));

        QuandoUtc = quandoUtc.Kind == DateTimeKind.Utc ? quandoUtc : quandoUtc.ToUniversalTime();
        AtorId = atorId;
        AtorPapel = Curto(atorPapel, 30);
        AtorNome = Curto(string.IsNullOrWhiteSpace(atorNome) ? "—" : atorNome.Trim(), 200);
        TenantAlvoId = tenantAlvoId;
        Acao = Curto(acao.Trim(), 80);
        Recurso = Curto(recurso.Trim(), 80);
        RecursoId = string.IsNullOrWhiteSpace(recursoId) ? null : Curto(recursoId.Trim(), 100);
        Motivo = string.IsNullOrWhiteSpace(motivo) ? null : Curto(motivo.Trim(), 500);
        Ip = string.IsNullOrWhiteSpace(ip) ? null : Curto(ip.Trim(), 64);
    }

    // Cortar aqui e melhor que estourar a coluna no meio de uma acao que ja aconteceu.
    private static string Curto(string s, int limite) => s.Length <= limite ? s : s[..limite];
}
