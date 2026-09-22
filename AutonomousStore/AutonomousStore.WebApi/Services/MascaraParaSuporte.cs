using System.Text.Json;
using System.Text.Json.Nodes;
using AutonomousStore.Domain.Common;

namespace AutonomousStore.WebApi.Services;

/// <summary>
/// A máscara aplicada ao que os chamados carregam, para o técnico. Os chamados
/// guardam e-mail e nome de quem pediu ajuda em texto puro (o campo
/// <c>AbertoPor</c> e o JSON de <c>DadosEnvolvidos</c>): sem passar por aqui, o
/// técnico leria o e-mail inteiro de qualquer pessoa que abriu um pedido.
/// </summary>
public static class MascaraParaSuporte
{
    /// <summary>
    /// Os campos, dentro do JSON de detalhes técnicos, que são e-mail de alguém.
    /// Só o pedido ao suporte grava um (<c>quemEmail</c>); os demais detectores gravam
    /// tag de RFID, id de sessão e nome de produto, que não identificam pessoa.
    /// </summary>
    private static readonly string[] CamposDeEmail = ["quemEmail"];

    public static string? Email(string? email) => Mascara.Email(email);

    /// <summary>
    /// Devolve o JSON com os e-mails mascarados. JSON que não dá para ler volta como está:
    /// é gerado pelo próprio sistema, e esconder um detalhe técnico que ninguém consegue
    /// ler não protegeria ninguém.
    /// </summary>
    public static string? DadosEnvolvidos(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return json;

        try
        {
            if (JsonNode.Parse(json) is not JsonObject objeto) return json;

            foreach (var campo in CamposDeEmail)
            {
                if (objeto[campo] is JsonValue valor && valor.TryGetValue<string>(out var email))
                    objeto[campo] = Mascara.Email(email);
            }

            return objeto.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        }
        catch (JsonException)
        {
            return json;
        }
    }
}
