using System.Security.Claims;
using AutonomousStore.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AutonomousStore.WebApi.Hubs;

/// <summary>
/// O canal aberto de um chamado: por onde a mensagem chega sem ninguem
/// precisar perguntar.
/// </summary>
/// <remarks>
/// POR QUE ISTO EXISTE. Ate aqui, o navegador so sabia o que tinha ido buscar.
/// A resposta do tecnico ficava gravada no banco e o cliente olhava uma tela
/// que dizia a verdade de dez minutos atras. Fechar e reabrir a conversa
/// "resolvia" porque era o unico momento em que alguem perguntava de novo.
///
/// UM GRUPO POR CHAMADO, e nao um canal unico com filtro no cliente. Filtrar
/// no navegador significa MANDAR para todo mundo e pedir que os errados
/// ignorem — e conversa de suporte tem nome, e-mail e o problema da pessoa
/// dentro. O que nao deve chegar, nao sai daqui.
///
/// A AUTORIZACAO E A MESMA DA ROTA HTTP, e de proposito: `PodeConversar`, a
/// mesma regra, na mesma entidade. Se este arquivo tivesse a sua propria
/// versao da condicao, bastaria as duas discordarem um dia para a que estiver
/// errada ser a que vaza.
/// </remarks>
[Authorize]
public class ChamadoHub : Hub
{
    private readonly IOcorrenciaRepository _ocorrencias;

    public ChamadoHub(IOcorrenciaRepository ocorrencias) => _ocorrencias = ocorrencias;

    /// <summary>O nome do grupo de um chamado. O controlador usa o mesmo.</summary>
    /// <remarks>
    /// Publico e estatico para que exista UM lugar que decide este nome. Se o
    /// controlador montasse a string por conta propria, um erro de digitacao
    /// nao daria erro nenhum: a mensagem seria enviada para um grupo vazio e
    /// simplesmente nao chegaria, sem nada no log.
    /// </remarks>
    public static string Grupo(Guid chamadoId) => $"chamado-{chamadoId}";

    /// <summary>
    /// Os chamados que ESTA conexao provou poder ver.
    /// </summary>
    /// <remarks>
    /// Vive na conexao e morre com ela — nada disto encosta no banco.
    ///
    /// Serve ao `Digitando`: sem esta lista, qualquer um que descobrisse o id
    /// de um chamado poderia despejar "fulano esta digitando" na conversa dos
    /// outros, porque `OthersInGroup` entrega ao grupo sem perguntar se quem
    /// mandou pertence a ele.
    /// </remarks>
    private HashSet<Guid> Autorizados
    {
        get
        {
            if (Context.Items["chamados"] is not HashSet<Guid> jaTem)
            {
                jaTem = new HashSet<Guid>();
                Context.Items["chamados"] = jaTem;
            }

            return jaTem;
        }
    }

    /// <summary>Passa a ouvir um chamado — se puder ve-lo.</summary>
    public async Task Entrar(Guid chamadoId)
    {
        var chamado = await _ocorrencias.ObterComConversaAsync(chamadoId, Context.ConnectionAborted);

        // Mesma mensagem para "nao existe" e "nao e seu", pelo mesmo motivo do
        // 404 na rota HTTP: um erro diferente confirmaria que o chamado
        // existe, e o id e a unica coisa que separa um chamado de outro.
        if (chamado is null || !chamado.PodeConversar(Email(), EhDaCasa()))
            throw new HubException("Chamado não encontrado.");

        Autorizados.Add(chamadoId);
        await Groups.AddToGroupAsync(Context.ConnectionId, Grupo(chamadoId), Context.ConnectionAborted);
    }

    /// <summary>Para de ouvir. A pessoa trocou de chamado ou fechou a tela.</summary>
    public async Task Sair(Guid chamadoId)
    {
        Autorizados.Remove(chamadoId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, Grupo(chamadoId), Context.ConnectionAborted);
    }

    /// <summary>"Estou escrevendo agora." Nao vira linha em lugar nenhum.</summary>
    /// <remarks>
    /// ISTO E ESTADO DE TRES SEGUNDOS, e por isso nao chega perto do banco.
    /// Gravar cada tecla numa tabela encheria de lixo permanente justamente a
    /// tabela que o Eduardo usa para enxergar a loja — e o dado nao vale nada
    /// no segundo seguinte.
    ///
    /// Quem apaga o aviso e o RELOGIO DE QUEM RECEBE, nao um "parei de
    /// digitar" enviado daqui. Se dependesse de uma segunda mensagem, fechar a
    /// aba no meio de uma frase deixaria "fulano esta digitando" na tela do
    /// outro para sempre.
    /// </remarks>
    public async Task Digitando(Guid chamadoId)
    {
        if (!Autorizados.Contains(chamadoId)) return;

        await Clients.OthersInGroup(Grupo(chamadoId))
            .SendAsync("Digitando", Nome(), Papel(), Context.ConnectionAborted);
    }

    // ── quem esta do outro lado do token ─────────────────────────────────
    //
    // Repetido do ChamadosController de proposito: sao dois jeitos de entrar
    // (HTTP e WebSocket) e cada um le o seu proprio `User`. O que NAO pode se
    // repetir e a regra de quem ve o que — essa mora na entidade.

    private bool EhDaCasa() => Context.User?.IsInRole("Admin") == true
                            || Context.User?.IsInRole("Suporte") == true;

    /// <remarks>
    /// Duas chaves porque o JwtBearer mapeia os nomes curtos do token para as
    /// URIs longas do WS-Federation quando `MapInboundClaims` esta no padrao.
    /// </remarks>
    private string? Email()
        => Context.User?.FindFirstValue(ClaimTypes.Email)
           ?? Context.User?.FindFirstValue("email");

    private string Nome()
        => Context.User?.FindFirstValue(ClaimTypes.Name)
           ?? Context.User?.FindFirstValue("name")
           ?? Email()
           ?? "alguém";

    private string Papel()
        => Context.User?.IsInRole("Suporte") == true ? "Suporte"
         : Context.User?.IsInRole("Admin") == true ? "Admin"
         : "Cliente";
}
