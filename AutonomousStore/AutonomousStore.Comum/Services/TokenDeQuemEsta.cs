namespace AutonomousStore.Comum.Services;

/// <summary>
/// O token de quem está logado, perguntado ao app que hospeda este componente.
/// </summary>
/// <remarks>
/// Uma classe e não um `Func` solto no contêiner porque `Func&lt;string?&gt;` é um
/// tipo genérico demais: registrar um significa que o próximo que precisar de
/// uma função que devolve texto vai receber o token sem querer. Um tipo com
/// nome próprio diz o que é e não colide com nada.
/// </remarks>
public class TokenDeQuemEsta
{
    private readonly Func<string?> _ler;

    public TokenDeQuemEsta(Func<string?> ler) => _ler = ler;

    /// <summary>O token de agora — nulo se ninguém está logado.</summary>
    public string? Agora() => _ler();
}
