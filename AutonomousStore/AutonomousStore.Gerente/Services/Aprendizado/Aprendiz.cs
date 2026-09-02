namespace AutonomousStore.Gerente.Services.Aprendizado;

/// <summary>
/// Quem deixa a loja aprender — e quem impede que ela desaprenda.
/// </summary>
/// <remarks>
/// O PROBLEMA, MEDIDO E NÃO SUPOSTO.
///
/// Ensinar uma frase que o modelo errava funcionou de primeira: a intenção
/// certa saiu de 11,79% para 98,38% em cinco passos. E quebrou sete outras
/// de mil cento e noventa e uma — 99,92% para 99,33% no conjunto de fora.
///
/// Isso é esquecimento catastrófico, e numa rede de 84 mil parâmetros ele
/// não é sutil: um punhado de passos fortes sobre um exemplo só reescreve o
/// tronco inteiro em favor dele.
///
/// A CORREÇÃO NÃO É PROIBIR, É MEDIR.
///
/// Recusar todo aprendizado seria voltar ao problema original. O que esta
/// classe faz é tentar aprender com força, medir o estrago, e ir baixando a
/// taxa até achar um passo que ensina SEM quebrar. Se nenhum servir, desfaz
/// e diz que não deu.
///
/// TRÊS COISAS PRECISAM SER VERDADE PARA UM PASSO SER ACEITO:
///   1. a frase ensinada passa a ser respondida certo
///   2. o acerto no conjunto de guarda não cai
///   3. a base do Python continua intacta, para dar sempre como desfazer
///
/// A BASE NUNCA MUDA. É o modelo que veio do Python, e ele fica guardado
/// inteiro. Sem um ponto de retorno, "aprendeu errado" viraria um problema
/// sem saída — e o Chefe descobriria isso no pior momento possível.
/// </remarks>
public sealed class Aprendiz
{
    /// <summary>O que veio do Python. Nunca muda.</summary>
    private readonly RedeTreinavel _base;

    /// <summary>O que a loja sabe agora. É esta que responde.</summary>
    public RedeTreinavel Atual { get; private set; }

    /// <summary>As frases que a trava usa para medir estrago.</summary>
    private readonly IReadOnlyList<RedeTreinavel.Exemplo> _guarda;

    /// <summary>
    /// As taxas tentadas, da mais forte para a mais fraca.
    /// </summary>
    /// <remarks>
    /// Aprender é o objetivo; não estragar é a condição. Então tenta forte
    /// primeiro — um passo forte que passa na guarda é melhor que cinco
    /// fracos, porque ensina de vez. Só quando ele reprova é que vale
    /// insistir devagar.
    /// </remarks>
    private static readonly double[] Taxas = { 0.5, 0.25, 0.1, 0.05, 0.02, 0.01 };

    private const int PassosPorTentativa = 5;

    public int Aceitas { get; private set; }
    public int Recusadas { get; private set; }

    /// <summary>O acerto da base na guarda. O piso de que ninguém desce.</summary>
    private readonly double _acertoDaBase;

    public Aprendiz(RedeTreinavel baseDoPython, IReadOnlyList<RedeTreinavel.Exemplo> guarda)
    {
        _base = baseDoPython;
        Atual = baseDoPython.Copiar();
        _guarda = guarda;
        _acertoDaBase = baseDoPython.Acerto(guarda);
    }

    /// <summary>O acerto de agora no conjunto de guarda.</summary>
    public double AcertoNaGuarda() => Atual.Acerto(_guarda);

    public enum Resultado
    {
        /// <summary>Aprendeu, e o resto continuou de pé.</summary>
        Aceito,

        /// <summary>Nenhuma taxa ensinou sem quebrar. Nada mudou.</summary>
        Recusado,

        /// <summary>Já respondia certo. Não havia o que ensinar.</summary>
        JaSabia,
    }

    public readonly record struct Veredito(
        Resultado Resultado,
        double TaxaUsada,
        double AcertoAntes,
        double AcertoDepois,
        double ConfiancaAntes,
        double ConfiancaDepois)
    {
        public bool Mudou => Resultado == Resultado.Aceito;
    }

