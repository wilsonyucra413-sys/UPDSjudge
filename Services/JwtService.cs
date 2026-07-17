using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using UPDSjudgeB.Models;

namespace UPDSjudgeB.Services
{
    /// <summary>
    /// Servicio encargado únicamente de generar tokens JWT.
    /// No valida credenciales ni accede a la base de datos.
    /// </summary>
    public class JwtService
    {
        private readonly IConfiguration _configuration;

        public JwtService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public (string token, DateTime expiraEn) GenerarToken(Usuario usuario)
        {
            // Leer configuración JWT desde appsettings.json
            var jwtKey = _configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("Jwt:Key no está configurada en appsettings.json.");
            var jwtIssuer = _configuration["Jwt:Issuer"] ?? "UPDSJudge";
            var jwtAudience = _configuration["Jwt:Audience"] ?? "UPDSJudgeUsers";

            // Duración del token: 8 horas (sin refresh token por ahora)
            var expiraEn = DateTime.UtcNow.AddHours(8);

            // Construir los claims que irán dentro del payload del JWT
            var claims = new List<Claim>
            {
                // Identificador único del usuario
                new Claim("idUsuario", usuario.idUsuario.ToString()),
                // Nombre y correo del usuario
                new Claim("nombre", usuario.nombre),
                new Claim("correo", usuario.correo),
                // Claim estándar de nombre (útil para User.Identity.Name)
                new Claim(ClaimTypes.Name, usuario.nombre),
                // Claim estándar de email
                new Claim(ClaimTypes.Email, usuario.correo)
            };

            // Agregar cada rol del usuario como ClaimTypes.Role
            // Esto permite usar [Authorize(Roles="AdministradorConcursos")] en los controladores
            if (usuario.UsuarioRoles != null)
            {
                foreach (var usuarioRol in usuario.UsuarioRoles)
                {
                    if (usuarioRol.Rol != null)
                    {
                        claims.Add(new Claim(ClaimTypes.Role, usuarioRol.Rol.nombre));
                    }
                }
            }

            // Crear la clave simétrica de firma a partir de Jwt:Key
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credenciales = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Construir el token con issuer, audience, claims y tiempo de expiración
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expiraEn,
                Issuer = jwtIssuer,
                Audience = jwtAudience,
                SigningCredentials = credenciales
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return (tokenHandler.WriteToken(token), expiraEn);
        }
    }
}
