using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

public class LoginModel : PageModel
{
    private readonly AppDbContext _db;

    public LoginModel(AppDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new InputModel();

    public string? ErrorMessage { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        var usuario = _db.Usuarios.FirstOrDefault(u => u.Correo == Input.Correo && u.Password == Input.Password);
        if (usuario != null)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, usuario.Correo)
            };
            var identity = new ClaimsIdentity(claims, "MiCookieAuth");
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync("MiCookieAuth", principal);

            return RedirectToPage("/Index");
        }

        ErrorMessage = "Correo o contraseña incorrectos";
        return Page();
    }

    public class InputModel
    {
        public string Correo { get; set; } = "";
        public string Password { get; set; } = "";
    }
}