using System.Security.Cryptography;
using System.Text;

namespace CasaMulher.Api.Security;

public sealed record TermoSigiloSecao(string Titulo, IReadOnlyList<string> Paragrafos);

/// <summary>
/// Conteúdo oficial do Termo de Confidencialidade e Proteção de Dados.
/// Qualquer mudança no texto exige incrementar <see cref="Versao"/>: isso obriga
/// todas as funcionárias a aceitarem novamente a nova redação.
/// </summary>
public static class TermoSigilo
{
    public const string Versao = "2026-10-v1";

    public const string Titulo = "Termo de Confidencialidade e Proteção de Dados";

    public static readonly IReadOnlyList<string> Resumo =
    [
        "Tudo o que você vê no sistema sobre as mulheres atendidas é sigiloso: dados pessoais, endereço, telefone, situação de violência, processos e atendimentos.",
        "Use essas informações somente para o seu trabalho na Casa da Mulher, e apenas o necessário para a sua função.",
        "Não comente, não fotografe, não copie, não imprima nem envie esses dados para ninguém fora do atendimento, nem por WhatsApp ou redes sociais.",
        "Seu ID e sua senha são pessoais. Não empreste, não compartilhe e saia do sistema ao deixar o computador.",
        "Todo acesso fica registrado com seu ID. O descumprimento pode gerar medidas administrativas, responsabilidade civil e responsabilização criminal."
    ];

    public static readonly IReadOnlyList<string> Confirmacoes =
    [
        "Li o termo completo e entendi que os dados das mulheres atendidas são sigilosos e protegidos pela LGPD.",
        "Comprometo-me a usar as informações somente para o meu trabalho e a não divulgá-las, copiá-las ou compartilhá-las fora do atendimento.",
        "Comprometo-me a não compartilhar meu ID e minha senha e a comunicar imediatamente a coordenação sobre qualquer vazamento ou uso indevido.",
        "Estou ciente de que todos os meus acessos são registrados e de que o descumprimento deste termo pode gerar consequências administrativas, civis e criminais, mesmo após o fim do meu vínculo."
    ];

    public static readonly IReadOnlyList<TermoSigiloSecao> Secoes =
    [
        new("1. Objetivo",
        [
            "Este termo estabelece as obrigações de confidencialidade e de proteção de dados pessoais de todas as pessoas que acessam o Sistema Casa da Mulher, em razão do caráter sensível das informações das mulheres atendidas, muitas delas em situação de violência doméstica e familiar.",
            "O aceite é pessoal, obrigatório para o uso do sistema e fica registrado com data, hora, ID de acesso e endereço de rede."
        ]),
        new("2. Base legal",
        [
            "Lei nº 13.709/2018 (Lei Geral de Proteção de Dados Pessoais - LGPD), que protege os dados pessoais e, de forma reforçada, os dados pessoais sensíveis, como informações sobre saúde, vida sexual, origem racial ou étnica e convicções.",
            "Lei nº 11.340/2006 (Lei Maria da Penha), que prevê a proteção e a preservação da intimidade e da segurança da mulher em situação de violência.",
            "Código Penal, arts. 153 (divulgação de segredo), 154 (violação do segredo profissional) e, para servidoras e servidores públicos, art. 325 (violação de sigilo funcional), além do dever de reparar danos previsto no Código Civil (arts. 186 e 927)."
        ]),
        new("3. Informações protegidas",
        [
            "São confidenciais todas as informações a que você tiver acesso por meio do sistema ou do seu trabalho na Casa da Mulher, incluindo: nome, documentos, endereço, telefone, dados de familiares e filhos, relatos de violência, informações de saúde, dados jurídicos e processuais, encaminhamentos, atendimentos sociais e psicológicos, fotografias e quaisquer anotações.",
            "O sigilo vale para dados digitais, impressos, falados ou vistos na tela, inclusive informações que pareçam simples, como o fato de uma mulher ser atendida pela Casa."
        ]),
        new("4. Suas obrigações",
        [
            "Acessar somente as informações necessárias para executar a sua função e apenas durante o seu trabalho.",
            "Não divulgar, comentar, copiar, fotografar, imprimir, gravar ou transmitir dados das atendidas a qualquer pessoa ou meio não autorizado, incluindo aplicativos de mensagem, redes sociais, e-mail pessoal ou conversas com terceiros, colegas sem relação com o atendimento e familiares.",
            "Nunca informar a qualquer pessoa, inclusive a quem se apresente como parente, companheiro ou ex-companheiro, se uma mulher é atendida, onde ela está ou como encontrá-la.",
            "Manter seu ID de acesso e sua senha em segredo, não emprestar sua conta e não usar a conta de outra pessoa.",
            "Bloquear a tela ou sair do sistema sempre que se afastar do computador e não deixar documentos com dados expostos.",
            "Comunicar imediatamente à coordenação qualquer suspeita de vazamento, perda de documento, acesso indevido ou uso irregular de dados."
        ]),
        new("5. Registro e fiscalização",
        [
            "Todas as ações no sistema ficam registradas com o seu ID de acesso, data e hora, e podem ser auditadas pela coordenação a qualquer momento.",
            "O uso da sua conta por outra pessoa será considerado de sua responsabilidade."
        ]),
        new("6. Consequências do descumprimento",
        [
            "O descumprimento deste termo pode resultar em bloqueio imediato do acesso, medidas administrativas e disciplinares conforme o seu vínculo, obrigação de reparar os danos causados às mulheres atendidas e à instituição, e responsabilização criminal nos termos da legislação aplicável.",
            "A violação do sigilo pode colocar em risco a vida e a segurança de mulheres em situação de violência."
        ]),
        new("7. Vigência",
        [
            "As obrigações deste termo começam no aceite e continuam valendo por tempo indeterminado, inclusive após o fim do seu vínculo com a Casa da Mulher, com troca de função ou com a desativação da sua conta.",
            "Quando o texto deste termo for atualizado, o sistema solicitará um novo aceite."
        ])
    ];

    public static readonly string Hash = CalcularHash();

    private static string CalcularHash()
    {
        var texto = new StringBuilder();
        texto.AppendLine(Versao).AppendLine(Titulo);

        foreach (var item in Resumo) texto.AppendLine(item);
        foreach (var secao in Secoes)
        {
            texto.AppendLine(secao.Titulo);
            foreach (var paragrafo in secao.Paragrafos) texto.AppendLine(paragrafo);
        }
        foreach (var item in Confirmacoes) texto.AppendLine(item);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto.ToString())));
    }

    /// <summary>Perfis da equipe de desenvolvimento não lidam com atendimentos e não assinam o termo.</summary>
    public static bool PerfilExigeAceite(string? perfil)
    {
        return !string.IsNullOrWhiteSpace(perfil)
            && !string.Equals(perfil, PerfisAcesso.Equipe, StringComparison.OrdinalIgnoreCase);
    }
}
