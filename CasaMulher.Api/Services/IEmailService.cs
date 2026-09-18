namespace CasaMulher.Api.Services;

public sealed record ResultadoEnvioEmail(bool Enviado, string Status)
{
    public static ResultadoEnvioEmail EnviadoComSucesso()
    {
        return new ResultadoEnvioEmail(true, "Enviado");
    }

    public static ResultadoEnvioEmail Simulado()
    {
        return new ResultadoEnvioEmail(false, "Simulado");
    }
}

public interface IEmailService
{
    Task<ResultadoEnvioEmail> EnviarAsync(string destinatario, string assunto, string corpoHtml, string tipo);
}
