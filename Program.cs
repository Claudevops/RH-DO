//Se utiliza entity framework core para la base de datos
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

// Configurar la cadena de conexión
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

//Esta línea configura el acceso a la base de datos en una aplicación .NET usando Entity Framework Core y SQLite
builder.Services.AddDbContext<AppDbContext>(options =>options.UseSqlite("Data Source=RH-DO.db"));
builder.Services.AddHttpContextAccessor();
//Esta línea configura la autenticación de cookies en una aplicación .NET
builder.Services.AddAuthentication("MiCookieAuth")
    .AddCookie("MiCookieAuth", options =>
    {
        options.LoginPath = "/Login/login";
    });

// Registrar el servicio de email
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<EncryptionService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Configuración de la aplicación (Verificar que no se repitan)
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseRouting();
app.UseAuthorization();

// Siempre al final
app.MapRazorPages();
app.Run();