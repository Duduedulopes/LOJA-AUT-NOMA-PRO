using AutonomousStore.Gerente.Services;

namespace AutonomousStore.Gerente.Tests;

/// <summary>
/// O gerente falando de verdade com cada perfil. A lista de permissão (PerfilDeQuemFalaTests) diz o
/// que PODE; aqui se prova o que ACONTECE: a resposta certa para cada um, e — o que mais importa —
/// que uma intenção recusada NEM CHEGA a buscar o dado. Recusar depois de ler o faturamento seria
/// esconder o número da tela, e não do servidor.
///
/// Os dois caminhos são testados: o BOTÃO (a intenção já vem escolhida) e a FRASE DIGITADA (a rede
/// classifica). O caderno do Rede-Neural registra que a trava quase ficou só de um lado.
/// </summary>
public class ConversaPorPerfilTests
{
    private static (GerenteService Gerente, ApiFalsa Api, ClassificadorFalso Classificador) Montar(PerfilDeQuemFala perfil)
    {
        var api = new ApiFalsa();
        var classificador = new ClassificadorFalso();
        var gerente = new GerenteService(api, api, api, classificador, new FabricaDeHttpFalsa()) { Perfil = perfil };

        return (gerente, api, classificador);
    }

    public static IEnumerable<object[]> DaPlataforma => new[]
    {
        new object[] { PerfilDeQuemFala.Tecnico("Carlos Souza") },
        new object[] { PerfilDeQuemFala.Criador("Eduardo Lopes") },
    };

    // ═══════════════════════════════════ o que é recusado, e sem buscar dado
    [Theory]
    [MemberData(nameof(DaPlataforma))]
    public async Task TodaIntencaoRecusadaPeloBotaoRecebeARecusaESemBuscarDadoNenhum(PerfilDeQuemFala perfil)
    {
        var (gerente, api, _) = Montar(perfil);

        var recusadas = ModeloTreinado.Intencoes.Where(i => !perfil.Pode(i)).ToList();
        Assert.NotEmpty(recusadas);

        foreach (var intencao in recusadas)
        {
            var resposta = await gerente.ResponderComIntencaoAsync(intencao, "qualquer coisa");

            Assert.Equal(perfil.Recusa(), resposta);
        }

        // Nenhuma das 34 foi buscar nada: nem produto, nem sessao, nem sistema espacial.
        Assert.Empty(api.Chamadas);
    }

    [Theory]
    [MemberData(nameof(DaPlataforma))]
    public async Task TodaIntencaoRecusadaNaFraseDigitadaRecebeARecusaESemBuscarDadoNenhum(PerfilDeQuemFala perfil)
    {
        var (gerente, api, classificador) = Montar(perfil);

        foreach (var intencao in ModeloTreinado.Intencoes.Where(i => !perfil.Pode(i)))
        {
            classificador.IntencaoQueVaiDevolver = intencao;

            var resposta = await gerente.ResponderAsync("uma frase que a rede leu como " + intencao);

            Assert.Equal(perfil.Recusa(), resposta);
        }

        Assert.Empty(api.Chamadas);
    }

    [Theory]
    [MemberData(nameof(DaPlataforma))]
    public async Task OFaturamentoEmEspecialNaoSaiParaQuemVeTodasAsEmpresas(PerfilDeQuemFala perfil)
    {
        var (gerente, api, classificador) = Montar(perfil);
        classificador.IntencaoQueVaiDevolver = "faturamento";

        var digitando = await gerente.ResponderAsync("quanto faturamos hoje?");
        var clicando = await gerente.ResponderComIntencaoAsync("faturamento", "quanto faturamos hoje?");

        Assert.DoesNotContain("R$", digitando);
        Assert.DoesNotContain("R$", clicando);
        Assert.DoesNotContain(nameof(ApiFalsa.GetHistoryAsync), api.Chamadas);   // o historico de vendas nem foi lido
    }

    [Fact]
    public async Task ARecusaDoTecnicoNaoMandaAbrirChamadoNoSuporte()
    {
        var (gerente, _, _) = Montar(PerfilDeQuemFala.Tecnico("Carlos"));

        var resposta = await gerente.ResponderComIntencaoAsync("faturamento", "quanto faturamos?");

        Assert.DoesNotContain("chamado", resposta, StringComparison.OrdinalIgnoreCase);
    }

    // ═════════════════════════════════════════ cada um ouve o texto certo
    [Theory]
    [MemberData(nameof(DaPlataforma))]
    public async Task AAjudaDoSuporteFalaDoLadoTecnicoENaoDoCarrinhoNemDeMudarPreco(PerfilDeQuemFala perfil)
    {
        var (gerente, _, _) = Montar(perfil);

        var ajuda = await gerente.ResponderComIntencaoAsync("ajuda", "o que voce faz?");

        Assert.Contains("lado técnico", ajuda);
        Assert.Contains("se a API está respondendo", ajuda);
        Assert.DoesNotContain("carrinho", ajuda, StringComparison.OrdinalIgnoreCase);      // ajuda de comprador
        Assert.DoesNotContain("Mudar as coisas", ajuda);                                    // ajuda do Chefe
        Assert.DoesNotContain("Dinheiro", ajuda);
    }

    [Fact]
    public async Task AAjudaDoCompradorEDoChefeContinuamComoEram()
    {
        var (comprador, _, _) = Montar(PerfilDeQuemFala.Cliente("Maria Souza", Guid.NewGuid()));
        var (chefe, _, _) = Montar(PerfilDeQuemFala.Chefe("Eduardo Lopes"));

        var doComprador = await comprador.ResponderComIntencaoAsync("ajuda", "ajuda");
        var doChefe = await chefe.ResponderComIntencaoAsync("ajuda", "ajuda");

        Assert.Contains("A sua compra", doComprador);
        Assert.Contains("abra um chamado em **Suporte**", doComprador);
        Assert.DoesNotContain("lado técnico", doComprador);

        Assert.Contains("Mudar as coisas", doChefe);
        Assert.DoesNotContain("lado técnico", doChefe);
    }

