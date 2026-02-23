using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;


public class Configuracion_POSTModel : PageModel
{
 private readonly AppDbContext _db;
    public int SolicitudesPendientes { get; set; } = 0;
    public Configuracion_POSTModel(AppDbContext db)
    {
        _db = db;
    }
    // Listas para mostrar en la vista
    public List<Analista> Analistas { get; set; } = new();


    public List<AdjuntoCorreo_post> AdjuntoCorreo_post { get; set; } = new();
    [BindProperty] public int? Id_AC { get; set; }
    [BindProperty] public string? Nombre_Adjunto { get; set; }
    [BindProperty] public string? ArchivoPath { get; set; }
    [BindProperty] public IFormFile? Archivo { get; set; }
    [BindProperty] public int Tipo { get; set; } 
    [BindProperty] public DateTime FechaSubida { get; set; } = DateTime.Now;

    public List<DestinatariosCorreo_POST> DestinatariosCorreo_POST { get; set; } = new();
    [BindProperty] public int? Id_DCP { get; set; }
    [BindProperty] public int? Tipo_DCP { get; set; }
    [BindProperty] public string? DestinatariosFijos { get; set; }
    [BindProperty] public string? CCFijos { get; set; }
    [BindProperty] public string? CCOFijos { get; set; }

    public List<PlantillaCorreo_POST> PlantillaCorreo_POST { get; set; } = new();
    [BindProperty] public int? Id_PC { get; set; }
    [BindProperty] public string? Tipo_PC { get; set; }
    [BindProperty] public string? Nombre_PC { get; set; }
    [BindProperty] public string? HtmlContenido { get; set; }
    [BindProperty(SupportsGet = true)]
    public int? TipoSeleccionado { get; set; }
    public CorreoConfiguracionViewModelpost? CorreoConfig { get; set; }
    public bool EsAdmin { get; set; }
    public void OnGet()
    {

        AdjuntoCorreo_post = _db.AdjuntoCorreo_post.ToList();
        PlantillaCorreo_POST = _db.PlantillaCorreo_POST.ToList();
        DestinatariosCorreo_POST = _db.DestinatariosCorreo_POST.ToList();
        EsAdmin = User.IsInRole("1");
        int tipo = TipoSeleccionado ?? 1;
        CorreoConfig = new CorreoConfiguracionViewModelpost
        {
            Tipo = tipo,
            Destinatarios = _db.DestinatariosCorreo_POST.FirstOrDefault(d => d.Tipo_DCP == tipo),
            Adjuntos = _db.AdjuntoCorreo_post.Where(a => a.Tipo == tipo).ToList(),
            Plantilla = _db.PlantillaCorreo_POST.FirstOrDefault(p => p.Tipo_PC == tipo)
        };
        // Verifica si el usuario es administrador
        var usuario = _db.Usuarios.FirstOrDefault(u => u.Correo == System.Web.HttpUtility.HtmlEncode(User.Identity.Name));
        EsAdmin = usuario?.EsAdmin ?? false;
                if (EsAdmin)
        {
            SolicitudesPendientes = _db.SolicitudesEdicion.Count(s => !s.Procesada);
        }

    }