    /// <summary>Ensina uma correção — se der para ensinar sem estragar.</summary>
    public Veredito Ensinar(IReadOnlyList<int> indices, int certa)
    {
        if (indices.Count == 0)
            return new Veredito(Resultado.Recusado, 0, 0, 0, 0, 0);

        var acertoAntes = AcertoNaGuarda();
        var confAntes = Atual.Frente(indices).A1[certa];

        if (Melhor(Atual, indices) == certa)
            return new Veredito(Resultado.JaSabia, 0, acertoAntes, acertoAntes, confAntes, confAntes);

        foreach (var taxa in Taxas)
        {
            // Sempre a partir do estado ATUAL, nunca acumulando tentativas:
            // senão a terceira taxa estaria treinando por cima do estrago
            // que a primeira fez e a medição não valeria nada.
            var tentativa = Atual.Copiar();
            for (var i = 0; i < PassosPorTentativa; i++)
                tentativa.Passo(new[] { new RedeTreinavel.Exemplo(indices, certa) }, taxa);

            var aprendeu = Melhor(tentativa, indices) == certa;
            var acertoDepois = tentativa.Acerto(_guarda);

            // DUAS COMPARAÇÕES, E A SEGUNDA FOI APRENDIDA APANHANDO.
            //
            // A primeira versão só comparava com o estado de AGORA. Passou
            // no teste de uma correção e falhou no de vinte: cinco passos,
            // cada um "sem piorar", e no fim onze frases quebradas. Nenhum
            // deles foi o culpado — todos foram.
            //
            // Comparar só com o presente é medir a inclinação e ignorar a
            // altura. Um modelo pode descer um degrau de cada vez para
            // sempre, e cada degrau passa.
            //
            // Por isso o piso é a BASE. Não importa quantas correções
            // vieram antes: se o acerto na guarda ficar abaixo do que o
            // Python entregou, o passo não entra. A loja pode aprender o
            // quanto quiser — desde que nunca fique pior do que começou.
            if (aprendeu && acertoDepois >= acertoAntes && acertoDepois >= _acertoDaBase)
            {
                Atual = tentativa;
                Aceitas++;
                return new Veredito(Resultado.Aceito, taxa, acertoAntes, acertoDepois,
                                    confAntes, tentativa.Frente(indices).A1[certa]);
            }
        }

        Recusadas++;
        return new Veredito(Resultado.Recusado, 0, acertoAntes, acertoAntes, confAntes, confAntes);
    }

    /// <summary>Joga fora tudo o que a loja aprendeu e volta ao modelo do Python.</summary>
    /// <remarks>
    /// O botão de pânico. Existe porque um sistema que aprende sem ter como
    /// voltar atrás é um sistema que ninguém liga.
    /// </remarks>
    public void Reiniciar()
    {
        Atual = _base.Copiar();
        Aceitas = 0;
        Recusadas = 0;
    }

    /// <summary>Quanto a loja se afastou da base, peso a peso.</summary>
    /// <remarks>
    /// Serve para responder "o que mudou desde o Python?" com um número em
    /// vez de com uma sensação — e para o Python saber, na reimportação, se
    /// vale a pena recalibrar.
    /// </remarks>
    public double DistanciaDaBase()
    {
        var soma = 0.0;
        var n = 0;

        void Somar(double[] a, double[] b)
        {
            for (var i = 0; i < a.Length; i++) { var d = a[i] - b[i]; soma += d * d; n++; }
        }

        for (var j = 0; j < _base.TroncoPesos.Length; j++) Somar(Atual.TroncoPesos[j], _base.TroncoPesos[j]);
        Somar(Atual.TroncoVies, _base.TroncoVies);
        for (var k = 0; k < _base.IntencaoPesos.Length; k++) Somar(Atual.IntencaoPesos[k], _base.IntencaoPesos[k]);
        Somar(Atual.IntencaoVies, _base.IntencaoVies);
        for (var i = 0; i < _base.Tabela.Length; i++) Somar(Atual.Tabela[i], _base.Tabela[i]);

        return n == 0 ? 0 : Math.Sqrt(soma / n);
    }

    private static int Melhor(RedeTreinavel r, IReadOnlyList<int> indices)
    {
        var a = r.Frente(indices).A1;
        var melhor = 0;
        for (var k = 1; k < a.Length; k++) if (a[k] > a[melhor]) melhor = k;
        return melhor;
    }
}
