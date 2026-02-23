using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;


public class SolicitudesEdicionModel : PageModel
{
    private readonly AppDbContext _db;
    public List<SolicitudEdicion> SolicitudesPendientes { get; set; } = new();
    public List<SolicitudEdicion> SolicitudesProcesadas { get; set; } = new();
    public bool EsAdmin { get; set; }
    public SolicitudesEdicionModel(AppDbContext db)
    {
        _db = db;
    }


    public async Task OnGetAsync()
    {
        var usuario = _db.Usuarios.FirstOrDefault(u => u.Correo == User.Identity.Name);
        EsAdmin = usuario?.EsAdmin ?? false;
        // Cargar solicitudes pendientes y procesadas
        SolicitudesPendientes = await _db.SolicitudesEdicion
            .Where(s => !s.Procesada)
            .OrderByDescending(s => s.FechaSolicitud)
            .ToListAsync();

        SolicitudesProcesadas = await _db.SolicitudesEdicion
            .Where(s => s.Procesada)
            .OrderByDescending(s => s.FechaSolicitud)
            .Take(100) // Limitar a las 100 más recientes
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostAprobarAsync(int id)
    {
        var solicitud = await _db.SolicitudesEdicion.FindAsync(id);
        if (solicitud == null || solicitud.Procesada)
            return RedirectToPage();

        try
        {
            if (solicitud.Tipo == "Eliminacion")
            {
                // Procesar eliminación
                var empleado = await _db.Empleados.FirstOrDefaultAsync(e => e.RUT == solicitud.RutEmpleado);
                if (empleado != null)
                {
                    _db.Empleados.Remove(empleado);
                    await _db.SaveChangesAsync();
                }

                solicitud.Procesada = true;
                solicitud.Aprobada = true;
                solicitud.FechaProcesamiento = DateTime.Now;
                solicitud.UsuarioProceso = System.Web.HttpUtility.HtmlEncode(User.Identity?.Name);
                await _db.SaveChangesAsync();

                TempData["MensajeExito"] = "Solicitud de eliminación aprobada. El empleado ha sido eliminado del sistema.";
            }
            else
            {
                // Procesar edición (código existente)
                var datosSolicitados = JsonDocument.Parse(solicitud.DatosSolicitados).RootElement;
                string rut = datosSolicitados.GetProperty("RUT").GetString() ?? "";

                var empleado = await _db.Empleados.FirstOrDefaultAsync(e => e.RUT == rut);
                if (empleado != null)
                {
                    string datosAnteriores = JsonSerializer.Serialize(empleado);
                    solicitud.DatosAnteriores = datosAnteriores;

                    ActualizarEmpleadoDesdeSolicitud(empleado, datosSolicitados);

                    empleado.UltimaModificacion = DateTime.Now;
                    empleado.UsuarioModifico = User.Identity?.Name ?? "Administrador";
                    await _db.SaveChangesAsync();
                }

                solicitud.Procesada = true;
                solicitud.Aprobada = true;
                solicitud.FechaProcesamiento = DateTime.Now;
                solicitud.UsuarioProceso = System.Web.HttpUtility.HtmlEncode(User.Identity?.Name);
                await _db.SaveChangesAsync();

                TempData["MensajeExito"] = "Solicitud aprobada y cambios aplicados correctamente.";
            }
        }
        catch (Exception ex)
        {
            TempData["MensajeError"] = $"Error al procesar la solicitud: {ex.Message}";
        }

        return RedirectToPage();
    }


    public async Task<IActionResult> OnPostRechazarAsync(int id)
    {
        var solicitud = await _db.SolicitudesEdicion.FindAsync(id);
        if (solicitud == null || solicitud.Procesada)
            return RedirectToPage();

        solicitud.Procesada = true;
        solicitud.Aprobada = false;
        solicitud.FechaProcesamiento = DateTime.Now;
        solicitud.UsuarioProceso = System.Web.HttpUtility.HtmlEncode(User.Identity?.Name);
        await _db.SaveChangesAsync();

        TempData["MensajeExito"] = "Solicitud rechazada correctamente.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRevertirAsync(int id)
    {
        var solicitud = await _db.SolicitudesEdicion.FindAsync(id);
        if (solicitud == null || !solicitud.Procesada || solicitud.Aprobada != true)
        {
            TempData["MensajeError"] = "No se puede revertir esta solicitud.";
            return RedirectToPage();
        }

        // VERIFICAR SI ES UNA ELIMINACIÓN
        if (solicitud.Tipo == "Eliminacion")
        {
            TempData["MensajeError"] = "Las solicitudes de eliminación no se pueden revertir.";
            return RedirectToPage();
        }

        // VERIFICAR SI TIENE DATOS ANTERIORES (solo para ediciones)
        if (string.IsNullOrEmpty(solicitud.DatosAnteriores))
        {
            TempData["MensajeError"] = "No se puede revertir esta solicitud porque no hay datos anteriores disponibles.";
            return RedirectToPage();
        }

        try
        {
            // Obtener el RUT del empleado desde los datos solicitados
            var datosSolicitados = JsonDocument.Parse(solicitud.DatosSolicitados).RootElement;
            string rut = datosSolicitados.GetProperty("RUT").GetString() ?? "";

            // Buscar el empleado en la base de datos
            var empleado = await _db.Empleados.FirstOrDefaultAsync(e => e.RUT == rut);
            if (empleado == null)
            {
                TempData["MensajeError"] = "No se encontró el empleado para revertir los cambios.";
                return RedirectToPage();
            }

            // Restaurar los datos anteriores
            var datosAnteriores = JsonDocument.Parse(solicitud.DatosAnteriores).RootElement;
            ActualizarEmpleadoDesdeSolicitud(empleado, datosAnteriores);

            // Registrar la reversión
            empleado.UltimaModificacion = DateTime.Now;
            empleado.UsuarioModifico = User.Identity?.Name + " (Reversión)";
            await _db.SaveChangesAsync();

            TempData["MensajeExito"] = "Los cambios se han revertido correctamente.";
        }
        catch (Exception ex)
        {
            TempData["MensajeError"] = $"Error al revertir los cambios: {ex.Message}";
        }

        return RedirectToPage();
    }

    // Método auxiliar para actualizar los campos del empleado desde los datos de solicitud
    private void ActualizarEmpleadoDesdeSolicitud(Empleado empleado, JsonElement datos)
    {
        // Actualizar propiedades de texto - Solo campos principales
        ActualizarPropiedad(datos, "Nombre", value => empleado.Nombre = value);
        ActualizarPropiedad(datos, "Apellido_Paterno", value => empleado.Apellido_Paterno = value);
        ActualizarPropiedad(datos, "Apellido_Materno", value => empleado.Apellido_Materno = value);
        ActualizarPropiedad(datos, "Correo", value => empleado.Correo = value);
        ActualizarPropiedad(datos, "Telefono", value => empleado.Telefono = value);
        ActualizarPropiedad(datos, "Direccion", value => empleado.Direccion = value);
        ActualizarPropiedad(datos, "Sociedad", value => empleado.Sociedad = value);
        ActualizarPropiedad(datos, "Cargo", value => empleado.Cargo = value);
        ActualizarPropiedad(datos, "Gerencia", value => empleado.Gerencia = value);  // Solo este
        ActualizarPropiedad(datos, "Gerencia_mail", value => empleado.Gerencia_mail = value);
        ActualizarPropiedad(datos, "Ubicacion", value => empleado.Ubicacion = value);  // Solo este
        ActualizarPropiedad(datos, "Jefe_Directo", value => empleado.Jefe_Directo = value);
        ActualizarPropiedad(datos, "Tipo_de_Contrato", value => empleado.Tipo_de_Contrato = value);
        ActualizarPropiedad(datos, "Contrato", value => empleado.Contrato = value);
        ActualizarPropiedad(datos, "Ticket", value => empleado.Ticket = value);
        ActualizarPropiedad(datos, "Carrera", value => empleado.Carrera = value);
        ActualizarPropiedad(datos, "Universidad", value => empleado.Universidad = value);
        ActualizarPropiedad(datos, "Analista", value => empleado.Analista = value);

        // Actualizar fechas
        ActualizarPropiedadFecha(datos, "Fecha_Induccion", value => empleado.Fecha_Induccion = value);
        ActualizarPropiedadFecha(datos, "Fecha_Ingreso", value => empleado.Fecha_Ingreso = value);
        ActualizarPropiedadFecha(datos, "Fecha_Nacimiento", value => empleado.Fecha_Nacimiento = value);
    }

    private void ActualizarPropiedad(JsonElement datos, string nombrePropiedad, Action<string> setter)
    {
        if (datos.TryGetProperty(nombrePropiedad, out var elemento) &&
            elemento.ValueKind != JsonValueKind.Null)
        {
            setter(elemento.GetString() ?? "");
        }
    }

    private void ActualizarPropiedadFecha(JsonElement datos, string nombrePropiedad, Action<DateTime?> setter)
    {
        if (datos.TryGetProperty(nombrePropiedad, out var elemento) &&
            elemento.ValueKind != JsonValueKind.Null)
        {
            string fechaStr = elemento.GetString() ?? "";

            System.Diagnostics.Debug.WriteLine($"🔍 Procesando fecha '{nombrePropiedad}': '{fechaStr}'");

            if (!string.IsNullOrEmpty(fechaStr))
            {
                // USAR PARSEO SIN CONVERSIÓN DE ZONA HORARIA
                var fechaParseada = ParsearFechaSinZonaHoraria(fechaStr);
                if (fechaParseada.HasValue)
                {
                    setter(fechaParseada);
                    System.Diagnostics.Debug.WriteLine($"✅ Fecha '{nombrePropiedad}' parseada: '{fechaStr}' -> {fechaParseada:yyyy-MM-dd}");
                }
                else
                {
                    setter(null);
                    System.Diagnostics.Debug.WriteLine($"⚠️ No se pudo parsear fecha '{nombrePropiedad}': '{fechaStr}'");
                }
            }
            else
            {
                setter(null);
                System.Diagnostics.Debug.WriteLine($"ℹ️ Fecha '{nombrePropiedad}' está vacía");
            }
        }
    }

    private DateTime? ParsearFechaSinZonaHoraria(string fechaInput)
    {
        if (string.IsNullOrWhiteSpace(fechaInput))
            return null;

        try
        {
            System.Diagnostics.Debug.WriteLine($"🔧 Parseando fecha: '{fechaInput}'");

            // MÉTODO 1: Formato yyyy-MM-dd
            if (DateTime.TryParseExact(fechaInput, "yyyy-MM-dd", null,
                System.Globalization.DateTimeStyles.None, out var fecha1))
            {
                System.Diagnostics.Debug.WriteLine($"  -> Método 1 exitoso: {fecha1:yyyy-MM-dd}");
                return fecha1;
            }

            // MÉTODO 2: Formato dd/MM/yyyy
            if (DateTime.TryParseExact(fechaInput, "dd/MM/yyyy", null,
                System.Globalization.DateTimeStyles.None, out var fecha2))
            {
                System.Diagnostics.Debug.WriteLine($"  -> Método 2 exitoso: {fecha2:yyyy-MM-dd}");
                return fecha2;
            }

            // MÉTODO 3: Formato MM/dd/yyyy
            if (DateTime.TryParseExact(fechaInput, "MM/dd/yyyy", null,
                System.Globalization.DateTimeStyles.None, out var fecha3))
            {
                System.Diagnostics.Debug.WriteLine($"  -> Método 3 exitoso: {fecha3:yyyy-MM-dd}");
                return fecha3;
            }

            // MÉTODO 4: Si contiene 'T' (formato ISO), extraer solo la fecha
            if (fechaInput.Contains("T"))
            {
                var soloFecha = fechaInput.Split('T')[0];
                if (DateTime.TryParseExact(soloFecha, "yyyy-MM-dd", null,
                    System.Globalization.DateTimeStyles.None, out var fecha4))
                {
                    System.Diagnostics.Debug.WriteLine($"  -> Método 4 exitoso: {fecha4:yyyy-MM-dd}");
                    return fecha4;
                }
            }

            // MÉTODO 5: Parseo controlado con DateTimeStyles.AssumeLocal
            if (DateTime.TryParse(fechaInput, null,
                System.Globalization.DateTimeStyles.AssumeLocal, out var fecha5))
            {
                // Crear nueva fecha en mediodía para evitar problemas de zona horaria
                var fechaSegura = new DateTime(fecha5.Year, fecha5.Month, fecha5.Day, 12, 0, 0, DateTimeKind.Local);
                System.Diagnostics.Debug.WriteLine($"  -> Método 5 exitoso: {fechaSegura:yyyy-MM-dd}");
                return fechaSegura;
            }

            System.Diagnostics.Debug.WriteLine($"  -> ❌ Todos los métodos fallaron para: '{fechaInput}'");
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Error parseando fecha '{fechaInput}': {ex.Message}");
            return null;
        }
    }



public JsonResult OnGetDetallesSolicitud(int id)
{
    try
    {
        var solicitud = _db.SolicitudesEdicion.Find(id);
        if (solicitud == null)
        {
            return new JsonResult(new { error = "Solicitud no encontrada" });
        }

        System.Diagnostics.Debug.WriteLine($"=== OBTENIENDO DETALLES SOLICITUD {id} ===");
        System.Diagnostics.Debug.WriteLine($"Datos solicitados RAW: {solicitud.DatosSolicitados}");

        // DESERIALIZAR Y FORMATEAR FECHAS
        var datosSolicitados = JsonSerializer.Deserialize<Dictionary<string, object>>(solicitud.DatosSolicitados);
        var datosSolicitadosFormateados = FormatearFechasEnDiccionario(datosSolicitados);

        // DATOS ACTUALES DEL EMPLEADO - SERIALIZAR CON camelCase
        var empleado = _db.Empleados.FirstOrDefault(e => e.RUT == solicitud.RutEmpleado);
        Dictionary<string, object> datosActuales;

        if (empleado != null)
        {
            // CREAR OBJETO MANUALMENTE CON NOMBRES CONSISTENTES
            datosActuales = new Dictionary<string, object>
            {
                ["id"] = empleado.Id,
                ["analista"] = empleado.Analista ?? "",
                ["entrevista_Experiencia"] = empleado.Entrevista_Experiencia ?? "",
                ["encuesta_Eficacia"] = empleado.Encuesta_Eficacia ?? "",
                ["fecha_Induccion"] = empleado.Fecha_Induccion?.ToString("yyyy-MM-dd"),
                ["fecha_Ingreso"] = empleado.Fecha_Ingreso?.ToString("yyyy-MM-dd"),
                ["rut"] = empleado.RUT ?? "",
                ["nombre"] = empleado.Nombre ?? "",
                ["apellido_Paterno"] = empleado.Apellido_Paterno ?? "",
                ["apellido_Materno"] = empleado.Apellido_Materno ?? "",
                ["fecha_Nacimiento"] = empleado.Fecha_Nacimiento?.ToString("yyyy-MM-dd"),
                ["correo"] = empleado.Correo ?? "",
                ["telefono"] = empleado.Telefono ?? "",
                ["direccion"] = empleado.Direccion ?? "",
                ["sociedad"] = empleado.Sociedad ?? "",
                ["cargo"] = empleado.Cargo ?? "",
                ["gerencia"] = empleado.Gerencia ?? "",
                ["gerencia_mail"] = empleado.Gerencia_mail ?? "",
                ["ubicacion"] = empleado.Ubicacion ?? "",
                ["jefe_Directo"] = empleado.Jefe_Directo ?? "",
                ["tipo_de_Contrato"] = empleado.Tipo_de_Contrato ?? "",
                ["contrato"] = empleado.Contrato ?? "",
                ["ticket"] = empleado.Ticket ?? "",
                ["fecha_Induccion_Cultural"] = empleado.Fecha_Induccion_Cultural?.ToString("yyyy-MM-dd"),
                ["estado_Induccion_Cultural"] = empleado.Estado_Induccion_Cultural ?? "",
                ["carrera"] = empleado.Carrera ?? "",
                ["universidad"] = empleado.Universidad ?? ""
            };
        }
        else
        {
            datosActuales = new Dictionary<string, object>();
        }

        System.Diagnostics.Debug.WriteLine($"Datos solicitados formateados: {JsonSerializer.Serialize(datosSolicitadosFormateados)}");
        System.Diagnostics.Debug.WriteLine($"Datos actuales formateados: {JsonSerializer.Serialize(datosActuales)}");

        // COMPARAR CAMPOS Y MARCAR SOLO LOS QUE REALMENTE CAMBIARON
        var camposModificados = new List<string>();
        foreach (var kvp in datosSolicitadosFormateados)
        {
            var campo = kvp.Key;
            var valorSolicitado = kvp.Value?.ToString() ?? "";
            var valorActual = datosActuales.ContainsKey(campo) ? datosActuales[campo]?.ToString() ?? "" : "";

            if (valorSolicitado != valorActual)
            {
                camposModificados.Add(campo);
                System.Diagnostics.Debug.WriteLine($"🔄 Campo modificado '{campo}': '{valorActual}' -> '{valorSolicitado}'");
            }
        }

        System.Diagnostics.Debug.WriteLine($"Total campos modificados: {camposModificados.Count}");

        return new JsonResult(new
        {
            solicitud = new
            {
                id = solicitud.Id,
                rutEmpleado = solicitud.RutEmpleado,
                usuarioSolicitante = solicitud.UsuarioSolicitante,
                fechaSolicitud = solicitud.FechaSolicitud,
                tipo = solicitud.Tipo,
                procesada = solicitud.Procesada,
                aprobada = solicitud.Aprobada
            },
            datosActuales = datosActuales,
            datosSolicitados = datosSolicitadosFormateados,
            camposModificados = camposModificados
        });
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"❌ Error en OnGetDetallesSolicitud: {ex.Message}");
        return new JsonResult(new { error = ex.Message });
    }
}

// Formatear fechas en diccionario
private Dictionary<string, object> FormatearFechasEnDiccionario(Dictionary<string, object> datos)
{
    var resultado = new Dictionary<string, object>();
    var camposFecha = new[] { "fecha_Induccion", "fecha_Ingreso", "fecha_Nacimiento", "fecha_Induccion_Cultural" };

    foreach (var kvp in datos)
    {
        var clave = kvp.Key;
        var valor = kvp.Value;

        // ✅ NORMALIZAR NOMBRE DE PROPIEDAD (convertir a camelCase)
        var claveNormalizada = NormalizarNombrePropiedad(clave);

        // ✅ VERIFICAR SI ES UN CAMPO DE FECHA
        if (camposFecha.Any(cf => string.Equals(cf, claveNormalizada, StringComparison.OrdinalIgnoreCase)))
        {
            if (valor != null)
            {
                string valorString = valor.ToString();
                System.Diagnostics.Debug.WriteLine($"🔧 Formateando campo fecha '{claveNormalizada}': '{valorString}'");

                // Si es JsonElement, extraer el string
                if (valor is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.String)
                {
                    valorString = jsonElement.GetString() ?? "";
                }

                // ✅ PARSEAR Y FORMATEAR LA FECHA
                var fechaParseada = ParsearFechaSinZonaHoraria(valorString);
                if (fechaParseada.HasValue)
                {
                    resultado[claveNormalizada] = fechaParseada.Value.ToString("yyyy-MM-dd");
                    System.Diagnostics.Debug.WriteLine($"  -> Formateada: '{valorString}' -> '{resultado[claveNormalizada]}'");
                }
                else
                {
                    resultado[claveNormalizada] = valorString;
                    System.Diagnostics.Debug.WriteLine($"  -> Mantenida original: '{valorString}'");
                }
            }
            else
            {
                resultado[claveNormalizada] = null;
            }
        }
        else
        {
            // ✅ PARA CAMPOS QUE NO SON FECHAS, TAMBIÉN NORMALIZAR
            if (valor != null)
            {
                if (valor is JsonElement jsonElement)
                {
                    resultado[claveNormalizada] = ExtraerValorJsonElement(jsonElement);
                }
                else
                {
                    resultado[claveNormalizada] = valor.ToString();
                }
            }
            else
            {
                resultado[claveNormalizada] = null;
            }
        }
    }

    return resultado;
}

// ✅ NUEVO MÉTODO: Normalizar nombres de propiedades
private string NormalizarNombrePropiedad(string nombrePropiedad)
{
    if (string.IsNullOrEmpty(nombrePropiedad))
        return nombrePropiedad;

    // ✅ CONVERTIR PascalCase a camelCase
    if (char.IsUpper(nombrePropiedad[0]))
    {
        return char.ToLower(nombrePropiedad[0]) + nombrePropiedad.Substring(1);
    }

    return nombrePropiedad;
}

// ✅ NUEVO MÉTODO: Extraer valor de JsonElement
private object? ExtraerValorJsonElement(JsonElement element)
{
    return element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt32(out var intVal) ? intVal : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => element.GetRawText()
    };
}

}