using CasaMulher.Api.Data;
using CasaMulher.Api.Models;
using CasaMulher.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace CasaMulher.Api.Services;

public sealed record AcessoProntuarioSocial(
    bool AtuaComoServicoSocial,
    bool SouResponsavel,
    bool PodeVerConteudo,
    bool PodeAtribuir,
    DateTime? EmergenciaExpiraEm);

/// <summary>
/// Regras de acesso do Serviço Social, usadas pela tela, pelos anexos e pelos relatórios:
/// - conteúdo (relatos, estudo social, rede, plano e anexos) só para as responsáveis, para toda a
///   equipe do Serviço Social quando a visibilidade é "equipe", ou com acesso de emergência;
/// - as outras assistentes sociais veem que o atendimento existiu;
/// - Jurídico e Psicologia responsáveis pela mesma assistida só baixam anexos marcados como
///   "equipe técnica"; Recepção e Coordenação nunca veem anexos.
/// </summary>
public sealed class ServicoSocialAcessoService
{
    public static readonly TimeSpan DuracaoAcessoEmergencia = TimeSpan.FromHours(12);

    private readonly AppDbContext _dbContext;

    public ServicoSocialAcessoService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AcessoProntuarioSocial> AvaliarAsync(ProntuarioSocial? prontuario, UsuarioAtuante usuario, CancellationToken cancellationToken)
    {
        DateTime? emergencia = null;
        if (prontuario is not null && AssistidasService.AtuaComoServicoSocial(usuario.Perfil))
        {
            var agora = DateTime.UtcNow;
            emergencia = await _dbContext.ProntuariosSociaisAcessosEmergencia.AsNoTracking()
                .Where(a => a.ProntuarioSocialId == prontuario.Id && a.UserId == usuario.UserId && a.ExpiraEm > agora)
                .MaxAsync(a => (DateTime?)a.ExpiraEm, cancellationToken);
        }

        return Avaliar(prontuario, usuario, emergencia);
    }

    /// <summary>Mesma regra da Psicologia: sem prontuário, qualquer assistente social pode abrir com o 1º atendimento.</summary>
    public static AcessoProntuarioSocial Avaliar(ProntuarioSocial? prontuario, UsuarioAtuante usuario, DateTime? emergenciaExpiraEm)
    {
        var social = AssistidasService.AtuaComoServicoSocial(usuario.Perfil);
        var souResponsavel = prontuario?.Responsaveis.Any(r => r.UserId == usuario.UserId) == true;
        var podeVer = social && (prontuario is null
            || prontuario.Visibilidade == ProntuarioVisibilidade.Equipe
            || souResponsavel
            || emergenciaExpiraEm is not null);
        var semResponsavel = prontuario is not null && prontuario.Responsaveis.Count == 0;

        return new AcessoProntuarioSocial(social, souResponsavel, podeVer, podeVer || (social && semResponsavel), emergenciaExpiraEm);
    }

    /// <summary>Jurídico ou Psicologia responsável pela assistida (caso jurídico ou prontuário psicológico).</summary>
    public async Task<bool> ResponsavelTecnicoAsync(int assistidaId, UsuarioAtuante usuario, CancellationToken cancellationToken)
    {
        if (usuario.Perfil is PerfisAcesso.Juridico or PerfisAcesso.Equipe
            && await _dbContext.CasosJuridicosResponsaveis.AsNoTracking()
                .AnyAsync(r => r.UserId == usuario.UserId && r.Caso!.AssistidaId == assistidaId, cancellationToken))
        {
            return true;
        }

        return usuario.Perfil is PerfisAcesso.Psicologia or PerfisAcesso.Equipe
            && await _dbContext.ProntuariosPsicologicosResponsaveis.AsNoTracking()
                .AnyAsync(r => r.UserId == usuario.UserId && r.Prontuario!.AssistidaId == assistidaId, cancellationToken);
    }

    /// <summary>Quem pode listar e baixar um anexo. Recepção e Coordenação nunca.</summary>
    public static bool PodeVerAnexo(AnexoSocial anexo, AcessoProntuarioSocial acesso, bool responsavelTecnico)
    {
        if (anexo.Excluido) return false;
        if (acesso.PodeVerConteudo) return true;
        return anexo.Visibilidade == AnexoSocialVisibilidade.EquipeTecnica && responsavelTecnico;
    }
}
