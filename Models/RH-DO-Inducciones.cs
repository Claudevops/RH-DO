using System.ComponentModel.DataAnnotations; // Agrega esto al inicio del archivo
using System.Collections.Generic;
using System;
using System.Security.Cryptography;
using System.Text;

public class AuditoriaAcceso
{
    [Key]
    public int Id { get; set; }
    public string Usuario { get; set; } = "";
    public string RUTEmpleadoAccedido { get; set; } = "";
    public string AccionRealizada { get; set; } = ""; // Consulta, Edición, Eliminación
    public DateTime FechaHora { get; set; } = DateTime.Now;
    public string? DatosModificados { get; set; } // JSON de cambios
    public string IPAddress { get; set; } = "";
}

public class Empleado //Plantilla para la base de datos de empleados (Subida de datos)
{
    [Key]
    public int Id { get; set; }
    public string? Analista { get; set; }
    public string? Entrevista_Experiencia { get; set; }
    public string? Encuesta_Eficacia { get; set; }
    public DateTime? Fecha_Induccion { get; set; }
    public DateTime? Fecha_Ingreso { get; set; }
    public string RUT { get; set; } = "";
    public string? Nombre { get; set; } = "";
    public string? Apellido_Paterno { get; set; }
    public string? Apellido_Materno { get; set; }
    public DateTime? Fecha_Nacimiento { get; set; }
    [EncryptedData]
    public string? Correo { get; set; }
    [EncryptedData]
    public string? Telefono { get; set; }
    public string? Direccion { get; set; }
    public string? Sociedad { get; set; }
    public string? Cargo { get; set; }
    public string? Gerencia { get; set; }
    [EncryptedData]
    public string? Gerencia_mail { get; set; } = "";
    public string? Ubicacion { get; set; }
    public string? Jefe_Directo { get; set; }
    public string? Tipo_de_Contrato { get; set; }
    public string? Quienrepone { get; set; }
    [EncryptedData]
    public string? Ticket { get; set; }
    public DateTime? Fecha_Induccion_Cultural { get; set; }
    public string? Estado_Induccion_Cultural { get; set; }
    public string? Contrato { get; set; }
    public string? Carrera { get; set; }
    public string? Universidad { get; set; }

    
    public int OrdenImportacion { get; set; } = 0;
    public DateTime? UltimaModificacion { get; set; }
    public string? UsuarioModifico { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    public string UsuarioCreador { get; set; } = "";

}

public class Analista
{
    [Key] // Agrega esto para que sea la clave primaria
    public int Id_ANA { get; set; }
    public string? Nombre_ANA { get; set; } = "";
}

public class Sociedad
{
    [Key] // Agrega esto para que sea la clave primaria
    public int Id_SOC { get; set; }
    public string? Nombre_SOC { get; set; } = "";
}

public class Gerencia
{
    [Key] // Agrega esto para que sea la clave primaria
    public int Id_GER { get; set; }
    public string? Nombre_GER { get; set; } = "";
}


public class Ubicaciones
{
    [Key] // Agrega esto para que sea la clave primaria
    public int Id_UBI { get; set; }
    public string? Nombre_UBI { get; set; } = "";
}

public class AdjuntoCorreo
{
    [Key]
    public int Id_AC { get; set; }
    public string? Nombre_Adjunto { get; set; } = "";
    public string? ArchivoPath { get; set; } = ""; // Ruta del archivo subido
    public int Tipo { get; set; } // 2 o 3
    public DateTime FechaSubida { get; set; } = DateTime.Now;
}


public class Usuario //Plantilla Provisoria para el login con la base de datos 
{
    [Key]
    public int Id { get; set; }
    public string NombreUsuario { get; set; } = "";
    public string Correo { get; set; } = "";
    public string Password { get; set; } = "";
    public bool EsAdmin { get; set; } = false;
    public bool Activo { get; set; } = true;
}

public class DestinatariosCorreo_PRE
{
    [Key]
    public int Id_DCP { get; set; }
    public int Tipo_DCP { get; set; }
    public string? DestinatariosFijos { get; set; }
    public string? CCFijos { get; set; }
    public string? CCOFijos { get; set; }
}

public class PlantillaCorreo
{
    [Key] // Agrega esto para que sea la clave primaria
    public int Id_PC { get; set; }
    public int Tipo_PC { get; set; }
    public string Nombre_PC { get; set; } = "";
    public string HtmlContenido { get; set; } = "";
}

//unificación correo y plantilla

public class CorreoConfiguracionViewModel
{
    public int Tipo { get; set; }
    public DestinatariosCorreo_PRE? Destinatarios { get; set; }
    public List<AdjuntoCorreo> Adjuntos { get; set; } = new();
    public PlantillaCorreo? Plantilla { get; set; }
}

//modelo SolicitudEdicion
public class SolicitudEdicion
{
    public int Id { get; set; }
    public string RutEmpleado { get; set; } = "";
    public string DatosSolicitados { get; set; } = ""; // JSON con los datos solicitados o info de eliminación
    public string? DatosAnteriores { get; set; } // JSON con datos anteriores (para reversión)
    public string UsuarioSolicitante { get; set; } = "";
    public string Tipo { get; set; } = ""; // "Edicion" o "Eliminacion"
    public DateTime FechaSolicitud { get; set; }
    public bool Procesada { get; set; } = false;
    public bool? Aprobada { get; set; } // null = pendiente, true = aprobada, false = rechazada
    public DateTime? FechaProcesamiento { get; set; }
    public string? UsuarioProceso { get; set; }
}


//modelos de correos para las post inducciones
public class DestinatariosCorreo_POST
{
    [Key]
    public int Id_DCP { get; set; }
    public int Tipo_DCP { get; set; }
    public string? DestinatariosFijos { get; set; }
    public string? CCFijos { get; set; }
    public string? CCOFijos { get; set; }
}

public class AdjuntoCorreo_post
{
    [Key]
    public int Id_AC { get; set; }
    public string? Nombre_Adjunto { get; set; } = "";
    public string? ArchivoPath { get; set; } = ""; // Ruta del archivo subido
    public int Tipo { get; set; }
    public DateTime FechaSubida { get; set; } = DateTime.Now;
}

public class PlantillaCorreo_POST
{
    [Key] // Agrega esto para que sea la clave primaria
    public int Id_PC { get; set; }
    public int Tipo_PC { get; set; }
    public string Nombre_PC { get; set; } = "";
    public string HtmlContenido { get; set; } = "";
}

public class CorreoConfiguracionViewModelpost
{
    public int Tipo { get; set; }
    public DestinatariosCorreo_POST? Destinatarios { get; set; }
    public List<AdjuntoCorreo_post> Adjuntos { get; set; } = new();
    public PlantillaCorreo_POST? Plantilla { get; set; }
}

public class CorreoEnviado_POST
{
    [Key]
    public int Id { get; set; }
    public string RUT { get; set; } = "";
    public int TipoCorreo { get; set; } // 1, 2, 3
    public DateTime FechaEnvio { get; set; }
    public string EmailDestinatario { get; set; } = "";
    public bool Completado { get; set; } = true;
}