using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class PlaniModel : PageModel
{
    private readonly AppDbContext _db; // Agrega esto
    public int SolicitudesPendientes { get; set; } = 0;
    public PlaniModel(AppDbContext db) // Agrega esto
    {
        _db = db;
    }
    public List<Analista> Analistas { get; set; } = new();
    public List<Sociedad> Sociedades { get; set; } = new();
    public List<Gerencia> Gerencias { get; set; } = new();
    public List<Ubicaciones> Ubicaciones { get; set; } = new();
    public bool EsAdmin { get; set; }

    [BindProperty] public string? Analista { get; set; }
    [BindProperty] public DateTime Fecha_Induccion { get; set; }
    [BindProperty] public DateTime Fecha_Ingreso { get; set; }
    [BindProperty] public string? RUT { get; set; }
    [BindProperty] public string? Nombre { get; set; }
    [BindProperty] public string? Apellido_Paterno { get; set; }
    [BindProperty] public string? Apellido_Materno { get; set; }
    [BindProperty] public DateTime Fecha_Nacimiento { get; set; }
    [BindProperty] public string? Correo { get; set; }
    [BindProperty] public string? Telefono { get; set; }
    [BindProperty] public string? Direccion { get; set; }
    [BindProperty] public string? Sociedad { get; set; }
    [BindProperty] public string? Cargo { get; set; }
    [BindProperty] public string? Gerencia { get; set; }
    [BindProperty] public string? Ubicacion { get; set; }
    [BindProperty] public string? Jefe_Directo { get; set; }
    [BindProperty] public string? Gerencia_mail { get; set; }
    [BindProperty] public string? Tipo_de_Contrato { get; set; }
    [BindProperty] public string? Contrato { get; set; }
    [BindProperty] public string? Ticket { get; set; }
    [BindProperty] public string? Carrera { get; set; }
    [BindProperty] public string? Universidad { get; set; }
    [BindProperty] public string? Quienrepone { get; set; }

    public void OnGet()
    {
        Analistas = _db.Analistas.ToList();
        Sociedades = _db.Sociedades.ToList();
        Gerencias = _db.Gerencias.ToList();
        Ubicaciones = _db.Ubicaciones.ToList();
        var usuario = _db.Usuarios.FirstOrDefault(u => u.Correo == User.Identity.Name);
        EsAdmin = usuario?.EsAdmin ?? false;
        if (EsAdmin)
        {
            SolicitudesPendientes = _db.SolicitudesEdicion.Count(s => !s.Procesada);
        }
    }

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid)
        {
            // Recargar las listas para el formulario
            Analistas = _db.Analistas.ToList();
            Sociedades = _db.Sociedades.ToList();
            Gerencias = _db.Gerencias.ToList();
            Ubicaciones = _db.Ubicaciones.ToList();
            var usuario = _db.Usuarios.FirstOrDefault(u => u.Correo == User.Identity.Name);
            EsAdmin = usuario?.EsAdmin ?? false;
            if (EsAdmin)
            {
                SolicitudesPendientes = _db.SolicitudesEdicion.Count(s => !s.Procesada);
            }

            ViewData["Error"] = "Por favor, corrige los errores del formulario.";
            return Page();
        }

        try
        {
            // VALIDAR SI EL RUT YA EXISTE
            var rutLimpio = RUT?.Replace(".", "").Replace("-", "").Trim();
            var empleadoExistente = _db.Empleados.FirstOrDefault(e =>
                e.RUT.Replace(".", "").Replace("-", "").Trim() == rutLimpio);

            if (empleadoExistente != null)
            {
                // Recargar las listas para el formulario
                Analistas = _db.Analistas.ToList();
                Sociedades = _db.Sociedades.ToList();
                Gerencias = _db.Gerencias.ToList();
                Ubicaciones = _db.Ubicaciones.ToList();
                var usuario = _db.Usuarios.FirstOrDefault(u => u.Correo == User.Identity.Name);
                EsAdmin = usuario?.EsAdmin ?? false;
                if (EsAdmin)
                {
                    SolicitudesPendientes = _db.SolicitudesEdicion.Count(s => !s.Procesada);
                }

                TempData["ErrorMessage"] = $"Empleado existente. El RUT {RUT} ya está registrado a nombre de {empleadoExistente.Nombre} {empleadoExistente.Apellido_Paterno}. Por favor edite en \"Datos Subidos\".";
                return Page();
            }

            // Obtener el siguiente número de orden
            var ultimoOrden = _db.Empleados.Max(e => (int?)e.OrdenImportacion) ?? 0;
            var siguienteOrden = ultimoOrden + 1;

            // Obtener el valor de Quienrepone desde el formulario
            var quienreponeValue = Request.Form["Quienrepone"].ToString();
            var tipoContrato = Request.Form["Tipo_de_Contrato"].ToString();

            // Si es Expansión, asegurar que Quienrepone esté vacío
            if (tipoContrato == "Expansión")
            {
                quienreponeValue = "";
            }

            // Si no existe, proceder con el registro
            var empleado = new Empleado
            {
                Analista = Request.Form["Analista"],
                Fecha_Induccion = Fecha_Induccion,
                Fecha_Ingreso = Fecha_Ingreso,
                RUT = RUT ?? "",
                Nombre = Nombre ?? "",
                Apellido_Paterno = Apellido_Paterno,
                Apellido_Materno = Apellido_Materno,
                Fecha_Nacimiento = Fecha_Nacimiento,
                Correo = Correo,
                Telefono = Telefono,
                Direccion = Direccion,
                Sociedad = Request.Form["sociedad"],
                Cargo = Cargo,
                Gerencia = Request.Form["Gerencia"],
                Gerencia_mail = Gerencia_mail,
                Ubicacion = Request.Form["Ubicacion"],
                Jefe_Directo = Jefe_Directo,
                Tipo_de_Contrato = tipoContrato,
                Quienrepone = quienreponeValue, // Usar el valor obtenido del formulario
                Contrato = Request.Form["contrato"],
                Ticket = Request.Form["ticket"],
                Carrera = Carrera,
                Universidad = Universidad,
                OrdenImportacion = siguienteOrden,
                UsuarioCreador = User.Identity?.Name ?? ""
            };

            _db.Empleados.Add(empleado);
            _db.SaveChanges();

            TempData["Message"] = "Empleado registrado correctamente.";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            // Recargar las listas para el formulario
            Analistas = _db.Analistas.ToList();
            Sociedades = _db.Sociedades.ToList();
            Gerencias = _db.Gerencias.ToList();
            Ubicaciones = _db.Ubicaciones.ToList();
            var usuario = _db.Usuarios.FirstOrDefault(u => u.Correo == User.Identity.Name);
            EsAdmin = usuario?.EsAdmin ?? false;
            if (EsAdmin)
            {
                SolicitudesPendientes = _db.SolicitudesEdicion.Count(s => !s.Procesada);
            }

            TempData["ErrorMessage"] = "Error al registrar empleado: " + ex.Message;
            return Page();
        }
    }

}
