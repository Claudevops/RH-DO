using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;

public class index_pre : PageModel
{
    private readonly AppDbContext _db;

    public bool EsAdmin { get; set; }
    public int SolicitudesPendientes { get; set; } = 0;

    private readonly EncryptionService _encryptionService;


    // Agrega el constructor para inyectar el contexto
    public index_pre(AppDbContext db, EncryptionService encryptionService)
    {
        _db = db;
        _encryptionService = encryptionService;
    }

    public void OnGet()
    {
        var usuario = _db.Usuarios.FirstOrDefault(u => u.Correo == User.Identity.Name);
        EsAdmin = usuario?.EsAdmin ?? false;

        if (EsAdmin)
        {
            SolicitudesPendientes = _db.SolicitudesEdicion.Count(s => !s.Procesada);
        }
    }

    // Handler para subir archivos Excel
    public async Task<IActionResult> OnPostUploadExcelAsync()
    {
        try
        {
            var file = Request.Form.Files.FirstOrDefault();

            if (file == null || file.Length == 0)
            {
                return new JsonResult(new { success = false, message = "No se seleccionó ningún archivo" });
            }

            // Verificar que sea un archivo Excel
            var extension = Path.GetExtension(file.FileName).ToLower();
            if (extension != ".xlsx" && extension != ".xlsm")
            {
                return new JsonResult(new { success = false, message = "El archivo debe ser un Excel (.xlsx o .xlsm)" });
            }

            // Verificar permisos de administrador
            var usuario = _db.Usuarios.FirstOrDefault(u => u.Correo == User.Identity.Name);
            if (usuario?.EsAdmin != true)
            {
                return new JsonResult(new { success = false, message = "No tienes permisos para realizar esta operación" });
            }

            Console.WriteLine($"📁 Procesando archivo: {file.FileName} ({file.Length} bytes)");

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);

            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.First();

            // Validar que hay datos
            var lastRow = worksheet.LastRowUsed();
            var lastCol = worksheet.LastColumnUsed();
            if (lastRow == null || lastCol == null)
            {
                return new JsonResult(new { success = false, message = "El archivo Excel está vacío o no tiene datos." });
            }

            Console.WriteLine($"📊 Datos encontrados: {lastRow.RowNumber()} filas, {lastCol.ColumnNumber()} columnas");

            // LEER ENCABEZADOS CON LOGGING
            var headers = new Dictionary<string, int>();
            var headerRow = worksheet.Row(1);
            for (int col = 1; col <= lastCol.ColumnNumber(); col++)
            {
                var header = headerRow.Cell(col).GetString().Trim();
                if (!string.IsNullOrEmpty(header))
                {
                    headers[header] = col;
                    Console.WriteLine($"Columna {col}: '{header}'");
                }
            }

            // VALIDAR ENCABEZADOS ESENCIALES 
            string[] requeridosEsenciales = {
                "Analista","Fecha Ingreso","RUT","Nombre","Apellido Paterno","Apellido Materno",
                "Fecha de Nacimiento","Correo","Teléfono","Dirección","Sociedad","Cargo","Gerencia","Ubicación",
                "Jefe Directo","Tipo de Contrato (Reposición, Expansión)","Ticket","Fecha Inducción Cultura",
                "Estado Inducción Cultura","Contrato","Carrera","Universidad",
            };

            var columnasNoEncontradas = requeridosEsenciales.Where(req => !headers.ContainsKey(req)).ToList();

            // VERIFICAR SI EXISTEN LAS NUEVAS COLUMNAS
            bool tieneEntrevistaExperiencia = headers.Keys.Any(k =>
                k.Contains("Entrevista") && k.Contains("Experiencia"));

            bool tieneEncuestaEficacia = headers.Keys.Any(k =>
                k.Contains("Encuesta") && k.Contains("Eficac"));

            Console.WriteLine($"📋 Columnas nuevas encontradas: Entrevista Experiencia={tieneEntrevistaExperiencia}, Encuesta Eficacia={tieneEncuestaEficacia}");

            // Solo dar error si faltan las columnas esenciales
            if (columnasNoEncontradas.Any())
            {
                return new JsonResult(new
                {
                    success = false,
                    message = $"Faltan las siguientes columnas esenciales: {string.Join(", ", columnasNoEncontradas)}"
                });
            }

            // VARIABLES DE CONTROL CON INICIALIZACIÓN CORRECTA
            int empleadosNuevos = 0;
            int empleadosActualizados = 0;
            var errores = new List<string>();
            var nuevosEmpleados = new List<Empleado>();

            // OBTENER EL SIGUIENTE NÚMERO DE ORDEN
            int siguienteOrden = _db.Empleados.Any() ? _db.Empleados.Max(e => e.OrdenImportacion) + 1 : 1;
            Console.WriteLine($"🔢 Siguiente orden de importación: {siguienteOrden}");

            // PROCESAR FILAS CON MEJOR MANEJO DE ERRORES
            for (int row = 2; row <= lastRow.RowNumber(); row++)
            {
                try
                {
                    var rutCell = worksheet.Cell(row, headers["RUT"]).GetString().Trim();
                    if (string.IsNullOrEmpty(rutCell))
                    {
                        Console.WriteLine($"⚠️ Fila {row}: RUT vacío, saltando...");
                        continue;
                    }

                    Console.WriteLine($"📝 Procesando fila {row}: RUT {rutCell}");

                    // Verificar si el empleado ya existe
                    var empleadoExistente = _db.Empleados.FirstOrDefault(e => e.RUT == rutCell);

                    if (empleadoExistente == null)
                    {
                        // CREAR NUEVO EMPLEADO CON ORDEN SECUENCIAL
                        var nuevoEmpleado = CrearNuevoEmpleado(worksheet, row, headers);
                        nuevoEmpleado.OrdenImportacion = siguienteOrden++;

                        nuevosEmpleados.Add(nuevoEmpleado);
                        empleadosNuevos++;
                        Console.WriteLine($"✅ Nuevo empleado creado: {nuevoEmpleado.Nombre} {nuevoEmpleado.Apellido_Paterno} (Orden: {nuevoEmpleado.OrdenImportacion})");
                    }
                    else
                    {
                        // ACTUALIZAR EMPLEADO EXISTENTE
                        ActualizarEmpleado(empleadoExistente, worksheet, row, headers);
                        empleadosActualizados++;
                        Console.WriteLine($"🔄 Empleado actualizado: {empleadoExistente.Nombre} {empleadoExistente.Apellido_Paterno} (Mantuvo orden: {empleadoExistente.OrdenImportacion})");
                    }
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Fila {row}: {ex.Message}";
                    errores.Add(errorMsg);
                    Console.WriteLine($"❌ {errorMsg}");
                }
            }

            Console.WriteLine($"💾 Guardando cambios: {empleadosNuevos} nuevos, {empleadosActualizados} actualizados");

            if (nuevosEmpleados.Any())
            {
                _db.Empleados.AddRange(nuevosEmpleados);
            }

            await _db.SaveChangesAsync();
            var mensaje = $"Importación completada exitosamente";
            if (empleadosNuevos > 0) mensaje += $" - {empleadosNuevos} empleados nuevos creados";
            if (empleadosActualizados > 0) mensaje += $" - {empleadosActualizados} empleados actualizados";
            if (errores.Any()) mensaje += $" - {errores.Count} errores encontrados";

            Console.WriteLine($"✅ {mensaje}");

            return new JsonResult(new
            {
                success = true,
                message = mensaje,
                detalles = new
                {
                    nuevos = empleadosNuevos,
                    actualizados = empleadosActualizados,
                    errores = errores.Take(10).ToList(),
                    ordenInicio = siguienteOrden - empleadosNuevos,
                    ordenFin = siguienteOrden - 1
                }
            });
        }
        catch (Exception ex)
        {
            var errorCompleto = $"Error al procesar el archivo: {ex.Message}";
            if (ex.InnerException != null)
            {
                errorCompleto += $" | Inner: {ex.InnerException.Message}";
            }

            Console.WriteLine($"💥 Error crítico: {errorCompleto}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");

            return new JsonResult(new
            {
                success = false,
                message = errorCompleto
            });
        }
    }

    private string BuscarValorColumna(IXLWorksheet worksheet, int row, Dictionary<string, int> headers, params string[] posiblesNombres)
    {
        foreach (var nombre in posiblesNombres)
        {
            if (headers.ContainsKey(nombre))
            {
                return worksheet.Cell(row, headers[nombre]).GetString();
            }
        }
        return "";
    }