    // Agregar Adjunto Correo
    public IActionResult OnPostAddAdjuntoCorreo()
    {
        if (Archivo != null)
        {
            var nombreAdjunto = string.IsNullOrWhiteSpace(Nombre_Adjunto)
                ? Archivo.FileName
                : Nombre_Adjunto;

            var uploadsFolder = Path.Combine("wwwroot", "adjuntos");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var fileName = Guid.NewGuid() + Path.GetExtension(Archivo.FileName);
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                Archivo.CopyTo(stream);
            }

            var adjunto = new AdjuntoCorreo_post
            {
                Nombre_Adjunto = nombreAdjunto,
                ArchivoPath = "/adjuntos/" + fileName,
                Tipo = Tipo,
                FechaSubida = DateTime.Now
            };
            _db.AdjuntoCorreo_post.Add(adjunto);
            _db.SaveChanges();
        }
        return RedirectToPage();
    }
    // Eliminar Adjunto Correo
    public IActionResult OnPostDeleteAdjuntoCorreo()
    {
        if (Id_AC.HasValue)
        {
            var adjunto = _db.AdjuntoCorreo_post.FirstOrDefault(a => a.Id_AC == Id_AC);
            if (adjunto != null)
            {
                var filePath = Path.Combine("wwwroot", adjunto.ArchivoPath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);

                _db.AdjuntoCorreo_post.Remove(adjunto);
                _db.SaveChanges();
            }
        }
        return RedirectToPage();
    }

    // Editar HTML de plantilla de correo
    public IActionResult OnPostEditarHtmlCorreo()
    {
        if (Id_PC.HasValue && !string.IsNullOrWhiteSpace(HtmlContenido))
        {
            var plantilla = _db.PlantillaCorreo_POST.FirstOrDefault(p => p.Id_PC == Id_PC);
            if (plantilla != null)
            {
                plantilla.Nombre_PC = Nombre_PC ?? plantilla.Nombre_PC;
                plantilla.HtmlContenido = HtmlContenido;
                _db.SaveChanges();
            }
        }
        return RedirectToPage();
    }


    // CRUD DE DESTINATARIOS CORREO POST
    private bool CorreosSonValidos(string? correos)
    {
        if (string.IsNullOrWhiteSpace(correos))
            return true; // Campo vacío es válido (ajusta si quieres que sea obligatorio)

        var dominiosPermitidos = new[] { "@aguasdelvalle.cl", "@esval.cl" };
        var lista = correos.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var correo in lista)
        {
            if (!dominiosPermitidos.Any(d => correo.EndsWith(d, StringComparison.OrdinalIgnoreCase)))
                return false;
        }
        return true;
    }

    // Agregar destinatario fijo
    public IActionResult OnPostAddDestinatario()
    {
        if (Tipo_DCP.HasValue)
        {
            if (!CorreosSonValidos(DestinatariosFijos) || !CorreosSonValidos(CCFijos) || !CorreosSonValidos(CCOFijos))
            {
                ModelState.AddModelError(string.Empty, "Solo se permiten correos de los dominios aguasdelvalle.cl y esval.cl.");
                return Page();
            }

            var nuevo = new DestinatariosCorreo_POST
            {
                Tipo_DCP = Tipo_DCP.Value,
                DestinatariosFijos = DestinatariosFijos,
                CCFijos = CCFijos,
                CCOFijos = CCOFijos
            };
            _db.DestinatariosCorreo_POST.Add(nuevo);
            _db.SaveChanges();
        }
        return RedirectToPage();
    }

    // Editar destinatario fijo
    public IActionResult OnPostEditDestinatarios()
    {
        if (Id_DCP.HasValue)
        {
            if (!CorreosSonValidos(DestinatariosFijos) || !CorreosSonValidos(CCFijos) || !CorreosSonValidos(CCOFijos))
            {
                ModelState.AddModelError(string.Empty, "Solo se permiten correos de los dominios aguasdelvalle.cl y esval.cl.");
                return Page();
            }

            var correo = _db.DestinatariosCorreo_POST.FirstOrDefault(c => c.Id_DCP == Id_DCP);
            if (correo != null)
            {
                correo.Tipo_DCP = Tipo_DCP ?? correo.Tipo_DCP;
                correo.DestinatariosFijos = DestinatariosFijos;
                correo.CCFijos = CCFijos;
                correo.CCOFijos = CCOFijos;
                _db.SaveChanges();
            }
        }
        return RedirectToPage();
    }

    //unificación