    [Fact]
    public async Task ForaDeEscopoFalaComCadaPerfilDoJeitoDele()
    {
        var (tecnico, _, _) = Montar(PerfilDeQuemFala.Tecnico("Carlos"));
        var (comprador, _, _) = Montar(PerfilDeQuemFala.Cliente("Maria"));
        var (chefe, _, _) = Montar(PerfilDeQuemFala.Chefe("Eduardo Lopes"));

        Assert.Contains("lado técnico do sistema", await tecnico.ResponderComIntencaoAsync("fora_de_escopo", "x"));
        Assert.Contains("abrir um chamado em **Suporte**", await comprador.ResponderComIntencaoAsync("fora_de_escopo", "x"));
        Assert.Contains("Estoque, vendas", await chefe.ResponderComIntencaoAsync("fora_de_escopo", "x"));

        // ...e o tecnico nunca ouve o texto do comprador, que o mandaria abrir chamado no proprio suporte.
        Assert.DoesNotContain("chamado", await tecnico.ResponderComIntencaoAsync("fora_de_escopo", "x"), StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════ chamar todos pelo nome
    private static PerfilDeQuemFala[] OsQuatro(string nome) =>
    [
        PerfilDeQuemFala.Chefe(nome),
        PerfilDeQuemFala.Cliente(nome, Guid.NewGuid()),
        PerfilDeQuemFala.Tecnico(nome),
        PerfilDeQuemFala.Criador(nome),
    ];

    [Fact]
    public async Task ASaudacaoChamaCadaUmPeloNome()
    {
        foreach (var perfil in OsQuatro("Eduardo Lopes"))
        {
            var (gerente, _, _) = Montar(perfil);

            var resposta = await gerente.ResponderComIntencaoAsync("saudacao", "oi");

            Assert.StartsWith("Eduardo", resposta);
        }
    }

    [Fact]
    public async Task NenhumaRespostaDeNenhumaIntencaoParaNenhumPerfilChamaAlguemDeChefe()
    {
        // As 42 intencoes do modelo x os 4 perfis, pelo botao. Inclui as recusas, a ajuda, o fora de escopo e o comeco de
        // cada ordem de escrever. O gerente ja chamou o dono da loja de "Chefe"; agora chama todos pelo nome.
        var achados = new List<string>();

        foreach (var perfil in OsQuatro("Eduardo Lopes"))
        {
            foreach (var intencao in ModeloTreinado.Intencoes)
            {
                var (gerente, _, _) = Montar(perfil);     // um gerente novo por resposta: nenhuma conversa aberta atrapalha

                var resposta = await gerente.ResponderComIntencaoAsync(intencao, "quero mudar o preco da agua para 5,00");

                if (System.Text.RegularExpressions.Regex.IsMatch(resposta, @"\bChefe\b"))
                    achados.Add($"{perfil.Tipo}/{intencao}: {resposta[..Math.Min(80, resposta.Length)]}");
            }
        }

        Assert.True(achados.Count == 0, "Respostas que chamam alguem de Chefe:\n" + string.Join("\n", achados));
    }

    // ═══════════════════════════════════════════════════ as sugestões
    [Theory]
    [MemberData(nameof(DaPlataforma))]
    public void TecnicoECriadorVeemSoSugestoesDoLadoTecnico(PerfilDeQuemFala perfil)
    {
        var (gerente, _, _) = Montar(perfil);

        Assert.Equal(
            new[] { "como funciona o rfid?", "teve algum furo de sistema?", "status da api" },
            gerente.Sugestoes.ToArray());
    }

    [Fact]
    public void ToDaSugestaoQueOTecnicoVeELiberadaParaOTecnico()
    {
        // A regra do GerenteService ("permissao manda por cima da plateia") vista de fora: nenhuma
        // sugestao oferecida leva a uma recusa.
        var perfil = PerfilDeQuemFala.Tecnico("Carlos");
        var (gerente, _, _) = Montar(perfil);

        Assert.NotEmpty(gerente.Sugestoes);
    }

    [Fact]
    public void CompradorEChefeNaoGanharamAsSugestoesDoSuporte()
    {
        var (comprador, _, _) = Montar(PerfilDeQuemFala.Cliente("Maria", Guid.NewGuid()));
        var (chefe, _, _) = Montar(PerfilDeQuemFala.Chefe("Eduardo Lopes"));

        Assert.DoesNotContain("teve algum furo de sistema?", comprador.Sugestoes);
        Assert.DoesNotContain("teve algum furo de sistema?", chefe.Sugestoes);

        // E o comprador continua com as dele, sem nada do painel.
        Assert.Contains("quanto custa a água?", comprador.Sugestoes);
        Assert.DoesNotContain("quanto faturamos hoje?", comprador.Sugestoes);
        Assert.Contains("quanto faturamos hoje?", chefe.Sugestoes);
    }

    [Fact]
    public void TecnicoNaoVeSugestaoDeCompradorNemDeChefe()
    {
        var (tecnico, _, _) = Montar(PerfilDeQuemFala.Tecnico("Carlos"));

        Assert.DoesNotContain("quanto custa a água?", tecnico.Sugestoes);
        Assert.DoesNotContain("quanto faturamos hoje?", tecnico.Sugestoes);
        Assert.DoesNotContain("adiciona um produto novo", tecnico.Sugestoes);
    }
}
