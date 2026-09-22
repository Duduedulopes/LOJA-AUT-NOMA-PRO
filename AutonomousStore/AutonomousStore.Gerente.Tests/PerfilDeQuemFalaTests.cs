namespace AutonomousStore.Gerente.Tests;

/// <summary>
/// Quem pode perguntar o quê ao gerente. É lista de PERMISSÃO, e por isso estes testes dizem o que
/// cada perfil PODE, por extenso: acrescentar uma intenção a um perfil vira uma mudança que o
/// teste obriga a escrever aqui, à vista. O esquecimento erra para o lado seguro: uma intenção
/// nova, treinada amanhã no Rede-Neural, aparece no modelo e nasce RECUSADA para todos, menos o Chefe.
/// </summary>
public class PerfilDeQuemFalaTests
{
    // As que existem hoje para o comprador — as doze documentadas no PerfilDeQuemFala.
    private static readonly string[] DoComprador =
    {
        "saudacao", "agradecimento", "ajuda", "duvida_sistema", "preco", "listar_produtos",
        "estoque", "pagamento", "entrada_loja", "meu_carrinho", "carrinho", "fora_de_escopo",
    };

    // O lado técnico do sistema, e a conversa. Nada de dinheiro, estoque, carrinho nem pessoa.
    private static readonly string[] DoSuporte =
    {
        "saudacao", "agradecimento", "ajuda", "duvida_sistema", "fora_de_escopo",
        "status_api", "logs_sistema", "furo_sistema",
    };

    // O que MUDA o banco ou o sistema. Nenhum perfil, exceto o Chefe, chega perto.
    private static readonly string[] Escritas =
    {
        "adicionar_produto", "alterar_preco", "alterar_estoque", "repor_estoque", "remover_produto",
        "configurar_camera", "configurar_sistema", "reiniciar_servico", "confirmar_acao", "cancelar_operacao",
    };

    // O que é DO NEGÓCIO de uma empresa. Quem opera a plataforma vê várias de uma vez: a resposta
    // somaria empresas diferentes, e o técnico não tem por que ler o faturamento de ninguém.
    private static readonly string[] DoNegocio =
    {
        "faturamento", "mais_vendidos", "relatorio_periodo", "estoque", "estoque_baixo", "listar_produtos",
        "preco", "carrinho", "meu_carrinho", "pessoas_na_loja", "analise_combinada", "comparar", "pagamento",
    };

    public static IEnumerable<object[]> Perfis => new[]
    {
        new object[] { "Tecnico", PerfilDeQuemFala.Tecnico("Carlos Souza") },
        new object[] { "Criador", PerfilDeQuemFala.Criador("Eduardo Lopes") },
    };

    // ══════════════════════════════════════════════════ o modelo é a fonte
    [Fact]
    public void OModeloTreinadoTem42IntencoesEEssasSaoTodasAsQueExistem()
    {
        // Se este numero mudar, o Rede-Neural treinou intencoes novas. Nada quebra — elas ja nascem
        // recusadas para o comprador, o tecnico e o Criador — mas alguem deve DECIDIR para quem abrir.
        Assert.Equal(42, ModeloTreinado.Intencoes.Count);
    }

    [Fact]
    public void TodaIntencaoDeTodaListaDePermissaoExisteNoModelo()
    {
        // Um nome errado numa lista de permissao (typo) nao da erro: a intencao so fica recusada
        // para sempre. Aqui o typo aparece.
        var todas = ModeloTreinado.Intencoes.ToHashSet();

        foreach (var nome in DoComprador.Concat(DoSuporte))
            Assert.Contains(nome, todas);
    }

    // ═══════════════════════════════════════════════════ o que cada um PODE
    [Fact]
    public void ChefeVeTudo()
    {
        foreach (var intencao in ModeloTreinado.Intencoes)
            Assert.True(PerfilDeQuemFala.Chefe("Eduardo Lopes").Pode(intencao), intencao);

        Assert.True(PerfilDeQuemFala.Chefe("Eduardo Lopes").PodeEscrever);
    }

    [Fact]
    public void CompradorVeExatamenteAsDozeDele()
    {
        var perfil = PerfilDeQuemFala.Cliente("Maria Souza", Guid.NewGuid());

        Assert.True(perfil.Permitidas.SetEquals(DoComprador));
        Assert.Equal(
            DoComprador.OrderBy(x => x),
            ModeloTreinado.Intencoes.Where(perfil.Pode).OrderBy(x => x));
    }

