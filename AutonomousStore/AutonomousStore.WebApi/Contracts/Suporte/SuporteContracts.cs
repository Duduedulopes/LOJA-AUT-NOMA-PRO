using AutonomousStore.WebApi.Contracts.Customers;

namespace AutonomousStore.WebApi.Contracts.Suporte;

/// <summary>Como o técnico enxerga uma empresa: código + nome fantasia, e mais nada.</summary>
public record EmpresaParaSuporteResponse(Guid Id, int Codigo, string Rotulo, bool Ativa);

/// <summary>
/// Um comprador como o técnico o vê: o nome aparece, o resto vem mascarado
/// (<see cref="AutonomousStore.Domain.Common.Mascara"/>). O dado completo só sai por
/// <c>POST .../revelar</c>, com motivo.
/// </summary>
public record CompradorParaSuporteResponse(
    Guid Id,
    Guid? EmpresaId,
    string? Empresa,
    string Nome,
    string? Email,
    string? Cpf,
    string? Telefone,
    bool Ativo,
    DateTime CriadoEm,
    bool TemSenha,
    IReadOnlyList<PaymentMethodResponse> Pagamentos);

/// <param name="Motivo">
/// Por que o técnico precisa ver o dado completo. Fica na auditoria, ao lado de quem
/// pediu e de qual empresa era — é o que responde, meses depois, "quem viu e por quê".
/// </param>
public record RevelarRequest(string Motivo);

public record CompradorReveladoResponse(Guid Id, string Nome, string Email, string Cpf, string Telefone);

/// <summary>
/// O técnico cadastra o comprador, mas NÃO escolhe a senha dele: o servidor gera uma
/// aleatória que ninguém conhece, e a pessoa define a sua por "esqueci minha senha".
/// </summary>
public record CriarCompradorRequest(Guid EmpresaId, string Nome, string Email, string Telefone, string Cpf);
