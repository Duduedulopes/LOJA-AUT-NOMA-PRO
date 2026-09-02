namespace AutonomousStore.Gerente.Services.Aprendizado;

/// <summary>
/// O gradiente — a conta que faz a rede aprender, agora em C#.
/// </summary>
/// <remarks>
/// TRADUÇÃO, NÃO INVENÇÃO.
///
/// Isto é o `rede/retropropagacao.py` do projeto Rede-Neural, linha por
/// linha: `gradiente` e `gradiente_com_entrada`. A mesma conta que treinou o
/// modelo de 5.586 frases que a loja carrega hoje.
///
/// A ARQUITETURA, PARA QUEM CHEGAR AQUI SEM CONTEXTO
///
///     índices das peças  →  média dos embutimentos (24)
///                        →  tronco (32) com sigmoide
///                        →  cabeça de intenção (42) com softmax
///
/// A média é o que transforma uma frase de tamanho qualquer num vetor de
/// tamanho fixo. E é por causa dela que a tabela de embutimento também
/// aprende: cada peça da frase recebe de volta uma fatia do erro.
///
/// O QUE ESTA CLASSE NÃO FAZ
///
/// Não toca na cabeça de TOM. O tronco é compartilhado entre as duas
/// cabeças, e o `passo()` do Python soma os dois gradientes nele de
/// propósito — "o que faz as duas tarefas aprenderem juntas em vez de uma
/// desfazer a outra". Só que o clique do Chefe rotula INTENÇÃO, e ninguém
/// diz qual era o tom. Usar o palpite do próprio modelo como alvo seria ele
/// se auto-confirmando, que é como uma rede aprende a errar com convicção.
///
/// Então o tom fica congelado aqui e é reajustado no Python, que tem o
/// corpus rotulado. É perda conhecida e escrita, não descuido.
/// </remarks>
public sealed class RedeTreinavel
{
    /// <summary>Um vetor por peça de texto. É PARÂMETRO: aprende junto.</summary>
    public double[][] Tabela { get; }

    public double[][] TroncoPesos { get; }
    public double[] TroncoVies { get; }

    public double[][] IntencaoPesos { get; }
    public double[] IntencaoVies { get; }

    public int Dimensao => Tabela.Length > 0 ? Tabela[0].Length : 0;
    public int Ocultos => TroncoVies.Length;
    public int Intencoes => IntencaoVies.Length;

    public RedeTreinavel(
        double[][] tabela,
        double[][] troncoPesos, double[] troncoVies,
        double[][] intencaoPesos, double[] intencaoVies)
    {
        Tabela = tabela;
        TroncoPesos = troncoPesos;
        TroncoVies = troncoVies;
        IntencaoPesos = intencaoPesos;
        IntencaoVies = intencaoVies;
    }

    // ══════════════════════════════════════════════════════════════════
    //  IDA
    // ══════════════════════════════════════════════════════════════════

    /// <summary>A média dos embutimentos das peças. É a entrada da rede.</summary>
    public double[] Entrada(IReadOnlyList<int> indices)
    {
        var x = new double[Dimensao];
        if (indices.Count == 0) return x;

        foreach (var i in indices)
        {
            var linha = Tabela[i];
            for (var d = 0; d < x.Length; d++) x[d] += linha[d];
        }
        for (var d = 0; d < x.Length; d++) x[d] /= indices.Count;
        return x;
    }

    /// <summary>Guarda o que a volta vai precisar — igual ao `Camada.frente`.</summary>
    public readonly record struct Passagem(double[] X, double[] Z0, double[] A0, double[] A1);

    public Passagem Frente(IReadOnlyList<int> indices)
    {
        var x = Entrada(indices);

        var z0 = new double[Ocultos];
        var a0 = new double[Ocultos];
        for (var j = 0; j < Ocultos; j++)
        {
            var s = TroncoVies[j];
            var w = TroncoPesos[j];
            for (var d = 0; d < x.Length; d++) s += w[d] * x[d];
            z0[j] = s;
            a0[j] = Sigmoid(s);
        }

        var z1 = new double[Intencoes];
        for (var k = 0; k < Intencoes; k++)
        {
            var s = IntencaoVies[k];
            var w = IntencaoPesos[k];
            for (var j = 0; j < Ocultos; j++) s += w[j] * a0[j];
            z1[k] = s;
        }

        return new Passagem(x, z0, a0, Softmax(z1));
    }

