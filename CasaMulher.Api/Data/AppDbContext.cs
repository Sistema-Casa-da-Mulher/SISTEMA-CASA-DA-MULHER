using CasaMulher.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CasaMulher.Api.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<FuncionarioConvite> FuncionariosConvites => Set<FuncionarioConvite>();

    public DbSet<EquipeConvite> EquipeConvites => Set<EquipeConvite>();

    public DbSet<EquipeMembro> EquipeMembros => Set<EquipeMembro>();

    public DbSet<EquipeSenhaReset> EquipeSenhaResets => Set<EquipeSenhaReset>();

    public DbSet<AuditoriaEvento> AuditoriaEventos => Set<AuditoriaEvento>();

    public DbSet<EmailEvento> EmailEventos => Set<EmailEvento>();

    public DbSet<PasskeyCredential> PasskeyCredentials => Set<PasskeyCredential>();

    public DbSet<PasskeyChallenge> PasskeyChallenges => Set<PasskeyChallenge>();

    public DbSet<PasskeyReconfirmacao> PasskeyReconfirmacoes => Set<PasskeyReconfirmacao>();

    public DbSet<UserLoginIdentifier> UserLoginIdentifiers => Set<UserLoginIdentifier>();

    public DbSet<RecuperacaoSegurancaToken> RecuperacaoSegurancaTokens => Set<RecuperacaoSegurancaToken>();

    public DbSet<GitHubOAuthState> GitHubOAuthStates => Set<GitHubOAuthState>();

    public DbSet<GitHubUsuarioVinculo> GitHubUsuarioVinculos => Set<GitHubUsuarioVinculo>();

    public DbSet<TermoSigiloAceite> TermoSigiloAceites => Set<TermoSigiloAceite>();

    public DbSet<Assistida> Assistidas => Set<Assistida>();

    public DbSet<CasoJuridico> CasosJuridicos => Set<CasoJuridico>();

    public DbSet<CasoJuridicoResponsavel> CasosJuridicosResponsaveis => Set<CasoJuridicoResponsavel>();

    public DbSet<AtendimentoJuridico> AtendimentosJuridicos => Set<AtendimentoJuridico>();

    public DbSet<AtendimentoJuridicoAdendo> AtendimentosJuridicosAdendos => Set<AtendimentoJuridicoAdendo>();

    public DbSet<CasoJuridicoAcessoEmergencia> CasosJuridicosAcessosEmergencia => Set<CasoJuridicoAcessoEmergencia>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.HasIndex(user => user.IdentificadorFuncionario)
                .IsUnique();

            entity.Property(user => user.NomeCompleto)
                .HasMaxLength(160)
                .IsRequired();

            entity.Property(user => user.Perfil)
                .HasMaxLength(40)
                .IsRequired();

            entity.Property(user => user.IdentificadorFuncionario)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(user => user.EmailRecuperacao)
                .HasMaxLength(256);
        });

        builder.Entity<UserLoginIdentifier>(entity =>
        {
            entity.ToTable("UserLoginIdentifiers");

            entity.HasIndex(identifier => identifier.Identificador)
                .IsUnique();

            entity.HasIndex(identifier => identifier.UserId);

            entity.Property(identifier => identifier.UserId)
                .HasMaxLength(80)
                .IsRequired();

            entity.Property(identifier => identifier.Identificador)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(identifier => identifier.Tipo)
                .HasMaxLength(20)
                .IsRequired();

            entity.HasOne(identifier => identifier.User)
                .WithMany(user => user.LoginIdentifiers)
                .HasForeignKey(identifier => identifier.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FuncionarioConvite>(entity =>
        {
            entity.ToTable("FuncionariosConvites");

            entity.HasIndex(convite => convite.CodigoHash)
                .IsUnique();

            entity.HasIndex(convite => convite.Email);

            entity.Property(convite => convite.NomeCompleto)
                .HasMaxLength(160)
                .IsRequired();

            entity.Property(convite => convite.Email)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(convite => convite.Perfil)
                .HasMaxLength(40)
                .IsRequired();

            entity.Property(convite => convite.IdentificadorFuncionario)
                .HasMaxLength(20)
                .IsRequired();

            entity.HasIndex(convite => convite.IdentificadorFuncionario);

            entity.Property(convite => convite.CodigoHash)
                .HasMaxLength(128)
                .IsRequired();
        });

        builder.Entity<EquipeConvite>(entity =>
        {
            entity.ToTable("EquipeConvites");

            entity.HasIndex(convite => convite.CodigoEquipe)
                .IsUnique();

            entity.HasIndex(convite => convite.CodigoAtivacaoHash)
                .IsUnique();

            entity.HasIndex(convite => convite.Status);

            entity.Property(convite => convite.CodigoEquipe)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(convite => convite.CodigoAtivacaoHash)
                .HasMaxLength(128)
                .IsRequired();

            entity.Property(convite => convite.Status)
                .HasMaxLength(40)
                .IsRequired();

            entity.Property(convite => convite.CriadoPorUserId)
                .HasMaxLength(80);

            entity.Property(convite => convite.UsadoPorUserId)
                .HasMaxLength(80);

            entity.Property(convite => convite.NomeInformado)
                .HasMaxLength(160);

            entity.Property(convite => convite.PapelEquipe)
                .HasMaxLength(40)
                .IsRequired();

            entity.Property(convite => convite.FluxoTrabalho)
                .HasMaxLength(40)
                .IsRequired();

            entity.Property(convite => convite.Observacao)
                .HasMaxLength(500);
        });

        builder.Entity<EquipeMembro>(entity =>
        {
            entity.ToTable("EquipeMembros");

            entity.HasIndex(membro => membro.UserId)
                .IsUnique();

            entity.HasIndex(membro => membro.CodigoEquipe)
                .IsUnique();

            entity.Property(membro => membro.UserId)
                .HasMaxLength(80)
                .IsRequired();

            entity.Property(membro => membro.CodigoEquipe)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(membro => membro.Nome)
                .HasMaxLength(160)
                .IsRequired();

            entity.Property(membro => membro.PapelEquipe)
                .HasMaxLength(40)
                .IsRequired();

            entity.Property(membro => membro.FluxoTrabalho)
                .HasMaxLength(40)
                .IsRequired();

            entity.Property(membro => membro.GitHubUsername)
                .HasMaxLength(80);

            entity.Property(membro => membro.GitHubId)
                .HasMaxLength(80);

            entity.Property(membro => membro.ForkUrl)
                .HasMaxLength(300);
        });

        builder.Entity<EquipeSenhaReset>(entity =>
        {
            entity.ToTable("EquipeSenhaResets");

            entity.HasIndex(reset => reset.CodigoHash)
                .IsUnique();

            entity.HasIndex(reset => reset.CodigoEquipe);
            entity.HasIndex(reset => reset.ExpiraEm);

            entity.Property(reset => reset.CodigoEquipe)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(reset => reset.CodigoHash)
                .HasMaxLength(128)
                .IsRequired();

            entity.Property(reset => reset.GeradoPorUserId)
                .HasMaxLength(80)
                .IsRequired();
        });

        builder.Entity<AuditoriaEvento>(entity =>
        {
            entity.ToTable("AuditoriaEventos");

            entity.HasIndex(evento => evento.CriadoEm);
            entity.HasIndex(evento => evento.UsuarioId);
            entity.HasIndex(evento => evento.EntidadeId);

            entity.Property(evento => evento.UsuarioId).HasMaxLength(80);
            entity.Property(evento => evento.IdentificadorFuncionario).HasMaxLength(20);
            entity.Property(evento => evento.NomeFuncionario).HasMaxLength(160);
            entity.Property(evento => evento.PerfilFuncionario).HasMaxLength(40);
            entity.Property(evento => evento.Escopo).HasMaxLength(20).IsRequired();
            entity.HasIndex(evento => evento.Escopo);
            entity.Property(evento => evento.Acao).HasMaxLength(80).IsRequired();
            entity.Property(evento => evento.Entidade).HasMaxLength(80).IsRequired();
            entity.Property(evento => evento.EntidadeId).HasMaxLength(80);
            entity.Property(evento => evento.Descricao).HasMaxLength(500).IsRequired();
            entity.Property(evento => evento.IpOrigem).HasMaxLength(80);
            entity.Property(evento => evento.UserAgent).HasMaxLength(500);
        });

        builder.Entity<EmailEvento>(entity =>
        {
            entity.ToTable("EmailEventos");

            entity.HasIndex(evento => evento.CriadoEm);
            entity.HasIndex(evento => evento.Destinatario);
            entity.HasIndex(evento => evento.Tipo);
            entity.HasIndex(evento => evento.Status);

            entity.Property(evento => evento.Destinatario).HasMaxLength(256).IsRequired();
            entity.Property(evento => evento.Assunto).HasMaxLength(200).IsRequired();
            entity.Property(evento => evento.Tipo).HasMaxLength(80).IsRequired();
            entity.Property(evento => evento.Status).HasMaxLength(40).IsRequired();
            entity.Property(evento => evento.Erro).HasMaxLength(500);
        });

        builder.Entity<PasskeyCredential>(entity =>
        {
            entity.ToTable("PasskeyCredentials");

            entity.HasIndex(c => c.CredentialId).IsUnique();
            entity.HasIndex(c => c.UserId);

            entity.Property(c => c.UserId)
                .HasMaxLength(80)
                .IsRequired();

            entity.Property(c => c.NomeDispositivo)
                .HasMaxLength(120);

            entity.Property(c => c.Transports)
                .HasMaxLength(200);

            entity.Property(c => c.RpId)
                .HasMaxLength(253)
                .IsRequired();

            entity.Property(c => c.Origin)
                .HasMaxLength(253);

            entity.Property(c => c.CreatedEnvironment)
                .HasMaxLength(40);

            entity.HasOne(c => c.User)
                .WithMany(u => u.PasskeyCredentials)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PasskeyChallenge>(entity =>
        {
            entity.ToTable("PasskeyChallenges");

            entity.HasIndex(c => c.ChallengeId).IsUnique();
            entity.HasIndex(c => c.ExpiracaoEm);

            entity.Property(c => c.ChallengeId)
                .HasMaxLength(80)
                .IsRequired();

            entity.Property(c => c.Tipo)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(c => c.UserId)
                .HasMaxLength(80);

            entity.Property(c => c.ContextoPerfil)
                .HasMaxLength(20);

            entity.Property(c => c.ContextoIdentificador)
                .HasMaxLength(20);

            entity.Property(c => c.OptionsJson)
                .IsRequired();
        });

        builder.Entity<PasskeyReconfirmacao>(entity =>
        {
            entity.ToTable("PasskeyReconfirmacoes");

            entity.HasIndex(r => r.ReconfirmacaoId).IsUnique();
            entity.HasIndex(r => r.ExpiracaoEm);

            entity.Property(r => r.ReconfirmacaoId)
                .HasMaxLength(80)
                .IsRequired();

            entity.Property(r => r.UserId)
                .HasMaxLength(80)
                .IsRequired();
        });
        builder.Entity<RecuperacaoSegurancaToken>(entity =>
        {
            entity.ToTable("RecuperacaoSegurancaTokens");
            entity.HasIndex(t => t.TokenHash).IsUnique();
            entity.HasIndex(t => t.FuncionarioId);
            entity.HasIndex(t => t.ExpiraEm);

            entity.Property(t => t.TokenHash).IsRequired().HasMaxLength(256);
            entity.Property(t => t.Tipo).IsRequired().HasMaxLength(50);
            entity.Property(t => t.EmailDestino).HasMaxLength(256);
        });

        builder.Entity<TermoSigiloAceite>(entity =>
        {
            entity.ToTable("TermoSigiloAceites");
            entity.HasIndex(t => new { t.UserId, t.VersaoTermo });
            entity.HasIndex(t => t.AceitoEm);

            entity.Property(t => t.UserId).IsRequired().HasMaxLength(450);
            entity.Property(t => t.IdentificadorFuncionario).IsRequired().HasMaxLength(80);
            entity.Property(t => t.Perfil).IsRequired().HasMaxLength(50);
            entity.Property(t => t.VersaoTermo).IsRequired().HasMaxLength(40);
            entity.Property(t => t.HashTermo).IsRequired().HasMaxLength(128);
            entity.Property(t => t.NomeAssinado).IsRequired().HasMaxLength(256);
            entity.Property(t => t.EnderecoIp).HasMaxLength(64);
            entity.Property(t => t.UserAgent).HasMaxLength(512);

            // Sem cascata: o registro do aceite é prova e não pode sumir junto com a conta.
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Assistida>(entity =>
        {
            entity.ToTable("Assistidas");
            entity.HasIndex(a => a.Cpf).IsUnique();
            entity.HasIndex(a => a.Nome);
            entity.HasIndex(a => a.EncaminhadaJuridicoEm);

            entity.Property(a => a.Nome).IsRequired().HasMaxLength(160);
            entity.Property(a => a.Cpf).IsRequired().HasMaxLength(11);
            entity.Property(a => a.Telefone).HasMaxLength(20);
            entity.Property(a => a.ContatoSeguro).IsRequired().HasMaxLength(20);
            entity.Property(a => a.ContatoSeguroObservacao).HasMaxLength(300);
            entity.Property(a => a.Email).HasMaxLength(256);
            entity.Property(a => a.Cep).HasMaxLength(8);
            entity.Property(a => a.Endereco).HasMaxLength(200);
            entity.Property(a => a.EnderecoNumero).HasMaxLength(60);
            entity.Property(a => a.Bairro).HasMaxLength(120);
            entity.Property(a => a.Cidade).HasMaxLength(120);
            entity.Property(a => a.Uf).HasMaxLength(2);
            entity.Property(a => a.Direcionamento).HasMaxLength(60);
            entity.Property(a => a.Observacoes).HasMaxLength(1000);
            entity.Property(a => a.Origem).IsRequired().HasMaxLength(20);
            entity.Property(a => a.CadastradaPorUserId).IsRequired().HasMaxLength(450);
            entity.Property(a => a.CadastradaPorIdentificador).IsRequired().HasMaxLength(80);
            entity.Property(a => a.CadastradaPorNome).IsRequired().HasMaxLength(160);
            entity.Property(a => a.CadastradaPorEmail).HasMaxLength(256);
            entity.Property(a => a.MotivoRestricao).HasMaxLength(40);
            entity.Property(a => a.RestricaoAlteradaPorIdentificador).HasMaxLength(80);
        });

        builder.Entity<CasoJuridico>(entity =>
        {
            entity.ToTable("CasosJuridicos");
            entity.HasIndex(c => c.AssistidaId).IsUnique();
            entity.Property(c => c.Visibilidade).IsRequired().HasMaxLength(20);
            entity.Property(c => c.Situacao).IsRequired().HasMaxLength(30);

            // Restrict: o histórico jurídico nunca some junto com o cadastro.
            entity.HasOne(c => c.Assistida)
                .WithMany()
                .HasForeignKey(c => c.AssistidaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CasoJuridicoResponsavel>(entity =>
        {
            entity.ToTable("CasosJuridicosResponsaveis");
            entity.HasIndex(r => new { r.CasoJuridicoId, r.UserId }).IsUnique();
            entity.HasIndex(r => r.UserId);
            entity.Property(r => r.UserId).IsRequired().HasMaxLength(450);
            entity.Property(r => r.Identificador).IsRequired().HasMaxLength(80);
            entity.Property(r => r.Nome).IsRequired().HasMaxLength(160);
            entity.Property(r => r.AtribuidoPorIdentificador).IsRequired().HasMaxLength(80);

            entity.HasOne(r => r.Caso)
                .WithMany(c => c.Responsaveis)
                .HasForeignKey(r => r.CasoJuridicoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AtendimentoJuridico>(entity =>
        {
            entity.ToTable("AtendimentosJuridicos");
            entity.HasIndex(a => a.Protocolo).IsUnique();
            entity.HasIndex(a => new { a.Ano, a.Sequencial }).IsUnique();
            entity.HasIndex(a => a.CriadoEm);
            entity.Property(a => a.Protocolo).IsRequired().HasMaxLength(20);
            entity.Property(a => a.ProfissionalUserId).IsRequired().HasMaxLength(450);
            entity.Property(a => a.ProfissionalIdentificador).IsRequired().HasMaxLength(80);
            entity.Property(a => a.ProfissionalNome).IsRequired().HasMaxLength(160);
            entity.Property(a => a.TipoDemanda).IsRequired().HasMaxLength(40);
            entity.Property(a => a.TipoDemandaOutro).HasMaxLength(120);
            entity.Property(a => a.NumeroProcesso).HasMaxLength(40);
            entity.Property(a => a.Vara).HasMaxLength(120);
            entity.Property(a => a.Encaminhamentos).HasMaxLength(200);
            entity.Property(a => a.EncaminhamentoOutro).HasMaxLength(120);
            entity.Property(a => a.Relato).IsRequired().HasMaxLength(5000);
            entity.Property(a => a.ProximoPasso).HasMaxLength(500);
            entity.Property(a => a.Situacao).IsRequired().HasMaxLength(30);

            entity.HasOne(a => a.Caso)
                .WithMany(c => c.Atendimentos)
                .HasForeignKey(a => a.CasoJuridicoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AtendimentoJuridicoAdendo>(entity =>
        {
            entity.ToTable("AtendimentosJuridicosAdendos");
            entity.Property(a => a.Texto).IsRequired().HasMaxLength(2000);
            entity.Property(a => a.AutorUserId).IsRequired().HasMaxLength(450);
            entity.Property(a => a.AutorIdentificador).IsRequired().HasMaxLength(80);
            entity.Property(a => a.AutorNome).IsRequired().HasMaxLength(160);

            entity.HasOne(a => a.Atendimento)
                .WithMany(a => a.Adendos)
                .HasForeignKey(a => a.AtendimentoJuridicoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CasoJuridicoAcessoEmergencia>(entity =>
        {
            entity.ToTable("CasosJuridicosAcessosEmergencia");
            entity.HasIndex(a => new { a.CasoJuridicoId, a.UserId });
            entity.Property(a => a.UserId).IsRequired().HasMaxLength(450);
            entity.Property(a => a.Identificador).IsRequired().HasMaxLength(80);
            entity.Property(a => a.Nome).IsRequired().HasMaxLength(160);
            entity.Property(a => a.Motivo).IsRequired().HasMaxLength(500);

            entity.HasOne(a => a.Caso)
                .WithMany()
                .HasForeignKey(a => a.CasoJuridicoId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
