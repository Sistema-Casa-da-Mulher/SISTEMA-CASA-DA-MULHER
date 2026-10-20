using CasaMulher.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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

    public DbSet<ProntuarioPsicologico> ProntuariosPsicologicos => Set<ProntuarioPsicologico>();

    public DbSet<ProntuarioPsicologicoResponsavel> ProntuariosPsicologicosResponsaveis => Set<ProntuarioPsicologicoResponsavel>();

    public DbSet<SessaoPsicologica> SessoesPsicologicas => Set<SessaoPsicologica>();

    public DbSet<SessaoPsicologicaAdendo> SessoesPsicologicasAdendos => Set<SessaoPsicologicaAdendo>();

    public DbSet<ProntuarioPsicologicoAcessoEmergencia> ProntuariosPsicologicosAcessosEmergencia => Set<ProntuarioPsicologicoAcessoEmergencia>();

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

            entity.Property(user => user.Crp)
                .HasMaxLength(20);
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

            entity.Property(convite => convite.Crp)
                .HasMaxLength(20);

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
            entity.HasIndex(a => a.EncaminhadaPsicologiaEm);

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

        builder.Entity<ProntuarioPsicologico>(entity =>
        {
            entity.ToTable("ProntuariosPsicologicos");
            entity.HasIndex(p => p.AssistidaId).IsUnique();
            entity.HasIndex(p => p.Numero).IsUnique();
            entity.HasIndex(p => new { p.Ano, p.Sequencial }).IsUnique();
            entity.Property(p => p.Numero).IsRequired().HasMaxLength(20);
            entity.Property(p => p.Visibilidade).IsRequired().HasMaxLength(20);
            entity.Property(p => p.Situacao).IsRequired().HasMaxLength(30);
            entity.Property(p => p.MotivoEncerramento).HasMaxLength(40);
            entity.Property(p => p.RiscoAtual).HasMaxLength(20);
            entity.Property(p => p.ConsentimentoPorIdentificador).HasMaxLength(80);

            // Restrict: o prontuário psicológico tem guarda obrigatória e nunca some com o cadastro.
            entity.HasOne(p => p.Assistida)
                .WithMany()
                .HasForeignKey(p => p.AssistidaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProntuarioPsicologicoResponsavel>(entity =>
        {
            entity.ToTable("ProntuariosPsicologicosResponsaveis");
            entity.HasIndex(r => new { r.ProntuarioPsicologicoId, r.UserId }).IsUnique();
            entity.HasIndex(r => r.UserId);
            entity.Property(r => r.UserId).IsRequired().HasMaxLength(450);
            entity.Property(r => r.Identificador).IsRequired().HasMaxLength(80);
            entity.Property(r => r.Nome).IsRequired().HasMaxLength(160);
            entity.Property(r => r.Crp).HasMaxLength(20);
            entity.Property(r => r.AtribuidoPorIdentificador).IsRequired().HasMaxLength(80);

            entity.HasOne(r => r.Prontuario)
                .WithMany(p => p.Responsaveis)
                .HasForeignKey(r => r.ProntuarioPsicologicoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SessaoPsicologica>(entity =>
        {
            entity.ToTable("SessoesPsicologicas");
            entity.HasIndex(s => new { s.ProntuarioPsicologicoId, s.Numero }).IsUnique();
            entity.HasIndex(s => s.CriadoEm);
            entity.Property(s => s.ProfissionalUserId).IsRequired().HasMaxLength(450);
            entity.Property(s => s.ProfissionalIdentificador).IsRequired().HasMaxLength(80);
            entity.Property(s => s.ProfissionalNome).IsRequired().HasMaxLength(160);
            entity.Property(s => s.ProfissionalCrp).HasMaxLength(20);
            entity.Property(s => s.TipoAtendimento).IsRequired().HasMaxLength(30);
            entity.Property(s => s.Presenca).IsRequired().HasMaxLength(30);
            entity.Property(s => s.Modalidade).HasMaxLength(20);
            entity.Property(s => s.Demanda).HasMaxLength(500);
            entity.Property(s => s.Evolucao).HasMaxLength(5000);
            entity.Property(s => s.Plano).HasMaxLength(1000);
            entity.Property(s => s.Risco).HasMaxLength(20);
            entity.Property(s => s.EncaminhamentosExternos).HasMaxLength(200);
            entity.Property(s => s.EncaminhamentoExternoOutro).HasMaxLength(120);

            entity.HasOne(s => s.Prontuario)
                .WithMany(p => p.Sessoes)
                .HasForeignKey(s => s.ProntuarioPsicologicoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SessaoPsicologicaAdendo>(entity =>
        {
            entity.ToTable("SessoesPsicologicasAdendos");
            entity.Property(a => a.Texto).IsRequired().HasMaxLength(2000);
            entity.Property(a => a.AutorUserId).IsRequired().HasMaxLength(450);
            entity.Property(a => a.AutorIdentificador).IsRequired().HasMaxLength(80);
            entity.Property(a => a.AutorNome).IsRequired().HasMaxLength(160);

            entity.HasOne(a => a.Sessao)
                .WithMany(s => s.Adendos)
                .HasForeignKey(a => a.SessaoPsicologicaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProntuarioPsicologicoAcessoEmergencia>(entity =>
        {
            entity.ToTable("ProntuariosPsicologicosAcessosEmergencia");
            entity.HasIndex(a => new { a.ProntuarioPsicologicoId, a.UserId });
            entity.Property(a => a.UserId).IsRequired().HasMaxLength(450);
            entity.Property(a => a.Identificador).IsRequired().HasMaxLength(80);
            entity.Property(a => a.Nome).IsRequired().HasMaxLength(160);
            entity.Property(a => a.Motivo).IsRequired().HasMaxLength(500);

            entity.HasOne(a => a.Prontuario)
                .WithMany()
                .HasForeignKey(a => a.ProntuarioPsicologicoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // O sistema grava tudo em UTC, mas o SQLite devolve as datas sem fuso.
        // Marca como UTC na leitura para a API responder com "Z" e o navegador
        // mostrar o horário local correto.
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            valor => valor,
            valor => DateTime.SpecifyKind(valor, DateTimeKind.Utc));
        var utcNullableConverter = new ValueConverter<DateTime?, DateTime?>(
            valor => valor,
            valor => valor.HasValue ? DateTime.SpecifyKind(valor.Value, DateTimeKind.Utc) : valor);

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(utcConverter);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(utcNullableConverter);
                }
            }
        }
    }
}
