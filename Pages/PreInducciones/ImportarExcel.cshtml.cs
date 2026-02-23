using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ClosedXML.Excel;

public class ImportarExcelModel : PageModel
{
    private readonly AppDbContext _db;
    public ImportarExcelModel(AppDbContext db) { _db = db; }

    [BindProperty]
    public IFormFile? ExcelFile { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            if (ExcelFile == null || ExcelFile.Length == 0)
                return new JsonResult(new { success = false, message = "Archivo no válido o vacío" });

            using var stream = new MemoryStream();
            await ExcelFile.CopyToAsync(stream);

            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.First();

            // Validar que hay datos
            var lastRow = worksheet.LastRowUsed();
            var lastCol = worksheet.LastColumnUsed();
            if (lastRow == null || lastCol == null)
                return new JsonResult(new { success = false, message = "El archivo Excel está vacío o no tiene datos." });

            // Leer encabezados
            var headers = new Dictionary<string, int>();
            var headerRow = worksheet.Row(1);
            for (int col = 1; col <= lastCol.ColumnNumber(); col++)
            {
                var header = headerRow.Cell(col).GetString().Trim();
                if (!string.IsNullOrEmpty(header))
                    headers[header] = col;
            }
            // Validar encabezados requeridos
            string[] requeridos = {
                "Analista","Fecha Inducción","Fecha Ingreso","RUT","Nombre","Apellido Paterno","Apellido Materno",
                "Fecha de Nacimiento","Correo","Teléfono","Dirección","Sociedad","Cargo","Gerencia","Ubicación",
                "Jefe Directo","Tipo de Contrato (Reposición, Expansión)","Ticket","Fecha Inducción Cultura",
                "Estado Inducción Cultura","Contrato","Carrera","Universidad"
            };
            foreach (var req in requeridos)
                if (!headers.ContainsKey(req))
                    return new JsonResult(new { success = false, message = $"Falta columna: {req}" });

            for (int row = 2; row <= lastRow.RowNumber(); row++)
            {
                var empleado = new Empleado
                {
                    Analista = worksheet.Cell(row, headers["Analista"]).GetString(),
                    Entrevista_Experiencia = worksheet.Cell(row, headers["Entrevista Experiencia"]).GetString(),
                    Encuesta_Eficacia = worksheet.Cell(row, headers["Encuesta Eficaciad"]).GetString(),
                    Fecha_Induccion = DateTime.TryParse(worksheet.Cell(row, headers["Fecha Inducción"]).GetString(), out var fi) ? fi : (DateTime?)null,
                    Fecha_Ingreso = DateTime.TryParse(worksheet.Cell(row, headers["Fecha Ingreso"]).GetString(), out var fing) ? fing : (DateTime?)null,
                    RUT = worksheet.Cell(row, headers["RUT"]).GetString(),
                    Nombre = worksheet.Cell(row, headers["Nombre"]).GetString(),
                    Apellido_Paterno = worksheet.Cell(row, headers["Apellido Paterno"]).GetString(),
                    Apellido_Materno = worksheet.Cell(row, headers["Apellido Materno"]).GetString(),
                    Fecha_Nacimiento = DateTime.TryParse(worksheet.Cell(row, headers["Fecha de Nacimiento"]).GetString(), out var fnac) ? fnac : (DateTime?)null,
                    Correo = worksheet.Cell(row, headers["Correo"]).GetString(),
                    Telefono = worksheet.Cell(row, headers["Teléfono"]).GetString(),
                    Direccion = worksheet.Cell(row, headers["Dirección"]).GetString(),
                    Sociedad = worksheet.Cell(row, headers["Sociedad"]).GetString(),
                    Cargo = worksheet.Cell(row, headers["Cargo"]).GetString(),
                    Gerencia = worksheet.Cell(row, headers["Gerencia"]).GetString(),
                    Ubicacion = worksheet.Cell(row, headers["Ubicación"]).GetString(),
                    Jefe_Directo = worksheet.Cell(row, headers["Jefe Directo"]).GetString(),
                    Tipo_de_Contrato = worksheet.Cell(row, headers["Tipo de Contrato (Reposición, Expansión)"]).GetString(),
                    Ticket = worksheet.Cell(row, headers["Ticket"]).GetString(),
                    Fecha_Induccion_Cultural = DateTime.TryParse(worksheet.Cell(row, headers["Fecha Inducción Cultura"]).GetString(), out var fic) ? fic : (DateTime?)null,
                    Estado_Induccion_Cultural = worksheet.Cell(row, headers["Estado Inducción Cultura"]).GetString(),
                    Contrato = worksheet.Cell(row, headers["Contrato"]).GetString(),
                    Carrera = worksheet.Cell(row, headers["Carrera"]).GetString(),
                    Universidad = worksheet.Cell(row, headers["Universidad"]).GetString()
                    
                };
                _db.Empleados.Add(empleado);
            }
            await _db.SaveChangesAsync();
            return new JsonResult(new { success = true, message = "Archivo importado correctamente" });
        }
catch (Exception ex)
{
    var inner = ex.InnerException?.Message ?? "";
    return new JsonResult(new { success = false, message = "Error: " + ex.Message + " " + inner });
}
    }
}