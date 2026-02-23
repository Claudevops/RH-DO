using Microsoft.EntityFrameworkCore;
using System.Reflection;
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Empleado> Empleados { get; set; }
    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<Analista> Analistas { get; set; }
    public DbSet<Sociedad> Sociedades { get; set; }
    public DbSet<Gerencia> Gerencias { get; set; }
    public DbSet<Ubicaciones> Ubicaciones { get; set; }
    public DbSet<AdjuntoCorreo> AdjuntosCorreos { get; set; }
    public DbSet<DestinatariosCorreo_PRE> DestinatariosCorreo_PRE { get; set; }
    public DbSet<PlantillaCorreo> PlantillasCorreos { get; set; }
    public DbSet<SolicitudEdicion> SolicitudesEdicion { get; set; }
    public DbSet<DestinatariosCorreo_POST> DestinatariosCorreo_POST { get; set; }
    public DbSet<AdjuntoCorreo_post> AdjuntoCorreo_post { get; set; }
    public DbSet<PlantillaCorreo_POST> PlantillaCorreo_POST { get; set; }
    public DbSet<CorreoEnviado_POST> CorreosEnviados_POST { get; set; }
    public DbSet<AuditoriaAcceso> AuditoriasAcceso { get; set; }

    public override int SaveChanges()
    {
        EncryptSensitiveData();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EncryptSensitiveData();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void EncryptSensitiveData()
    {
        var encryptionService = new EncryptionService();
        
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                foreach (var property in entry.Entity.GetType().GetProperties())
                {
                    if (property.GetCustomAttribute<EncryptedDataAttribute>() != null)
                    {
                        var value = property.GetValue(entry.Entity)?.ToString();
                        if (!string.IsNullOrEmpty(value) && !IsAlreadyEncrypted(value))
                        {
                            var encrypted = encryptionService.Encrypt(value);
                            property.SetValue(entry.Entity, encrypted);
                            Console.WriteLine($"Encriptando {property.Name}: {value} -> {encrypted.Substring(0, Math.Min(10, encrypted.Length))}...");
                        }
                    }
                }
            }
        }
    }

    private bool IsAlreadyEncrypted(string value)
    {
        try
        {
            // Verificar si parece Base64 y es suficientemente largo
            if (value.Length < 20) return false;
            
            Convert.FromBase64String(value);
            
            // Si no contiene caracteres comunes de datos sin encriptar, probablemente ya está encriptado
            return !value.Contains("@") && !value.Contains("-") && !value.Contains(".") && !value.Contains(" ");
        }
        catch
        {
            return false;
        }
    }
}