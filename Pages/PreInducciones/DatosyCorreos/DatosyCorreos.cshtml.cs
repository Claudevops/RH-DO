using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
//DESENCRIPTAR DATOS
public class DatosyCorreosModel : PageModel
{
    private readonly EmailService _emailService;
    private readonly EncryptionService _encryptionService;
    public DatosyCorreosModel(AppDbContext db, EmailService emailService, EncryptionService encryptionService)
    {
        _db = db;
        _emailService = emailService;
        _encryptionService = encryptionService;
    }


    public List<Analista> Analistas { get; set; } = new();
    public List<Sociedad> Sociedades { get; set; } = new();
    public List<Gerencia> Gerencias { get; set; } = new();
    public List<Ubicaciones> Ubicaciones { get; set; } = new();

    public bool EsAdmin { get; set; }


    private readonly AppDbContext _db;
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

            var empleados = query.ToList();

            // DESENCRIPTAR DATOS PARA LA BÚSQUEDA
            foreach (var emp in empleados)
            {
                DesencriptarDatosEmpleado(emp);
            }

            // También actualizar la sección de búsqueda:
            Empleados = empleados.Where(e =>
                (!string.IsNullOrEmpty(e.RUT) && new string(e.RUT.Where(char.IsLetterOrDigit).ToArray()).ToLower().Contains(filtro)) ||
                (((e.Nombre ?? "") + " " + (e.Apellido_Paterno ?? "") + " " + (e.Apellido_Materno ?? "")).ToLower().Contains(filtro)) ||
                (!string.IsNullOrEmpty(e.Ticket) && e.Ticket.ToLower().Contains(filtro))
            )
            .OrderByDescending(e => e.OrdenImportacion) // Mantener orden de importación en búsquedas
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToList();

