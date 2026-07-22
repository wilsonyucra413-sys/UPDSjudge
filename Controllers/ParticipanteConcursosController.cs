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
    [ApiController]
    [Route("api/[controller]")]
    public class ParticipanteConcursosController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public ParticipanteConcursosController(ApplicationDbContext context)
        {
            _context = context;
        }
        // aqui colcoare el ranking
        [Authorize(Roles = "Usuario")]
        [HttpPost("unirse")]
        public async Task<IActionResult> Unirse([FromBody] UnirseConcursoDto dto)
        {
            var userIdClaim = User.FindFirst("idUsuario")?.Value;
            if (userIdClaim == null)
                return Unauthorized(new { mensaje = "Token inválido" });
            int idUsuarioLogueado = int.Parse(userIdClaim);

            dto.codigo = dto.codigo?.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(dto.codigo))
                return BadRequest(new { mensaje = "El código del concurso es obligatorio." });

            var concurso = await _context.Concursos
                .FirstOrDefaultAsync(c => c.codigo == dto.codigo && c.estado == "Activo");

            if (concurso == null)
                return NotFound(new { mensaje = "El concurso no existe o fue eliminado." });

            if (concurso.idUsuarioCreador == idUsuarioLogueado)
                return BadRequest(new { mensaje = "No puedes inscribirte a un concurso que tú mismo creaste." });

            var ahora = DateTime.UtcNow;

            // Las inscripciones cierran apenas inicia el concurso, no cuando termina.
            if (ahora >= concurso.fechaInicio)
                return BadRequest(new { mensaje = "Las inscripciones para este concurso ya están cerradas." });

            bool esPrivado = !string.IsNullOrWhiteSpace(concurso.contrasena);
            if (esPrivado)
            {
                if (string.IsNullOrWhiteSpace(dto.contrasena))
                    return BadRequest(new { mensaje = "Este concurso es privado, debes ingresar la contraseña." });

                if (dto.contrasena != concurso.contrasena)
                    return Unauthorized(new { mensaje = "Contraseña incorrecta." });
            }

            var participacionExistente = await _context.ParticipantesConcursos
                .FirstOrDefaultAsync(p => p.idUsuario == idUsuarioLogueado && p.idConcurso == concurso.idConcurso);

            if (participacionExistente != null)
            {
                if (participacionExistente.estado == "Activo")
                    return Ok(new { mensaje = "Ya estás inscrito en este concurso.", codConcurso = concurso.codigo });

                participacionExistente.estado = "Activo";
                participacionExistente.fechaIngreso = ahora;
                await _context.SaveChangesAsync();
                return Ok(new { mensaje = "Te has vuelto a inscribir al concurso.", codConcurso = concurso.codigo });
            }

            var nuevaParticipacion = new ParticipanteConcurso
            {
                idUsuario = idUsuarioLogueado,
                idConcurso = concurso.idConcurso,
                fechaIngreso = ahora,
                estado = "Activo"
            };

            _context.ParticipantesConcursos.Add(nuevaParticipacion);

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { mensaje = "Te has inscrito al concurso exitosamente.", codConcurso = concurso.codigo });
            }
            catch (DbUpdateException)
            {
                return Ok(new { mensaje = "Ya estás inscrito en este concurso.", codConcurso = concurso.codigo });
            }
        }

    }
}