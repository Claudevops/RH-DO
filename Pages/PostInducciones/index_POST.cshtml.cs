
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
public class index_postmodel : PageModel
{

    private readonly EncryptionService _encryptionService;
    public index_postmodel(AppDbContext db, EmailService emailService, EncryptionService encryptionService)
    {
        _db = db;
        _emailService = emailService;
        _encryptionService = encryptionService;
    }


    public List<Analista> Analistas { get; set; } = new();
    public List<Sociedad> Sociedades { get; set; } = new();
    public List<Gerencia> Gerencias { get; set; } = new();
    public List<Ubicaciones> Ubicaciones { get; set; } = new();

    // PARA LAS NOTIFICACIONES
    public List<Empleado> EmpleadosEficacia90Dias { get; set; } = new();
    public int NotificacionesEficacia { get; set; } = 0;

    // NUEVAS PROPIEDADES PARA NOTIFICACIONES DE 7 DÍAS
    public List<Empleado> Empleados7Dias { get; set; } = new();
    public int Notificaciones7Dias { get; set; } = 0;

    // PROPIEDADES DE DEBUG
    public string DebugInfo { get; set; } = "";
    public DateTime FechaHoy { get; set; }
    public DateTime FechaLimite { get; set; }
    public DateTime FechaRango { get; set; }
    public int TotalEmpleadosConFecha { get; set; }
    public int EmpleadosCandidatos { get; set; }
    public int CorreosEnviados { get; set; }
    public DateTime FechaLimite7Dias { get; set; }
    public DateTime FechaRango7Dias { get; set; }
    public int EmpleadosCandidatos7Dias { get; set; }
    public int CorreosEnviados7Dias { get; set; }

    public bool EsAdmin { get; set; }


