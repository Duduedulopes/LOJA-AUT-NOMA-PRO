using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AutonomousStore.Gerente.Services.Agente;

/// <summary>Um pedaco do tempo: comeco, fim, e o nome que a pessoa deu a ele.</summary>
/// <remarks>
/// FIM E EXCLUSIVO. "Hoje" vai de 00:00 de hoje ate 00:00 de amanha. A
/// alternativa — terminar em 23:59:59 — perde a venda fechada em
/// 23:59:59.7, e ninguem descobre isso olhando o relatorio.
/// </remarks>
public readonly record struct Periodo(DateTime Inicio, DateTime Fim, string Nome)
{
    public bool Contem(DateTime quando) => quando >= Inicio && quando < Fim;

    /// <summary>O periodo "desde sempre", que nao tem datas para mostrar.</summary>
    public bool Tudo => Inicio == DateTime.MinValue;

    public int Dias => Tudo ? 0 : (int)(Fim - Inicio).TotalDays;

    /// <summary>
    /// As datas por extenso, para o gerente SEMPRE dizer sobre o que ele
    /// respondeu.
    /// </summary>
    /// <remarks>
    /// ISTO NAO E ENFEITE. O erro que existia antes deste arquivo era
    /// silencioso: pedia-se o mes e recebia-se o dia, com a mesma cara de
    /// resposta certa. Dizer as datas de volta e o que torna um erro de
    /// leitura visivel na hora.
    ///
    /// `CultureInfo.InvariantCulture` em toda formatacao de data: numa
    /// cultura onde o separador e ".", a barra do formato "dd/MM/yyyy"
    /// vira ponto sozinha. Mesma familia do R$ 300.
    /// </remarks>
    public string Datas
    {
        get
        {
            if (Tudo) return "desde o começo";
            var i = Inicio.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            if (Dias <= 1) return i;
            var f = Fim.AddDays(-1).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            return $"{i} a {f}";
        }
    }

    /// <summary>
    /// A janela imediatamente anterior, do mesmo tamanho — para comparar.
    /// </summary>
    public Periodo Anterior => Tudo
        ? this
        : new Periodo(Inicio.AddDays(-Dias), Inicio, "no periodo anterior");
}

/// <summary>
/// Tira da frase QUANDO. "esta semana", "mes passado", "agosto",
/// "ultimos 7 dias", "15/08" — e mais de um de uma vez.
/// </summary>
/// <remarks>
/// POR QUE ISTO NAO E A REDE. A rede ja disse "ele quer faturamento". Aqui
/// so falta o recorte de tempo, que e regra fechada e nao interpretacao:
/// nao ha 39 jeitos de significar "ontem".
///
/// O QUE ISTO CONSERTA. `FaturamentoAsync` conhecia dois periodos: hoje e
/// total. Procurava as palavras `total/sempre/geral/tudo/historico`; nao
/// achando, respondia HOJE. Entao "quanto faturamos este mes?" devolvia o
/// dia — com numero, ponto final e cara de resposta certa. Um erro que nao
/// da erro.
///
/// POR QUE UMA LISTA E NAO UM PERIODO. "Me fale o faturamento de hoje, da
/// semana e do mes" e UMA pergunta com TRES respostas. Devolver so a
/// primeira seria o mesmo erro silencioso de antes, com outra roupa.
///
/// A ARMADILHA DO `Contains`: "anteontem".Contains("ontem") e verdadeiro. O
/// mesmo bug ja custou caro do lado da rede, quando o rotulador de tom
/// casava marcador por pedaco de palavra e 2617 frases de 4291 viraram
/// "curiosidade" porque continham a letra "e". Aqui a busca e por PALAVRA
/// INTEIRA, sobre a lista de tokens — nunca por pedaco de texto.
///
/// A ARMADILHA DA VIZINHANCA: em "a semana passada e este mes", a palavra
/// "passada" existe na frase — mas e da semana, nao do mes. Por isso
/// "passado" so conta colado na unidade, como se fala em portugues.
/// </remarks>
public static class LeitorDePeriodo
{
    /// <summary>
    /// Segunda-feira. A semana comercial comeca na segunda, ainda que o
    /// calendario de parede comece no domingo. Um numero so para mudar — e
    /// como a resposta sempre mostra as datas, a escolha nunca fica
    /// escondida do Chefe.
    /// </summary>
    public const DayOfWeek PrimeiroDiaDaSemana = DayOfWeek.Monday;

    /// <summary>Quantos periodos no maximo numa resposta so.</summary>
    private const int Teto = 4;

    private static readonly string[] Meses =
    {
        "janeiro", "fevereiro", "marco", "abril", "maio", "junho",
        "julho", "agosto", "setembro", "outubro", "novembro", "dezembro",
    };

