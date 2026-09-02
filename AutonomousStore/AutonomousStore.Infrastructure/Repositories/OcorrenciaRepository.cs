using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Enums;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutonomousStore.Infrastructure.Repositories;

public class OcorrenciaRepository : IOcorrenciaRepository
{
    private readonly AutonomousDbContext _context;

    public OcorrenciaRepository(AutonomousDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Ocorrencia ocorrencia, CancellationToken cancellationToken = default)
    {
        await _context.Ocorrencias.AddAsync(ocorrencia, cancellationToken);
    }

    public async Task<Ocorrencia> RegistrarOuSomarAsync(
        Ocorrencia ocorrencia, CancellationToken cancellationToken = default)
    {
        if (ocorrencia.Chave is not { Length: > 0 } chave)
        {
            await _context.Ocorrencias.AddAsync(ocorrencia, cancellationToken);
            return ocorrencia;
        }

        // O `Local` VEM ANTES DO BANCO, E ESSA ORDEM IMPORTA.
        //
        // Uma varredura pode produzir duas ocorrencias iguais ANTES de
        // qualquer SaveChanges. O banco ainda nao as tem, entao a consulta
        // devolveria nada duas vezes, as duas entrariam, e o indice unico
        // derrubaria o lote inteiro — um alerta matando a operacao que ele
        // existe para vigiar. O `Local` e o unico lugar onde a primeira ja
        // esta visivel.
        var jaEsta = _context.Ocorrencias.Local.FirstOrDefault(o => o.Chave == chave)
            // SEM AsNoTracking: para somar, a linha precisa estar sendo
            // observada, senao o SaveChanges nao veria a mudanca.
            ?? await _context.Ocorrencias
                .FirstOrDefaultAsync(o => o.Chave == chave, cancellationToken);

        if (jaEsta is null)
        {
            await _context.Ocorrencias.AddAsync(ocorrencia, cancellationToken);
            return ocorrencia;
        }

        jaEsta.RegistrarRepeticao(ocorrencia.QuandoUtc);
        return jaEsta;
    }

    public async Task AddRangeAsync(IEnumerable<Ocorrencia> ocorrencias, CancellationToken cancellationToken = default)
    {
        await _context.Ocorrencias.AddRangeAsync(ocorrencias, cancellationToken);
    }

    public async Task<Ocorrencia?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // SEM `AsNoTracking` DE PROPOSITO, ao contrario dos outros
        // repositorios: quem busca uma ocorrencia por id quase sempre vai
        // marcar como vista ou resolver em seguida. Devolver desanexada
        // faria o `SaveChanges` seguinte nao gravar nada, calado.
        return await _context.Ocorrencias
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Ocorrencia>> BuscarAsync(
        FiltroDeOcorrencia filtro, CancellationToken cancellationToken = default)
    {
        var q = _context.Ocorrencias.AsNoTracking().AsQueryable();

        if (filtro.Desde is { } desde) q = q.Where(o => o.QuandoUtc >= desde);
        if (filtro.Ate is { } ate) q = q.Where(o => o.QuandoUtc < ate);
        if (filtro.Tipo is { } tipo) q = q.Where(o => o.Tipo == tipo);
        if (filtro.Estado is { } estado) q = q.Where(o => o.Estado == estado);
        if (filtro.CorrelationId is { } cid) q = q.Where(o => o.CorrelationId == cid);

        // SeveridadeMinima e um PISO, nao uma igualdade. Quem pede "alta"
        // quer alta e critica: filtrar por igual esconderia justamente a
        // pior.
        if (filtro.SeveridadeMinima is { } sev) q = q.Where(o => o.Severidade >= sev);

        return await q
            .OrderByDescending(o => o.QuandoUtc)
            .Take(Math.Clamp(filtro.Limite, 1, 1000))
            .ToListAsync(cancellationToken);
    }

    public async Task<(int Total, int Criticas, DateTime? MaisRecente)> NaoVistasAsync(
        CancellationToken cancellationToken = default)
    {
        // UMA IDA AO BANCO, NAO TRES. Isto roda a cada 20 segundos em toda
        // tela de admin aberta; tres consultas viram tres por tela por
        // vinte segundos.
        var r = await _context.Ocorrencias
            .AsNoTracking()
            .Where(o => o.Estado == EstadoDaOcorrencia.Nova)
            .GroupBy(o => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Criticas = g.Count(o => o.Severidade == Severidade.Critica),
                MaisRecente = (DateTime?)g.Max(o => o.QuandoUtc),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return r is null ? (0, 0, null) : (r.Total, r.Criticas, r.MaisRecente);
    }

    public async Task<IReadOnlyList<(TipoDeOcorrencia Tipo, Severidade Severidade, int Quantidade)>>
        ResumoAsync(DateTime desde, DateTime ate, CancellationToken cancellationToken = default)
    {
        var linhas = await _context.Ocorrencias
            .AsNoTracking()
            .Where(o => o.QuandoUtc >= desde && o.QuandoUtc < ate)
            .GroupBy(o => new { o.Tipo, o.Severidade })
            .Select(g => new { g.Key.Tipo, g.Key.Severidade, Quantidade = g.Count() })
            .ToListAsync(cancellationToken);

        return linhas
            .Select(l => (l.Tipo, l.Severidade, l.Quantidade))
            .OrderByDescending(l => l.Quantidade)
            .ToList();
    }

    public async Task<bool> JaExisteAsync(
        TipoDeOcorrencia tipo, string chave, CancellationToken cancellationToken = default)
    {
        return await _context.Ocorrencias
            .AsNoTracking()
            .AnyAsync(o => o.Tipo == tipo && o.Chave == chave, cancellationToken);
    }

    public async Task<Ocorrencia?> ObterComConversaAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        // SEM AsNoTracking: quem abre a conversa quase sempre vai escrever
        // nela em seguida, e uma entidade não observada não salva.
        return await _context.Ocorrencias
            .Include(o => o.Mensagens)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Ocorrencia>> ChamadosDeAsync(
        string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return Array.Empty<Ocorrencia>();
        var alvo = email.Trim();

        // Aqui SIM AsNoTracking: é lista para mostrar, ninguém escreve nela.
        return await _context.Ocorrencias
            .AsNoTracking()
            .Where(o => o.AbertoPor != null && o.AbertoPor.ToLower() == alvo.ToLower())
            .OrderByDescending(o => o.QuandoUtc)
            .Take(200)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