    [Theory]
    [MemberData(nameof(Perfis))]
    public void TecnicoECriadorVeemExatamenteOLadoTecnico(string quem, PerfilDeQuemFala perfil)
    {
        Assert.True(perfil.Permitidas.SetEquals(DoSuporte), quem);
        Assert.Equal(
            DoSuporte.OrderBy(x => x),
            ModeloTreinado.Intencoes.Where(perfil.Pode).OrderBy(x => x));
    }

    // ═════════════════════════════════════════════ o que NUNCA passa
    [Theory]
    [MemberData(nameof(Perfis))]
    public void TecnicoECriadorNuncaEscrevem(string quem, PerfilDeQuemFala perfil)
    {
        Assert.False(perfil.PodeEscrever, quem);

        foreach (var intencao in Escritas)
            Assert.False(perfil.Pode(intencao), $"{quem} nao pode {intencao}");
    }

    [Theory]
    [MemberData(nameof(Perfis))]
    public void TecnicoECriadorNaoLeemDadosDeNegocio(string quem, PerfilDeQuemFala perfil)
    {
        foreach (var intencao in DoNegocio)
            Assert.False(perfil.Pode(intencao), $"{quem} nao pode {intencao}");
    }

    [Fact]
    public void SoOChefePodeEscrever()
    {
        Assert.True(PerfilDeQuemFala.Chefe("Eduardo Lopes").PodeEscrever);
        Assert.False(PerfilDeQuemFala.Cliente("Maria").PodeEscrever);
        Assert.False(PerfilDeQuemFala.Tecnico("Carlos").PodeEscrever);
        Assert.False(PerfilDeQuemFala.Criador("Eduardo Lopes").PodeEscrever);
    }

    [Fact]
    public void UmaIntencaoQueNaoExisteERecusadaParaTodosMenosOChefe()
    {
        // O que o Rede-Neural treinar amanha, e ninguem listou: nasce fechada.
        const string nova = "intencao_treinada_amanha";

        Assert.False(PerfilDeQuemFala.Cliente("Maria").Pode(nova));
        Assert.False(PerfilDeQuemFala.Tecnico("Carlos").Pode(nova));
        Assert.False(PerfilDeQuemFala.Criador("Eduardo Lopes").Pode(nova));
    }

    // ═════════════════════════════════════════════ quem é cada um
    [Fact]
    public void CadaPerfilSabeQuemE()
    {
        Assert.Equal(TipoDePerfil.Chefe, PerfilDeQuemFala.Chefe("Eduardo Lopes").Tipo);
        Assert.Equal(TipoDePerfil.Cliente, PerfilDeQuemFala.Cliente("Maria").Tipo);
        Assert.Equal(TipoDePerfil.Tecnico, PerfilDeQuemFala.Tecnico("Carlos").Tipo);
        Assert.Equal(TipoDePerfil.Criador, PerfilDeQuemFala.Criador("Eduardo Lopes").Tipo);

        Assert.True(PerfilDeQuemFala.Cliente("Maria").EhCliente);
        Assert.False(PerfilDeQuemFala.Tecnico("Carlos").EhCliente);       // nao escreve, e nem por isso e comprador
        Assert.True(PerfilDeQuemFala.Tecnico("Carlos").EhDaPlataforma);
        Assert.True(PerfilDeQuemFala.Criador("Eduardo Lopes").EhDaPlataforma);
        Assert.False(PerfilDeQuemFala.Chefe("Eduardo Lopes").EhDaPlataforma);
    }

    [Fact]
    public void UmPerfilCriadoSemDizerOTipoContinuaSendoDoChefeEDeMaisNinguem()
    {
        // O valor padrao do tipo: quem montar um perfil na mao sem dizer o tipo nao vira, por engano,
        // tecnico ou comprador — o Chefe e o unico que ja existia antes do tipo existir.
        var semTipo = new PerfilDeQuemFala("Fulano", new HashSet<string>(), PodeEscrever: false, Origem: "Teste");

        Assert.Equal(TipoDePerfil.Chefe, semTipo.Tipo);
        Assert.False(semTipo.EhCliente);
        Assert.False(semTipo.EhDaPlataforma);
    }

