using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

public class EmailService
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public EmailService(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

   public async Task<bool> EnviarCorreoAsync(string asunto, string destinatarios, string cc, string cco,
                                         string contenidoHtml, List<IFormFile> adjuntosManuales = null, 
                                         List<string> adjuntosBD = null)
{
    try
    {
        // Configuración SMTP para Outlook/Exchange
        var smtpClient = new SmtpClient
        {
            Host = _configuration["Email:SmtpHost"] ?? "smtp-mail.outlook.com",
            Port = int.Parse(_configuration["Email:SmtpPort"] ?? "587"),
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(
                _configuration["Email:Username"],
                _configuration["Email:Password"]
            )
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress(_configuration["Email:Username"], _configuration["Email:DisplayName"] ?? "Sistema RH-DO"),
            Subject = asunto,
            Body = contenidoHtml,
            IsBodyHtml = true
        };

        // Agregar destinatarios
        if (!string.IsNullOrWhiteSpace(destinatarios))
        {
            foreach (var destinatario in destinatarios.Split(';', ','))
            {
                var email = destinatario.Trim();
                if (!string.IsNullOrWhiteSpace(email) && IsValidEmail(email))
                {
                    mailMessage.To.Add(email);
                }
            }
        }

        // Agregar CC
        if (!string.IsNullOrWhiteSpace(cc))
        {
            foreach (var ccEmail in cc.Split(';', ','))
            {
                var email = ccEmail.Trim();
                if (!string.IsNullOrWhiteSpace(email) && IsValidEmail(email))
                {
                    mailMessage.CC.Add(email);
                }
            }
        }

        // Agregar CCO
        if (!string.IsNullOrWhiteSpace(cco))
        {
            foreach (var ccoEmail in cco.Split(';', ','))
            {
                var email = ccoEmail.Trim();
                if (!string.IsNullOrWhiteSpace(email) && IsValidEmail(email))
                {
                    mailMessage.Bcc.Add(email);
                }
            }
        }

        // Agregar adjuntos MANUALES
        if (adjuntosManuales != null && adjuntosManuales.Count > 0)
        {
            Console.WriteLine($"Procesando {adjuntosManuales.Count} adjuntos manuales");

            foreach (var adjunto in adjuntosManuales)
            {
                if (adjunto.Length > 0)
                {
                    Console.WriteLine($"Adjuntando archivo manual: {adjunto.FileName} ({adjunto.Length} bytes)");

                    var memoryStream = new MemoryStream();
                    await adjunto.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;

                    var attachment = new Attachment(memoryStream, adjunto.FileName, adjunto.ContentType);
                    mailMessage.Attachments.Add(attachment);

                    Console.WriteLine($"Archivo manual adjuntado exitosamente: {adjunto.FileName}");
                }
            }
        }

        // Agregar adjuntos de la BASE DE DATOS
        if (adjuntosBD != null && adjuntosBD.Count > 0)
        {
            Console.WriteLine($"Procesando {adjuntosBD.Count} adjuntos de BD");

            string adjuntosPath = Path.Combine(_environment.WebRootPath, "adjuntos");
            
            foreach (var nombreAdjunto in adjuntosBD)
            {
                try
                {
                    string rutaCompleta = Path.Combine(adjuntosPath, nombreAdjunto);
                    
                    if (File.Exists(rutaCompleta))
                    {
                        Console.WriteLine($"Adjuntando archivo de BD: {nombreAdjunto}");
                        
                        var fileBytes = await File.ReadAllBytesAsync(rutaCompleta);
                        var memoryStream = new MemoryStream(fileBytes);
                        
                        var attachment = new Attachment(memoryStream, nombreAdjunto);
                        mailMessage.Attachments.Add(attachment);
                        
                        Console.WriteLine($"Archivo de BD adjuntado exitosamente: {nombreAdjunto}");
                    }
                    else
                    {
                        Console.WriteLine($"Archivo de BD no encontrado: {rutaCompleta}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error al adjuntar archivo de BD {nombreAdjunto}: {ex.Message}");
                }
            }
        }

        // Validar que hay al menos un destinatario
        if (mailMessage.To.Count == 0)
        {
            throw new Exception("No se encontraron destinatarios válidos");
        }

        Console.WriteLine($"Enviando correo con {mailMessage.Attachments.Count} adjuntos total");
        await smtpClient.SendMailAsync(mailMessage);

        Console.WriteLine("Correo enviado exitosamente");
        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error al enviar correo: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        return false;
    }
}

    // Validar formato de email
    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}