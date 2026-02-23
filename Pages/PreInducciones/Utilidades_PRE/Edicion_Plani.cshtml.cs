using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class Edicion_PlaniModel : PageModel
{
    private readonly AppDbContext _db;
    public int SolicitudesPendientes { get; set; } = 0;
    public Edicion_PlaniModel(AppDbContext db)
    {
        _db = db;
    }
    // Listas para mostrar en la vista (nombres coinciden con la vista)
    public List<Analista> Analistas { get; set; } = new();

    [BindProperty] public int? Id_ANA { get; set; }
    [BindProperty] public string? Nombre_ANA { get; set; }

    public List<Sociedad> Sociedades { get; set; } = new();
    [BindProperty] public int? Id_SOC { get; set; }
    [BindProperty] public string? Nombre_SOC { get; set; }

    public List<Gerencia> Gerencias { get; set; } = new();
    [BindProperty] public int? Id_GER { get; set; }
    [BindProperty] public string? Nombre_GER { get; set; }

    public List<Ubicaciones> Ubicaciones { get; set; } = new();
    [BindProperty] public int? Id_UBI { get; set; }
    [BindProperty] public string? Nombre_UBI { get; set; }

    public List<AdjuntoCorreo> AdjuntosCorreos { get; set; } = new();
    [BindProperty] public int? Id_AC { get; set; }
    [BindProperty] public string? Nombre_Adjunto { get; set; }
    [BindProperty] public string? ArchivoPath { get; set; }
    [BindProperty] public IFormFile? Archivo { get; set; }
    [BindProperty] public int Tipo { get; set; } // 2 o 3
    [BindProperty] public DateTime FechaSubida { get; set; } = DateTime.Now;

    public List<DestinatariosCorreo_PRE> DestinatariosCorreosPRE { get; set; } = new();
    [BindProperty] public int? Id_DCP { get; set; }
    [BindProperty] public int? Tipo_DCP { get; set; } // 1 o 2
    [BindProperty] public string? DestinatariosFijos { get; set; }
    [BindProperty] public string? CCFijos { get; set; }
    [BindProperty] public string? CCOFijos { get; set; }

    public List<PlantillaCorreo> PlantillasCorreos { get; set; } = new();
    [BindProperty] public int? Id_PC { get; set; }
    [BindProperty] public string? Tipo_PC { get; set; }
    [BindProperty] public string? Nombre_PC { get; set; }
    [BindProperty] public string? HtmlContenido { get; set; }
    [BindProperty(SupportsGet = true)]
    public int? TipoSeleccionado { get; set; }
    public CorreoConfiguracionViewModel? CorreoConfig { get; set; }
    public bool EsAdmin { get; set; }
    public void OnGet()
    {
        Analistas = _db.Analistas.ToList();
        Sociedades = _db.Sociedades.ToList();
        Gerencias = _db.Gerencias.ToList();
        Ubicaciones = _db.Ubicaciones.ToList();
        AdjuntosCorreos = _db.AdjuntosCorreos.ToList();
        PlantillasCorreos = _db.PlantillasCorreos.ToList();
        DestinatariosCorreosPRE = _db.DestinatariosCorreo_PRE.ToList();
        EsAdmin = User.IsInRole("1");
        int tipo = TipoSeleccionado ?? 1;
        CorreoConfig = new CorreoConfiguracionViewModel
        {
            Tipo = tipo,
            Destinatarios = _db.DestinatariosCorreo_PRE.FirstOrDefault(d => d.Tipo_DCP == tipo),
            Adjuntos = _db.AdjuntosCorreos.Where(a => a.Tipo == tipo).ToList(),
            Plantilla = _db.PlantillasCorreos.FirstOrDefault(p => p.Tipo_PC == tipo)
        };
        // Verifica si el usuario es administrador
        var usuario = _db.Usuarios.FirstOrDefault(u => u.Correo == User.Identity.Name);
        EsAdmin = usuario?.EsAdmin ?? false;
                if (EsAdmin)
        {
            SolicitudesPendientes = _db.SolicitudesEdicion.Count(s => !s.Procesada);
        }

    }


    //POST DE EDICIÓN DE PLANTILLAS
    public IActionResult OnPost()
    {
        if (!string.IsNullOrWhiteSpace(Nombre_ANA))
        {
            var nuevo = new Analista { Nombre_ANA = Nombre_ANA };
            _db.Analistas.Add(nuevo);
            _db.SaveChanges();
        }
        return RedirectToPage();
    }

    // Editar Analista
    public IActionResult OnPostEditAnalista()
    {
        if (Id_ANA.HasValue && !string.IsNullOrWhiteSpace(Nombre_ANA))
        {
            var analista = _db.Analistas.FirstOrDefault(a => a.Id_ANA == Id_ANA);
            if (analista != null)
            {
                analista.Nombre_ANA = Nombre_ANA;
                _db.SaveChanges();
            }
        }
        return RedirectToPage();
    }

    // Eliminar Analista
    public IActionResult OnPostDeleteAnalista()
    {
        if (Id_ANA.HasValue)
        {
            var analista = _db.Analistas.FirstOrDefault(a => a.Id_ANA == Id_ANA);
            if (analista != null)
            {
                _db.Analistas.Remove(analista);
                _db.SaveChanges();
            }
        }
        return RedirectToPage();
    }

    // Agregar Sociedad
    public IActionResult OnPostAddSociedad()
    {
        if (!string.IsNullOrWhiteSpace(Nombre_SOC))
        {
            var nueva = new Sociedad { Nombre_SOC = Nombre_SOC.ToUpper() };
            _db.Sociedades.Add(nueva);
            _db.SaveChanges();
        }
        return RedirectToPage();
    }

    // Editar Sociedad
    public IActionResult OnPostEditSociedad()
    {
        if (Id_SOC.HasValue && !string.IsNullOrWhiteSpace(Nombre_SOC))
        {
            var sociedad = _db.Sociedades.FirstOrDefault(s => s.Id_SOC == Id_SOC);
            if (sociedad != null)
            {
                sociedad.Nombre_SOC = Nombre_SOC.ToUpper();
                _db.SaveChanges();
            }
        }
        return RedirectToPage();
    }

    // Eliminar Sociedad
    public IActionResult OnPostDeleteSociedad()
    {
        if (Id_SOC.HasValue)
        {
            var sociedad = _db.Sociedades.FirstOrDefault(s => s.Id_SOC == Id_SOC);
            if (sociedad != null)
            {
                _db.Sociedades.Remove(sociedad);
                _db.SaveChanges();
            }
        }
        return RedirectToPage();
    }

    // Agregar Gerencia
    public IActionResult OnPostAddGerencia()
    {
        if (!string.IsNullOrWhiteSpace(Nombre_GER))
        {
            var nueva = new Gerencia { Nombre_GER = Nombre_GER.ToUpper() };
            _db.Gerencias.Add(nueva);
            _db.SaveChanges();
        }
        return RedirectToPage();
    }

    // Editar Gerencia
    public IActionResult OnPostEditGerencia()
    {
        if (Id_GER.HasValue && !string.IsNullOrWhiteSpace(Nombre_GER))
        {
            var gerencia = _db.Gerencias.FirstOrDefault(g => g.Id_GER == Id_GER);
            if (gerencia != null)
            {
                gerencia.Nombre_GER = Nombre_GER.ToUpper();
                _db.SaveChanges();
            }
        }
        return RedirectToPage();
    }

    // Eliminar Gerencia
    public IActionResult OnPostDeleteGerencia()
    {
        if (Id_GER.HasValue)
        {
            var gerencia = _db.Gerencias.FirstOrDefault(g => g.Id_GER == Id_GER);
            if (gerencia != null)
            {
                _db.Gerencias.Remove(gerencia);
                _db.SaveChanges();
            }
        }
        return RedirectToPage();
    }

    // Agregar Ubicacion
    public IActionResult OnPostAddUbicacion()
    {
        if (!string.IsNullOrWhiteSpace(Nombre_UBI))
        {
            var nueva = new Ubicaciones { Nombre_UBI = Nombre_UBI.ToUpper() };
            _db.Ubicaciones.Add(nueva);
            _db.SaveChanges();
        }
        return RedirectToPage();
    }

    // Editar Ubicacion
    public IActionResult OnPostEditUbicacion()
    {
        if (Id_UBI.HasValue && !string.IsNullOrWhiteSpace(Nombre_UBI))
        {
            var ubicacion = _db.Ubicaciones.FirstOrDefault(u => u.Id_UBI == Id_UBI);
            if (ubicacion != null)
            {
                ubicacion.Nombre_UBI = Nombre_UBI.ToUpper();
                _db.SaveChanges();
            }
        }
        return RedirectToPage();
    }

    // Eliminar Ubicacion
    public IActionResult OnPostDeleteUbicacion()
    {
        if (Id_UBI.HasValue)
        {
            var ubicacion = _db.Ubicaciones.FirstOrDefault(u => u.Id_UBI == Id_UBI);
            if (ubicacion != null)
            {
                _db.Ubicaciones.Remove(ubicacion);
                _db.SaveChanges();
            }
        }
        return RedirectToPage();
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

            var adjunto = new AdjuntoCorreo
            {
                Nombre_Adjunto = nombreAdjunto,
                ArchivoPath = "/adjuntos/" + fileName,
                Tipo = Tipo,
                FechaSubida = DateTime.Now
            };
            _db.AdjuntosCorreos.Add(adjunto);
            _db.SaveChanges();
        }
        return RedirectToPage();
    }
    // Eliminar Adjunto Correo
    public IActionResult OnPostDeleteAdjuntoCorreo()
    {
        if (Id_AC.HasValue)
        {
            var adjunto = _db.AdjuntosCorreos.FirstOrDefault(a => a.Id_AC == Id_AC);
            if (adjunto != null)
            {
                var filePath = Path.Combine("wwwroot", adjunto.ArchivoPath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);

                _db.AdjuntosCorreos.Remove(adjunto);
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
            var plantilla = _db.PlantillasCorreos.FirstOrDefault(p => p.Id_PC == Id_PC);
            if (plantilla != null)
            {
                plantilla.Nombre_PC = Nombre_PC ?? plantilla.Nombre_PC; // Actualiza el asunto
                plantilla.HtmlContenido = HtmlContenido;
                _db.SaveChanges();
            }
        }
        return RedirectToPage();
    }


    // CRUD DE DESTINATARIOS CORREO PRE
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

            var nuevo = new DestinatariosCorreo_PRE
            {
                Tipo_DCP = Tipo_DCP.Value,
                DestinatariosFijos = DestinatariosFijos,
                CCFijos = CCFijos,
                CCOFijos = CCOFijos
            };
            _db.DestinatariosCorreo_PRE.Add(nuevo);
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

            var correo = _db.DestinatariosCorreo_PRE.FirstOrDefault(c => c.Id_DCP == Id_DCP);
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
    Console.WriteLine("Entró al método GuardarCorreoConfig PRE");
    Console.WriteLine($"Id_DCP: {Id_DCP}, Tipo_DCP: {Tipo_DCP}, DestinatariosFijos: {DestinatariosFijos}");
    Console.WriteLine($"Id_PC: {Id_PC}, TipoSeleccionado: {TipoSeleccionado}, Nombre_PC: {Nombre_PC}");
    Console.WriteLine($"Tipo: {Tipo}, Archivo: {(Archivo != null ? Archivo.FileName : "null")}");

    // Obtener el tipo actual con fallback a 1 si todo es null/0
    int tipoActual = TipoSeleccionado ?? Tipo_DCP ?? Tipo;
    if (tipoActual == 0) tipoActual = 1; // Fallback por defecto
    
    Console.WriteLine($"TipoActual calculado: {tipoActual}");

    // **DESTINATARIOS PRE: Actualizar o crear**
    var dest = _db.DestinatariosCorreo_PRE.FirstOrDefault(d => d.Tipo_DCP == tipoActual);
    if (dest != null)
    {
        // Actualizar existente
        dest.DestinatariosFijos = DestinatariosFijos;
        dest.CCFijos = CCFijos;
        dest.CCOFijos = CCOFijos;
        Console.WriteLine($"Actualizando destinatario PRE existente para tipo {tipoActual}");
    }
    else
    {
        // Crear nuevo
        var nuevoDest = new DestinatariosCorreo_PRE
        {
            Tipo_DCP = tipoActual,
            DestinatariosFijos = DestinatariosFijos ?? "",
            CCFijos = CCFijos ?? "",
            CCOFijos = CCOFijos ?? ""
        };
        _db.DestinatariosCorreo_PRE.Add(nuevoDest);
        Console.WriteLine($"Creando nuevo destinatario PRE para tipo {tipoActual}");
    }

    // **PLANTILLA PRE: Actualizar o crear**
    var plantilla = _db.PlantillasCorreos.FirstOrDefault(p => p.Tipo_PC == tipoActual);
    if (plantilla != null)
    {
        // Actualizar existente
        plantilla.Nombre_PC = Nombre_PC ?? plantilla.Nombre_PC;
        plantilla.HtmlContenido = HtmlContenido ?? plantilla.HtmlContenido;
        Console.WriteLine($"Actualizando plantilla PRE existente para tipo {tipoActual}");
    }
    else
    {
        // Crear nueva
        var nuevaPlantilla = new PlantillaCorreo
        {
            Tipo_PC = tipoActual,
            Nombre_PC = Nombre_PC ?? "",
            HtmlContenido = HtmlContenido ?? ""
        };
        _db.PlantillasCorreos.Add(nuevaPlantilla);
        Console.WriteLine($"Creando nueva plantilla PRE para tipo {tipoActual}");
    }

    // **ADJUNTOS PRE: Eliminar seleccionados**
    var eliminarAdjuntos = Request.Form["EliminarAdjuntos"];
    foreach (var idStr in eliminarAdjuntos)
    {
        if (int.TryParse(idStr, out int id))
        {
            var adj = _db.AdjuntosCorreos.FirstOrDefault(a => a.Id_AC == id);
            if (adj != null)
            {
                // Eliminar archivo físico
                var filePath = Path.Combine("wwwroot", adj.ArchivoPath.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);

                _db.AdjuntosCorreos.Remove(adj);
                Console.WriteLine($"Eliminando adjunto PRE {id}");
            }
        }
    }

    // **ADJUNTOS PRE: Agregar nuevo si hay archivo**
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

        var adjunto = new AdjuntoCorreo
        {
            Nombre_Adjunto = Nombre_Adjunto,
            ArchivoPath = "/adjuntos/" + fileName,
            Tipo = tipoActual,
            FechaSubida = DateTime.Now
        };
        _db.AdjuntosCorreos.Add(adjunto);
        Console.WriteLine($"Agregando nuevo adjunto PRE para tipo {tipoActual}");
    }

    try
    {
        await _db.SaveChangesAsync();
        Console.WriteLine("Datos PRE guardados exitosamente");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error al guardar PRE: {ex.Message}");
        Console.WriteLine($"StackTrace: {ex.StackTrace}");
    }

    return RedirectToPage();
}




}
    