    /// <summary>
    /// O custo: menos o logaritmo da probabilidade que a rede deu para a
    /// resposta certa.
    /// </summary>
    /// <remarks>
    /// a = 0,99 → 0,01 (quase nada).  a = 0,01 → 4,61 (caro).
    ///
    /// O PISO EXISTE PARA EVITAR `ln(0)`, E ONDE ELE FICA IMPORTA.
    ///
    /// A primeira versão disto usava 1e-15, que é o palpite instintivo. O
    /// verificador numérico reprovou na primeira rodada, com diferença
    /// relativa de exatamente 1,00 — e o número exato foi a pista.
    ///
    /// Quando a rede dá à resposta certa uma probabilidade ABAIXO do piso, o
    /// custo trava: mexer num peso não muda mais nada, e a derivada numérica
    /// dá zero. A retropropagação, corretamente, continua dizendo `a − y ≈
    /// −1`. Zero contra um: diferença 1,00.
    ///
    /// Não era o gradiente errado — era o piso alto demais. `double` vai até
    /// ~1e-308, e `-ln(1e-300)` ≈ 691, que é finito e derivável. O piso
    /// agora só entra quando a probabilidade ZEROU de verdade por underflow,
    /// que é o único caso em que ele precisava existir.
    /// </remarks>
    public double Custo(IReadOnlyList<int> indices, int certa)
        => -Math.Log(Math.Max(Frente(indices).A1[certa], 1e-300));

    // ══════════════════════════════════════════════════════════════════
    //  VOLTA
    // ══════════════════════════════════════════════════════════════════

    public sealed class Gradiente
    {
        public required double[][] TroncoPesos { get; init; }
        public required double[] TroncoVies { get; init; }
        public required double[][] IntencaoPesos { get; init; }
        public required double[] IntencaoVies { get; init; }

        /// <summary>O erro que chega na PRÓPRIA ENTRADA — o que ajusta a tabela.</summary>
        public required double[] Entrada { get; init; }
    }

    /// <summary>O gradiente do custo para UM exemplo.</summary>
    /// <remarks>
    /// O erro da ÚLTIMA camada é o único que o custo sabe calcular sozinho;
    /// todos os outros são derivados dele. Com softmax e entropia cruzada
    /// juntas, esse erro é simplesmente `a − y` — a jacobiana da softmax e a
    /// derivada do log se cancelam, e é por isso que as duas andam sempre em
    /// par.
    ///
    /// O ERRO NA ENTRADA NÃO LEVA DERIVADA DE ATIVAÇÃO. Nas camadas internas
    /// ela aparece porque o valor passou por uma sigmoide; a entrada não
    /// passou por nenhuma — ela é o próprio vetor da tabela. Esquecer isso é
    /// sutil: o treino ainda roda, a perda ainda cai um pouco, e a tabela
    /// aprende torto.
    /// </remarks>
    public Gradiente Calcular(IReadOnlyList<int> indices, int certa)
    {
        var p = Frente(indices);

        // ── erro da saída: a − y ──────────────────────────────────────
        var erro1 = new double[Intencoes];
        for (var k = 0; k < Intencoes; k++) erro1[k] = p.A1[k];
        erro1[certa] -= 1.0;

        // dC/dvies = erro (o viés entra somado, derivada 1: passa inteiro)
        // dC/dpesos = erro ⊗ entrada da camada
        var gIntPesos = new double[Intencoes][];
        for (var k = 0; k < Intencoes; k++)
        {
            var linha = new double[Ocultos];
            for (var j = 0; j < Ocultos; j++) linha[j] = erro1[k] * p.A0[j];
            gIntPesos[k] = linha;
        }

        // ── o erro empurrado para trás ────────────────────────────────
        // Atravessa a MESMA matriz, transposta, e depois passa pela derivada
        // da ativação da camada anterior — foi ela que transformou z em a.
        var erro0 = new double[Ocultos];
        for (var j = 0; j < Ocultos; j++)
        {
            var s = 0.0;
            for (var k = 0; k < Intencoes; k++) s += IntencaoPesos[k][j] * erro1[k];
            var a = Sigmoid(p.Z0[j]);
            erro0[j] = s * a * (1.0 - a);
        }

        var gTroncoPesos = new double[Ocultos][];
        for (var j = 0; j < Ocultos; j++)
        {
            var linha = new double[Dimensao];
            for (var d = 0; d < Dimensao; d++) linha[d] = erro0[j] * p.X[d];
            gTroncoPesos[j] = linha;
        }

        // ── e o erro que sobra na entrada: W0ᵀ @ erro0 ────────────────
        var erroEntrada = new double[Dimensao];
        for (var d = 0; d < Dimensao; d++)
        {
            var s = 0.0;
            for (var j = 0; j < Ocultos; j++) s += TroncoPesos[j][d] * erro0[j];
            erroEntrada[d] = s;
        }

        return new Gradiente
        {
            TroncoPesos = gTroncoPesos,
            TroncoVies = erro0,
            IntencaoPesos = gIntPesos,
            IntencaoVies = erro1,
            Entrada = erroEntrada,
        };
    }