// Crear nuevo empleado con búsqueda flexible
private Empleado CrearNuevoEmpleado(IXLWorksheet worksheet, int row, Dictionary<string, int> headers)
{
    var nuevoEmpleado = new Empleado
    {
        Analista = BuscarValorColumna(worksheet, row, headers, "Analista"),
        
        // Búsqueda flexible para las nuevas columnas
        Entrevista_Experiencia = BuscarValorColumna(worksheet, row, headers, 
            "Entrevista Experiencia", "Entrevista_Experiencia", "EntrevistaExperiencia"),
        
        Encuesta_Eficacia = BuscarValorColumna(worksheet, row, headers, 
            "Encuesta Eficacia", "Encuesta Eficaciad", "Encuesta_Eficacia", "EncuestaEficacia"),

        Fecha_Induccion = ParseDateTime(BuscarValorColumna(worksheet, row, headers, "Fecha Inducción", "Fecha Induccion")),
        Fecha_Ingreso = ParseDateTime(BuscarValorColumna(worksheet, row, headers, "Fecha Ingreso")),
        RUT = BuscarValorColumna(worksheet, row, headers, "RUT"),
        Nombre = BuscarValorColumna(worksheet, row, headers, "Nombre"),
        Apellido_Paterno = BuscarValorColumna(worksheet, row, headers, "Apellido Paterno"),
        Apellido_Materno = BuscarValorColumna(worksheet, row, headers, "Apellido Materno"),
        Fecha_Nacimiento = ParseDateTime(BuscarValorColumna(worksheet, row, headers, "Fecha de Nacimiento", "Fecha Nacimiento")),
        Correo = BuscarValorColumna(worksheet, row, headers, "Correo"),
        Telefono = BuscarValorColumna(worksheet, row, headers, "Teléfono", "Telefono"),
        Direccion = BuscarValorColumna(worksheet, row, headers, "Dirección", "Direccion"),
        Sociedad = BuscarValorColumna(worksheet, row, headers, "Sociedad"),
        Cargo = BuscarValorColumna(worksheet, row, headers, "Cargo"),
        Gerencia = BuscarValorColumna(worksheet, row, headers, "Gerencia"),
        Gerencia_mail = BuscarValorColumna(worksheet, row, headers, "Gerencia Mail", "Gerencia_mail", "GerenciaMail"),
        Ubicacion = BuscarValorColumna(worksheet, row, headers, "Ubicación", "Ubicacion"),
        Jefe_Directo = BuscarValorColumna(worksheet, row, headers, "Jefe Directo"),
        Tipo_de_Contrato = BuscarValorColumna(worksheet, row, headers, "Tipo de Contrato (Reposición, Expansión)", "Tipo de Contrato"),
        Quienrepone = BuscarValorColumna(worksheet, row, headers, "A quien repone", "Quienrepone", "Quien repone"),
        Ticket = BuscarValorColumna(worksheet, row, headers, "Ticket"),
        Fecha_Induccion_Cultural = ParseDateTime(BuscarValorColumna(worksheet, row, headers, "Fecha Inducción Cultura", "Fecha Induccion Cultura")),
        Estado_Induccion_Cultural = BuscarValorColumna(worksheet, row, headers, "Estado Inducción Cultura", "Estado Induccion Cultura"),
        Contrato = BuscarValorColumna(worksheet, row, headers, "Contrato"),
        Carrera = BuscarValorColumna(worksheet, row, headers, "Carrera"),
        Universidad = BuscarValorColumna(worksheet, row, headers, "Universidad"),

        // CAMPOS DE TRACKING
        UltimaModificacion = DateTime.Now,
        UsuarioModifico = User.Identity?.Name ?? "Sistema"
    };

    // ENCRIPTAR DATOS SENSIBLES ANTES DE GUARDAR
    EncriptarDatosEmpleado(nuevoEmpleado);
    
    return nuevoEmpleado;
}

 // Actualizar empleado existente con búsqueda flexible
