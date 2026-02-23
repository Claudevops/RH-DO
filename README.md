# Sistema RH-DO

Sistema web desarrollado en ASP.NET Core 8.0 para la gestión de Recursos Humanos.

## Características

- Gestión de empleados
- Visualización de datos
- Interfaz web moderna
- Arquitectura MVC
- Conexión a base de datos
- CRUD y gestión de personal / datos

## Tecnologías utilizadas

- ASP.NET Core 8.0
- C#
- Entity Framework
- SQLite
- HTML / CSS / JavaScript
## Instalación

Requisitos Previos.
- Windows 7 o Superior
- .NET SDK versión 8.0 o superior
- Base de Datos: RH-DO MysqlServer (Acceso y credenciales)
- Instalar .NET SDK Descargar .NET 8.0 SDK (v8.0.413) - Windows x64 Installer
- Despliegue de aplicación.
- Configurar Appsettings.json. 

Instalar Paquetes NuGet:
- Paquetes Utilizados (Instalación mediante terminal).
- dotnet add package Microsoft.EntityFrameworkCore.SqlServer
- dotnet add package Microsoft.EntityFrameworkCore.Tools
- dotnet add package Microsoft.AspNetCore.Authentication.OpenIdConnect
- dotnet add package Microsoft.Identity.Web
- dotnet add package Microsoft.Graph
- dotnet add package ClosedXML
- dotnet add package EPPlus
- dotnet add package MailKit

Restaurar Paquetes NuGet:
- dotnet restore

Compilar Aplicación:
- Dotnet Build

## Autor

Claudio Calderón  
Ingeniero Informático  
Chile