    // ══════════════════════════════════════════════════════════════════
    //  O PASSO DE DESCIDA
    // ══════════════════════════════════════════════════════════════════

    /// <summary>Um exemplo de treino: as peças da frase e a intenção certa.</summary>
    public readonly record struct Exemplo(IReadOnlyList<int> Indices, int Certa);

    /// <summary>A perda média sobre um conjunto. É o número que tem de cair.</summary>
    public double Perda(IReadOnlyList<Exemplo> exemplos)
    {
        if (exemplos.Count == 0) return 0;
        var soma = 0.0;
        foreach (var e in exemplos) soma += Custo(e.Indices, e.Certa);
        return soma / exemplos.Count;
    }

    /// <summary>Quantos o modelo acerta de primeira.</summary>
    public double Acerto(IReadOnlyList<Exemplo> exemplos)
    {
        if (exemplos.Count == 0) return 0;
        var certos = 0;
        foreach (var e in exemplos)
        {
            var a = Frente(e.Indices).A1;
            var melhor = 0;
            for (var k = 1; k < a.Length; k++) if (a[k] > a[melhor]) melhor = k;
            if (melhor == e.Certa) certos++;
        }
        return (double)certos / exemplos.Count;
    }

    /// <summary>Um passo de descida sobre um lote.</summary>
    /// <remarks>
    /// É o `passo()` do `classificador_duplo.py`, com UMA diferença
    /// deliberada: aqui o tom não entra.
    ///
    /// No Python o tronco recebe `gp_i[0] + peso_tom * gp_t[0]` — os dois
    /// gradientes somados. Aqui só o de intenção, porque o clique do Chefe
    /// rotula intenção e mais nada. Inventar um alvo de tom (usando o palpite
    /// do próprio modelo, por exemplo) seria a rede se auto-confirmando, que
    /// é como se aprende a errar com convicção.
    ///
    /// A consequência é conhecida e aceita: o tronco desliza um pouco em
    /// relação ao que a cabeça de tom espera. Quem reajusta é o Python, na
    /// reimportação, com o corpus rotulado nas duas dimensões.
    ///
    /// A TAXA É FIXA, e isso também muda em relação ao Python. Lá o
    /// `taxa_cosseno` desce ao longo das épocas — só que ele precisa saber
    /// quantas faltam, e aprendizado contínuo não tem fim. Taxa constante e
    /// pequena é o que sobra, e é por isso que a trava de validação existe:
    /// sem o resfriamento, nada impede um passo de piorar as coisas.
    /// </remarks>
    public void Passo(IReadOnlyList<Exemplo> lote, double taxa)
    {
        if (lote.Count == 0) return;

        var gTroncoP = Zeros(Ocultos, Dimensao);
        var gTroncoV = new double[Ocultos];
        var gIntP = Zeros(Intencoes, Ocultos);
        var gIntV = new double[Intencoes];

        // A tabela é esparsa: um lote toca poucas peças de 3.409. Zerar a
        // tabela inteira a cada passo custaria mais que o passo.
        var gTabela = new Dictionary<int, double[]>();

        foreach (var ex in lote)
        {
            var g = Calcular(ex.Indices, ex.Certa);

            for (var j = 0; j < Ocultos; j++)
            {
                gTroncoV[j] += g.TroncoVies[j];
                for (var d = 0; d < Dimensao; d++) gTroncoP[j][d] += g.TroncoPesos[j][d];
            }
            for (var k = 0; k < Intencoes; k++)
            {
                gIntV[k] += g.IntencaoVies[k];
                for (var j = 0; j < Ocultos; j++) gIntP[k][j] += g.IntencaoPesos[k][j];
            }

            // A DIVISÃO POR len(indices) É A DERIVADA DA MÉDIA. Sem ela o
            // treino pesaria mais as frases longas.
            if (ex.Indices.Count == 0) continue;
            foreach (var i in ex.Indices)
            {
                if (!gTabela.TryGetValue(i, out var linha))
                    gTabela[i] = linha = new double[Dimensao];
                for (var d = 0; d < Dimensao; d++)
                    linha[d] += g.Entrada[d] / ex.Indices.Count;
            }
        }

        var passo = taxa / lote.Count;

        for (var j = 0; j < Ocultos; j++)
        {
            TroncoVies[j] -= passo * gTroncoV[j];
            for (var d = 0; d < Dimensao; d++) TroncoPesos[j][d] -= passo * gTroncoP[j][d];
        }
        for (var k = 0; k < Intencoes; k++)
        {
            IntencaoVies[k] -= passo * gIntV[k];
            for (var j = 0; j < Ocultos; j++) IntencaoPesos[k][j] -= passo * gIntP[k][j];
        }
        foreach (var (i, linha) in gTabela)
            for (var d = 0; d < Dimensao; d++) Tabela[i][d] -= passo * linha[d];
    }

