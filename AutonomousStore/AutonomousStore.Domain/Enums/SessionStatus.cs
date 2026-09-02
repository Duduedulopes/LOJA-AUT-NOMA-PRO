using System.Text.Json.Serialization;

namespace AutonomousStore.Domain.Enums;

// ──────────────────────────────────────────────────────────────────────
//  ENUM ATRAVESSA O FIO COMO TEXTO. AQUI, E NAO EM CADA LADO.
//
//  O QUE QUEBROU, PARA NAO QUEBRAR DE NOVO.
//
//  A WebApi ganhou um `JsonStringEnumConverter` global quando as
//  ocorrencias nasceram, para que o painel lesse "Roubo" em vez de 9. So
//  que o conversor vale para TODO enum que sai de la — inclusive este.
//  A partir daquele dia a WebApi passou a mandar `"status":"Aberta"`, e
//  nenhum cliente sabia ler: o `ReadFromJsonAsync` do Blazor usa as opcoes
//  padrao, que nao tem o conversor. O app do cliente estourava assim que
//  entrava, ao buscar a sessao ativa.
//
//  POR QUE O CONSERTO E NO ENUM E NAO NOS CLIENTES.
//
//  Registrar o conversor em cada aplicativo seriam quatro lugares para
//  lembrar, e o quinto aplicativo nasceria quebrado. O atributo AQUI vale
//  para os dois lados de uma vez, porque servidor e clientes compartilham
//  este mesmo projeto: quem serializa e quem le concordam por construcao,
//  e nao por disciplina.
// ──────────────────────────────────────────────────────────────────────
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SessionStatus
{
    AguardandoEntrada = 1,
    Aberta = 2,
    AguardandoPagamento = 3,
    Concluida = 4,
    Cancelada = 5
}
