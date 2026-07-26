using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UPDSjudgeB.data;
using UPDSjudgeB.DTOs;
using UPDSjudgeB.Models;

namespace UPDSjudgeB.Controllers
{
    [Authorize(Roles = "AdministradorRoles")]
    [ApiController]
    [Route("api/[controller]")]
    public class RolesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        // no pueden modificar al superusuario (el que tiene los 3 roles)
        private const int ID_USUARIO_PROTEGIDO = 1;

        public RolesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Listar()
        {
            var roles = await _context.Roles
                .Select(r => new { r.idRol, r.nombre })
                .ToListAsync();

            return Ok(roles);
        }

        [HttpGet("usuarios")]
        [HttpGet("usuarios")]
        public async Task<IActionResult> BuscarUsuarios(
            [FromQuery] string? query, [FromQuery] int pagina = 1, [FromQuery] int tamanoPagina = 10)
        {
            if (pagina < 1) pagina = 1;
            if (tamanoPagina < 1 || tamanoPagina > 50) tamanoPagina = 10;

            var busqueda = _context.Usuarios
                .Where(u => u.idUsuario != ID_USUARIO_PROTEGIDO) // nunca se muestra ni se puede tocar
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                busqueda = busqueda.Where(u =>
                    u.correo.Contains(query) || u.nombre.Contains(query));
            }

            var total = await busqueda.CountAsync();

            var usuarios = await busqueda
                .OrderBy(u => u.nombre)
                .Skip((pagina - 1) * tamanoPagina)
                .Take(tamanoPagina)
                .Select(u => new UsuarioBusquedaDto
                {
                    idUsuario = u.idUsuario,
                    nombre = u.nombre,
                    correo = u.correo,
                    estado = u.estado,
                    roles = u.UsuarioRoles.Select(ur => ur.Rol.nombre).ToList()
                })
                .ToListAsync();

            return Ok(new { total, pagina, tamanoPagina, usuarios });
        }

        [HttpPost("agregar")]
        public async Task<IActionResult> Agregar([FromBody] CambiarRolDto dto)
        {
            var (esValido, mensajeError, usuario, rol) = await ValidarUsuarioYRolAsync(dto);
            if (!esValido)
                return BadRequest(new { mensaje = mensajeError });

            if (usuario!.idUsuario == ID_USUARIO_PROTEGIDO)
                return BadRequest(new { mensaje = "No se pueden modificar los roles del usuario administrador por defecto." });

            bool yaLoTiene = await _context.UsuarioRoles
                .AnyAsync(ur => ur.idUsuario == usuario.idUsuario && ur.idRol == rol!.idRol);

            if (yaLoTiene)
                return BadRequest(new { mensaje = $"El usuario ya tiene el rol '{rol!.nombre}'." });

            _context.UsuarioRoles.Add(new UsuarioRol
            {
                idUsuario = usuario.idUsuario,
                idRol = rol!.idRol
            });

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { mensaje = $"Rol '{rol.nombre}' asignado correctamente a {usuario.correo}." });
            }
            catch (DbUpdateException)
            {
                return BadRequest(new { mensaje = $"El usuario ya tiene el rol '{rol.nombre}'." });
            }
        }

        [HttpPost("quitar")]
        public async Task<IActionResult> Quitar([FromBody] CambiarRolDto dto)
        {
            var (esValido, mensajeError, usuario, rol) = await ValidarUsuarioYRolAsync(dto);
            if (!esValido)
                return BadRequest(new { mensaje = mensajeError });

            if (usuario!.idUsuario == ID_USUARIO_PROTEGIDO)
                return BadRequest(new { mensaje = "No se pueden modificar los roles del usuario administrador por defecto." });

            var relacion = await _context.UsuarioRoles
                .FirstOrDefaultAsync(ur => ur.idUsuario == usuario.idUsuario && ur.idRol == rol!.idRol);

            if (relacion == null)
                return BadRequest(new { mensaje = $"El usuario no tiene el rol '{rol!.nombre}', no se puede quitar." });

            _context.UsuarioRoles.Remove(relacion);
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = $"Rol '{rol!.nombre}' quitado correctamente a {usuario.correo}." });
        }

        private async Task<(bool EsValido, string Mensaje, Usuario? Usuario, Rol? Rol)> ValidarUsuarioYRolAsync(CambiarRolDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.correo))
                return (false, "El correo es obligatorio.", null, null);

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.correo == dto.correo);

            if (usuario == null)
                return (false, "No existe un usuario con ese correo.", null, null);

            if (usuario.estado != "Activo")
                return (false, "El usuario está inactivo, no se pueden modificar sus roles.", null, null);

            var rol = await _context.Roles
                .FirstOrDefaultAsync(r => r.idRol == dto.idRol);

            if (rol == null)
                return (false, "El rol especificado no existe.", null, null);

            return (true, string.Empty, usuario, rol);
        }
    }
}