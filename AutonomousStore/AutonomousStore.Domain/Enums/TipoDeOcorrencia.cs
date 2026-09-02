using System.Text.Json.Serialization;

namespace AutonomousStore.Domain.Enums;

/// <summary>
/// O que deu errado. Cada valor aqui SO EXISTE se houver um detector que
/// saiba prova-lo.
/// </summary>
/// <remarks>
/// A LISTA COMECOU MAIOR E ENCOLHEU DE PROPOSITO.
///
/// Sairam "erro sintatico" (um sistema em execucao nao tem: ele nao teria
/// compilado), "erro de logica" (quem acha isso e teste e revisao, nao o
/// programa se olhando) e "contradicao entre regras de negocio" (as regras
/// ainda nao existem como dado, so como codigo).
///
/// O motivo de tirar nao e purismo. Um tipo sem detector cria uma consulta
/// que nunca devolve nada, e uma tela que nunca acusa nada ensina o Chefe a
/// confiar no que jamais foi conferido. Este projeto ja tem um caso vivo
/// disso: o botao de desambiguacao promete "seu clique me ensina", o clique
/// e gravado em `perguntas_reais.jsonl`, e nenhum programa le esse arquivo de
/// volta para o treino.
///
/// Tipo novo entra junto com o detector dele, nunca antes.
/// </remarks>
// Texto no fio, e nao numero. O porque esta em SessionStatus.cs.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TipoDeOcorrencia
{
    /// <summary>Excecao nao tratada.</summary>
    ErroExecucao = 1,

    /// <summary>Dado inconsistente, duplicado ou ausente.</summary>
    ErroDados = 2,

    /// <summary>Fora da linha de base medida.</summary>
    Anomalia = 3,

    /// <summary>Dois dados que nao podem ser verdade ao mesmo tempo.</summary>
    Contradicao = 4,

    /// <summary>Chamada que nao voltou, ou voltou erro.</summary>
    FalhaApi = 5,

    /// <summary>Operacao executada fora da sequencia esperada.</summary>
    FalhaWorkflow = 6,

    /// <summary>Contrato quebrado entre modulos.</summary>
    FalhaIntegracao = 7,

    /// <summary>
    /// O SISTEMA NAO VIU. Produto saiu da prateleira e nenhuma camera pegou,
    /// ou estava fora do alcance. Nao e crime: e limitacao de instalacao, e
    /// so aparece na contagem fisica.
    /// </summary>
    FuroDeCobertura = 8,

    /// <summary>
    /// O RFID DA PORTA VIU SAIR SEM PAGAMENTO. E a unica que acusa uma
    /// pessoa, entao o registro guarda o que a leitora leu e quando —
    /// nunca uma conclusao sobre quem.
    /// </summary>
    Roubo = 9,

    /// <summary>
    /// O SOFTWARE PERDEU A CONTA. Ninguem fez nada errado; o programa
    /// errou. Hoje o caso vivo e o `Cancel` que nao devolve estoque.
    /// </summary>
    FuroDeSistema = 10,

    /// <summary>Alguém perguntou como se faz alguma coisa.</summary>
    /// <remarks>
    /// NEM TUDO QUE CHEGA AO SUPORTE E DEFEITO, E TRATAR IGUAL ATRAPALHA OS
    /// DOIS LADOS. Uma duvida com cara de erro entra na fila com a mesma
    /// urgencia de um roubo; um erro com cara de duvida espera na fila do
    /// "quando der". Sao coisas diferentes e por isso tem tipo diferente.
    ///
    /// Diferente de todos os outros tipos, este NAO nasce de um detector:
    /// nasce de alguem escrevendo. E o unico que tem conversa.
    /// </remarks>
    Duvida = 11,

    /// <summary>Alguém pediu para mudar alguma coisa no sistema.</summary>
    /// <remarks>
    /// Separado da duvida de proposito. "Como eu mudo o preco?" se resolve
    /// respondendo; "quero que o preco mude sozinho na sexta" e trabalho que
    /// entra numa lista. Misturar os dois faz o pedido de mudanca virar
    /// resposta rapida e ser esquecido.
    /// </remarks>
    PedidoDeMudanca = 12,
}
