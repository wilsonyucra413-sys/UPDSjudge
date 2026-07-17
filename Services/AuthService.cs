using Microsoft.EntityFrameworkCore;
using UPDSjudgeB.data;
using UPDSjudgeB.DTOs;
using UPDSjudgeB.Models;

namespace UPDSjudgeB.Services
{

    public class AuthService
    {
        private readonly ApplicationDbContext _context;


        private const int IdRolUsuario = 3;

        public AuthService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<(bool exito, string? error, RegisterResponse? respuesta)> RegistrarUsuarioAsync(RegisterRequest request)
        {
            var correoExiste = await _context.Usuarios
                .AnyAsync(u => u.correo == request.correo);

            if (correoExiste)
            {
                return (false, "El correo ya está registrado.", null);
            }

            var contrasenaHash = BCrypt.Net.BCrypt.HashPassword(request.contrasena);


            var usuario = new Usuario
            {
                nombre = request.nombre.Trim(),
                correo = request.correo.Trim(),
                contrasena = contrasenaHash,
                fechaRegistro = DateTime.UtcNow,
                estado = "Activo"
            };

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            var usuarioRol = new UsuarioRol
            {
                idUsuario = usuario.idUsuario,
                idRol = IdRolUsuario
            };

            _context.UsuarioRoles.Add(usuarioRol);
            await _context.SaveChangesAsync();

            return (true, null, new RegisterResponse
            {
                mensaje = "Usuario registrado exitosamente.",
                correo = usuario.correo
            });
        }
    }
}