    private readonly AppDbContext _db;
    private readonly EmailService _emailService;
    [BindProperty(SupportsGet = true)]
    public string? BusquedaGeneral { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;
    public int TotalPages { get; set; }

    public List<Empleado> Empleados { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public DateTime? FechaBusqueda { get; set; }


    public int SolicitudesPendientes { get; set; } = 0;
    //FILTRADO
    public void OnGet()
    {

        Analistas = _db.Analistas.ToList();
        Sociedades = _db.Sociedades.ToList();
        Gerencias = _db.Gerencias.ToList();
        Ubicaciones = _db.Ubicaciones.ToList();
        var query = _db.Empleados.AsQueryable();


        // Verifica si el usuario es administrador
        var usuario = _db.Usuarios.FirstOrDefault(u => u.Correo == User.Identity.Name);
        EsAdmin = usuario?.EsAdmin ?? false;

        if (FechaBusqueda.HasValue)
        {
            query = query.Where(e => e.Fecha_Ingreso.HasValue && e.Fecha_Ingreso.Value.Date == FechaBusqueda.Value.Date);
        }
        else if (!string.IsNullOrWhiteSpace(BusquedaGeneral))
        {
            string filtro = BusquedaGeneral.ToLower().Replace(".", "").Replace("-", "").Trim();

            // Trae solo los campos necesarios para filtrar en memoria
            var empleados = query.ToList();

            foreach (var emp in empleados)
            {
                DesencriptarDatosEmpleado(emp);
            }

            Empleados = empleados.Where(e =>
                (!string.IsNullOrEmpty(e.RUT) && new string(e.RUT.Where(char.IsLetterOrDigit).ToArray()).ToLower().Contains(filtro)) ||
                (((e.Nombre ?? "") + " " + (e.Apellido_Paterno ?? "") + " " + (e.Apellido_Materno ?? "")).ToLower().Contains(filtro)) ||
                (!string.IsNullOrEmpty(e.Ticket) && e.Ticket.ToLower().Contains(filtro))
            )
            .OrderByDescending(e => e.OrdenImportacion)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToList();

            TotalPages = (int)Math.Ceiling(empleados.Count / (double)PageSize);
            return;
        }

        int totalItems = query.Count();
        TotalPages = (int)Math.Ceiling(totalItems / (double)PageSize);

        Empleados = query
            .OrderByDescending(e => e.OrdenImportacion)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToList();


        CalcularNotificacionesEficacia();
        CalcularNotificaciones7Dias();

        foreach (var empleado in Empleados)
        {
            DesencriptarDatosEmpleado(empleado);
        }
        if (EsAdmin)
        {
            SolicitudesPendientes = _db.SolicitudesEdicion.Count(s => !s.Procesada);
        }

    }

private void CalcularNotificaciones7Dias()
{
    try
    {
        var fechaHoy = DateTime.Now.Date;
        var fechaLimite7Dias = fechaHoy.AddDays(-7); 
        var fechaRango7Dias = fechaHoy.AddDays(-10);  

        FechaLimite7Dias = fechaLimite7Dias;
        FechaRango7Dias = fechaRango7Dias;
        var empleadosCandidatos7Dias = _db.Empleados
            .Where(e => e.Fecha_Ingreso.HasValue &&
                       e.Fecha_Ingreso.Value.Date <= fechaRango7Dias && 
                       e.Fecha_Ingreso.Value.Date >= fechaLimite7Dias) 
            .ToList();

        EmpleadosCandidatos7Dias = empleadosCandidatos7Dias.Count;

        var correosTipo7Dias = _db.CorreosEnviados_POST
            .Where(c => c.TipoCorreo == 6)
            .ToList();

        CorreosEnviados7Dias = correosTipo7Dias.Count;
        var rutsConCorreoEnviado7Dias = correosTipo7Dias
            .Where(c => c.Completado)
            .Select(c => c.RUT)
            .ToList();

        Empleados7Dias = empleadosCandidatos7Dias
            .Where(e => !rutsConCorreoEnviado7Dias.Contains(e.RUT))
            .OrderBy(e => e.Fecha_Ingreso)
            .ToList();

        Notificaciones7Dias = Empleados7Dias.Count;

        var debugList7Dias = new List<string>
        {
            $"=== NOTIFICACIONES 7 DÍAS (CORREGIDO) ===",
            $"Fecha actual: {fechaHoy:dd/MM/yyyy}",
            $"Empleados que ingresaron DESDE {fechaLimite7Dias:dd/MM/yyyy} HASTA {fechaRango7Dias:dd/MM/yyyy}",
            $"Empleados candidatos 7 días (en rango): {EmpleadosCandidatos7Dias}",
            $"Correos tipo 6 enviados en BD: {CorreosEnviados7Dias}",
            $"RUTs con correo tipo 6 completado: [{string.Join(", ", rutsConCorreoEnviado7Dias)}]",
            $"Empleados finales para notificación 7 días: {Notificaciones7Dias}"
        };

        if (empleadosCandidatos7Dias.Any())
        {
            debugList7Dias.Add("Empleados candidatos 7 días:");
            foreach (var emp in empleadosCandidatos7Dias)
            {
                var diasTranscurridos = (fechaHoy - emp.Fecha_Ingreso.Value.Date).Days;
                debugList7Dias.Add($"  - {emp.Nombre} {emp.Apellido_Paterno} (RUT: {emp.RUT}) - Ingreso: {emp.Fecha_Ingreso?.ToString("dd/MM/yyyy")} ({diasTranscurridos} días transcurridos)");
            }
        }

        if (Empleados7Dias.Any())
        {
            debugList7Dias.Add("Empleados para notificar (7 días):");
            foreach (var emp in Empleados7Dias)
            {
                var diasTranscurridos = (fechaHoy - emp.Fecha_Ingreso.Value.Date).Days;
                debugList7Dias.Add($"  → {emp.Nombre} {emp.Apellido_Paterno} (RUT: {emp.RUT}) - Ingreso: {emp.Fecha_Ingreso?.ToString("dd/MM/yyyy")} ({diasTranscurridos} días transcurridos)");
            }
        }

        // Combinar debug info existente con nuevo
        DebugInfo += "\n\n" + string.Join("\n", debugList7Dias);
    }
    catch (Exception ex)
    {
        DebugInfo += $"\n\nERROR 7 DÍAS: {ex.Message}\nStack: {ex.StackTrace}";
        Empleados7Dias = new List<Empleado>();
        Notificaciones7Dias = 0;
    }
}
    private void CalcularNotificacionesEficacia()
    {
        try
        {
            var fechaHoy = DateTime.Now.Date;
            var fechaLimite = fechaHoy.AddDays(-90);
            var fechaRango = fechaHoy.AddDays(-100);

            FechaHoy = fechaHoy;
            FechaLimite = fechaLimite;
            FechaRango = fechaRango;

            // Contar total de empleados con fecha de ingreso
            TotalEmpleadosConFecha = _db.Empleados.Count(e => e.Fecha_Ingreso.HasValue);

            // Obtener empleados que ingresaron hace aproximadamente 90 días
            var empleadosCandidatos = _db.Empleados
                .Where(e => e.Fecha_Ingreso.HasValue &&
                           e.Fecha_Ingreso.Value.Date >= fechaRango &&
                           e.Fecha_Ingreso.Value.Date <= fechaLimite)
                .ToList();

            EmpleadosCandidatos = empleadosCandidatos.Count;

            // Verificar si existe la tabla de correos enviados
            var correosTipoEficacia = _db.CorreosEnviados_POST
                .Where(c => c.TipoCorreo == 3)
                .ToList();

            CorreosEnviados = correosTipoEficacia.Count;

            // Filtrar empleados que NO han recibido el correo tipo 3 (Eficacia)
            var rutsConCorreoEnviado = correosTipoEficacia
                .Where(c => c.Completado)
                .Select(c => c.RUT)
                .ToList();

            EmpleadosEficacia90Dias = empleadosCandidatos
                .Where(e => !rutsConCorreoEnviado.Contains(e.RUT))
                .OrderBy(e => e.Fecha_Ingreso)
                .ToList();

            NotificacionesEficacia = EmpleadosEficacia90Dias.Count;

            // Crear información de debug
            var debugList = new List<string>
        {
            $"Fecha actual: {fechaHoy:dd/MM/yyyy}",
            $"Rango búsqueda: {fechaRango:dd/MM/yyyy} - {fechaLimite:dd/MM/yyyy}",
            $"Total empleados con fecha ingreso: {TotalEmpleadosConFecha}",
            $"Empleados candidatos (en rango): {EmpleadosCandidatos}",
            $"Correos tipo 3 enviados en BD: {CorreosEnviados}",
            $"RUTs con correo completado: [{string.Join(", ", rutsConCorreoEnviado)}]",
            $"Empleados finales para notificación: {NotificacionesEficacia}"
        };

            if (empleadosCandidatos.Any())
            {
                debugList.Add("Empleados candidatos:");
                foreach (var emp in empleadosCandidatos)
                {
                    debugList.Add($"  - {emp.Nombre} {emp.Apellido_Paterno} (RUT: {emp.RUT}) - Ingreso: {emp.Fecha_Ingreso?.ToString("dd/MM/yyyy")}");
                }
            }

            if (EmpleadosEficacia90Dias.Any())
            {
                debugList.Add("Empleados para notificar:");
                foreach (var emp in EmpleadosEficacia90Dias)
                {
                    debugList.Add($"  → {emp.Nombre} {emp.Apellido_Paterno} (RUT: {emp.RUT}) - Ingreso: {emp.Fecha_Ingreso?.ToString("dd/MM/yyyy")}");
                }
            }

            DebugInfo = string.Join("\n", debugList);
        }
        catch (Exception ex)
        {
            DebugInfo = $"ERROR: {ex.Message}\nStack: {ex.StackTrace}";
            EmpleadosEficacia90Dias = new List<Empleado>();
            NotificacionesEficacia = 0;
        }
    }

    // Método para marcar correo como enviado
    public async Task<IActionResult> OnPostMarcarCorreoEnviado()
    {
        try
        {
            string body;
            using (var reader = new StreamReader(Request.Body, encoding: Encoding.UTF8,
                   detectEncodingFromByteOrderMarks: false, leaveOpen: true))
            {
                body = await reader.ReadToEndAsync();
            }

            using var jsonDoc = JsonDocument.Parse(body);
            var root = jsonDoc.RootElement;

            var rutsArray = root.GetProperty("ruts").EnumerateArray();
            var ruts = rutsArray.Select(x => x.GetString()).Where(r => !string.IsNullOrEmpty(r)).ToList();
            var tipoCorreo = root.GetProperty("tipoCorreo").GetInt32();

            foreach (var rut in ruts)
            {
                var empleado = await _db.Empleados.FirstOrDefaultAsync(e => e.RUT == rut);
                if (empleado != null)
                {
                    var correoEnviado = new CorreoEnviado_POST
                    {
                        RUT = rut!,
                        TipoCorreo = tipoCorreo,
                        FechaEnvio = DateTime.Now,
                        EmailDestinatario = empleado.Correo ?? "",
                        Completado = true
                    };

                    _db.CorreosEnviados_POST.Add(correoEnviado);
                }
            }

            await _db.SaveChangesAsync();

            return new JsonResult(new { success = true, message = "Correos marcados como enviados" });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, error = ex.Message });
        }
    }



