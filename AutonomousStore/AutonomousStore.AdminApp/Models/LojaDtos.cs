namespace AutonomousStore.AdminApp.Models;

// Espelham a leitura de api/lojas. Quem cadastra/altera é o Criador ou o suporte — o Admin só consulta.
public record LojaDto(Guid Id, string Nome, string? Segmento, bool Ativa, DateTime CriadaEm);

public record LojasDto(List<LojaDto> Lojas, int Ativas, int Limite);
