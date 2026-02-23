using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

public class ApplicationDbContext : DbContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IHttpContextAccessor httpContextAccessor) 
        : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }
    
    public DbSet<Empleado> Empleados { get; set; }
    public DbSet<AuditoriaAcceso> AuditoriasAcceso { get; set; }
    
    public override int SaveChanges()
    {
        var usuario = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Sistema";
        var ahora = DateTime.Now;
        
        // Detectar cambios automáticamente
        foreach (var entry in ChangeTracker.Entries<Empleado>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.FechaCreacion = ahora;
                    entry.Entity.UsuarioCreador = usuario;
                    // Crear registro de auditoría
                    CrearAuditoria(entry.Entity.RUT, "Creación", usuario, "Nuevo empleado creado");
                    break;
                    
                case EntityState.Modified:
                    entry.Entity.UltimaModificacion = ahora;
                    entry.Entity.UsuarioModifico = usuario;
                    // Detectar qué campos cambiaron
                    var cambios = DetectarCambios(entry);
                    CrearAuditoria(entry.Entity.RUT, "Modificación", usuario, cambios);
                    break;
            }
        }
        
        return base.SaveChanges();
    }
    
    private void CrearAuditoria(string rut, string accion, string usuario, string cambios)
    {
        AuditoriasAcceso.Add(new AuditoriaAcceso
        {
            Usuario = usuario,
            RUTEmpleadoAccedido = rut,
            AccionRealizada = accion,
            FechaHora = DateTime.Now,
            DatosModificados = cambios,
            IPAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? ""
        });
    }
    
    private string DetectarCambios(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        var cambios = new List<string>();
        foreach (var prop in entry.Properties)
        {
            if (prop.IsModified)
            {
                cambios.Add($"{prop.Metadata.Name}: {prop.OriginalValue} → {prop.CurrentValue}");
            }
        }
        return string.Join(", ", cambios);
    }
}