    /// <summary>
    /// Abreviacoes de mes — SEM "mar" e SEM "dez".
    /// </summary>
    /// <remarks>
    /// "dez" e o numero dez: "vendemos dez aguas" viraria dezembro. "mar"
    /// e o mar. Esses dois so valem por extenso; os outros dez nao colidem
    /// com nada em portugues de loja.
    /// </remarks>
    private static readonly string[] Curtos =
    {
        "jan", "fev", "", "abr", "mai", "jun",
        "jul", "ago", "set", "out", "nov", "",
    };

    /// <summary>
    /// Data escrita: 15/08, 15/08/2026, 15-08.
    /// </summary>
    /// <remarks>
    /// So barra e hifen. O ponto ficou de fora de proposito: `\d{1,2}.\d{1,2}`
    /// casa com "3.00", e ai o preco de tres reais viraria dia 3 do mes 0.
    /// </remarks>
    private static readonly Regex DataEscrita = new(
        @"(?<![\d/-])(\d{1,2})[/\-](\d{1,2})(?:[/\-](\d{2,4}))?(?![\d/-])",
        RegexOptions.Compiled);

    private static readonly string[] Este = { "este", "esse", "esta", "essa", "atual", "corrente", "deste", "desta", "nesta", "neste" };
    private static readonly string[] Passado = { "passado", "passada", "anterior", "ultimo", "ultima", "ultimos", "ultimas", "retrasado", "retrasada" };
    private static readonly string[] Tudo = { "total", "sempre", "geral", "historico", "acumulado" };

    /// <summary>Sem acento, minusculo, palavras separadas.</summary>
    private static string[] Palavras(string s)
    {
        var d = (s ?? "").ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }
        return sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private static bool Tem(string[] p, params string[] quais)
        => quais.Any(q => Array.IndexOf(p, q) >= 0);

    /// <summary>Uma palavra de `marcas` colada em `unidade` — antes ou depois.</summary>
    private static bool Colado(string[] p, string unidade, string[] marcas)
    {
        for (var i = 0; i < p.Length; i++)
        {
            if (p[i] != unidade) continue;
            if (i > 0 && marcas.Contains(p[i - 1])) return true;
            if (i + 1 < p.Length && marcas.Contains(p[i + 1])) return true;
        }
        return false;
    }

    /// <summary>
    /// Le UM periodo — o primeiro que a frase disser. `null` quando a frase
    /// nao diz quando, e ai quem chama decide o padrao dizendo qual escolheu.
    /// </summary>
    public static Periodo? Ler(string frase, DateTime agora)
    {
        var todos = Todos(frase, agora);
        return todos.Count == 0 ? null : todos[0];
    }

