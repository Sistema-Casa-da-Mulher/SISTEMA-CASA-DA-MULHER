namespace CasaMulher.Api.Security;

public static class PerfisAcesso
{
    public const string Adm = "adm";
    public const string Recepcao = "recepcao";
    public const string Professor = "professor";
    public const string AssistenteSocial = "as_social";
    public const string Juridico = "juridico";
    public const string Psicologia = "psicologia";
    public const string Equipe = "equipe";

    public static readonly string[] Todos =
    [
        Adm,
        Recepcao,
        Professor,
        AssistenteSocial,
        Juridico,
        Psicologia,
        Equipe
    ];

    public static readonly string[] FuncionariosInstitucionais =
    [
        Adm,
        Recepcao,
        Professor,
        AssistenteSocial,
        Juridico,
        Psicologia
    ];

    public static bool EhValido(string perfil)
    {
        return Todos.Contains(perfil, StringComparer.OrdinalIgnoreCase);
    }

    public static bool EhFuncionarioInstitucionalValido(string perfil)
    {
        return FuncionariosInstitucionais.Contains(perfil, StringComparer.OrdinalIgnoreCase);
    }
}