    //EDITAR PARA CORREOS DE POST INDUCCIONES
    public JsonResult OnGetCorreoPreview(int? tipo, string? fecha = null, string? rut = null, string? modo = null)
    {
        // Validar tipo de correo POST (1, 2, 3, 4, 6)
        if (tipo == null || tipo < 1 || tipo > 6 || tipo == 5)
            return new JsonResult(new { Error = "Tipo de correo inválido", Asunto = "", Destinatarios = "", CC = "", CCO = "", Contenido = "", Adjuntos = new List<string>() });

        // USAR TABLAS POST 
        var plantilla = _db.PlantillaCorreo_POST.FirstOrDefault(p => p.Tipo_PC == tipo);
        var destinatarios = _db.DestinatariosCorreo_POST.FirstOrDefault(d => d.Tipo_DCP == tipo);

        string destinatarioFinal = destinatarios?.DestinatariosFijos ?? "";

        // 1. Obtener empleados seleccionados
        List<Empleado> empleados = new();

        if (!string.IsNullOrEmpty(rut))
        {
            // Si hay múltiples RUTs (separados por coma)
            var ruts = rut.Split(',').Select(r => r.Trim()).Where(r => !string.IsNullOrEmpty(r));
            empleados = _db.Empleados.Where(e => ruts.Contains(e.RUT)).ToList();
        }
        else if (!string.IsNullOrEmpty(fecha))
        {
            if (DateTime.TryParse(fecha, out var fechaIngreso))
            {
                empleados = _db.Empleados.Where(e => e.Fecha_Ingreso.HasValue && e.Fecha_Ingreso.Value.Date == fechaIngreso.Date).ToList();
            }
        }

        // DESENCRIPTAR DATOS DE LOS EMPLEADOS PARA EL CORREO
        foreach (var empleado in empleados)
        {
            DesencriptarDatosEmpleado(empleado);
        }

        // Determinar destinatarios según el tipo
        if (empleados.Count > 0)
        {
            switch (tipo)
            {
                case 2:
                    // Para tipo 2, usar los correos personales de los empleados
                    var correosPersonales = empleados
                        .Where(e => !string.IsNullOrWhiteSpace(e.Correo))
                        .Select(e => e.Correo.Trim())
                        .Distinct()
                        .ToList();
                    destinatarioFinal = string.Join(";", correosPersonales);
                    break;

                case 6:
                    // Para tipo 6, usar los correos personales de los empleados (igual que tipo 2)
                    var correosPersonales6 = empleados
                        .Where(e => !string.IsNullOrWhiteSpace(e.Correo))
                        .Select(e => e.Correo.Trim())
                        .Distinct()
                        .ToList();
                    destinatarioFinal = string.Join(";", correosPersonales6);
                    break;

                default:
                    // Para otros tipos (ej: tipo 3), usar correos de gerencia
                    var correosGerencia = empleados
                        .Where(e => !string.IsNullOrWhiteSpace(e.Gerencia_mail))
                        .Select(e => e.Gerencia_mail.Trim())
                        .Distinct()
                        .ToList();
                    destinatarioFinal = string.Join(";", correosGerencia);
                    break;
            }
        }

        // 2. Construir tabla HTML según el tipo de correo
        string tablaEmpleados = "";
        if (empleados.Count > 0)
        {
            if (tipo == 6)
            {
                // Tabla específica para correo tipo 6 - Consulta por accesos y elementos corporativos
                tablaEmpleados = @"
<table style='border-collapse: collapse; margin: 10px 0; font-family: ""Trebuchet MS"", Arial, sans-serif; font-size: 10px; border: 1px solid #000000; width: 100%;'>
    <thead>
        <tr style='border: 1px solid #000000;'>
            <th style='background-color: #4472C4; color: white; padding: 8px; border: 1px solid #000000; text-align: left; font-weight: bold; font-size: 10px; white-space: nowrap; font-family: ""Trebuchet MS"", Arial, sans-serif;'>Nombres</th>
            <th style='background-color: #4472C4; color: white; padding: 8px; border: 1px solid #000000; text-align: left; font-weight: bold; font-size: 10px; font-family: ""Trebuchet MS"", Arial, sans-serif;'>Acceso y funcionamiento óptimo de correo corporativo (Si/No)</th>
            <th style='background-color: #4472C4; color: white; padding: 8px; border: 1px solid #000000; text-align: left; font-weight: bold; font-size: 10px; font-family: ""Trebuchet MS"", Arial, sans-serif;'>Laptop entregado (Si/No)<br/><small>Importante: En caso de no tenerlo aún, por favor confirmar si subirán al T1 vs tomo contacto con usted para indicar fecha estimada de entrega</small></th>
            <th style='background-color: #4472C4; color: white; padding: 8px; border: 1px solid #000000; text-align: left; font-weight: bold; font-size: 10px; font-family: ""Trebuchet MS"", Arial, sans-serif;'>Acceso a Fiori (Si, No)<br/><small>Importante: De no tener acceso, favor detallar el problema que tomo contacto con usted para indicar fecha estimada de entrega</small><br/><a href='https://fiori.esval.cl/sites#Shell-home' style='color: #ffffff; text-decoration: underline;'>https://fiori.esval.cl/sites#Shell-home</a></th>
            <th style='background-color: #4472C4; color: white; padding: 8px; border: 1px solid #000000; text-align: left; font-weight: bold; font-size: 10px; font-family: ""Trebuchet MS"", Arial, sans-serif;'>Acceso a Academia de Formación y Entrenamiento (Si, No)<br/><small>Link a Academia - continuación:</small><br/><a href='https://esvaladv.academia365.cl/' style='color: #ffffff; text-decoration: underline;'>https://esvaladv.academia365.cl/</a></th>
            <th style='background-color: #4472C4; color: white; padding: 8px; border: 1px solid #000000; text-align: left; font-weight: bold; font-size: 10px; font-family: ""Trebuchet MS"", Arial, sans-serif;'>Cuento con el apoyo de mi líder o equipo directo para conocer detalles de mi cargo y funciones (Si, No)</th>
        </tr>
    </thead>
    <tbody>";
                
                // Agregar filas de empleados - UNA FILA POR EMPLEADO
                for (int i = 0; i < empleados.Count; i++)
                {
                    var emp = empleados[i];

                    tablaEmpleados += $@"
        <tr style='border: 1px solid #000000;'>
            <td style='padding: 6px 8px; border: 1px solid #000000; font-size: 10px; font-family: ""Trebuchet MS"", Arial, sans-serif; vertical-align: top;'>{emp.Nombre} {emp.Apellido_Paterno}</td>
            <td style='padding: 6px 8px; border: 1px solid #000000; font-size: 10px; font-family: ""Trebuchet MS"", Arial, sans-serif; vertical-align: top;'></td>
            <td style='padding: 6px 8px; border: 1px solid #000000; font-size: 10px; font-family: ""Trebuchet MS"", Arial, sans-serif; vertical-align: top;'></td>
            <td style='padding: 6px 8px; border: 1px solid #000000; font-size: 10px; font-family: ""Trebuchet MS"", Arial, sans-serif; vertical-align: top;'></td>
            <td style='padding: 6px 8px; border: 1px solid #000000; font-size: 10px; font-family: ""Trebuchet MS"", Arial, sans-serif; vertical-align: top;'></td>
            <td style='padding: 6px 8px; border: 1px solid #000000; font-size: 10px; font-family: ""Trebuchet MS"", Arial, sans-serif; vertical-align: top;'></td>
        </tr>";
                }

                tablaEmpleados += "</tbody></table>";
            }
            else
            {
                // Tabla estándar para otros tipos POST
                tablaEmpleados = CrearTablaEmpleadosEstandar(empleados);
            }
        }

        // 3. Reemplazar placeholders en el contenido y asunto
        string contenido = plantilla?.HtmlContenido ?? "(Sin contenido)";
        string asunto = plantilla?.Nombre_PC ?? "(Sin asunto)";

        // Reemplazar placeholder de tabla
        contenido = contenido.Replace("<strong>&lt;!--EMPLEADOS_TABLA--&gt;</strong>", tablaEmpleados);

        // Siempre reemplazar placeholders, aunque haya múltiples empleados
        if (empleados.Count > 0)
        {
            var primerEmpleado = empleados[0]; // Usar el primer empleado para el preview

            // Reemplazar placeholders en contenido
            contenido = ReemplazarPlaceholders(contenido, primerEmpleado);

            // Reemplazar placeholders en asunto
            asunto = ReemplazarPlaceholders(asunto, primerEmpleado);

            // Si hay múltiples empleados, agregar nota en el asunto
            if (empleados.Count > 1)
            {
                asunto += $" (y {empleados.Count - 1} más)";
            }
        }

        // USAR ADJUNTOS POST
        var adjuntos = _db.AdjuntoCorreo_post
            .Where(a => a.Tipo == tipo)
            .Select(a => a.Nombre_Adjunto)
            .ToList();

        return new JsonResult(new
        {
            Asunto = asunto,
            Destinatarios = destinatarioFinal,
            CC = destinatarios?.CCFijos ?? "",
            CCO = destinatarios?.CCOFijos ?? "",
            Contenido = contenido,
            Adjuntos = adjuntos
        });
    }