    [Fact]
    public void TratamentoEOrigemDeCadaUm()
    {
        Assert.Equal("Carlos", PerfilDeQuemFala.Tecnico("Carlos Souza").Tratamento);      // primeiro nome, como o comprador
        Assert.Equal("Olá", PerfilDeQuemFala.Tecnico(null).Tratamento);
        Assert.Equal("Eduardo", PerfilDeQuemFala.Criador("Eduardo Lopes").Tratamento);    // todos pelo nome, ate o Criador

        Assert.Equal("AdminApp", PerfilDeQuemFala.Chefe("Eduardo Lopes").Origem);
        Assert.Equal("ClientApp", PerfilDeQuemFala.Cliente("Maria").Origem);
        Assert.Equal("SuporteApp", PerfilDeQuemFala.Tecnico("Carlos").Origem);            // o corpus do Python separa por origem
        Assert.Equal("CriadorApp", PerfilDeQuemFala.Criador("Eduardo Lopes").Origem);
    }

    // ═══════════════════════════════════════════ todos pelo primeiro nome
    private static PerfilDeQuemFala[] OsQuatro(string? nome) =>
    [
        PerfilDeQuemFala.Chefe(nome),
        PerfilDeQuemFala.Cliente(nome, Guid.NewGuid()),
        PerfilDeQuemFala.Tecnico(nome),
        PerfilDeQuemFala.Criador(nome),
    ];

    [Fact]
    public void TodosSaoChamadosPeloPrimeiroNomeIndiferenteDeQuemSejam()
    {
        // A decisao do Eduardo: chamar todos pelo nome. Chefe, comprador, tecnico e ate o Criador.
        foreach (var perfil in OsQuatro("Maria Eduarda Souza"))
            Assert.Equal("Maria", perfil.Tratamento);
    }

    [Theory]
    [InlineData("Eduardo Lopes")]
    [InlineData("Ana")]
    [InlineData("Carlos Souza")]
    public void NinguemEChamadoDeChefe(string nome)
    {
        foreach (var perfil in OsQuatro(nome))
        {
            Assert.NotEqual("Chefe", perfil.Tratamento);
            Assert.Equal(nome.Split(' ')[0], perfil.Tratamento);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]                                        // uma letra so: nao e nome
    [InlineData("NomeQueTemMaisDeVinteLetrasNaoDa")]         // longo demais para abrir uma frase
    public void SemNomeUsavelTodosCaemEmOlaEDeNovoNuncaEmChefe(string? nome)
    {
        // Um gerente que comeca a frase com virgula e pior que um que nao usa o nome.
        foreach (var perfil in OsQuatro(nome))
            Assert.Equal("Olá", perfil.Tratamento);
    }

    [Fact]
    public void AOrigemContinuaDizendoDeQualAppVeioMesmoComTodosPeloNome()
    {
        // O nome mudou; o corpus do Python continua separando as perguntas por app.
        Assert.Equal(
            new[] { "AdminApp", "ClientApp", "SuporteApp", "CriadorApp" },
            OsQuatro("Maria").Select(p => p.Origem).ToArray());
    }

    // ══════════════════════════════════════════ a recusa fala com cada um
    [Fact]
    public void ARecusaDoCompradorNaoMudouEAindaApontaParaOChamado()
    {
        var recusa = PerfilDeQuemFala.Cliente("Maria Souza").Recusa();

        Assert.StartsWith("Maria, ", recusa);
        Assert.Contains("abrir um chamado em **Suporte**", recusa);
        Assert.Contains("administração da loja", recusa);
    }

    [Theory]
    [MemberData(nameof(Perfis))]
    public void ARecusaDoTecnicoENaoDoCriadorNaoMandaAbrirChamadoNemFalaDeAdministracaoDaLoja(string quem, PerfilDeQuemFala perfil)
    {
        // "Abra um chamado em Suporte" dito ao proprio suporte seria absurdo — e era o que sairia se o
        // tecnico caisse na recusa do comprador.
        var recusa = perfil.Recusa();

        Assert.False(
            recusa.Contains("chamado", StringComparison.OrdinalIgnoreCase),
            $"a recusa de {quem} manda abrir chamado: {recusa}");
        Assert.DoesNotContain("administração da loja", recusa);
        Assert.Contains("parte técnica", recusa);
    }

    [Theory]
    [MemberData(nameof(Perfis))]
    public void ARecusaNaoContaOQueNaoVaiMostrar(string quem, PerfilDeQuemFala perfil)
    {
        // Mesma regra do comprador: "nao posso te falar do faturamento" ja entrega que existe um.
        var recusa = perfil.Recusa();

        foreach (var palavra in new[] { "faturamento", "vendas", "estoque", "cadastro" })
            Assert.False(
                recusa.Contains(palavra, StringComparison.OrdinalIgnoreCase),
                $"a recusa de {quem} conta que existe \"{palavra}\": {recusa}");
    }
}