    /// <summary>Uma cópia independente — para guardar a base antes de aprender.</summary>
    /// <remarks>
    /// A trava de validação precisa poder DESFAZER um passo. Guardar uma
    /// cópia antes é mais simples e mais seguro que tentar recalcular o passo
    /// ao contrário — subtração de ponto flutuante não é exatamente
    /// reversível, e um "desfazer" que deixa resíduo é pior que não ter.
    /// </remarks>
    public RedeTreinavel Copiar() => new(
        Tabela.Select(l => (double[])l.Clone()).ToArray(),
        TroncoPesos.Select(l => (double[])l.Clone()).ToArray(),
        (double[])TroncoVies.Clone(),
        IntencaoPesos.Select(l => (double[])l.Clone()).ToArray(),
        (double[])IntencaoVies.Clone());

    private static double[][] Zeros(int linhas, int colunas)
    {
        var m = new double[linhas][];
        for (var i = 0; i < linhas; i++) m[i] = new double[colunas];
        return m;
    }

    // ══════════════════════════════════════════════════════════════════
    //  A PROVA
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Confere a retropropagação contra a definição de derivada.
    /// </summary>
    /// <remarks>
    /// POR QUE ISTO EXISTE, E POR QUE FICA NO CÓDIGO E NÃO NUM RASCUNHO.
    ///
    /// Um gradiente errado não estoura. Ele treina — devagar, torto, e
    /// convergindo para um lugar pior. Não há teste de comportamento que
    /// acuse isso: a perda cai, a acurácia sobe um pouco, e o defeito só
    /// aparece como "esse modelo nunca fica bom" seis meses depois.
    ///
    /// A única defesa é a definição:
    ///
    ///     dC/dw  ≈  ( C(w + ε) − C(w − ε) ) / 2ε
    ///
    /// Mexe num parâmetro por vez, mede o custo dos dois lados, compara com
    /// o que a retropropagação afirmou. Se a diferença relativa passar de
    /// 1e-6, a conta está errada — e é melhor descobrir aqui do que num
    /// modelo que ninguém entende por que não aprende.
    ///
    /// É a tradução do `conferir_numericamente` do Rede-Neural. O mesmo
    /// teste que o Python já passa.
    /// </remarks>
    /// <param name="afirmado">
    /// O gradiente a conferir. Nulo usa o desta classe — que é o caso normal.
    /// Passar um de fora serve para o controle do teste: dá para entregar um
    /// gradiente ERRADO de propósito e exigir que o verificador o reprove.
    /// Sem isso, "o teste pegaria um erro" seria promessa e não demonstração.
    /// </param>
    public double ConferirNumericamente(
        IReadOnlyList<int> indices, int certa, double epsilon = 1e-5, Gradiente? afirmado = null)
    {
        var g = afirmado ?? Calcular(indices, certa);
        var pior = 0.0;

        void Conferir(double dito, Func<double, double> mexer)
        {
            var mais = Medir(mexer, +epsilon);
            var menos = Medir(mexer, -epsilon);
            var numerico = (mais - menos) / (2 * epsilon);

            var escala = Math.Max(1.0, Math.Max(Math.Abs(dito), Math.Abs(numerico)));
            pior = Math.Max(pior, Math.Abs(dito - numerico) / escala);
        }

        double Medir(Func<double, double> mexer, double delta)
        {
            var original = mexer(delta);          // aplica e devolve o valor antigo
            var c = Custo(indices, certa);
            mexer(double.NaN);                    // NaN = "restaura"
            _ = original;
            return c;
        }

        // ── viés e pesos da cabeça de intenção ────────────────────────
        for (var k = 0; k < Intencoes; k++)
        {
            var kk = k;
            Conferir(g.IntencaoVies[kk], d => Mexer(ref IntencaoViesRef(kk), d));
            for (var j = 0; j < Ocultos; j++)
            {
                var jj = j;
                Conferir(g.IntencaoPesos[kk][jj], d => Mexer(ref IntencaoPesoRef(kk, jj), d));
            }
        }

        // ── viés e pesos do tronco ────────────────────────────────────
        for (var j = 0; j < Ocultos; j++)
        {
            var jj = j;
            Conferir(g.TroncoVies[jj], d => Mexer(ref TroncoViesRef(jj), d));
            for (var d0 = 0; d0 < Dimensao; d0++)
            {
                var dd = d0;
                Conferir(g.TroncoPesos[jj][dd], d => Mexer(ref TroncoPesoRef(jj, dd), d));
            }
        }

        // ── e a tabela de embutimento ─────────────────────────────────
        //
        // Aqui o gradiente afirmado não é `Entrada` direto: a média divide o
        // erro entre as peças da frase. Uma peça que aparece duas vezes na
        // mesma frase recebe duas fatias — por isso a contagem.
        foreach (var i in indices.Distinct())
        {
            var vezes = indices.Count(x => x == i);
            for (var d0 = 0; d0 < Dimensao; d0++)
            {
                var ii = i; var dd = d0;
                var fatia = g.Entrada[dd] * vezes / indices.Count;
                Conferir(fatia, d => Mexer(ref TabelaRef(ii, dd), d));
            }
        }

        return pior;
    }