    // Método auxiliar para crear tabla estándar de empleados
    private string CrearTablaEmpleadosEstandar(List<Empleado> empleados)
    {
        var tabla = @"
<table style='border-collapse: collapse; margin: 10px 0; font-family: ""Trebuchet MS"", Arial, sans-serif; font-size: 10px; border: 1px solid #000000; width: 100%;'>
    <thead>
        <tr style='border: 1px solid #000000;'>
            <th style='background-color: #4472C4; color: white; padding: 8px; border: 1px solid #000000; text-align: left; font-weight: bold; font-size: 10px; white-space: nowrap; font-family: ""Trebuchet MS"", Arial, sans-serif;'>Fecha de Ingreso</th>
            <th style='background-color: #4472C4; color: white; padding: 8px; border: 1px solid #000000; text-align: left; font-weight: bold; font-size: 10px; white-space: nowrap; font-family: ""Trebuchet MS"", Arial, sans-serif;'>RUT</th>
            <th style='background-color: #4472C4; color: white; padding: 8px; border: 1px solid #000000; text-align: left; font-weight: bold; font-size: 10px; white-space: nowrap; font-family: ""Trebuchet MS"", Arial, sans-serif;'>Nombre</th>
            <th style='background-color: #4472C4; color: white; padding: 8px; border: 1px solid #000000; text-align: left; font-weight: bold; font-size: 10px; white-space: nowrap; font-family: ""Trebuchet MS"", Arial, sans-serif;'>Correo</th>
            <th style='background-color: #4472C4; color: white; padding: 8px; border: 1px solid #000000; text-align: left; font-weight: bold; font-size: 10px; white-space: nowrap; font-family: ""Trebuchet MS"", Arial, sans-serif;'>Cargo</th>
        </tr>
    </thead>
    <tbody>";

        foreach (var emp in empleados)
        {
            tabla += $@"
        <tr style='border: 1px solid #000000;'>
            <td style='padding: 6px 8px; border: 1px solid #000000; font-size: 10px; font-family: ""Trebuchet MS"", Arial, sans-serif; vertical-align: top;'>{(emp.Fecha_Ingreso.HasValue ? emp.Fecha_Ingreso.Value.ToString("dd-MM-yyyy") : "")}</td>
            <td style='padding: 6px 8px; border: 1px solid #000000; font-size: 10px; font-family: ""Trebuchet MS"", Arial, sans-serif; vertical-align: top; white-space: nowrap; font-weight: bold;'>{emp.RUT}</td>
            <td style='padding: 6px 8px; border: 1px solid #000000; font-size: 10px; font-family: ""Trebuchet MS"", Arial, sans-serif; vertical-align: top;'>{emp.Nombre} {emp.Apellido_Paterno}</td>
            <td style='padding: 6px 8px; border: 1px solid #000000; font-size: 10px; font-family: ""Trebuchet MS"", Arial, sans-serif; vertical-align: top;'>{emp.Correo}</td>
            <td style='padding: 6px 8px; border: 1px solid #000000; font-size: 10px; font-family: ""Trebuchet MS"", Arial, sans-serif; vertical-align: top;'>{emp.Cargo}</td>
        </tr>";
        }

        tabla += "</tbody></table>";
        return tabla;
    }

