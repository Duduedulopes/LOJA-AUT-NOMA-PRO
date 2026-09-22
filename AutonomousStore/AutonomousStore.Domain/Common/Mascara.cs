namespace AutonomousStore.Domain.Common;

/// <summary>
/// Esconde o que é pessoal, deixando o suficiente para reconhecer. É o que o
/// técnico de suporte enxerga no lugar do dado de verdade: dá para dizer "é a
/// Maria do e-mail que termina em @gmail.com", e não dá para usar o dado.
///
/// Roda no SERVIDOR, antes de a resposta sair. Máscara feita na tela seria
/// enfeite: o dado completo já teria viajado pela rede até o navegador do técnico.
/// </summary>
public static class Mascara
{
    private const string Escondido = "***";

    /// <summary>"maria.silva@gmail.com" → "m***@gmail.com".</summary>
    public static string? Email(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return email;

        var texto = email.Trim();
        var arroba = texto.LastIndexOf('@');

        // Sem arroba nao e e-mail que se possa mascarar pela regra: esconde tudo.
        if (arroba < 1) return Escondido;

        return texto[0] + Escondido + texto[arroba..];
    }

    /// <summary>"529.982.247-25" (ou só os dígitos) → "***.***.***-25".</summary>
    public static string? Cpf(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf)) return cpf;

        var digitos = Digitos(cpf);
        return digitos.Length == 11 ? $"***.***.***-{digitos[^2..]}" : Escondido;
    }

    /// <summary>"(11) 99999-1234" (ou só os dígitos) → "(**) *****-1234".</summary>
    public static string? Telefone(string? telefone)
    {
        if (string.IsNullOrWhiteSpace(telefone)) return telefone;

        var digitos = Digitos(telefone);
        return digitos.Length >= 8 ? $"(**) *****-{digitos[^4..]}" : Escondido;
    }

    private static string Digitos(string texto) => new(texto.Where(char.IsDigit).ToArray());
}