    // O truque do `ref`: um só lugar aplica e restaura, e nenhuma cópia do
    // modelo é feita — conferir 14 mil parâmetros clonando a rede a cada um
    // seria lento a ponto de ninguém rodar o teste.
    private double _guardado;

    private double Mexer(ref double alvo, double delta)
    {
        if (double.IsNaN(delta)) { alvo = _guardado; return alvo; }
        _guardado = alvo;
        alvo += delta;
        return _guardado;
    }

    private ref double IntencaoViesRef(int k) => ref IntencaoVies[k];
    private ref double IntencaoPesoRef(int k, int j) => ref IntencaoPesos[k][j];
    private ref double TroncoViesRef(int j) => ref TroncoVies[j];
    private ref double TroncoPesoRef(int j, int d) => ref TroncoPesos[j][d];
    private ref double TabelaRef(int i, int d) => ref Tabela[i][d];

    // ══════════════════════════════════════════════════════════════════

    /// <summary>Estável nos dois lados: `Math.Exp` de um número grande estoura.</summary>
    private static double Sigmoid(double z)
        => z >= 0 ? 1.0 / (1.0 + Math.Exp(-z)) : Math.Exp(z) / (1.0 + Math.Exp(z));

    private static double[] Softmax(double[] z)
    {
        var maior = z.Max();
        var soma = 0.0;
        var saida = new double[z.Length];
        for (var k = 0; k < z.Length; k++) { saida[k] = Math.Exp(z[k] - maior); soma += saida[k]; }
        for (var k = 0; k < z.Length; k++) saida[k] /= soma;
        return saida;
    }
}
