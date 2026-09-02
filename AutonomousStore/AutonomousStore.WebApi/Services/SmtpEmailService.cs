using System.Net;
using System.Net.Mail;

namespace AutonomousStore.WebApi.Services;

public interface IEmailService
{
    Task SendWelcomeEmailAsync(string toEmail, string customerName, CancellationToken cancellationToken = default);
    Task SendPasswordResetEmailAsync(string toEmail, string customerName, string resetLink, CancellationToken cancellationToken = default);
}

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendWelcomeEmailAsync(string toEmail, string customerName, CancellationToken cancellationToken = default)
    {
        var section = _configuration.GetSection("Email");
        var supportEmail = section["SupportEmail"];
        var supportWhatsApp = section["SupportWhatsApp"];

        await SendAsync(
            toEmail,
            "Bem-vindo(a) à AutonomousStore! 🛒",
            BuildWelcomeHtml(customerName, supportEmail, supportWhatsApp),
            "e-mail de boas-vindas",
            cancellationToken);
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string customerName, string resetLink, CancellationToken cancellationToken = default)
    {
        await SendAsync(
            toEmail,
            "Redefinição de senha — AutonomousStore 🔒",
            BuildPasswordResetHtml(customerName, resetLink),
            "e-mail de redefinição de senha",
            cancellationToken);
    }

    private async Task SendAsync(string toEmail, string subject, string bodyHtml, string logDescription, CancellationToken cancellationToken)
    {
        var section = _configuration.GetSection("Email");
        var senderEmail = section["SenderEmail"];
        var senderPassword = section["SenderPassword"];
        var senderName = section["SenderName"] ?? "AutonomousStore";
        var smtpHost = section["SmtpHost"] ?? "smtp.gmail.com";
        var smtpPort = int.TryParse(section["SmtpPort"], out var port) ? port : 587;

        if (string.IsNullOrWhiteSpace(senderEmail) || senderEmail.StartsWith("COLOQUE_") || string.IsNullOrWhiteSpace(senderPassword))
        {
            // Simplificação do protótipo: se o e-mail não estiver configurado, só avisa no log
            // e segue em frente — nunca deixamos isso quebrar o fluxo do cliente.
            _logger.LogWarning("{Description} não enviado para {Email}: credenciais SMTP não configuradas no appsettings.json.", logDescription, toEmail);
            return;
        }

        try
        {
            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(senderEmail, senderPassword),
                EnableSsl = true
            };

            using var message = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = subject,
                Body = bodyHtml,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao enviar {Description} para {Email}", logDescription, toEmail);
        }
    }

    private static string BuildWelcomeHtml(string customerName, string? supportEmail, string? supportWhatsApp)
    {
        var supportLines = "";

        if (!string.IsNullOrWhiteSpace(supportEmail))
            supportLines += $"<p style=\"margin:4px 0;\">📧 E-mail: <a href=\"mailto:{supportEmail}\" style=\"color:#22d3ee;\">{supportEmail}</a></p>";

        if (!string.IsNullOrWhiteSpace(supportWhatsApp))
            supportLines += $"<p style=\"margin:4px 0;\">💬 WhatsApp: {supportWhatsApp}</p>";

        return $"""
            <div style="font-family: Arial, sans-serif; max-width: 480px; margin: 0 auto; padding: 28px; background:#0c1220; color:#eaf6ff; border-radius:8px; border:1px solid #22d3ee44;">
                <h2 style="color:#22d3ee; margin-top:0;">Bem-vindo(a) à AutonomousStore, {customerName}! 🛒</h2>
                <p>Seu cadastro foi criado com sucesso. Agora você já pode abrir o app, gerar seu QR code
                e começar a comprar na nossa loja autônoma — 24 horas por dia, sem filas e sem caixas.</p>
                <p>Se tiver qualquer dúvida ou precisar de ajuda, é só chamar a gente:</p>
                {supportLines}
                <p style="margin-top:24px; font-size:0.85em; color:#8fa3bd;">Obrigado por fazer parte da AutonomousStore!</p>
            </div>
            """;
    }

    private static string BuildPasswordResetHtml(string customerName, string resetLink)
    {
        return $"""
            <div style="font-family: Arial, sans-serif; max-width: 480px; margin: 0 auto; padding: 28px; background:#0c1220; color:#eaf6ff; border-radius:8px; border:1px solid #22d3ee44;">
                <h2 style="color:#22d3ee; margin-top:0;">Olá, {customerName} 👋</h2>
                <p>Recebemos um pedido pra redefinir a senha da sua conta na AutonomousStore.
                Clique no botão abaixo pra escolher uma nova senha. Esse link vale por 30 minutos.</p>
                <p style="text-align:center; margin:28px 0;">
                    <a href="{resetLink}" style="background:#22d3ee; color:#04121a; text-decoration:none; font-weight:bold; padding:12px 28px; border-radius:4px; display:inline-block;">Redefinir minha senha</a>
                </p>
                <p style="font-size:0.85em; color:#8fa3bd;">Se você não pediu essa troca de senha, pode ignorar este e-mail —
                sua senha atual continua funcionando normalmente.</p>
                <p style="margin-top:24px; font-size:0.85em; color:#8fa3bd;">Equipe AutonomousStore</p>
            </div>
            """;
    }
}
