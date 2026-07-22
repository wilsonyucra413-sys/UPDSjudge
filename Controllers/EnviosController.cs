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
    public class EnviosController : ControllerBase
    {

        private readonly ApplicationDbContext _context;
        public EnviosController(ApplicationDbContext context)
        {
            _context = context;
        }
        // • Accepted
        // • Wrong Answer
        // • Compilation Error
        // • Runtime Error
        // • Time Limit Exceeded
        // • Memory Limit Exceeded
        [Authorize(Roles = "Usuario")]
        [HttpGet("mis-envios")]
        public async Task<IActionResult> ListarMisEnvios(
            [FromQuery] string? resultado = null, //"AC""WA""TLE""MLE""CE""RE"
            [FromQuery] string? concursoCodigo = null,
            [FromQuery] string? inciso = null,
            [FromQuery] int pagina = 1,
            [FromQuery] int tamanoPagina = 20)
        {
            var userIdClaim = User.FindFirst("idUsuario")?.Value;
            if (userIdClaim == null) return Unauthorized(new { mensaje = "Token inválido" });
            int idUsuarioLogueado = int.Parse(userIdClaim);

            if (pagina < 1) pagina = 1;
            if (tamanoPagina < 1 || tamanoPagina > 50) tamanoPagina = 20;

            var query = _context.Envios
                .Include(e => e.Problema)
                    .ThenInclude(p => p.Concurso)
                .Include(e => e.Lenguaje)
                .Where(e => e.idUsuario == idUsuarioLogueado
                         && e.Problema.Concurso.estado == "Activo");

            if (!string.IsNullOrWhiteSpace(resultado))
            {
                string veredictoBusqueda = resultado.ToUpper() switch
                {
                    "AC" => VeredictosEnvio.Aceptado,
                    "WA" => VeredictosEnvio.RespuestaIncorrecta,
                    "TLE" => VeredictosEnvio.TiempoExcedido,
                    "MLE" => VeredictosEnvio.MemoriaExcedida,
                    "CE" => VeredictosEnvio.ErrorCompilacion,
                    "RE" => VeredictosEnvio.ErrorEjecucion,
                    _ => resultado
                };
                query = query.Where(e => e.resultado == veredictoBusqueda);
            }

            if (!string.IsNullOrWhiteSpace(concursoCodigo))
            {
                query = query.Where(e => EF.Functions.ILike(e.Problema.Concurso.codigo, $"%{concursoCodigo}%"));
            }

            if (!string.IsNullOrWhiteSpace(inciso))
            {
                char incisoChar = inciso.Trim().ToUpper()[0];
                query = query.Where(e => e.Problema.inciso == incisoChar);
            }

            var total = await query.CountAsync();
            var envios = await query
                .OrderByDescending(e => e.fechaEnvio)
                .Skip((pagina - 1) * tamanoPagina)
                .Take(tamanoPagina)
                .Select(e => new
                {
                    e.idEnvio,
                    concursoCodigo = e.Problema.Concurso.codigo,
                    problemaTitulo = e.Problema.titulo,
                    inciso = e.Problema.inciso.ToString(),
                    lenguaje = e.Lenguaje.nombre,
                    veredicto = e.resultado,
                    consumoTiempo = e.tiempo,
                    consumoMemoria = e.memoria,
                    fechaEnvio = e.fechaEnvio
                })
                .ToListAsync();

            return Ok(new
            {
                total,
                pagina,
                tamanoPagina,
                datos = envios
            });
        }
        private const int LONGITUD_MAXIMA_CODIGO = 100_000;

        [Authorize(Roles = "Usuario")]
        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] CrearEnvioDto dto)
        {
            var userIdClaim = User.FindFirst("idUsuario")?.Value;
            if (userIdClaim == null)
                return Unauthorized(new { mensaje = "Token inválido" });
            int idUsuarioLogueado = int.Parse(userIdClaim);

            if (string.IsNullOrWhiteSpace(dto.codigoFuente))
                return BadRequest(new { mensaje = "El código fuente es obligatorio." });

            if (dto.codigoFuente.Length > LONGITUD_MAXIMA_CODIGO)
                return BadRequest(new { mensaje = $"El código fuente no puede superar los {LONGITUD_MAXIMA_CODIGO / 1000} KB." });

            if (string.IsNullOrWhiteSpace(dto.extension))
                return BadRequest(new { mensaje = "Debes indicar el lenguaje del código (extensión)." });

            string extensionNormalizada = dto.extension.Trim().ToLowerInvariant().TrimStart('.');

            var lenguaje = await _context.Lenguajes
                .FirstOrDefaultAsync(l => l.extension == extensionNormalizada && l.estado == "Activo");

            if (lenguaje == null)
                return BadRequest(new { mensaje = $"El lenguaje con extensión '.{extensionNormalizada}' no está soportado." });

            if (string.IsNullOrWhiteSpace(dto.codigo))
                return BadRequest(new { mensaje = "El código del concurso es obligatorio." });

            string codigoConcurso = dto.codigo.Trim().ToLowerInvariant();
            char incisoNormalizado = char.ToUpperInvariant(dto.inciso);

            // Buscamos el concurso primero, luego el problema dentro de él por inciso
            var concurso = await _context.Concursos
                .FirstOrDefaultAsync(c => c.codigo == codigoConcurso && c.estado == "Activo");

            if (concurso == null)
                return NotFound(new { mensaje = "El concurso no existe o fue eliminado." });

            var problema = await _context.Problemas
                .FirstOrDefaultAsync(p => p.idConcurso == concurso.idConcurso
                                        && p.inciso == incisoNormalizado
                                        && p.estado == "Activo");

            if (problema == null)
                return NotFound(new { mensaje = $"No existe el problema con inciso '{incisoNormalizado}' en este concurso." });

            var ahora = DateTime.UtcNow;
            var fechaFin = concurso.fechaInicio.AddMinutes(concurso.duracionMinutos);

            string estadoTiempo = ahora < concurso.fechaInicio ? "Proximo"
                : ahora < fechaFin ? "Activo" : "Finalizado";

            if (estadoTiempo == "Proximo")
                return BadRequest(new { mensaje = "El concurso todavía no ha iniciado." });

            bool esPrivado = !string.IsNullOrWhiteSpace(concurso.contrasena);
            bool esCreador = concurso.idUsuarioCreador == idUsuarioLogueado;

            bool yaInscrito = await _context.ParticipantesConcursos
                .AnyAsync(p => p.idUsuario == idUsuarioLogueado
                            && p.idConcurso == concurso.idConcurso
                            && p.estado == "Activo");

            if (!yaInscrito && !esCreador)
            {
                if (estadoTiempo != "Finalizado")
                    return BadRequest(new { mensaje = "Debes estar inscrito para enviar soluciones mientras el concurso está en curso." });

                if (esPrivado)
                {
                    if (string.IsNullOrWhiteSpace(dto.contrasena))
                        return BadRequest(new { mensaje = "Este concurso es privado, ingresa la contraseña para hacer upsolving." });

                    if (dto.contrasena != concurso.contrasena)
                        return Unauthorized(new { mensaje = "Contraseña incorrecta." });
                }
            }

            bool esUpsolving = ahora >= fechaFin;

            var nuevoEnvio = new Envio
            {
                codigo = dto.codigoFuente,
                resultado = "Pendiente",
                tiempo = 0,
                memoria = 0,
                token = null,
                upsolving = esUpsolving ? "Si" : "No",
                fechaEnvio = ahora,
                idUsuario = idUsuarioLogueado,
                idProblema = problema.idProblema,
                idLenguaje = lenguaje.idLenguaje
            };

            _context.Envios.Add(nuevoEnvio);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                idEnvio = nuevoEnvio.idEnvio,
                mensaje = "Envío registrado, en espera de evaluación.",
                upsolving = esUpsolving
            });
        }
        

    }
}