    /// <summary>
    /// Le TODOS os periodos da frase, do mais curto para o mais longo.
    /// Lista vazia quando a frase nao diz quando.
    /// </summary>
    /// <param name="agora">
    /// O relogio entra por parametro, nunca `DateTime.Now` la dentro. Sem
    /// isso o teste de "ontem" passa hoje e falha na virada do ano, e um
    /// teste que so vale em certos dias nao e teste.
    /// </param>
    public static List<Periodo> Todos(string frase, DateTime agora)
    {
        var hoje = agora.Date;
        var p = Palavras(frase);
        var achados = new List<Periodo>();
        if (p.Length == 0) return achados;

        // "desde sempre" engole todo o resto: nao ha recorte menor dentro
        // de "tudo".
        if (Tem(p, Tudo) || Seq(p, "desde", "o", "comeco") || Seq(p, "todo", "o", "periodo"))
        {
            achados.Add(new Periodo(DateTime.MinValue, DateTime.MaxValue, "no total"));
            return achados;
        }

        // ── dias soltos ───────────────────────────────────────────────
        // "anteontem" antes de "ontem". Por token, "anteontem" nao contem
        // "ontem" — que e exatamente o ponto da busca por palavra inteira.
        if (Tem(p, "anteontem")) achados.Add(Dia(hoje.AddDays(-2), "anteontem"));
        if (Tem(p, "ontem")) achados.Add(Dia(hoje.AddDays(-1), "ontem"));
        if (Tem(p, "hoje")) achados.Add(Dia(hoje, "hoje"));

        // ── data escrita e "dia 15" ───────────────────────────────────
        if (DataDaFrase(frase, hoje) is { } escrita) achados.Add(escrita);

        // "dia 3 de julho" ja gastou o "julho". Sem marcar isso, a mesma
        // frase devolvia o dia 3 E o mes de julho inteiro — duas respostas
        // para um pedido so.
        if (DiaDoMes(p, hoje, out var mesGasto) is { } diaN) achados.Add(diaN);

        // ── janelas: "ultimos 7 dias" ─────────────────────────────────
        achados.AddRange(Recentes(p, hoje));

        // ── semana ────────────────────────────────────────────────────
        if (Colado(p, "semana", Passado))
        {
            var ini = InicioDaSemana(hoje).AddDays(-7);
            achados.Add(new Periodo(ini, ini.AddDays(7), "na semana passada"));
        }
        else if (Tem(p, "semana", "semanal") || Colado(p, "semana", Este))
        {
            var ini = InicioDaSemana(hoje);
            achados.Add(new Periodo(ini, ini.AddDays(7), "nesta semana"));
        }

        // ── mes ───────────────────────────────────────────────────────
        var mesComNome = MesComNome(p, hoje, mesGasto);
        if (mesComNome is { } nomeado)
        {
            achados.Add(nomeado);
        }
        else if (Colado(p, "mes", Passado))
        {
            var ini = new DateTime(hoje.Year, hoje.Month, 1).AddMonths(-1);
            achados.Add(new Periodo(ini, ini.AddMonths(1), "no mês passado"));
        }
        else if (Tem(p, "mes", "mensal") || Colado(p, "mes", Este))
        {
            var ini = new DateTime(hoje.Year, hoje.Month, 1);
            achados.Add(new Periodo(ini, ini.AddMonths(1), "neste mês"));
        }

        // ── ano ───────────────────────────────────────────────────────
        var anoSolto = AnoSolto(p, hoje);
        if (anoSolto is { } solto)
        {
            achados.Add(solto);
        }
        else if (Colado(p, "ano", Passado))
        {
            var ini = new DateTime(hoje.Year - 1, 1, 1);
            achados.Add(new Periodo(ini, ini.AddYears(1), "no ano passado"));
        }
        else if (Tem(p, "ano", "anual") || Colado(p, "ano", Este))
        {
            var ini = new DateTime(hoje.Year, 1, 1);
            achados.Add(new Periodo(ini, ini.AddYears(1), "neste ano"));
        }

        // Duas frases podem apontar para a mesma janela ("o mes de agosto"
        // em agosto). Uma resposta so por janela.
        var vistos = new HashSet<(DateTime, DateTime)>();
        return achados
            .Where(x => vistos.Add((x.Inicio, x.Fim)))
            .OrderBy(x => x.Dias)
            .Take(Teto)
            .ToList();
    }

    private static Periodo Dia(DateTime d, string nome) => new(d, d.AddDays(1), nome);

    private static DateTime InicioDaSemana(DateTime d)
    {
        var recuo = ((int)d.DayOfWeek - (int)PrimeiroDiaDaSemana + 7) % 7;
        return d.AddDays(-recuo);
    }

    private static bool Seq(string[] p, params string[] seq)
    {
        for (var i = 0; i + seq.Length <= p.Length; i++)
        {
            var bate = true;
            for (var j = 0; j < seq.Length; j++)
                if (p[i + j] != seq[j]) { bate = false; break; }
            if (bate) return true;
        }
        return false;
    }