            TotalPages = (int)Math.Ceiling(empleados.Count / (double)PageSize);
            return;
        }

        int totalItems = query.Count();
        TotalPages = (int)Math.Ceiling(totalItems / (double)PageSize);

        Empleados = query
            .OrderByDescending(e => e.OrdenImportacion) // Ordenar por orden de importación ascendente
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        // DESENCRIPTAR DATOS DE LOS EMPLEADOS MOSTRADOS
        foreach (var empleado in Empleados)
        {
            DesencriptarDatosEmpleado(empleado);
        }


        if (EsAdmin)
        {
            SolicitudesPendientes = _db.SolicitudesEdicion.Count(s => !s.Procesada);
        }
    }


    public JsonResult OnGetCorreoPreview(int? tipo, string? fecha = null, string? rut = null)
    {
        // Validar tipo de correo
        if (tipo == null || tipo < 1 || tipo > 6)
            return new JsonResult(new { Error = "Tipo de correo inválido", Asunto = "", Destinatarios = "", CC = "", CCO = "", Contenido = "", Adjuntos = new List<string>() });

        var plantilla = _db.PlantillasCorreos.FirstOrDefault(p => p.Tipo_PC == tipo);
        var destinatarios = _db.DestinatariosCorreo_PRE.FirstOrDefault(d => d.Tipo_DCP == tipo);
        string destinatarioFinal = "";

        // 1. Obtener empleados seleccionados
        List<Empleado> empleados = new();
        if (!string.IsNullOrEmpty(rut))
        {
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

        switch (tipo)
        {
            case 1:
                // Correo tipo 1: Usar destinatarios fijos de la BD
                destinatarioFinal = destinatarios?.DestinatariosFijos ?? "";
                break;

            case 2:
                // Correo tipo 2: Los destinatarios son los correos de los empleados filtrados
                if (empleados.Count > 0)
                {
                    destinatarioFinal = string.Join(";", empleados
                        .Where(e => !string.IsNullOrWhiteSpace(e.Correo))
                        .Select(e => e.Correo.Trim()));
                }
                else
                {
                    destinatarioFinal = destinatarios?.DestinatariosFijos ?? "";
                }
                break;

            case 3:
                // Correo tipo 3: Los destinatarios son los "Gerencia_mail" de los empleados
                if (empleados.Count > 0)
                {
                    var gerenciasMail = empleados
                        .Where(e => !string.IsNullOrWhiteSpace(e.Gerencia_mail))
                        .Select(e => e.Gerencia_mail.Trim())
                        .Distinct() // Evitar duplicados si hay múltiples empleados con la misma gerencia
                        .ToList();

                    destinatarioFinal = string.Join(";", gerenciasMail);
                }
                else
                {
                    destinatarioFinal = destinatarios?.DestinatariosFijos ?? "";
                }
                break;

            case 4:
                // Correo tipo 4: Los destinatarios son los correos de los empleados seleccionados (mismo que tipo 2)
                if (empleados.Count > 0)
                {
                    destinatarioFinal = string.Join(";", empleados
                        .Where(e => !string.IsNullOrWhiteSpace(e.Correo))
                        .Select(e => e.Correo.Trim()));
                }
                else
                {
                    destinatarioFinal = destinatarios?.DestinatariosFijos ?? "";
                }
                break;

            case 5:
                // Correo tipo 5: Usar solo destinatarios fijos de la BD (no agregar empleados)
                destinatarioFinal = destinatarios?.DestinatariosFijos ?? "";
                break;

            case 6:
                // Correo tipo 6: Los destinatarios son los correos de los empleados seleccionados (igual que tipo 2 y 4)
                if (empleados.Count > 0)
                {
                    destinatarioFinal = string.Join(";", empleados
                        .Where(e => !string.IsNullOrWhiteSpace(e.Correo))
                        .Select(e => e.Correo.Trim()));
                }
                else
                {
                    destinatarioFinal = destinatarios?.DestinatariosFijos ?? "";
                }
                break;

            default:
                // Para cualquier otro tipo, usar destinatarios fijos
                destinatarioFinal = destinatarios?.DestinatariosFijos ?? "";
                break;
        }

        // 2. Construir tabla HTML de empleados CON ESTILOS INLINE COMPLETOS
        string tablaEmpleados = "";
        if (empleados.Count > 0)
        {
            // ===== TABLA ESPECÍFICA PARA TIPO 6 =====
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
                // ===== TABLA ESTÁNDAR PARA OTROS TIPOS (1, 2, 3, 4, 5) =====
                tablaEmpleados = @"
<table style='border-collapse: collapse; margin: 10px 0; font-family: ""Trebuchet MS"", Arial, sans-serif; font-size: 10px; border: 1px solid #000000; width: 100%;'>
    <thead>
        <tr style='border: 1px solid #000000;'>
            <th style='background-color: #4472C4; color: white; padding: 8px; border: 1px solid #000000; text-align: left; font-weight: bold; font-size: 10px; white-space: nowrap; font-family: ""Trebuchet MS"", Arial, sans-serif;'>Fecha de Ingreso</th>
            <th style='background-color: #4472C4; color: white; padding: 8px; border: 1px solid #000000; text-align: left; font-weight: bold; font-size: 10px; white-space: nowrap; font-family: ""Trebuchet MS"", Arial, sans-serif;'>Sociedad</th>
            <th style='background-color: #4472C4; color: white; padding: 8px; border: 1px solid #000000; text-align: left; font-weight: bold; font-size: 10px; white-space: nowrap; font-family: ""Trebuchet MS"", Arial, sans-serif;'>RUT</th>
            <th style='background-color: #4472C4; color: white; padding: 8px; border: 1px solid #000000; text-align: left; font-weight: bold; font-size: 10px; white-space: nowrap; font-family: ""Trebuchet MS"", Arial, sans-serif;'>Nombre</th>
            <th style='background-color: #4472C4; color: white; padding: 8px; border: 1px solid #000000; text-align: left; font-weight: bold; font-size: 10px; white-space: nowrap; font-family: ""Trebuchet MS"", Arial, sans-serif;'>Apellido Paterno</th>
            <th style='background-color: #4472C4; color: white; padding: 8px; border: 1px solid #000000; text-align: left; font-weight: bold; font-size: 10px; white-space: nowrap; font-family: ""Trebuchet MS"", Arial, sans-serif;'>Apellido Materno</th>
            <th style='background-color: #4472C4; color: white; padding: 8px; border: 1px solid #000000; text-align: left; font-weight: bold; font-size: 10px; white-space: nowrap; font-family: ""Trebuchet MS"", Arial, sans-serif;'>Correo</th>
            <th style='background-color: #4472C4; color: white; padding: 8px; border: 1px solid #000000; text-align: left; font-weight: bold; font-size: 10px; white-space: nowrap; font-family: ""Trebuchet MS"", Arial, sans-serif;'>Teléfono</th>
            <th style='background-color: #4472C4; color: white; padding: 8px; border: 1px solid #000000; text-align: left; font-weight: bold; font-size: 10px; white-space: nowrap; font-family: ""Trebuchet MS"", Arial, sans-serif;'>Cargo</th>
            <th style='background-color: #4472C4; color: white; padding: 8px; border: 1px solid #000000; text-align: left; font-weight: bold; font-size: 10px; white-space: nowrap; font-family: ""Trebuchet MS"", Arial, sans-serif;'>Gerencia</th>
            <th style='background-color: #4472C4; color: white; padding: 8px; border: 1px solid #000000; text-align: left; font-weight: bold; font-size: 10px; white-space: nowrap; font-family: ""Trebuchet MS"", Arial, sans-serif;'>Ubicación</th>
            <th style='background-color: #4472C4; color: white; padding: 8px; border: 1px solid #000000; text-align: left; font-weight: bold; font-size: 10px; white-space: nowrap; font-family: ""Trebuchet MS"", Arial, sans-serif;'>Líder</th>
            <th style='background-color: #4472C4; color: white; padding: 8px; border: 1px solid #000000; text-align: left; font-weight: bold; font-size: 10px; white-space: nowrap; font-family: ""Trebuchet MS"", Arial, sans-serif;'>N° de Ticket</th>
            <th style='background-color: #4472C4; color: white; padding: 8px; border: 1px solid #000000; text-align: left; font-weight: bold; font-size: 10px; white-space: nowrap; font-family: ""Trebuchet MS"", Arial, sans-serif;'>Tipo de Contrato</th>
        </tr>
    </thead>
    <tbody>";
                // Agregar filas con estilos alternados y mejorados
                for (int i = 0; i < empleados.Count; i++)
                {
                    var emp = empleados[i];

                    tablaEmpleados += $@"
        <tr style='border: 1px solid #000000;'>
            <td style='padding: 6px 8px; border: 1px solid #000000; font-size: 10px; font-family: ""Trebuchet MS"", Arial, sans-serif; vertical-align: top;'>{(emp.Fecha_Ingreso.HasValue ? emp.Fecha_Ingreso.Value.ToString("dd-MM-yyyy") : "")}</td>
            <td style='padding: 6px 8px; border: 1px solid #000000; font-size: 10px; font-family: ""Trebuchet MS"", Arial, sans-serif; vertical-align: top;'>{emp.Sociedad}</td>
            <td style='padding: 6px 8px; border: 1px solid #000000; font-size: 10px; font-family: ""Trebuchet MS"", Arial, sans-serif; vertical-align: top; white-space: nowrap; font-weight: bold;'>{emp.RUT}</td>
            <td style='padding: 6px 8px; border: 1px solid #000000; font-size: 10px; font-family: ""Trebuchet MS"", Arial, sans-serif; vertical-align: top;'>{emp.Nombre}</td>
            <td style='padding: 6px 8px; border: 1px solid #000000; font-size: 10px; font-family: ""Trebuchet MS"", Arial, sans-serif; vertical-align: top;'>{emp.Apellido_Paterno}</td>
            <td style='padding: 6px 8px; border: 1px solid #000000; font-size: 10px; font-family: ""Trebuchet MS"", Arial, sans-serif; vertical-align: top;'>{emp.Apellido_Materno}</td>
            <td style='padding: 6px 8px; border: 1px solid #000000; font-size: 10px; font-family: ""Trebuchet MS"", Arial, sans-serif; vertical-align: top;'>{emp.Correo}</td>
            <td style='padding: 6px 8px; border: 1px solid #000000; font-size: 10px; font-family: ""Trebuchet MS"", Arial, sans-serif; vertical-align: top;'>{emp.Telefono}</td>
            <td style='padding: 6px 8px; border: 1px solid #000000; font-size: 10px; font-family: ""Trebuchet MS"", Arial, sans-serif; vertical-align: top;'>{emp.Cargo}</td>
            <td style='padding: 6px 8px; border: 1px solid #000000; font-size: 10px; font-family: ""Trebuchet MS"", Arial, sans-serif; vertical-align: top;'>{emp.Gerencia}</td>
            <td style='padding: 6px 8px; border: 1px solid #000000; font-size: 10px; font-family: ""Trebuchet MS"", Arial, sans-serif; vertical-align: top;'>{emp.Ubicacion}</td>
            <td style='padding: 6px 8px; border: 1px solid #000000; font-size: 10px; font-family: ""Trebuchet MS"", Arial, sans-serif; vertical-align: top;'>{emp.Jefe_Directo}</td>
            <td style='padding: 6px 8px; border: 1px solid #000000; font-size: 10px; font-family: ""Trebuchet MS"", Arial, sans-serif; vertical-align: top; white-space: nowrap; font-weight: bold;'>{emp.Ticket}</td>
            <td style='padding: 6px 8px; border: 1px solid #000000; font-size: 10px; font-family: ""Trebuchet MS"", Arial, sans-serif; vertical-align: top;'>{emp.Tipo_de_Contrato}</td>
        </tr>";
                }

                tablaEmpleados += "</tbody></table>";
            }
        }
        else
        {
            tablaEmpleados = "<div style='padding: 20px; text-align: center; color: #6b7280; font-style: italic; background-color: #f9fafb; border-radius: 8px; border: 1px solid #e5e7eb;'>No hay empleados registrados.</div>";
        }

        // 3. Calcular fechas en formato "14 de Julio de 2025"
        string fechaIngresoStr = "";
        string fechaSegundoDiaStr = "";
        if (empleados.Count > 0 && empleados[0].Fecha_Ingreso.HasValue)
        {
            var fechaIngreso = empleados[0].Fecha_Ingreso ?? DateTime.Now;
            fechaIngresoStr = fechaIngreso.ToString("dd 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("es-ES"));
            fechaSegundoDiaStr = fechaIngreso.AddDays(1).ToString("dd 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("es-ES"));
        }
        else if (!string.IsNullOrEmpty(fecha) && DateTime.TryParse(fecha, out var fechaIngreso))
        {
            fechaIngresoStr = fechaIngreso.ToString("dd 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("es-ES"));
            fechaSegundoDiaStr = fechaIngreso.AddDays(1).ToString("dd 'de' MMMM 'de' yyyy", new System.Globalization.CultureInfo("es-ES"));
        }

        // 4. Reemplazar placeholders en el contenido
        string contenido = plantilla?.HtmlContenido ?? "(Sin contenido)";

        // Pre-limpiar el contenido antes de procesarlo
        contenido = System.Text.RegularExpressions.Regex.Replace(
            contenido,
            @"<figure[^>]*class=[""']?table[""']?[^>]*>(.*?)</figure>",
            "$1",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline
        );

        contenido = contenido.Replace("{{ fecha_ingreso_es }}", fechaIngresoStr)
                             .Replace("{{ fecha_segundo_dia }}", fechaSegundoDiaStr)
                             .Replace("<strong>&lt;!--EMPLEADOS_TABLA--&gt;</strong>", tablaEmpleados);
  
        // 5. Reemplazar datos individuales si es un solo empleado
        if (empleados.Count == 1)
        {
            var emp = empleados[0];
            contenido = contenido.Replace("{{ emp.Nombre }}", emp.Nombre ?? "")
                                .Replace("{{ emp.RUT }}", emp.RUT ?? "")
                                .Replace("{{ emp.Correo }}", emp.Correo ?? "")
                                .Replace("{{ emp.Cargo }}", emp.Cargo ?? "")
                                .Replace("{{ emp.Direccion }}", emp.Direccion ?? "")
                                .Replace("{{ emp.Jefe_Directo }}", emp.Jefe_Directo ?? "")
                                .Replace("{{ emp.Apellido_Paterno }}", emp.Apellido_Paterno ?? "")
                                .Replace("{{ emp.Telefono }}", emp.Telefono ?? "")
                                .Replace("{{ emp.Ticket }}", emp.Ticket ?? "");
        }
        else if (empleados.Count > 1)
        {
            // Solo reemplazar si el contenido específicamente necesita datos del primer empleado
            var primerEmpleado = empleados[0];
            
            // Reemplazar solo si es necesario para el contexto del correo
            contenido = contenido.Replace("{{ emp.Nombre }}", primerEmpleado.Nombre ?? "")
                                .Replace("{{ emp.Apellido_Paterno }}", primerEmpleado.Apellido_Paterno ?? "")
                                .Replace("{{ emp.RUT }}", primerEmpleado.RUT ?? "")
                                .Replace("{{ emp.Correo }}", primerEmpleado.Correo ?? "")
                                .Replace("{{ emp.Cargo }}", primerEmpleado.Cargo ?? "")
                                .Replace("{{ emp.Direccion }}", primerEmpleado.Direccion ?? "")
                                .Replace("{{ emp.Jefe_Directo }}", primerEmpleado.Jefe_Directo ?? "")
                                .Replace("{{ emp.Telefono }}", primerEmpleado.Telefono ?? "")
                                .Replace("{{ emp.Ticket }}", primerEmpleado.Ticket ?? "");
        }
        // 6. Mejorar las tablas existentes en el contenido (tablas de horarios)
        contenido = MejorarTablasExistentes(contenido);

        // 7. Envolver todo el contenido en un contenedor con estilos generales - SIN MARGIN TOP
        contenido = $@"
<div style='font-family: ""Trebuchet MS"", Arial, sans-serif; font-size: 10px; line-height: 1.6; color: #374151; max-width: 1200px; margin: 0 auto; padding: 0 20px 20px 20px; background-color: #ffffff;'>
    {contenido}
</div>";

        // 8. Obtener adjuntos
        var adjuntos = _db.AdjuntosCorreos
            .Where(a => a.Tipo == tipo)
            .Select(a => a.Nombre_Adjunto)
            .ToList();

        return new JsonResult(new
        {
            Asunto = plantilla?.Nombre_PC ?? "(Sin asunto)",
            Destinatarios = destinatarioFinal,
            CC = destinatarios?.CCFijos ?? "",
            CCO = destinatarios?.CCOFijos ?? "",
            Contenido = contenido,
            Adjuntos = adjuntos
        });
    }



    // Método auxiliar para mejorar las tablas existentes
    private string MejorarTablasExistentes(string contenido)
    {
        var tablaPattern = @"<table[^>]*>(.*?)</table>";
        var tablas = System.Text.RegularExpressions.Regex.Matches(contenido, tablaPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

        foreach (System.Text.RegularExpressions.Match tabla in tablas)
        {
            string tablaOriginal = tabla.Value;
            string tablaContenido = tabla.Groups[1].Value;

            // Saltar si es la tabla de empleados (contiene RUT o gradiente azul)
            if (tablaOriginal.Contains("#4472C4") || tablaOriginal.Contains("Sociedad") && tablaOriginal.Contains("Fecha de Ingreso"))
            {
                continue; // No tocar la tabla de empleados que ya tiene estilos
            }

            // Procesar TODOS los tipos de tablas posibles
            bool esTablaValida =
                tablaContenido.ToLower().Contains("actividad") ||
                tablaContenido.ToLower().Contains("horario") ||
                tablaContenido.ToLower().Contains("elemento") ||
                tablaContenido.ToLower().Contains("talla") ||
                tablaContenido.ToLower().Contains("polar") ||
                tablaContenido.ToLower().Contains("parka") ||
                tablaContenido.ToLower().Contains("camisas") ||
                tablaContenido.ToLower().Contains("datos del trabajador") ||
                tablaContenido.ToLower().Contains("nombre") ||
                tablaContenido.ToLower().Contains("cargo") ||
                tablaContenido.ToLower().Contains("correo") ||
                tablaContenido.ToLower().Contains("fono") ||
                tablaContenido.ToLower().Contains("centro de costos") ||
                tablaContenido.ToLower().Contains("jefatura") ||
                tablaContenido.ToLower().Contains("lugar de trabajo") ||
                tablaContenido.ToLower().Contains("ficha") ||
                tablaContenido.ToLower().Contains("requerimientos") ||
                tablaContenido.ToLower().Contains("trabajo") ||
                tablaContenido.ToLower().Contains("tipo de trabajador") ||
                tablaContenido.ToLower().Contains("jefatura directa") ||
                tablaContenido.ToLower().Contains("fono contacto") ||
                tablaContenido.ToLower().Contains("ropa corporativa") ||
                tablaContenido.ToLower().Contains("requerida") ||
                tablaContenido.ToLower().Contains("rut") ||
                tablaContenido.ToLower().Contains("contacto") ||
                tablaContenido.ToLower().Contains("telefono") ||
                tablaContenido.ToLower().Contains("directo") ||
                tablaContenido.Contains("<th") ||
                tablaContenido.Contains("<td");

            if (!esTablaValida)
            {
                continue;
            }

            // Limpiar elementos problemáticos ANTES de procesar
            tablaContenido = LimpiarTablaOriginal(tablaContenido);

            // Detectar y manejar headers combinados
            tablaContenido = ProcesarHeadersCombinados(tablaContenido);

            // Crear tabla SIMPLE Y SOBRIA
            string tablaMejorada = @"
<table style='border-collapse: collapse; margin: 10px 0; font-family: Arial, sans-serif; font-size: 12px; border: 1px solid #000000;'>
" + tablaContenido + "</table>";

            // SIMPLE: Headers azules básicos con bordes negros
            tablaMejorada = System.Text.RegularExpressions.Regex.Replace(
                tablaMejorada,
                @"<th([^>]*)>([^<\s][^<]*)</th>",
                match =>
                {
                    string attributes = match.Groups[1].Value;
                    string content = match.Groups[2].Value;

                    // Preservar colspan si existe
                    string colspanAttr = "";
                    var colspanMatch = System.Text.RegularExpressions.Regex.Match(attributes, @"colspan\s*=\s*[""']?(\d+)[""']?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (colspanMatch.Success)
                    {
                        colspanAttr = $" colspan='{colspanMatch.Groups[1].Value}'";
                    }

                    return $"<th{colspanAttr} style='background-color: #4472C4; color: white; padding: 8px; border: 1px solid #000000; text-align: left; font-weight: bold; font-size: 12px;'>{content}</th>";
                },
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            // Celdas básicas con bordes negros
            tablaMejorada = System.Text.RegularExpressions.Regex.Replace(
                tablaMejorada,
                @"<td[^>]*>([^<]*)</td>",
                "<td style='padding: 6px 8px; border: 1px solid #000000; font-size: 12px; vertical-align: top;'>$1</td>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            // Aplicar estilos a las filas - SIN colores alternados, solo bordes
            var filasPattern = @"<tr[^>]*>(.*?)</tr>";
            var filas = System.Text.RegularExpressions.Regex.Matches(tablaMejorada, filasPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

            string tablaConFilasEstilizadas = tablaMejorada;

            foreach (System.Text.RegularExpressions.Match fila in filas)
            {
                // Saltar header
                if (fila.Value.Contains("<th"))
                    continue;

                string filaOriginal = fila.Value;

                // SIMPLE: Solo agregar borde a las filas
                string filaMejorada = System.Text.RegularExpressions.Regex.Replace(
                    filaOriginal,
                    @"<tr[^>]*>",
                    "<tr style='border: 1px solid #000000;'>",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );

                tablaConFilasEstilizadas = tablaConFilasEstilizadas.Replace(filaOriginal, filaMejorada);
            }

            // Limpiar elementos que puedan estar causando desbordamiento
            tablaConFilasEstilizadas = LimpiarElementosDesbordantes(tablaConFilasEstilizadas);

            // Reemplazar la tabla original con la mejorada
            contenido = contenido.Replace(tablaOriginal, tablaConFilasEstilizadas);
        }

        return contenido;
    }

    // Procesar headers combinados como "DATOS DEL TRABAJADOR"
    private string ProcesarHeadersCombinados(string tablaContenido)
    {
        // Patrón 1: "DATOS DEL TRABAJADOR" seguido de filas con datos
        if (tablaContenido.ToLower().Contains("datos del trabajador"))
        {
            // Verificar si ya tiene estructura de tabla correcta
            if (!tablaContenido.Contains("colspan"))
            {
                // Reestructurar la tabla para que "DATOS DEL TRABAJADOR" sea un header que abarque 2 columnas

                // Buscar el patrón de la tabla actual
                var datosPattern = @"<tr[^>]*>\s*<th[^>]*>DATOS DEL TRABAJADOR</th>\s*</tr>";
                var match = System.Text.RegularExpressions.Regex.Match(tablaContenido, datosPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

                if (match.Success)
                {
                    // Reemplazar con header que abarca 2 columnas
                    string headerCombinado = @"<tr><th colspan='2'>DATOS DEL TRABAJADOR</th></tr>";
                    tablaContenido = tablaContenido.Replace(match.Value, headerCombinado);
                }
                else
                {
                    // Si no está en una fila separada, buscar otra estructura
                    // Intentar detectar si está al inicio de la tabla
                    if (tablaContenido.ToLower().IndexOf("datos del trabajador") < 100)
                    {
                        // Agregar header combinado al inicio
                        var primeraFilaPattern = @"(<tbody>|<tr[^>]*>)";
                        var primeraFilaMatch = System.Text.RegularExpressions.Regex.Match(tablaContenido, primeraFilaPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                        if (primeraFilaMatch.Success)
                        {
                            string headerCombinado = @"<tr><th colspan='2'>DATOS DEL TRABAJADOR</th></tr>";
                            tablaContenido = tablaContenido.Insert(primeraFilaMatch.Index + primeraFilaMatch.Length, headerCombinado);
                        }
                    }
                }
            }
        }

        // Patrón 2: Tablas de ropa corporativa con headers combinados
        if (tablaContenido.ToLower().Contains("ropa corporativa") && tablaContenido.ToLower().Contains("requerida"))
        {
            var ropaPattern = @"<tr[^>]*>\s*<th[^>]*>ROPA CORPORATIVA REQUERIDA</th>\s*</tr>";
            var match = System.Text.RegularExpressions.Regex.Match(tablaContenido, ropaPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);

            if (match.Success)
            {
                // Reemplazar con header que abarca las columnas necesarias
                string headerCombinado = @"<tr><th colspan='2'>ROPA CORPORATIVA REQUERIDA</th></tr>";
                tablaContenido = tablaContenido.Replace(match.Value, headerCombinado);
            }
        }

        return tablaContenido;
    }


    private string LimpiarTablaOriginal(string tablaContenido)
    {
        // Eliminar elementos <th> vacíos o invisibles
        tablaContenido = System.Text.RegularExpressions.Regex.Replace(
            tablaContenido,
            @"<th[^>]*>\s*</th>",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        // Eliminar elementos <th> que contienen solo espacios en blanco o &nbsp;
        tablaContenido = System.Text.RegularExpressions.Regex.Replace(
            tablaContenido,
            @"<th[^>]*>(\s|&nbsp;|&#160;)*</th>",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        // Eliminar <th> con contenido invisible (solo CSS que oculta)
        tablaContenido = System.Text.RegularExpressions.Regex.Replace(
            tablaContenido,
            @"<th[^>]*style=""[^""]*display\s*:\s*none[^""]*""[^>]*>.*?</th>",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline
        );

        // Eliminar <th> con visibility: hidden
        tablaContenido = System.Text.RegularExpressions.Regex.Replace(
            tablaContenido,
            @"<th[^>]*style=""[^""]*visibility\s*:\s*hidden[^""]*""[^>]*>.*?</th>",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline
        );

        // Eliminar filas de header vacías o problemáticas
        tablaContenido = System.Text.RegularExpressions.Regex.Replace(
            tablaContenido,
            @"<tr[^>]*>\s*(<th[^>]*>\s*</th>\s*)*</tr>",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        // Eliminar elementos <figure> que puedan estar envolviendo contenido
        tablaContenido = System.Text.RegularExpressions.Regex.Replace(
            tablaContenido,
            @"<figure[^>]*>",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        tablaContenido = System.Text.RegularExpressions.Regex.Replace(
            tablaContenido,
            @"</figure>",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        // Limpiar elementos con estilos problemáticos
        tablaContenido = System.Text.RegularExpressions.Regex.Replace(
            tablaContenido,
            @"position\s*:\s*(absolute|fixed|relative)",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        // Eliminar clases que puedan estar causando problemas
        tablaContenido = System.Text.RegularExpressions.Regex.Replace(
            tablaContenido,
            @"class=""[^""]*""",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        return tablaContenido;
    }

    // Método auxiliar para limpiar elementos que causan desbordamiento
    private string LimpiarElementosDesbordantes(string tablaHtml)
    {
        // Limpiar elementos <th> problemáticos que puedan haber quedado
        tablaHtml = System.Text.RegularExpressions.Regex.Replace(
            tablaHtml,
            @"<th[^>]*>\s*</th>",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        // Limpiar elementos <figure> que envuelven las tablas
        tablaHtml = System.Text.RegularExpressions.Regex.Replace(
            tablaHtml,
            @"<figure[^>]*class=""table""[^>]*>",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        tablaHtml = System.Text.RegularExpressions.Regex.Replace(
            tablaHtml,
            @"</figure>",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        // Limpiar cualquier <figure> que pueda estar envolviendo contenido
        tablaHtml = System.Text.RegularExpressions.Regex.Replace(
            tablaHtml,
            @"<figure[^>]*>",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        // Limpiar spans o divs con estilos problemáticos
        tablaHtml = System.Text.RegularExpressions.Regex.Replace(
            tablaHtml,
            @"<span[^>]*style=""[^""]*position[^""]*""[^>]*>([^<]*)</span>",
            "$1",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        // Limpiar elementos con position absolute o fixed
        tablaHtml = System.Text.RegularExpressions.Regex.Replace(
            tablaHtml,
            @"position\s*:\s*(absolute|fixed)",
            "position: static",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        // Limpiar z-index altos
        tablaHtml = System.Text.RegularExpressions.Regex.Replace(
            tablaHtml,
            @"z-index\s*:\s*\d+",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        // Limpiar elementos que se extienden fuera del contenedor
        tablaHtml = System.Text.RegularExpressions.Regex.Replace(
            tablaHtml,
            @"margin-left\s*:\s*-\d+",
            "margin-left: 0",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        tablaHtml = System.Text.RegularExpressions.Regex.Replace(
            tablaHtml,
            @"margin-right\s*:\s*-\d+",
            "margin-right: 0",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        return tablaHtml;
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
            empleado.Analista = form["Analista"];

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

        // CREAR OBJETO CON FECHAS EN FORMATO EXACTO
        var empleadoData = new
        {
            id = empleado.Id,
            analista = empleado.Analista,
            // FORMATO: yyyy-MM-dd
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
            quienrepone = empleado.Quienrepone, // AGREGAR ESTE CAMPO
            contrato = empleado.Contrato,
            ticket = empleado.Ticket,
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
                UsuarioSolicitante = System.Web.HttpUtility.HtmlEncode(User.Identity?.Name) ?? "Desconocido",
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


    private string CorregirFechasEnJson(string jsonString)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"=== PROCESANDO FECHAS EN JSON ===");
            System.Diagnostics.Debug.WriteLine($"JSON Original: {jsonString}");

            using var doc = System.Text.Json.JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            var jsonObject = new Dictionary<string, object?>();

            foreach (var property in root.EnumerateObject())
            {
                var propertyName = property.Name;
                var value = property.Value;

                // DETECTAR CAMPOS DE FECHA Y PROCESARLOS ESPECIALMENTE
                if (EsCampoFecha(propertyName))
                {
                    if (value.ValueKind == JsonValueKind.String)
                    {
                        var fechaString = value.GetString();
                        System.Diagnostics.Debug.WriteLine($"Procesando campo fecha '{propertyName}': '{fechaString}'");

                        if (!string.IsNullOrEmpty(fechaString))
                        {
                            // MÉTODO 1: Si ya está en formato yyyy-MM-dd
                            if (System.Text.RegularExpressions.Regex.IsMatch(fechaString, @"^\d{4}-\d{2}-\d{2}$"))
                            {
                                jsonObject[propertyName] = fechaString;
                                System.Diagnostics.Debug.WriteLine($"  -> MANTENIDO EXACTO: '{fechaString}'");
                            }
                            // MÉTODO 2: Parsear manualmente para evitar conversiones automáticas
                            else
                            {
                                var fechaCorregida = ParsearFechaSinConversion(fechaString);
                                jsonObject[propertyName] = fechaCorregida;
                                System.Diagnostics.Debug.WriteLine($"  -> PARSEADO MANUAL: '{fechaString}' -> '{fechaCorregida}'");
                            }
                        }
                        else
                        {
                            jsonObject[propertyName] = null;
                            System.Diagnostics.Debug.WriteLine($"  -> FECHA VACÍA");
                        }
                    }
                    else if (value.ValueKind == JsonValueKind.Null)
                    {
                        jsonObject[propertyName] = null;
                        System.Diagnostics.Debug.WriteLine($"  -> FECHA NULL");
                    }
                    else
                    {
                        // Mantener valor original si no es string ni null
                        jsonObject[propertyName] = ExtraerValorJson(value);
                        System.Diagnostics.Debug.WriteLine($"  -> VALOR NO STRING: {value.ValueKind}");
                    }
                }
                else
                {
                    // Para campos que NO son fechas, mantener el valor original
                    jsonObject[propertyName] = ExtraerValorJson(value);
                }
            }

            var resultado = System.Text.Json.JsonSerializer.Serialize(jsonObject, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            System.Diagnostics.Debug.WriteLine($"JSON Resultado: {resultado}");
            System.Diagnostics.Debug.WriteLine($"=== FIN PROCESAMIENTO ===");

            return resultado;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR en CorregirFechasEnJson: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
            return jsonString; // Retornar original si hay error
        }
    }

    //  Parsear fecha sin conversiones automáticas
    private string ParsearFechaSinConversion(string fechaInput)
    {
        if (string.IsNullOrWhiteSpace(fechaInput))
            return "";

        try
        {

            // Formato 1: dd/MM/yyyy
            if (System.Text.RegularExpressions.Regex.IsMatch(fechaInput, @"^\d{1,2}/\d{1,2}/\d{4}$"))
            {
                var parts = fechaInput.Split('/');
                if (parts.Length == 3)
                {
                    var dia = int.Parse(parts[0]).ToString("00");
                    var mes = int.Parse(parts[1]).ToString("00");
                    var año = parts[2];
                    return $"{año}-{mes}-{dia}";
                }
            }

            // Formato 2: dd-MM-yyyy
            if (System.Text.RegularExpressions.Regex.IsMatch(fechaInput, @"^\d{1,2}-\d{1,2}-\d{4}$"))
            {
                var parts = fechaInput.Split('-');
                if (parts.Length == 3)
                {
                    var dia = int.Parse(parts[0]).ToString("00");
                    var mes = int.Parse(parts[1]).ToString("00");
                    var año = parts[2];
                    return $"{año}-{mes}-{dia}";
                }
            }

            // Formato 3: yyyy-MM-dd
            if (System.Text.RegularExpressions.Regex.IsMatch(fechaInput, @"^\d{4}-\d{2}-\d{2}$"))
            {
                return fechaInput;
            }

            // Formato 4: yyyy/MM/dd
            if (fechaInput.Contains("T"))
            {
                var fechaParte = fechaInput.Split('T')[0];
                if (System.Text.RegularExpressions.Regex.IsMatch(fechaParte, @"^\d{4}-\d{2}-\d{2}$"))
                {
                    return fechaParte;
                }
            }
            if (DateTime.TryParse(fechaInput, out var fecha))
            {
                var fechaSegura = new DateTime(fecha.Year, fecha.Month, fecha.Day, 12, 0, 0, DateTimeKind.Local);
                return fechaSegura.ToString("yyyy-MM-dd");
            }

            System.Diagnostics.Debug.WriteLine($"⚠️ No se pudo parsear la fecha: '{fechaInput}'");
            return fechaInput;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Error parseando fecha '{fechaInput}': {ex.Message}");
            return fechaInput;
        }
    }


    private bool EsCampoFecha(string nombreCampo)
    {
        if (string.IsNullOrEmpty(nombreCampo))
            return false;

        var camposFecha = new[]
        {
        "fecha_Induccion", "fechaInduccion", "Fecha_Induccion",
        "fecha_Ingreso", "fechaIngreso", "Fecha_Ingreso",
        "fecha_Nacimiento", "fechaNacimiento", "Fecha_Nacimiento",
        "fecha_Induccion_Cultural", "fechaInduccionCultural", "Fecha_Induccion_Cultural"
    };

        var esCampoFecha = camposFecha.Any(cf => string.Equals(cf, nombreCampo, StringComparison.OrdinalIgnoreCase));

        if (esCampoFecha)
        {
            System.Diagnostics.Debug.WriteLine($"✅ Campo '{nombreCampo}' identificado como FECHA");
        }

        return esCampoFecha;
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
                DatosSolicitados = body, // Contiene la solicitud completa
                DatosAnteriores = datosEmpleado, // Datos actuales del empleado por si se necesita restaurar
                UsuarioSolicitante = System.Web.HttpUtility.HtmlEncode(User.Identity?.Name) ?? "Desconocido",
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

    // Método para Enviar los correos

    public async Task<IActionResult> OnPostEnviarCorreo()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== INICIO OnPostEnviarCorreo ===");

            var form = Request.Form;

            string asunto = form["asunto"];
            string destinatarios = form["destinatarios"];
            string cc = form["cc"];
            string cco = form["cco"];
            string contenido = form["contenido"];
            string adjuntosBDJson = form["adjuntosBD"];

            System.Diagnostics.Debug.WriteLine($"Asunto: {asunto}");
            System.Diagnostics.Debug.WriteLine($"Destinatarios: {destinatarios}");
            System.Diagnostics.Debug.WriteLine($"CC: {cc}");
            System.Diagnostics.Debug.WriteLine($"CCO: {cco}");
            System.Diagnostics.Debug.WriteLine($"Contenido length: {contenido?.Length ?? 0}");
            System.Diagnostics.Debug.WriteLine($"Adjuntos BD JSON: {adjuntosBDJson}");

            // Obtener archivos adjuntos MANUALES
            var adjuntosManuales = new List<IFormFile>();

            System.Diagnostics.Debug.WriteLine($"Total de archivos en Request.Form.Files: {Request.Form.Files.Count}");

            foreach (var file in Request.Form.Files)
            {
                System.Diagnostics.Debug.WriteLine($"Archivo encontrado - Name: {file.Name}, FileName: {file.FileName}, Size: {file.Length}");
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
                    foreach (var adj in adjuntosBD)
                    {
                        System.Diagnostics.Debug.WriteLine($"  - {adj}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error al deserializar adjuntos BD: {ex.Message}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"Total adjuntos manuales: {adjuntosManuales.Count}");
            System.Diagnostics.Debug.WriteLine($"Total adjuntos BD: {adjuntosBD.Count}");

            // Validaciones básicas
            if (string.IsNullOrWhiteSpace(asunto))
            {
                System.Diagnostics.Debug.WriteLine("Error: Asunto vacío");
                return new JsonResult(new { success = false, error = "El asunto es requerido" });
            }

            if (string.IsNullOrWhiteSpace(destinatarios))
            {
                System.Diagnostics.Debug.WriteLine("Error: Destinatarios vacío");
                return new JsonResult(new { success = false, error = "Los destinatarios son requeridos" });
            }

            if (string.IsNullOrWhiteSpace(contenido))
            {
                System.Diagnostics.Debug.WriteLine("Error: Contenido vacío");
                return new JsonResult(new { success = false, error = "El contenido es requerido" });
            }

            // Retornar estado de "enviando" primero
            System.Diagnostics.Debug.WriteLine("Iniciando proceso de envío de correo...");

            // Enviar respuesta de progreso inmediata
            Response.Headers.Add("X-Sending-Status", "sending");

            System.Diagnostics.Debug.WriteLine("Iniciando envío de correo...");

            // Enviar el correo CON AMBOS TIPOS DE ADJUNTOS
            bool enviado = await _emailService.EnviarCorreoAsync(
                asunto,
                destinatarios,
                cc,
                cco,
                contenido,
                adjuntosManuales,
                adjuntosBD
            );

            System.Diagnostics.Debug.WriteLine($"Resultado del envío: {enviado}");

            if (enviado)
            {
                await RegistrarEnvioCorreo(asunto, destinatarios, cc, cco, System.Web.HttpUtility.HtmlEncode(User.Identity?.Name) ?? "Desconocido");

                int totalAdjuntos = adjuntosManuales.Count + adjuntosBD.Count;
                System.Diagnostics.Debug.WriteLine("=== FIN OnPostEnviarCorreo - ÉXITO ===");
                return new JsonResult(new
                {
                    success = true,
                    message = $"Correo enviado exitosamente{(totalAdjuntos > 0 ? $" con {totalAdjuntos} adjunto(s)" : "")}"
                });
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("=== FIN OnPostEnviarCorreo - ERROR EN ENVÍO ===");
                return new JsonResult(new
                {
                    success = false,
                    error = "Error al enviar el correo. Verifica la configuración SMTP."
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"=== EXCEPCIÓN en OnPostEnviarCorreo ===");
            System.Diagnostics.Debug.WriteLine($"Mensaje: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
            System.Diagnostics.Debug.WriteLine($"InnerException: {ex.InnerException?.Message}");

            return new JsonResult(new
            {
                success = false,
                error = "Error interno del servidor: " + ex.Message
            });
        }
    }

    // Método opcional para registrar el envío en la base de datos
    private async Task RegistrarEnvioCorreo(string asunto, string destinatarios, string cc, string cco, string usuario)
    {
        try
        {
            // Log simple en consola/debug
            System.Diagnostics.Debug.WriteLine($"Correo enviado - Usuario: {usuario}, Asunto: {asunto}, Destinatarios: {destinatarios}");

            await Task.CompletedTask; // Por ahora solo es un placeholder
        }
        catch (Exception ex)
        {
            // Log del error pero no fallar el envío principal
            System.Diagnostics.Debug.WriteLine($"Error al registrar historial: {ex.Message}");
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
