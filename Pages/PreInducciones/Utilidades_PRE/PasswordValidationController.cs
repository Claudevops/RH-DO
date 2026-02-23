using Microsoft.AspNetCore.Mvc;

namespace RH_DO.Controllers.Api
{
    [Route("api")]
    [ApiController]
    public class PasswordValidationController : ControllerBase
    {
        private readonly AppDbContext _db;

        public PasswordValidationController(AppDbContext db)
        {
            _db = db;
        }

        [HttpPost("validate-password")]
        public IActionResult ValidatePassword([FromBody] PasswordRequest request)
        {
            // Obtener el usuario actual
            var currentUser = _db.Usuarios.FirstOrDefault(u => u.Correo == User.Identity.Name);
            if (currentUser == null)
            {
                return Unauthorized(new { valid = false });
            }

            // Validar la contraseña (en la aplicación real)
            bool isValid = currentUser.Password == request.Password;

            return Ok(new { valid = isValid });
        }

        [HttpGet("users")]
        public IActionResult GetUsers()
        {
            var users = _db.Usuarios.ToList();
            return Ok(users);
        }

        [HttpPost("update-admin-status")]
        public IActionResult UpdateAdminStatus([FromBody] AdminStatusRequest request)
        {
            // Obtener el usuario por ID
            var user = _db.Usuarios.FirstOrDefault(u => u.Id == request.UserId);
            if (user == null)
            {
                return NotFound(new { success = false, message = "Usuario no encontrado" });
            }

            // Si es que estoy tratando de quitar el rol de admin y no hay más de un admin
            if (!request.IsAdmin)
            {
                var adminCount = _db.Usuarios.Count(u => u.EsAdmin);
                if (adminCount <= 1 && user.EsAdmin)
                {
                    return BadRequest(new { success = false, message = "Debe haber al menos un administrador" });
                }
            }

            // Update admin status
            user.EsAdmin = request.IsAdmin;
            _db.SaveChanges();

            return Ok(new { success = true });
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
}