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

        // GET api/roles/usuarios?query=...
        // Búsqueda de usuarios por correo o nombre, con sus roles actuales
        [HttpGet("usuarios")]
        public async Task<IActionResult> BuscarUsuarios(
            [FromQuery] string? query, [FromQuery] int pagina = 1, [FromQuery] int tamanoPagina = 10)
        {
            if (pagina < 1) pagina = 1;
            if (tamanoPagina < 1 || tamanoPagina > 50) tamanoPagina = 10;

            var busqueda = _context.Usuarios.AsQueryable();

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

        // POST api/roles/agregar
        [HttpPost("agregar")]
        public async Task<IActionResult> Agregar([FromBody] CambiarRolDto dto)
        {
            var (esValido, mensajeError, usuario, rol) = await ValidarUsuarioYRolAsync(dto);
            if (!esValido)
                return BadRequest(new { mensaje = mensajeError });

            bool yaLoTiene = await _context.UsuarioRoles
                .AnyAsync(ur => ur.idUsuario == usuario!.idUsuario && ur.idRol == rol!.idRol);

            if (yaLoTiene)
                return BadRequest(new { mensaje = $"El usuario ya tiene el rol '{rol!.nombre}'." });

            _context.UsuarioRoles.Add(new UsuarioRol
            {
                idUsuario = usuario!.idUsuario,
                idRol = rol!.idRol
            });

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { mensaje = $"Rol '{rol.nombre}' asignado correctamente a {usuario.correo}." });
            }
            catch (DbUpdateException)
            {
                // Cubre dos requests casi simultáneos asignando el mismo rol
                return BadRequest(new { mensaje = $"El usuario ya tiene el rol '{rol.nombre}'." });
            }
        }

        // POST api/roles/quitar
        [HttpPost("quitar")]
        public async Task<IActionResult> Quitar([FromBody] CambiarRolDto dto)
        {
            var (esValido, mensajeError, usuario, rol) = await ValidarUsuarioYRolAsync(dto);
            if (!esValido)
                return BadRequest(new { mensaje = mensajeError });

            var relacion = await _context.UsuarioRoles
                .FirstOrDefaultAsync(ur => ur.idUsuario == usuario!.idUsuario && ur.idRol == rol!.idRol);

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