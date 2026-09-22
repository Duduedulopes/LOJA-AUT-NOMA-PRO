using AutonomousStore.Domain.Entities;

namespace AutonomousStore.ClientApp.Services;

/// <summary>
/// De qual EMPRESA é a loja em que o comprador está. Vem do link ou do QR code da loja
/// (<c>?empresa=redesabor</c>) e vai em toda chamada à API, no cabeçalho <c>X-Empresa</c> — é assim que
/// o cadastro e o login do comprador sabem em qual empresa acontecem.
///
/// Só serve para identificar a empresa ANTES do login. Depois dele, quem manda é o token: o servidor
/// ignora este cabeçalho quando há um token com empresa, então um comprador não vira "comprador de
/// outra empresa" mexendo nele.
/// </summary>
public sealed class EmpresaDaLoja
{
    /// <summary>Onde o navegador lembra da empresa, para o recarregar da página não a esquecer.</summary>
    public const string ChaveNoNavegador = "empresa";

    /// <summary>O identificador da empresa ("redesabor"), ou nulo quando o app foi aberto sem link de loja.</summary>
    public string? Slug { get; private set; }

    /// <summary>
    /// Aceita o identificador se ele tem cara de identificador de empresa. A mesma regra do servidor
    /// (<see cref="Tenant.NormalizarSlug"/>): um valor qualquer vindo da barra de endereço não deve
    /// virar cabeçalho de requisição.
    /// </summary>
    /// <returns>Verdadeiro se o identificador foi aceito e guardado.</returns>
    public bool Definir(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return false;

        try
        {
            Slug = Tenant.NormalizarSlug(slug);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