private void ActualizarEmpleado(Empleado empleado, IXLWorksheet worksheet, int row, Dictionary<string, int> headers)
{
    empleado.Analista = BuscarValorColumna(worksheet, row, headers, "Analista");
    
    // Búsqueda flexible para las nuevas columnas
    empleado.Entrevista_Experiencia = BuscarValorColumna(worksheet, row, headers, 
        "Entrevista Experiencia", "Entrevista_Experiencia", "EntrevistaExperiencia");
    
    empleado.Encuesta_Eficacia = BuscarValorColumna(worksheet, row, headers, 
        "Encuesta Eficacia", "Encuesta Eficaciad", "Encuesta_Eficacia", "EncuestaEficacia");
    
    empleado.Fecha_Induccion = ParseDateTime(BuscarValorColumna(worksheet, row, headers, "Fecha Inducción", "Fecha Induccion"));
    empleado.Fecha_Ingreso = ParseDateTime(BuscarValorColumna(worksheet, row, headers, "Fecha Ingreso"));
    empleado.Nombre = BuscarValorColumna(worksheet, row, headers, "Nombre");
    empleado.Apellido_Paterno = BuscarValorColumna(worksheet, row, headers, "Apellido Paterno");
    empleado.Apellido_Materno = BuscarValorColumna(worksheet, row, headers, "Apellido Materno");
    empleado.Fecha_Nacimiento = ParseDateTime(BuscarValorColumna(worksheet, row, headers, "Fecha de Nacimiento", "Fecha Nacimiento"));
    empleado.Correo = BuscarValorColumna(worksheet, row, headers, "Correo");
    empleado.Telefono = BuscarValorColumna(worksheet, row, headers, "Teléfono", "Telefono");
    empleado.Direccion = BuscarValorColumna(worksheet, row, headers, "Dirección", "Direccion");
    empleado.Sociedad = BuscarValorColumna(worksheet, row, headers, "Sociedad");
    empleado.Cargo = BuscarValorColumna(worksheet, row, headers, "Cargo");
    empleado.Gerencia = BuscarValorColumna(worksheet, row, headers, "Gerencia");
    empleado.Gerencia_mail = BuscarValorColumna(worksheet, row, headers, "Gerencia Mail", "Gerencia_mail", "GerenciaMail");
    empleado.Ubicacion = BuscarValorColumna(worksheet, row, headers, "Ubicación", "Ubicacion");
    empleado.Jefe_Directo = BuscarValorColumna(worksheet, row, headers, "Jefe Directo");
    empleado.Tipo_de_Contrato = BuscarValorColumna(worksheet, row, headers, "Tipo de Contrato (Reposición, Expansión)", "Tipo de Contrato");
    empleado.Quienrepone = BuscarValorColumna(worksheet, row, headers, "A quien repone", "Quienrepone", "Quien repone");
    empleado.Ticket = BuscarValorColumna(worksheet, row, headers, "Ticket");
    empleado.Fecha_Induccion_Cultural = ParseDateTime(BuscarValorColumna(worksheet, row, headers, "Fecha Inducción Cultura", "Fecha Induccion Cultura"));
    empleado.Estado_Induccion_Cultural = BuscarValorColumna(worksheet, row, headers, "Estado Inducción Cultura", "Estado Induccion Cultura");
    empleado.Contrato = BuscarValorColumna(worksheet, row, headers, "Contrato");
    empleado.Carrera = BuscarValorColumna(worksheet, row, headers, "Carrera");
    empleado.Universidad = BuscarValorColumna(worksheet, row, headers, "Universidad");

    // ENCRIPTAR DATOS SENSIBLES ANTES DE GUARDAR
    EncriptarDatosEmpleado(empleado);

    // ACTUALIZAR CAMPOS DE TRACKING
    empleado.UltimaModificacion = DateTime.Now;
    empleado.UsuarioModifico = User.Identity?.Name ?? "Sistema";
}