    private string ReemplazarPlaceholders(string texto, Empleado empleado)
    {
        if (string.IsNullOrEmpty(texto) || empleado == null)
            return texto;

        return texto.Replace("{{ emp.Nombre }}", empleado.Nombre ?? "")
                    .Replace("{{ emp.Analista }}", empleado.Analista ?? "")
                    .Replace("{{ emp.RUT }}", empleado.RUT ?? "")
                    .Replace("{{ emp.Correo }}", empleado.Correo ?? "")
                    .Replace("{{ emp.Cargo }}", empleado.Cargo ?? "")
                    .Replace("{{ emp.Direccion }}", empleado.Direccion ?? "")
                    .Replace("{{ emp.Jefe_Directo }}", empleado.Jefe_Directo ?? "")
                    .Replace("{{ emp.Apellido_Paterno }}", empleado.Apellido_Paterno ?? "")
                    .Replace("{{ emp.Telefono }}", empleado.Telefono ?? "")
                    .Replace("{{ emp.Ticket }}", empleado.Ticket ?? "")
                    .Replace("{{ emp.Apellido_Materno }}", empleado.Apellido_Materno ?? "")
                    .Replace("{{ emp.Sociedad }}", empleado.Sociedad ?? "")
                    .Replace("{{ emp.Gerencia }}", empleado.Gerencia ?? "")
                    .Replace("{{ emp.Ubicacion }}", empleado.Ubicacion ?? "")
                    .Replace("{{ emp.Tipo_de_Contrato }}", empleado.Tipo_de_Contrato ?? "")
                    .Replace("{{ emp.Fecha_Ingreso }}", empleado.Fecha_Ingreso?.ToString("dd/MM/yyyy") ?? "")
                    .Replace("{{ emp.Fecha_Induccion }}", empleado.Fecha_Induccion?.ToString("dd/MM/yyyy") ?? "")
                    .Replace("{{ emp.Fecha_Nacimiento }}", empleado.Fecha_Nacimiento?.ToString("dd/MM/yyyy") ?? "");
    }

