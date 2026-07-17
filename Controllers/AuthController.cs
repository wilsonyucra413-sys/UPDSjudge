using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UPDSjudgeB.data;
using UPDSjudgeB.DTOs;
using UPDSjudgeB.Services;

namespace UPDSjudgeB.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly JwtService _jwtService;
        private readonly AuthService _authService;

        public AuthController(ApplicationDbContext context, JwtService jwtService, AuthService authService)
        {
            _context = context;
            _jwtService = jwtService;
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            // Validar que se enviaron credenciales
            if (string.IsNullOrWhiteSpace(request.correo) || string.IsNullOrWhiteSpace(request.contrasena))
            {
                return BadRequest(new { mensaje = "Correo y contraseña son obligatorios." });
            }

            // Buscar el usuario por correo e incluir sus roles mediante Include y ThenInclude
            var usuario = await _context.Usuarios
                .Include(u => u.UsuarioRoles)
                    .ThenInclude(ur => ur.Rol)
                .FirstOrDefaultAsync(u => u.correo == request.correo);

            // Usuario no encontrado: responder con 401 sin revelar si el correo existe
            if (usuario == null)
            {
                return Unauthorized(new { mensaje = "Credenciales inválidas." });
            }

            // Verificar la contraseña usando BCrypt contra el hash almacenado en la BD
            if (!BCrypt.Net.BCrypt.Verify(request.contrasena, usuario.contrasena))
            {
                return Unauthorized(new { mensaje = "Credenciales inválidas." });
            }

            // Generar el token JWT con idUsuario, nombre, correo y roles
            var (token, expiraEn) = _jwtService.GenerarToken(usuario);

            return Ok(new LoginResponse
            {
                token = token,
                expiraEn = expiraEn
            });
        }

        [HttpPost("register")]
        public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest request)
        {
            // Validar que todos los campos obligatorios fueron enviados
            if (string.IsNullOrWhiteSpace(request.nombre)
                || string.IsNullOrWhiteSpace(request.correo)
                || string.IsNullOrWhiteSpace(request.contrasena))
            {
                return BadRequest(new { mensaje = "Nombre, correo y contraseña son obligatorios." });
            }

            // Delegar la lógica de creación al servicio
            var (exito, error, respuesta) = await _authService.RegistrarUsuarioAsync(request);

            if (!exito)
            {
                return BadRequest(new { mensaje = error });
            }

            // Registro exitoso: HTTP 201 Created
            return StatusCode(StatusCodes.Status201Created, respuesta);
        }
    }
}