// MÉTODO PARA ENCRIPTAR DATOS SENSIBLES DEL EMPLEADO
private void EncriptarDatosEmpleado(Empleado empleado)
{
    try
    {
        // Encriptar solo si el dato no está vacío y no está ya encriptado
        if (!string.IsNullOrEmpty(empleado.Correo) && !EstaEncriptado(empleado.Correo))
        {
            empleado.Correo = _encryptionService.Encrypt(empleado.Correo);
        }

        if (!string.IsNullOrEmpty(empleado.Telefono) && !EstaEncriptado(empleado.Telefono))
        {
            empleado.Telefono = _encryptionService.Encrypt(empleado.Telefono);
        }

        if (!string.IsNullOrEmpty(empleado.Gerencia_mail) && !EstaEncriptado(empleado.Gerencia_mail))
        {
            empleado.Gerencia_mail = _encryptionService.Encrypt(empleado.Gerencia_mail);
        }

        if (!string.IsNullOrEmpty(empleado.Ticket) && !EstaEncriptado(empleado.Ticket))
        {
            empleado.Ticket = _encryptionService.Encrypt(empleado.Ticket);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error al encriptar datos del empleado: {ex.Message}");
    }
}

// MÉTODO PARA VERIFICAR SI UN DATO YA ESTÁ ENCRIPTADO
private bool EstaEncriptado(string dato)
{
    try
    {
        // Intentar desencriptar, si falla es porque no está encriptado
        _encryptionService.Decrypt(dato);
        return true; 
    }
    catch
    {
        return false; 
    }
}

// Parsear fechas de manera segura
private DateTime? ParseDateTime(string dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
            return null;

        if (DateTime.TryParse(dateString, out var date))
            return date;

        return null;
    }


    public async Task<IActionResult> OnPostExportExcelAsync()
    {
        try
        {
            // Verificar permisos de administrador
            var usuario = _db.Usuarios.FirstOrDefault(u => u.Correo == User.Identity.Name);
            if (usuario?.EsAdmin != true)
            {
                return new JsonResult(new { success = false, message = "No tienes permisos para realizar esta operación" });
            }

            Console.WriteLine("Iniciando exportación de empleados...");

            // Obtener todos los empleados
            var empleados = _db.Empleados
                .OrderBy(e => e.OrdenImportacion)
                .ThenBy(e => e.Apellido_Paterno)
                .ThenBy(e => e.Nombre)
                .ToList();

            Console.WriteLine($"Encontrados {empleados.Count} empleados para exportar");

            if (!empleados.Any())
            {
                return new JsonResult(new { success = false, message = "No hay empleados para exportar" });
            }

            // Crear el archivo Excel
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Empleados");

            // CONFIGURAR DISEÑO PERSONALIZADO DEL EXCEL
            ConfigurarHojaExcel(worksheet);

            // AGREGAR ENCABEZADOS CON ESTILO
            AgregarEncabezados(worksheet);

            // AGREGAR DATOS DE EMPLEADOS
            AgregarDatosEmpleados(worksheet, empleados);

            // APLICAR FORMATO FINAL
            AplicarFormatoFinal(worksheet, empleados.Count);

            // Crear el archivo en memoria
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var content = stream.ToArray();

            var fileName = $"Planificacion - Induccion Corporativa {DateTime.Now:dd-MM-yyyy}.xlsx";

            Console.WriteLine($"Archivo Excel generado: {fileName}");

            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error en OnPostExportExcel: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");

            return new JsonResult(new
            {
                success = false,
                message = $"Error al generar el archivo: {ex.Message}"
            });
        }
    }

    // Configurar hoja Excel
    private void ConfigurarHojaExcel(IXLWorksheet worksheet)
    {
        // Configurar orientación y márgenes
        worksheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        worksheet.PageSetup.FitToPages(1, 0);
        worksheet.PageSetup.Margins.Top = 0.75;
        worksheet.PageSetup.Margins.Bottom = 0.75;
        worksheet.PageSetup.Margins.Left = 0.25;
        worksheet.PageSetup.Margins.Right = 0.25;

        // Configurar header/footer
        worksheet.PageSetup.Header.Center.AddText("PLANIFICACIÓN - INDUCCIÓN CORPORATIVA");
        worksheet.PageSetup.Footer.Center.AddText("Página &P de &N");
        worksheet.PageSetup.Footer.Right.AddText("Generado: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
    }

        // Agregar encabezados con estilo
    private void AgregarEncabezados(IXLWorksheet worksheet)
    {
        // Encabezados de columnas
        string[] headers = {
            "Analista", "Entrevista Experiencia", "Encuesta Eficacia", "Fecha Inducción", "Fecha Ingreso",
            "RUT", "Nombre", "Apellido Paterno", "Apellido Materno", "Fecha de Nacimiento", "Correo",
            "Teléfono", "Dirección", "Sociedad", "Cargo", "Gerencia", "Gerencia Mail", "Ubicación", "Jefe Directo",
            "Tipo de Contrato (Reposición, Expansión)", "A quien repone", "Ticket", "Fecha Inducción Cultura",
            "Estado Inducción Cultura", "Contrato", "Carrera", "Universidad"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cell(1, i + 1);
            cell.Value = headers[i];

            // ESTILO DE ENCABEZADOS
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontSize = 11;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(0, 112, 192);
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Alignment.WrapText = true;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = XLColor.Black;
        }
        worksheet.Row(1).Height = 30;
    }

private void AgregarDatosEmpleados(IXLWorksheet worksheet, List<Empleado> empleados)
{
    int row = 2;

    foreach (var emp in empleados)
    {

        string correoDesencriptado = DesencriptarDato(emp.Correo);
        string telefonoDesencriptado = DesencriptarDato(emp.Telefono);
        string gerenciaMailDesencriptado = DesencriptarDato(emp.Gerencia_mail);
        string ticketDesencriptado = DesencriptarDato(emp.Ticket);
        worksheet.Cell(row, 1).Value = emp.Analista ?? "";
        worksheet.Cell(row, 2).Value = emp.Entrevista_Experiencia ?? "";
        worksheet.Cell(row, 3).Value = emp.Encuesta_Eficacia ?? "";
        worksheet.Cell(row, 4).Value = emp.Fecha_Induccion?.ToString("dd-MM-yyyy") ?? "";
        worksheet.Cell(row, 5).Value = emp.Fecha_Ingreso?.ToString("dd-MM-yyyy") ?? "";
        worksheet.Cell(row, 6).Value = emp.RUT ?? "";
        worksheet.Cell(row, 7).Value = emp.Nombre ?? "";
        worksheet.Cell(row, 8).Value = emp.Apellido_Paterno ?? "";
        worksheet.Cell(row, 9).Value = emp.Apellido_Materno ?? "";
        worksheet.Cell(row, 10).Value = emp.Fecha_Nacimiento?.ToString("dd-MM-yyyy") ?? "";
        worksheet.Cell(row, 11).Value = correoDesencriptado; 
        worksheet.Cell(row, 12).Value = telefonoDesencriptado; 
        worksheet.Cell(row, 13).Value = emp.Direccion ?? "";
        worksheet.Cell(row, 14).Value = emp.Sociedad ?? "";
        worksheet.Cell(row, 15).Value = emp.Cargo ?? "";
        worksheet.Cell(row, 16).Value = emp.Gerencia ?? "";
        worksheet.Cell(row, 17).Value = gerenciaMailDesencriptado;
        worksheet.Cell(row, 18).Value = emp.Ubicacion ?? "";
        worksheet.Cell(row, 19).Value = emp.Jefe_Directo ?? "";
        worksheet.Cell(row, 20).Value = emp.Tipo_de_Contrato ?? "";
        worksheet.Cell(row, 21).Value = emp.Quienrepone ?? "";
        worksheet.Cell(row, 22).Value = ticketDesencriptado;
        worksheet.Cell(row, 23).Value = emp.Fecha_Induccion_Cultural?.ToString("dd-MM-yyyy") ?? "";
        worksheet.Cell(row, 24).Value = emp.Estado_Induccion_Cultural ?? "";
        worksheet.Cell(row, 25).Value = emp.Contrato ?? "";
        worksheet.Cell(row, 26).Value = emp.Carrera ?? "";
        worksheet.Cell(row, 27).Value = emp.Universidad ?? "";

        // APLICAR ESTILO A LA FILA
        var rowRange = worksheet.Range(row, 1, row, 27);

        if (row % 2 == 0) // Filas pares
        {
            rowRange.Style.Fill.BackgroundColor = XLColor.FromArgb(217, 225, 242); // #d9e1f2
        }
        else // Filas impares
        {
            rowRange.Style.Fill.BackgroundColor = XLColor.White; // #ffffff
        }

        // FORMATO ESPECIAL PARA SOCIEDAD
        var sociedadCell = worksheet.Cell(row, 14);
        var sociedad = emp.Sociedad?.ToUpper().Trim() ?? "";

        if (sociedad == "ADV")
        {
            // FORMATO ESPECIAL PARA ADV
            sociedadCell.Style.Fill.BackgroundColor = XLColor.FromArgb(255, 235, 156); // #ffeb9c
            sociedadCell.Style.Font.FontColor = XLColor.FromArgb(173, 87, 36); // #ad5724
            sociedadCell.Style.Font.Bold = true;
        }
        else
        {
            sociedadCell.Style.Font.FontColor = XLColor.Black;
            sociedadCell.Style.Font.Bold = false;
        }

        // Bordes y formato general
        rowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        rowRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        rowRange.Style.Font.FontSize = 10;
        rowRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        // Altura de la fila
        worksheet.Row(row).Height = 20;

        row++;
    }
}


    // MÉTODO PARA DESENCRIPTAR DATOS
    private string DesencriptarDato(string? datoEncriptado)
    {
        if (string.IsNullOrEmpty(datoEncriptado))
            return "";

        try
        {
            // Usar el servicio de encriptación para desencriptar
            string datoDesencriptado = _encryptionService.Decrypt(datoEncriptado);
            return datoDesencriptado ?? "";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al desencriptar dato: {ex.Message}");

            return "[Error al desencriptar]";
        }
    }


    // Aplicar formato final
private void AplicarFormatoFinal(IXLWorksheet worksheet, int totalEmpleados)
{
    // Ajustar ancho de columnas automáticamente
    worksheet.Columns(1, 27).AdjustToContents();

    // AJUSTAR ANCHOS ESPECÍFICOS PARA COLUMNAS IMPORTANTES
    worksheet.Column(1).Width = 15;  // Analista
    worksheet.Column(2).Width = 18;  // Entrevista Experiencia
    worksheet.Column(3).Width = 15;  // Encuesta Eficacia
    worksheet.Column(4).Width = 12;  // Fecha Inducción
    worksheet.Column(5).Width = 12;  // Fecha Ingreso
    worksheet.Column(6).Width = 12;  // RUT
    worksheet.Column(7).Width = 15;  // Nombre
    worksheet.Column(8).Width = 15;  // Apellido Paterno
    worksheet.Column(9).Width = 15;  // Apellido Materno
    worksheet.Column(10).Width = 15; // Fecha Nacimiento
    worksheet.Column(11).Width = 25; // Correo
    worksheet.Column(12).Width = 12; // Teléfono
    worksheet.Column(13).Width = 20; // Dirección
    worksheet.Column(14).Width = 10; // Sociedad
    worksheet.Column(15).Width = 20; // Cargo
    worksheet.Column(16).Width = 15; // Gerencia
    worksheet.Column(17).Width = 25; // Gerencia Mail
    worksheet.Column(18).Width = 20; // Ubicación
    worksheet.Column(19).Width = 15; // Jefe Directo
    worksheet.Column(20).Width = 20; // Tipo Contrato
    worksheet.Column(21).Width = 15; // A quien repone
    worksheet.Column(22).Width = 15; // Ticket
    worksheet.Column(23).Width = 15; // Fecha Inducción Cultural
    worksheet.Column(24).Width = 20; // Estado Inducción Cultural
    worksheet.Column(25).Width = 15; // Contrato
    worksheet.Column(26).Width = 15; // Carrera
    worksheet.Column(27).Width = 20; // Universidad

    // APLICAR FILTROS AUTOMÁTICOS
    var dataRange = worksheet.Range(1, 1, 1 + totalEmpleados, 27);
    dataRange.SetAutoFilter();

    // CONGELAR PANELES (primera fila de encabezados)
    worksheet.SheetView.FreezeRows(1);
}


    // Handler para eliminar base de datos
    public async Task<IActionResult> OnPostDeleteDatabaseAsync()
    {
        try
        {
            // Verificar permisos de administrador
            var usuario = _db.Usuarios.FirstOrDefault(u => u.Correo == User.Identity.Name);
            if (usuario?.EsAdmin != true)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "No tienes permisos para realizar esta operación"
                });
            }

            Console.WriteLine("Iniciando eliminación de base de datos de empleados...");

            // Contar empleados antes de eliminar
            var cantidadEmpleados = _db.Empleados.Count();
            Console.WriteLine($"Empleados a eliminar: {cantidadEmpleados}");

            if (cantidadEmpleados == 0)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "No hay empleados para eliminar. La base de datos ya está vacía."
                });
            }

            // Eliminar todos los empleados
            var empleados = _db.Empleados.ToList();
            _db.Empleados.RemoveRange(empleados);

            // Guardar cambios
            await _db.SaveChangesAsync();

            Console.WriteLine($"Base de datos eliminada exitosamente. {cantidadEmpleados} empleados eliminados.");

            return new JsonResult(new
            {
                success = true,
                message = "Base de datos eliminada exitosamente",
                deletedCount = cantidadEmpleados
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error en OnPostDeleteDatabase: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");

            return new JsonResult(new
            {
                success = false,
                message = $"Error al eliminar la base de datos: {ex.Message}"
            });
        }
    }

    // Handler para validar contraseña
    public async Task<IActionResult> OnPostValidatePasswordAsync([FromBody] PasswordRequest request)
    {
        // Get current user
        var currentUser = _db.Usuarios.FirstOrDefault(u => u.Correo == User.Identity.Name);
        if (currentUser == null)
        {
            return new JsonResult(new { valid = false });
        }

        // Validate password (in a real app, use proper password hashing)
        bool isValid = currentUser.Password == request.Password;

        return new JsonResult(new { valid = isValid });
    }
    
    // CLASE PARA LA REQUEST
    public class PasswordRequest
    {
        public string Password { get; set; } = "";
    }

}