    public async Task<IActionResult> OnPostEditarEmpleadoAsync()
    {
        try
        {
            var form = Request.Form;
            string rut = form["RUT"];
            var empleado = await _db.Empleados.FirstOrDefaultAsync(e => e.RUT == rut);
            if (empleado == null)
            {
                TempData["MensajeError"] = "Empleado no encontrado.";
                return Page();
            }

            // Actualizar campos
            empleado.Analista = form["Analista"];

            // Manejar fechas correctamente para evitar cambios de zona horaria
            if (DateTime.TryParseExact(form["Fecha_Induccion"], "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var fechaInd))
            {
                empleado.Fecha_Induccion = fechaInd;
            }

            if (DateTime.TryParseExact(form["Fecha_Ingreso"], "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var fechaIng))
            {
                empleado.Fecha_Ingreso = fechaIng;
            }

            var nombre = form["Nombre"].ToString();
            empleado.Nombre = string.IsNullOrEmpty(nombre) ? empleado.Nombre : nombre;
            empleado.Apellido_Paterno = form["Apellido_Paterno"];
            empleado.Apellido_Materno = form["Apellido_Materno"];

            if (DateTime.TryParseExact(form["Fecha_Nacimiento"], "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var fechaNac))
            {
                empleado.Fecha_Nacimiento = fechaNac;
            }

            empleado.Correo = form["Correo"];
            empleado.Telefono = form["Telefono"];
            empleado.Direccion = form["Direccion"];
            empleado.Sociedad = form["Sociedad"];
            empleado.Cargo = form["Cargo"];
            empleado.Gerencia = form["Gerencia"];
            empleado.Ubicacion = form["Ubicacion"];
            empleado.Jefe_Directo = form["Jefe_Directo"];
            empleado.Gerencia_mail = form["Gerencia_mail"];
            empleado.Tipo_de_Contrato = form["Tipo_de_Contrato"];
            empleado.Quienrepone = form["Quienrepone"];
            empleado.Contrato = form["Contrato"];
            empleado.Ticket = form["Ticket"];
            empleado.Carrera = form["Carrera"];
            empleado.Universidad = form["Universidad"];
            empleado.UltimaModificacion = DateTime.Now;
            empleado.UsuarioModifico = System.Web.HttpUtility.HtmlEncode(User.Identity?.Name) ?? "Desconocido";


            await _db.SaveChangesAsync();
            TempData["MensajeExito"] = "Empleado editado correctamente.";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            TempData["MensajeError"] = "Error al editar empleado: " + ex.Message;
            return Page();
        }
    }

    //WEA DEL JSON Y SOLICITUD DE EDICIÓN
    public JsonResult OnGetEmpleado(string rut)
    {
        var empleado = _db.Empleados.FirstOrDefault(e => e.RUT == rut);
        if (empleado == null)
            return new JsonResult(new { error = "Empleado no encontrado" });

        // DESENCRIPTAR DATOS ANTES DE ENVIAR AL FRONTEND
        DesencriptarDatosEmpleado(empleado);

        var empleadoData = new
        {
            id = empleado.Id,
            analista = empleado.Analista,
            entrevista_Experiencia = empleado.Entrevista_Experiencia,
            encuesta_Eficacia = empleado.Encuesta_Eficacia,
            fecha_Induccion = empleado.Fecha_Induccion?.ToString("yyyy-MM-dd"),
            fecha_Ingreso = empleado.Fecha_Ingreso?.ToString("yyyy-MM-dd"),
            rut = empleado.RUT,
            nombre = empleado.Nombre,
            apellido_Paterno = empleado.Apellido_Paterno,
            apellido_Materno = empleado.Apellido_Materno,
            fecha_Nacimiento = empleado.Fecha_Nacimiento?.ToString("yyyy-MM-dd"),
            correo = empleado.Correo,
            telefono = empleado.Telefono,
            direccion = empleado.Direccion,
            sociedad = empleado.Sociedad,
            cargo = empleado.Cargo,
            gerencia = empleado.Gerencia,
            gerencia_mail = empleado.Gerencia_mail,
            ubicacion = empleado.Ubicacion,
            jefe_Directo = empleado.Jefe_Directo,
            tipo_de_Contrato = empleado.Tipo_de_Contrato,
            quienrepone = empleado.Quienrepone,
            contrato = empleado.Contrato,
            ticket = empleado.Ticket,
            fecha_Induccion_Cultural = empleado.Fecha_Induccion_Cultural?.ToString("yyyy-MM-dd"),
            estado_Induccion_Cultural = empleado.Estado_Induccion_Cultural,
            carrera = empleado.Carrera,
            universidad = empleado.Universidad
        };

        return new JsonResult(empleadoData);
    }

    public async Task<IActionResult> OnPostSolicitarEdicion()
    {
        try
        {
            string body;
            using (var reader = new StreamReader(Request.Body, encoding: System.Text.Encoding.UTF8,
                   detectEncodingFromByteOrderMarks: false, leaveOpen: true))
            {
                body = await reader.ReadToEndAsync();
            }

            System.Diagnostics.Debug.WriteLine("Cuerpo recibido: " + body);

            if (string.IsNullOrWhiteSpace(body))
            {
                return new JsonResult(new { success = false, error = "Cuerpo de solicitud vacío" });
            }

            // PARSEAR JSON CON MANEJO CORRECTO DE FECHAS
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (!root.TryGetProperty("RUT", out var rutElement))
            {
                return new JsonResult(new { success = false, error = "RUT no encontrado en la solicitud" });
            }

            string rut = rutElement.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(rut))
            {
                return new JsonResult(new { success = false, error = "RUT vacío" });
            }

            // Buscar empleado actual
            var empleadoActual = await _db.Empleados.FirstOrDefaultAsync(e => e.RUT == rut);
            if (empleadoActual == null)
                return new JsonResult(new { success = false, error = "Empleado no encontrado" });

            // CORREGIR FECHAS EN LA SOLICITUD ANTES DE GUARDAR
            var bodySolicitadoCorregido = CorregirFechasEnJson(body);

            System.Diagnostics.Debug.WriteLine("Cuerpo original: " + body);
            System.Diagnostics.Debug.WriteLine("Cuerpo corregido: " + bodySolicitadoCorregido);

            // Guardar los datos actuales como JSON
            var datosActuales = System.Text.Json.JsonSerializer.Serialize(empleadoActual, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var solicitud = new SolicitudEdicion
            {
                RutEmpleado = rut,
                DatosSolicitados = bodySolicitadoCorregido,
                DatosAnteriores = datosActuales,
                UsuarioSolicitante = User.Identity?.Name ?? "Desconocido",
                Tipo = "Edicion",
                FechaSolicitud = DateTime.Now
            };

            _db.SolicitudesEdicion.Add(solicitud);
            await _db.SaveChangesAsync();

            return new JsonResult(new
            {
                success = true,
                message = "Solicitud enviada correctamente. El administrador revisará tu solicitud."
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Excepción en OnPostSolicitarEdicion: " + ex.ToString());
            return new JsonResult(new { success = false, error = "Error en el servidor: " + ex.Message });
        }
    }

    //
    private string CorregirFechasEnJson(string jsonString)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            var jsonObject = new Dictionary<string, object?>();

            foreach (var property in root.EnumerateObject())
            {
                var value = property.Value;

                if (EsCampoFecha(property.Name) && value.ValueKind == JsonValueKind.String)
                {
                    var fechaString = value.GetString();

                    if (!string.IsNullOrEmpty(fechaString))
                    {
                        if (fechaString.Length == 10 && fechaString.Contains('-'))
                        {
                            jsonObject[property.Name] = fechaString;
                            System.Diagnostics.Debug.WriteLine($"POST - Campo {property.Name}: MANTENIDO '{fechaString}'");
                        }
                        else if (DateTime.TryParse(fechaString, out var fecha))
                        {
                            var fechaFormateada = fecha.ToString("yyyy-MM-dd");
                            jsonObject[property.Name] = fechaFormateada;
                            System.Diagnostics.Debug.WriteLine($"POST - Campo {property.Name}: FORMATEADO '{fechaString}' -> '{fechaFormateada}'");
                        }
                        else
                        {
                            jsonObject[property.Name] = fechaString;
                        }
                    }
                    else
                    {
                        jsonObject[property.Name] = null;
                    }
                }
                else
                {
                    jsonObject[property.Name] = ExtraerValorJson(value);
                }
            }

            return System.Text.Json.JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"POST - Error al procesar fechas en JSON: {ex.Message}");
            return jsonString;
        }
    }

    private bool EsCampoFecha(string nombreCampo)
    {
        var camposFecha = new[]
        {
        "fecha_Induccion", "fechaInduccion", "Fecha_Induccion",
        "fecha_Ingreso", "fechaIngreso", "Fecha_Ingreso",
        "fecha_Nacimiento", "fechaNacimiento", "Fecha_Nacimiento",
        "fecha_Induccion_Cultural", "fechaInduccionCultural", "Fecha_Induccion_Cultural"
    };

        return camposFecha.Any(cf => string.Equals(cf, nombreCampo, StringComparison.OrdinalIgnoreCase));
    }



    private object? ExtraerValorJson(JsonElement element)
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







    // Método para eliminar empleado (solo administradores)
    public async Task<IActionResult> OnPostEliminarEmpleadoAsync(string rut)
    {
        try
        {
            // Verificar que el usuario sea administrador
            var usuario = _db.Usuarios.FirstOrDefault(u => u.Correo == User.Identity.Name);
            if (usuario?.EsAdmin != true)
            {
                TempData["MensajeError"] = "No tienes permisos para eliminar empleados.";
                return RedirectToPage();
            }

            var empleado = await _db.Empleados.FirstOrDefaultAsync(e => e.RUT == rut);
            if (empleado == null)
            {
                TempData["MensajeError"] = "Empleado no encontrado.";
                return RedirectToPage();
            }

            // Eliminar el empleado
            _db.Empleados.Remove(empleado);
            await _db.SaveChangesAsync();

            TempData["MensajeExito"] = $"Empleado {empleado.Nombre} {empleado.Apellido_Paterno} eliminado correctamente.";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            TempData["MensajeError"] = "Error al eliminar empleado: " + ex.Message;
            return RedirectToPage();
        }
    }

    // Método para solicitar eliminación (usuarios no administradores)
    public async Task<IActionResult> OnPostSolicitarEliminacion()
    {
        try
        {
            string body;
            using (var reader = new StreamReader(Request.Body, encoding: System.Text.Encoding.UTF8,
                   detectEncodingFromByteOrderMarks: false, leaveOpen: true))
            {
                body = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                return new JsonResult(new { success = false, error = "Cuerpo de solicitud vacío" });
            }

            // Parsear la solicitud
            string rut;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(body);
                if (!doc.RootElement.TryGetProperty("RUT", out var rutElement))
                {
                    return new JsonResult(new { success = false, error = "RUT no encontrado en la solicitud" });
                }
                rut = rutElement.GetString() ?? "";
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, error = "Error al parsear JSON: " + ex.Message });
            }

            if (string.IsNullOrWhiteSpace(rut))
            {
                return new JsonResult(new { success = false, error = "RUT vacío" });
            }

            // Verificar que el empleado existe
            var empleado = await _db.Empleados.FirstOrDefaultAsync(e => e.RUT == rut);
            if (empleado == null)
            {
                return new JsonResult(new { success = false, error = "Empleado no encontrado" });
            }

            // Verificar si ya existe una solicitud pendiente para este empleado
            var solicitudExistente = await _db.SolicitudesEdicion
                .FirstOrDefaultAsync(s => s.RutEmpleado == rut && s.Tipo == "Eliminacion" && !s.Procesada);

            if (solicitudExistente != null)
            {
                return new JsonResult(new { success = false, error = "Ya existe una solicitud de eliminación pendiente para este empleado" });
            }

            // Guardar los datos actuales del empleado
            var datosEmpleado = System.Text.Json.JsonSerializer.Serialize(empleado);

            // Crear la solicitud de eliminación
            var solicitud = new SolicitudEdicion
            {
                RutEmpleado = rut,
                DatosSolicitados = body,
                DatosAnteriores = datosEmpleado, 
                UsuarioSolicitante = User.Identity?.Name ?? "Desconocido",
                Tipo = "Eliminacion",
                FechaSolicitud = DateTime.Now,
                Procesada = false
            };

            _db.SolicitudesEdicion.Add(solicitud);
            await _db.SaveChangesAsync();

            return new JsonResult(new
            {
                success = true,
                message = $"Solicitud de eliminación enviada para el empleado {empleado.Nombre} {empleado.Apellido_Paterno}. El administrador revisará tu solicitud."
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Excepción en OnPostSolicitarEliminacion: " + ex.ToString());

            return new JsonResult(new
            {
                success = false,
                error = "Error en el servidor: " + ex.Message
            });
        }
    }

    // Método para enviar correo desde modal
    public async Task<IActionResult> OnPostEnviarCorreo()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== INICIO OnPostEnviarCorreo POST ===");

            var form = Request.Form;

            string asunto = form["asunto"];
            string destinatarios = form["destinatarios"];
            string cc = form["cc"];
            string cco = form["cco"];
            string contenido = form["contenido"];
            string adjuntosBDJson = form["adjuntosBD"];
            string tipoCorreoStr = form["tipoCorreo"];

            System.Diagnostics.Debug.WriteLine($"Asunto: {asunto}");
            System.Diagnostics.Debug.WriteLine($"Destinatarios: {destinatarios}");
            System.Diagnostics.Debug.WriteLine($"CC: {cc}");
            System.Diagnostics.Debug.WriteLine($"CCO: {cco}");
            System.Diagnostics.Debug.WriteLine($"Contenido length: {contenido?.Length ?? 0}");
            System.Diagnostics.Debug.WriteLine($"Tipo de correo: {tipoCorreoStr}");

            // Obtener archivos adjuntos MANUALES
            var adjuntosManuales = new List<IFormFile>();

            foreach (var file in Request.Form.Files)
            {
                if (file.Name == "adjuntos" && file.Length > 0)
                {
                    adjuntosManuales.Add(file);
                    System.Diagnostics.Debug.WriteLine($"Archivo manual agregado: {file.FileName} ({file.Length} bytes)");
                }
            }

            // Procesar adjuntos de la BASE DE DATOS
            var adjuntosBD = new List<string>();
            if (!string.IsNullOrWhiteSpace(adjuntosBDJson))
            {
                try
                {
                    adjuntosBD = System.Text.Json.JsonSerializer.Deserialize<List<string>>(adjuntosBDJson) ?? new List<string>();
                    System.Diagnostics.Debug.WriteLine($"Adjuntos de BD deserializados: {adjuntosBD.Count}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error al deserializar adjuntos BD: {ex.Message}");
                }
            }

            // Validaciones básicas
            if (string.IsNullOrWhiteSpace(asunto))
            {
                return new JsonResult(new { success = false, error = "El asunto es requerido" });
            }

            if (string.IsNullOrWhiteSpace(destinatarios))
            {
                return new JsonResult(new { success = false, error = "Los destinatarios son requeridos" });
            }

            if (string.IsNullOrWhiteSpace(contenido))
            {
                return new JsonResult(new { success = false, error = "El contenido es requerido" });
            }

            // Enviar el correo
            bool enviado = await _emailService.EnviarCorreoAsync(
                asunto,
                destinatarios,
                cc,
                cco,
                contenido,
                adjuntosManuales,
                adjuntosBD
            );

            if (enviado)
            {
                // Marcar correos como enviados según el tipo
                if (int.TryParse(tipoCorreoStr, out int tipoCorreo))
                {
                    if (tipoCorreo == 3)
                    {
                        await MarcarCorreoTipo3Enviado(destinatarios);
                    }
                    else if (tipoCorreo == 6)
                    {
                        await MarcarCorreoTipo6Enviado(destinatarios);
                    }
                }

                await RegistrarEnvioCorreo(asunto, destinatarios, cc, cco, User.Identity?.Name ?? "Desconocido");

                int totalAdjuntos = adjuntosManuales.Count + adjuntosBD.Count;
                return new JsonResult(new
                {
                    success = true,
                    message = $"Correo enviado exitosamente{(totalAdjuntos > 0 ? $" con {totalAdjuntos} adjunto(s)" : "")}"
                });
            }
            else
            {
                return new JsonResult(new
                {
                    success = false,
                    error = "Error al enviar el correo. Verifica la configuración SMTP."
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"=== EXCEPCIÓN en OnPostEnviarCorreo POST ===");
            System.Diagnostics.Debug.WriteLine($"Mensaje: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");

            return new JsonResult(new
            {
                success = false,
                error = "Error interno del servidor: " + ex.Message
            });
        }
    }
    private async Task MarcarCorreoTipo6Enviado(string destinatarios)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(destinatarios))
                return;

            // Extraer emails de los destinatarios
            var emailsDestinatarios = destinatarios.Split(';', ',')
                .Select(e => e.Trim())
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .ToList();

            System.Diagnostics.Debug.WriteLine($"Marcando correos tipo 6 para destinatarios: {string.Join(", ", emailsDestinatarios)}");

            // Para cada email destinatario, encontrar los empleados correspondientes
            foreach (var emailDestinatario in emailsDestinatarios)
            {
                // Buscar empleados que tienen este correo personal
                var empleadosConEsteCorreo = await _db.Empleados
                    .Where(e => e.Correo == emailDestinatario)
                    .ToListAsync();

                System.Diagnostics.Debug.WriteLine($"Empleados con correo {emailDestinatario}: {empleadosConEsteCorreo.Count}");

                foreach (var empleado in empleadosConEsteCorreo)
                {
                    // Verificar si ya existe un registro para este empleado
                    var correoExistente = await _db.CorreosEnviados_POST
                        .FirstOrDefaultAsync(c => c.RUT == empleado.RUT && c.TipoCorreo == 6);

                    if (correoExistente == null)
                    {
                        // Crear nuevo registro
                        var correoEnviado = new CorreoEnviado_POST
                        {
                            RUT = empleado.RUT,
                            TipoCorreo = 6,
                            FechaEnvio = DateTime.Now,
                            EmailDestinatario = emailDestinatario,
                            Completado = true
                        };

                        _db.CorreosEnviados_POST.Add(correoEnviado);
                        System.Diagnostics.Debug.WriteLine($"Marcando correo tipo 6 como enviado para empleado: {empleado.RUT} - Email: {emailDestinatario}");
                    }
                    else
                    {
                        // Actualizar registro existente
                        correoExistente.FechaEnvio = DateTime.Now;
                        correoExistente.EmailDestinatario = emailDestinatario;
                        correoExistente.Completado = true;
                        System.Diagnostics.Debug.WriteLine($"Actualizando correo tipo 6 existente para empleado: {empleado.RUT} - Email: {emailDestinatario}");
                    }
                }
            }

            await _db.SaveChangesAsync();
            System.Diagnostics.Debug.WriteLine("Correos tipo 6 marcados como enviados en BD");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al marcar correos tipo 6: {ex.Message}");
        }
    }

    

    private async Task MarcarCorreoTipo3Enviado(string destinatarios)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(destinatarios))
                return;