    /// <summary>"ultimos 7 dias", "ultimas 2 semanas", "ultimos 3 meses".</summary>
    private static IEnumerable<Periodo> Recentes(string[] p, DateTime hoje)
    {
        for (var i = 0; i < p.Length - 1; i++)
        {
            if (!int.TryParse(p[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                || n <= 0 || n > 999) continue;

            // O numero so vira janela depois de "ultimos"/"ultimas" — senao
            // "vendemos 7 aguas hoje" viraria uma semana de faturamento.
            if (i == 0 || !Passado.Contains(p[i - 1])) continue;

            var unidade = p[i + 1];
            if (unidade is "dia" or "dias")
                yield return new Periodo(hoje.AddDays(-(n - 1)), hoje.AddDays(1),
                                         $"nos últimos {Num(n)} dias");
            else if (unidade is "semana" or "semanas")
                yield return new Periodo(hoje.AddDays(-(7 * n - 1)), hoje.AddDays(1),
                                         $"nas últimas {Num(n)} semanas");
            else if (unidade is "mes" or "meses")
                yield return new Periodo(hoje.AddMonths(-n).AddDays(1), hoje.AddDays(1),
                                         $"nos últimos {Num(n)} meses");
        }
    }

    private static Periodo? MesComNome(string[] p, DateTime hoje, int jaGasto)
    {
        for (var i = 0; i < p.Length; i++)
        {
            if (i == jaGasto) continue;   // "dia 3 de julho" ja usou este mes
            var m = Array.IndexOf(Meses, p[i]);
            if (m < 0)
            {
                var c = Array.IndexOf(Curtos, p[i]);
                if (c < 0 || Curtos[c].Length == 0) continue;   // "" nunca casa
                m = c;
            }

            // Ano logo depois? "agosto de 2025" — pula o "de".
            var ano = hoje.Year;
            var achouAno = false;
            for (var j = i + 1; j <= i + 2 && j < p.Length; j++)
                if (Ano(p[j]) is { } a) { ano = a; achouAno = true; break; }

            var ini = new DateTime(ano, m + 1, 1);

            // FATURAMENTO E PASSADO. Sem ano dito, "dezembro" em agosto de
            // 2026 e o dezembro que JA ACONTECEU — 2025. Perguntar de um mes
            // que ainda nao chegou nao e o que alguem quer dizer.
            if (!achouAno && ini > hoje) ini = ini.AddYears(-1);

            return new Periodo(ini, ini.AddMonths(1),
                               $"em {Meses[m]} de {ini.Year.ToString(CultureInfo.InvariantCulture)}");
        }
        return null;
    }

    /// <summary>Um ano dito sozinho: "faturamento de 2025".</summary>
    /// <remarks>
    /// DUAS COISAS QUE PARECEM ANO E NAO SAO.
    ///
    /// 1. O ano de um mes: em "julho de 2025", o 2025 ja e do julho. Contar
    ///    de novo devolvia julho E 2025 inteiro. Mas so pular tudo que vem
    ///    depois de "de" era demais — matava "faturamento de 2025", que e
    ///    exatamente a pergunta. So o "de" precedido de mes conta.
    ///
    /// 2. O ano de uma data escrita: "03/07/2025" vira os tokens 03, 07,
    ///    2025, e ai o 2025 aparecia como ano solto — a resposta trazia o
    ///    dia 3 E o ano de 2025. Numero antes de ano = data escrita.
    /// </remarks>
    private static Periodo? AnoSolto(string[] p, DateTime hoje)
    {
        for (var i = 0; i < p.Length; i++)
        {
            if (Ano(p[i]) is not { } a || a > hoje.Year) continue;

            if (i > 0)
            {
                var antes = p[i - 1];
                if (Array.IndexOf(Meses, antes) >= 0) continue;
                if (antes == "de" && i > 1 && Array.IndexOf(Meses, p[i - 2]) >= 0) continue;
                if (antes.All(char.IsDigit)) continue;
            }

            return new Periodo(new DateTime(a, 1, 1), new DateTime(a + 1, 1, 1),
                               $"em {a.ToString(CultureInfo.InvariantCulture)}");
        }
        return null;
    }

    private static int? Ano(string w)
        => w.Length == 4
           && int.TryParse(w, NumberStyles.Integer, CultureInfo.InvariantCulture, out var a)
           && a is >= 2000 and <= 2200
            ? a : null;

    /// <param name="mesGasto">
    /// Indice do nome de mes que este dia consumiu, ou -1. Quem procura mes
    /// depois precisa saber, senao "dia 3 de julho" vira duas respostas.
    /// </param>
    private static Periodo? DiaDoMes(string[] p, DateTime hoje, out int mesGasto)
    {
        mesGasto = -1;
        for (var i = 0; i < p.Length - 1; i++)
        {
            if (p[i] != "dia") continue;
            if (!int.TryParse(p[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var d)
                || d is < 1 or > 31) continue;

            // "dia 15 de agosto" — o mes vem logo em seguida.
            var mes = hoje.Month;
            var ano = hoje.Year;
            for (var j = i + 2; j <= i + 3 && j < p.Length; j++)
            {
                var m = Array.IndexOf(Meses, p[j]);
                if (m < 0)
                {
                    var c = Array.IndexOf(Curtos, p[j]);
                    if (c >= 0 && Curtos[c].Length > 0) m = c;
                }
                if (m >= 0) { mes = m + 1; mesGasto = j; break; }
            }

            if (d > DateTime.DaysInMonth(ano, mes)) return null;
            var data = new DateTime(ano, mes, d);
            if (data > hoje) data = data.AddMonths(-1);   // dia que ainda nao chegou = o do mes passado
            return Dia(data, $"no dia {data.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}");
        }
        return null;
    }

    private static Periodo? DataDaFrase(string frase, DateTime hoje)
    {
        var m = DataEscrita.Match(frase ?? "");
        if (!m.Success) return null;

        var d = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var mes = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        if (mes is < 1 or > 12) return null;

        var ano = hoje.Year;
        if (m.Groups[3].Success)
        {
            ano = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            if (ano < 100) ano += 2000;
        }
        if (ano is < 2000 or > 2200) return null;
        if (d < 1 || d > DateTime.DaysInMonth(ano, mes)) return null;

        var data = new DateTime(ano, mes, d);
        if (!m.Groups[3].Success && data > hoje) data = data.AddYears(-1);
        return Dia(data, $"no dia {data.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}");
    }

    private static string Num(int n) => n.ToString(CultureInfo.InvariantCulture);
}