public async Task<IActionResult> OnPostGuardarCorreoConfigAsync()
{
    Console.WriteLine("Entró al método GuardarCorreoConfig");
    Console.WriteLine($"Id_DCP: {Id_DCP}, Tipo_DCP: {Tipo_DCP}, DestinatariosFijos: {DestinatariosFijos}");
    Console.WriteLine($"Id_PC: {Id_PC}, TipoSeleccionado: {TipoSeleccionado}, Nombre_PC: {Nombre_PC}");
    Console.WriteLine($"Tipo: {Tipo}, Archivo: {(Archivo != null ? Archivo.FileName : "null")}");

    // Obtener el tipo actual con fallback a 1 si todo es null/0
    int tipoActual = TipoSeleccionado ?? Tipo_DCP ?? Tipo;
    if (tipoActual == 0) tipoActual = 1; // Fallback por defecto
    
    Console.WriteLine($"TipoActual calculado: {tipoActual}");

    // **DESTINATARIOS: Actualizar o crear**
    var dest = _db.DestinatariosCorreo_POST.FirstOrDefault(d => d.Tipo_DCP == tipoActual);
    if (dest != null)
    {
        // Actualizar existente
        dest.DestinatariosFijos = DestinatariosFijos;
        dest.CCFijos = CCFijos;
        dest.CCOFijos = CCOFijos;
        Console.WriteLine($"Actualizando destinatario existente para tipo {tipoActual}");
    }
    else
    {
        var nuevoDest = new DestinatariosCorreo_POST
        {
            Tipo_DCP = tipoActual,
            DestinatariosFijos = DestinatariosFijos ?? "",
            CCFijos = CCFijos ?? "",
            CCOFijos = CCOFijos ?? ""
        };
        _db.DestinatariosCorreo_POST.Add(nuevoDest);
        Console.WriteLine($"Creando nuevo destinatario para tipo {tipoActual}");
    }

    // **PLANTILLA: Actualizar o crear**
    var plantilla = _db.PlantillaCorreo_POST.FirstOrDefault(p => p.Tipo_PC == tipoActual);
    if (plantilla != null)
    {
        // Actualizar existente
        plantilla.Nombre_PC = Nombre_PC ?? plantilla.Nombre_PC;
        plantilla.HtmlContenido = HtmlContenido ?? plantilla.HtmlContenido;
        Console.WriteLine($"Actualizando plantilla existente para tipo {tipoActual}");
    }
    else
    {
        var nuevaPlantilla = new PlantillaCorreo_POST
        {
            Tipo_PC = tipoActual,
            Nombre_PC = Nombre_PC ?? "",
            HtmlContenido = HtmlContenido ?? ""
        };
        _db.PlantillaCorreo_POST.Add(nuevaPlantilla);
        Console.WriteLine($"Creando nueva plantilla para tipo {tipoActual}");
    }
    var eliminarAdjuntos = Request.Form["EliminarAdjuntos"];
    foreach (var idStr in eliminarAdjuntos)
    {
        if (int.TryParse(idStr, out int id))
        {
            var adj = _db.AdjuntoCorreo_post.FirstOrDefault(a => a.Id_AC == id);
            if (adj != null)
            {
                _db.AdjuntoCorreo_post.Remove(adj);
                Console.WriteLine($"Eliminando adjunto {id}");
            }
        }
    }
    if (Archivo != null && !string.IsNullOrWhiteSpace(Nombre_Adjunto))
    {
        var fileName = Path.GetFileName(Archivo.FileName);
        var uploadsFolder = Path.Combine("wwwroot", "adjuntos");
        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);
        var filePath = Path.Combine(uploadsFolder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await Archivo.CopyToAsync(stream);
        }

        var adjunto = new AdjuntoCorreo_post
        {
            Nombre_Adjunto = Nombre_Adjunto,
            ArchivoPath = "/adjuntos/" + fileName,
            Tipo = tipoActual,
            FechaSubida = DateTime.Now
        };
        _db.AdjuntoCorreo_post.Add(adjunto);
        Console.WriteLine($"Agregando nuevo adjunto para tipo {tipoActual}");
    }

    try
    {
        await _db.SaveChangesAsync();
        Console.WriteLine("Datos guardados exitosamente");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error al guardar: {ex.Message}");
        Console.WriteLine($"StackTrace: {ex.StackTrace}");
    }

    return RedirectToPage();
}
}