            // Extraer emails de gerencia de los destinatarios
            var emailsGerencia = destinatarios.Split(';', ',')
                .Select(e => e.Trim())
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .ToList();

            System.Diagnostics.Debug.WriteLine($"Marcando correos tipo 3 para gerencias: {string.Join(", ", emailsGerencia)}");

            // Para cada email de gerencia, encontrar los empleados correspondientes
            foreach (var emailGerencia in emailsGerencia)
            {
                // Buscar empleados que tienen este correo de gerencia
                var empleadosDeEstaGerencia = await _db.Empleados
                    .Where(e => e.Gerencia_mail == emailGerencia)
                    .ToListAsync();

                System.Diagnostics.Debug.WriteLine($"Empleados con gerencia {emailGerencia}: {empleadosDeEstaGerencia.Count}");

                foreach (var empleado in empleadosDeEstaGerencia)
                {
                    // Verificar si ya existe un registro para este empleado
                    var correoExistente = await _db.CorreosEnviados_POST
                        .FirstOrDefaultAsync(c => c.RUT == empleado.RUT && c.TipoCorreo == 3);

                    if (correoExistente == null)
                    {
                        // Crear nuevo registro
                        var correoEnviado = new CorreoEnviado_POST
                        {
                            RUT = empleado.RUT,
                            TipoCorreo = 3,
                            FechaEnvio = DateTime.Now,
                            EmailDestinatario = emailGerencia,
                            Completado = true
                        };

                        _db.CorreosEnviados_POST.Add(correoEnviado);
                        System.Diagnostics.Debug.WriteLine($"Marcando correo tipo 3 como enviado para empleado: {empleado.RUT} - Gerencia: {emailGerencia}");
                    }
                    else
                    {
                        // Actualizar registro existente
                        correoExistente.FechaEnvio = DateTime.Now;
                        correoExistente.EmailDestinatario = emailGerencia;
                        correoExistente.Completado = true;
                        System.Diagnostics.Debug.WriteLine($"Actualizando correo tipo 3 existente para empleado: {empleado.RUT} - Gerencia: {emailGerencia}");
                    }
                }
            }

