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
        [Authorize(Roles = "Usuario")]
        [HttpGet("stats-contest")]
        public async Task<IActionResult> GetStatsDashboard()
        {
            var userIdClaim = User.FindFirst("idUsuario")?.Value;
            if (userIdClaim == null) return Unauthorized(new { mensaje = "Token inválido" });
            int idUsuarioLogueado = int.Parse(userIdClaim);

            var envios = await _context.Envios
                .Where(e => e.idUsuario == idUsuarioLogueado && e.Problema.Concurso.estado == "Activo")
                .Select(e => new
                {
                    e.idProblema,
                    resultado = e.resultado.ToLower(),
                    e.fechaEnvio,
                    idConcurso = e.Problema.idConcurso
                })
                .ToListAsync();

            if (!envios.Any())
                return Ok(new { concursosParticipados = 0, problemasResueltos = 0, problemasPendientes = 0, precisionPorcentaje = 0 });

            int concursosParticipados = envios.Select(e => e.idConcurso).Distinct().Count();
            var resueltosIds = envios.Where(e => e.resultado == "accepted").Select(e => e.idProblema).Distinct().ToHashSet();
            int problemasPendientes = envios.Select(e => e.idProblema).Distinct().Count(id => !resueltosIds.Contains(id));

            double precisionFinal = 0;
            if (resueltosIds.Any())
            {
                precisionFinal = envios
                    .GroupBy(e => e.idProblema)
                    .Where(g => resueltosIds.Contains(g.Key))
                    .Select(g =>
                    {
                        var primerAC = g.Where(x => x.resultado == "accepted").OrderBy(x => x.fechaEnvio).First();
                        int intentos = g.Count(x => x.fechaEnvio <= primerAC.fechaEnvio);
                        return 1.0 / intentos;
                    })
                    .Average() * 100;
            }

            return Ok(new
            {
                concursosParticipados,
                problemasResueltos = resueltosIds.Count,
                problemasPendientes,
                precisionPorcentaje = Math.Round(precisionFinal, 2)
            });
        }
    }
}