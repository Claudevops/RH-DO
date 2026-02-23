using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Threading.Tasks;

public class Configuracion_PRE : PageModel
{ 
    private readonly AppDbContext _db;
    public int SolicitudesPendientes { get; set; } = 0;
    public bool EsAdmin { get; set; }
    
    public Configuracion_PRE(AppDbContext db)
    {
        _db = db;
    }

    public void OnGet()
    {
        var usuario = _db.Usuarios.FirstOrDefault(u => u.Correo == User.Identity.Name);
        EsAdmin = usuario?.EsAdmin ?? false;

        // Si no es admin, para el index
        if (!EsAdmin)
        {
            Response.Redirect("/login/");
        }

        if (EsAdmin)
        {
            SolicitudesPendientes = _db.SolicitudesEdicion.Count(s => !s.Procesada);
        }
    }
    
    public class PasswordRequest
    {
        public string Password { get; set; }
    }
    
    public class AdminStatusRequest
    {
        public int UserId { get; set; }
        public bool IsAdmin { get; set; }
    }
    
    [BindProperty]
    public PasswordRequest PasswordData { get; set; }
    
    public async Task<IActionResult> OnPostValidatePasswordAsync([FromBody] PasswordRequest request)
    {
        // Get current user
        var currentUser = _db.Usuarios.FirstOrDefault(u => u.Correo == User.Identity.Name);
        if (currentUser == null)
        {
            return new JsonResult(new { valid = false });
        }

        // Validate password
        bool isValid = currentUser.Password == request.Password;

        return new JsonResult(new { valid = isValid });
    }
    
    public async Task<IActionResult> OnGetUsersAsync()
    {
        var users = _db.Usuarios.ToList();
        return new JsonResult(users);
    }
    
    public async Task<IActionResult> OnPostUpdateAdminStatusAsync([FromBody] AdminStatusRequest request)
    {
        var user = _db.Usuarios.FirstOrDefault(u => u.Id == request.UserId);
        if (user == null)
        {
            return new JsonResult(new { success = false, message = "Usuario no encontrado" });
        }

        if (!request.IsAdmin)
        {
            var adminCount = _db.Usuarios.Count(u => u.EsAdmin);
            if (adminCount <= 1 && user.EsAdmin)
            {
                return new JsonResult(new { success = false, message = "Debe haber al menos un administrador" });
            }
        }
        
        user.EsAdmin = request.IsAdmin;
        _db.SaveChanges();

        return new JsonResult(new { success = true });
    }
}