            await _db.SaveChangesAsync();
            System.Diagnostics.Debug.WriteLine("Correos tipo 3 marcados como enviados en BD");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al marcar correos tipo 3: {ex.Message}");
        }
    }

    // Método para registrar el envío en la base de datos
    private async Task RegistrarEnvioCorreo(string asunto, string destinatarios, string cc, string cco, string usuario)
    {
        try
        {
            // Log simple en consola/debug
            System.Diagnostics.Debug.WriteLine($"Correo POST enviado - Usuario: {usuario}, Asunto: {asunto}, Destinatarios: {destinatarios}");

            await Task.CompletedTask; // Por ahora solo es un placeholder
        }
        catch (Exception ex)
        {
            // Log del error pero no fallar el envío principal
            System.Diagnostics.Debug.WriteLine($"Error al registrar historial POST: {ex.Message}");
        }
    }


    // MÉTODO PARA DESENCRIPTAR DATOS DE UN EMPLEADO
    private void DesencriptarDatosEmpleado(Empleado empleado)
    {
        try
        {
            // Desencriptar Correo
            if (!string.IsNullOrEmpty(empleado.Correo))
            {
                empleado.Correo = _encryptionService.Decrypt(empleado.Correo);
            }

            // Desencriptar Teléfono
            if (!string.IsNullOrEmpty(empleado.Telefono))
            {
                empleado.Telefono = _encryptionService.Decrypt(empleado.Telefono);
            }

            // Desencriptar Gerencia_mail
            if (!string.IsNullOrEmpty(empleado.Gerencia_mail))
            {
                empleado.Gerencia_mail = _encryptionService.Decrypt(empleado.Gerencia_mail);
            }

            // Desencriptar Ticket
            if (!string.IsNullOrEmpty(empleado.Ticket))
            {
                empleado.Ticket = _encryptionService.Decrypt(empleado.Ticket);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al desencriptar datos del empleado {empleado.RUT}: {ex.Message}");
            // En caso de error, mantener los datos originales (encriptados)
        }
    }
    
}
