namespace AutonomousStore.Domain.Common;

/// <summary>
/// Os papéis que existem no sistema, do lado do JWT. Um lugar só, para a
/// string "Suporte" não ser digitada em quinze arquivos.
/// </summary>
public static class Papeis
{
    /// <summary>Dono da plataforma. Vê e controla tudo, de todas as empresas.</summary>
    public const string Criador = "Criador";

    /// <summary>
    /// Técnico de suporte da plataforma. Não pertence a nenhuma empresa: atende
    /// todas. (O nome "Suporte" já existia no código e nos tokens em uso.)
    /// </summary>
    public const string Suporte = "Suporte";

    /// <summary>Administrador de uma empresa assinante. Só enxerga a própria empresa.</summary>
    public const string Admin = "Admin";

    /// <summary>Comprador. Pertence a uma empresa só.</summary>
    public const string Comprador = "Comprador";

    /// <summary>Quem opera a plataforma — para <c>[Authorize(Roles = Papeis.DaPlataforma)]</c>.</summary>
    public const string DaPlataforma = Criador + "," + Suporte;

    /// <summary>Nome da claim do JWT que carrega a empresa de quem entrou.</summary>
    public const string ClaimEmpresa = "tenant_id";

    /// <summary>Cabeçalho em que o app diz de qual empresa é, antes do login.</summary>
    public const string CabecalhoEmpresa = "X-Empresa";
}
