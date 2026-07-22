using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UPDSjudgeB.data;
using UPDSjudgeB.DTOs;
using UPDSjudgeB.Models;
using static UPDSjudgeB.DTOs.ProblemaDto;

namespace UPDSjudgeB.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConcursosController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public ConcursosController(ApplicationDbContext context)
        {
            _context = context;
        }
        [Authorize(Roles = "AdministradorConcursos")]
        [HttpPost("crear")]
        [RequestSizeLimit(100 * 1024 * 1024)] // 100 MB
        [RequestFormLimits(MultipartBodyLengthLimit = 100 * 1024 * 1024)]
        public async Task<IActionResult> Crear([FromForm] CrearConcursoDto dto)
        {
            var userIdClaim = User.FindFirst("idUsuario")?.Value;
            if (userIdClaim == null)
                return Unauthorized(new { mensaje = "Token inválido" });
            int idUsuarioLogueado = int.Parse(userIdClaim);

            // Normalizamos ANTES de validar formato y ANTES de comparar duplicados
            dto.codigo = dto.codigo?.Trim().ToLowerInvariant();

            dto.fechaInicio = dto.fechaInicio.Kind switch
            {
                DateTimeKind.Utc => dto.fechaInicio,
                DateTimeKind.Local => dto.fechaInicio.ToUniversalTime(),
                _ => DateTime.SpecifyKind(dto.fechaInicio, DateTimeKind.Utc)
            };

            var (dtoValido, mensajeDto) = ValidarDatosConcurso(dto);
            if (!dtoValido)
                return BadRequest(new { mensaje = mensajeDto });

            if (await _context.Concursos.AnyAsync(c => c.codigo == dto.codigo && c.estado == "Activo"))
                return BadRequest(new { mensaje = "Ya existe un concurso activo con ese código." });

            var (estructuraValida, mensajeEstructura, mapaCarpetas) =
                await ValidarEstructuraZipAsync(dto.archivoZip, dto.listaProblemas);
            if (!estructuraValida)
                return BadRequest(new { mensaje = mensajeEstructura });

            var (casosValidos, mensajeCasos, casosPorInciso) =
                await ValidarCasosPruebaAsync(dto.archivoZip, mapaCarpetas);
            if (!casosValidos)
                return BadRequest(new { mensaje = mensajeCasos });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var nuevoConcurso = new Concurso
                {
                    nombre = dto.nombre,
                    descripcion = dto.descripcion,
                    fechaInicio = dto.fechaInicio,
                    duracionMinutos = dto.duracionMinutos,
                    contrasena = string.IsNullOrWhiteSpace(dto.contrasena) ? null : dto.contrasena,
                    urlSetProblemas = dto.urlSetProblemas,
                    minutosCongelamiento = dto.minutosCongelamiento,
                    codigo = dto.codigo,
                    estado = "Activo",
                    idUsuarioCreador = idUsuarioLogueado
                };
                _context.Concursos.Add(nuevoConcurso);
                await _context.SaveChangesAsync();

                foreach (var probDto in dto.listaProblemas)
                {
                    var nuevoProblema = new Problema
                    {
                        idConcurso = nuevoConcurso.idConcurso,
                        inciso = probDto.inciso,
                        titulo = probDto.titulo,
                        tiempo = probDto.tiempo,
                        memoria = probDto.memoria,
                        estado = "Activo"
                    };
                    _context.Problemas.Add(nuevoProblema);
                    await _context.SaveChangesAsync();

                    string claveInciso = probDto.inciso.ToString().ToUpperInvariant();
                    foreach (var (contenidoIn, contenidoOut) in casosPorInciso[claveInciso])
                    {
                        _context.CasosPrueba.Add(new CasoPrueba
                        {
                            idProblema = nuevoProblema.idProblema,
                            entrada = contenidoIn,
                            salida = contenidoOut,
                            estado = "Activo"
                        });
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    codigo = nuevoConcurso.codigo,
                    mensaje = "Concurso, problemas y casos de prueba creados exitosamente."
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { mensaje = "Error al procesar el concurso.", detalle = ex.Message });
            }
        }

        [Authorize(Roles = "Usuario")]
        [HttpGet]
        public async Task<IActionResult> Listar(
            [FromQuery] string filtro = "todos",
            [FromQuery] string? modalidad = null,
            [FromQuery] string? busqueda = null,
            [FromQuery] int pagina = 1,
            [FromQuery] int tamanoPagina = 20)
        {
            var userIdClaim = User.FindFirst("idUsuario")?.Value;
            int idUsuarioLogueado = userIdClaim != null ? int.Parse(userIdClaim) : 0;

            if (pagina < 1) pagina = 1;
            if (tamanoPagina < 1 || tamanoPagina > 50) tamanoPagina = 20;

            var ahora = DateTime.UtcNow;

            IQueryable<Concurso> query = _context.Concursos
                .Where(c => c.estado == "Activo");

            if (!string.IsNullOrWhiteSpace(busqueda))
                query = query.Where(c => EF.Functions.ILike(c.codigo, $"%{busqueda}%"));

            query = filtro?.ToLowerInvariant() switch
            {
                "activos" => query.Where(c =>
                    ahora >= c.fechaInicio && ahora < c.fechaInicio.AddMinutes(c.duracionMinutos)),
                "proximos" => query.Where(c => ahora < c.fechaInicio),
                "finalizados" => query.Where(c =>
                    ahora >= c.fechaInicio.AddMinutes(c.duracionMinutos)),
                _ => query
            };

            if (!string.IsNullOrWhiteSpace(modalidad))
            {
                bool quierePrivados = modalidad.Equals("privado", StringComparison.OrdinalIgnoreCase);
                bool quierePublicos = modalidad.Equals("publico", StringComparison.OrdinalIgnoreCase);

                if (quierePrivados)
                    query = query.Where(c => c.contrasena != null && c.contrasena != "");
                else if (quierePublicos)
                    query = query.Where(c => c.contrasena == null || c.contrasena == "");
            }

            var total = await query.CountAsync();

            query = query
                .OrderBy(c => ahora < c.fechaInicio ? 1
                            : (ahora < c.fechaInicio.AddMinutes(c.duracionMinutos) ? 0 : 2))
                .ThenBy(c => c.fechaInicio);

            var crudos = await query
                .Skip((pagina - 1) * tamanoPagina)
                .Take(tamanoPagina)
                .Select(c => new
                {
                    c.idConcurso,
                    c.nombre,
                    c.descripcion,
                    c.codigo,
                    c.contrasena,
                    c.fechaInicio,
                    c.duracionMinutos,
                    c.minutosCongelamiento,
                    cantidadProblemas = c.Problemas.Count(p => p.estado == "Activo"),
                    cantidadParticipantes = c.Participantes.Count(pc => pc.estado == "Activo"),
                    yaInscrito = c.Participantes.Any(pc =>
                        pc.idUsuario == idUsuarioLogueado && pc.estado == "Activo")
                })
                .ToListAsync();

            var paginaDatos = crudos.Select(c =>
            {
                var fechaFin = c.fechaInicio.AddMinutes(c.duracionMinutos);
                string estadoTiempo = ahora < c.fechaInicio ? "Proximo"
                    : ahora < fechaFin ? "Activo" : "Finalizado";

                DateTime? fechaCongelamiento = c.minutosCongelamiento > 0
                    ? fechaFin.AddMinutes(-c.minutosCongelamiento)
                    : null;

                int? segundosRestantes = estadoTiempo == "Activo"
                    ? (int)Math.Max(0, (fechaFin - ahora).TotalSeconds)
                    : null;

                return new ConcursoListItemDto
                {
                    idConcurso = c.idConcurso,
                    nombre = c.nombre,
                    descripcion = c.descripcion,
                    codigo = c.codigo,
                    estadoTiempo = estadoTiempo,
                    modalidad = string.IsNullOrWhiteSpace(c.contrasena) ? "Publico" : "Privado",
                    fechaInicio = c.fechaInicio,
                    fechaFin = fechaFin,
                    fechaCongelamiento = fechaCongelamiento,
                    duracionMinutos = c.duracionMinutos,
                    minutosCongelamiento = c.minutosCongelamiento,
                    cantidadProblemas = c.cantidadProblemas,
                    cantidadParticipantes = c.cantidadParticipantes,
                    yaInscrito = c.yaInscrito,
                    segundosRestantes = segundosRestantes
                };
            }).ToList();

            var idsFinalizadosInscritos = paginaDatos
                .Where(c => c.estadoTiempo == "Finalizado" && c.yaInscrito)
                .Select(c => c.idConcurso)
                .ToList();

            if (idsFinalizadosInscritos.Any())
            {
                await CompletarDesempenoAsync(paginaDatos, idsFinalizadosInscritos, idUsuarioLogueado);
            }

            return Ok(new ConcursosPaginadosDto
            {
                total = total,
                pagina = pagina,
                tamanoPagina = tamanoPagina,
                concursos = paginaDatos
            });
        }

        [Authorize(Roles = "Usuario")]
        [HttpGet("mis-registros")]
        public async Task<IActionResult> MisRegistros()
        {
            var userIdClaim = User.FindFirst("idUsuario")?.Value;
            if (userIdClaim == null) return Unauthorized(new { mensaje = "Token inválido" });
            int idUsuarioLogueado = int.Parse(userIdClaim);

            var ahora = DateTime.UtcNow;

            var crudos = await _context.ParticipantesConcursos
                .Where(pc => pc.idUsuario == idUsuarioLogueado && pc.estado == "Activo")
                .Select(pc => new
                {
                    pc.Concurso.idConcurso,
                    pc.Concurso.nombre,
                    pc.Concurso.descripcion,
                    pc.Concurso.codigo,
                    pc.Concurso.contrasena,
                    pc.Concurso.fechaInicio,
                    pc.Concurso.duracionMinutos,
                    pc.Concurso.minutosCongelamiento,
                    cantidadProblemas = pc.Concurso.Problemas.Count(p => p.estado == "Activo"),
                    cantidadParticipantes = pc.Concurso.Participantes.Count(p => p.estado == "Activo")
                })
                .ToListAsync();

            var resultado = crudos.Select(c =>
            {
                var fechaFin = c.fechaInicio.AddMinutes(c.duracionMinutos);
                string estadoTiempo = ahora < c.fechaInicio ? "Proximo"
                    : ahora < fechaFin ? "Activo" : "Finalizado";

                return new ConcursoListItemDto
                {
                    idConcurso = c.idConcurso,
                    nombre = c.nombre,
                    descripcion = c.descripcion,
                    codigo = c.codigo,
                    estadoTiempo = estadoTiempo,
                    modalidad = string.IsNullOrWhiteSpace(c.contrasena) ? "Publico" : "Privado",
                    fechaInicio = c.fechaInicio,
                    fechaFin = fechaFin,
                    duracionMinutos = c.duracionMinutos,
                    minutosCongelamiento = c.minutosCongelamiento,
                    cantidadProblemas = c.cantidadProblemas,
                    cantidadParticipantes = c.cantidadParticipantes,
                    yaInscrito = true,
                    segundosRestantes = estadoTiempo == "Activo"
                        ? (int)Math.Max(0, (fechaFin - ahora).TotalSeconds)
                        : null
                };
            }).ToList();

            if (resultado.Any(c => c.estadoTiempo == "Finalizado"))
            {
                var ids = resultado.Where(c => c.estadoTiempo == "Finalizado")
                    .Select(c => c.idConcurso).ToList();
                await CompletarDesempenoAsync(resultado, ids, idUsuarioLogueado);
            }

            return Ok(resultado);
        }

        private async Task CompletarDesempenoAsync(
            List<ConcursoListItemDto> items, List<int> idsConcursos, int idUsuario)
        {
            var resueltosPorConcurso = await _context.Envios
                .Where(e => e.idUsuario == idUsuario
                            && e.resultado == VeredictosEnvio.Aceptado
                            && idsConcursos.Contains(e.Problema.idConcurso))
                .Select(e => new { e.Problema.idConcurso, e.idProblema })
                .Distinct()
                .GroupBy(e => e.idConcurso)
                .Select(g => new { idConcurso = g.Key, cantidad = g.Count() })
                .ToDictionaryAsync(g => g.idConcurso, g => g.cantidad);

            foreach (var item in items)
            {
                if (resueltosPorConcurso.TryGetValue(item.idConcurso, out var cantidad))
                    item.miProblemasResueltos = cantidad;
            }
        }
        [Authorize(Roles = "Usuario")]
        [HttpGet("detalle/{codigo}")]
        public async Task<IActionResult> Detalle(string codigo)
        {
            var userIdClaim = User.FindFirst("idUsuario")?.Value;
            if (userIdClaim == null)
                return Unauthorized(new { mensaje = "Token inválido" });
            int idUsuarioLogueado = int.Parse(userIdClaim);

            codigo = codigo?.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(codigo))
                return BadRequest(new { mensaje = "El código del concurso es obligatorio." });

            var crudo = await _context.Concursos
                .Where(c => c.codigo == codigo && c.estado == "Activo")
                .Select(c => new
                {
                    c.idConcurso,
                    c.nombre,
                    c.descripcion,
                    c.codigo,
                    c.contrasena,
                    c.fechaInicio,
                    c.duracionMinutos,
                    c.minutosCongelamiento,
                    creador = c.Creador.nombre,
                    cantidadParticipantes = c.Participantes.Count(p => p.estado == "Activo"),
                    yaInscrito = c.Participantes.Any(p =>
                        p.idUsuario == idUsuarioLogueado && p.estado == "Activo"),
                    problemas = c.Problemas
                        .Where(p => p.estado == "Activo")
                        .OrderBy(p => p.inciso)
                        .Select(p => new ProblemaResumenDto
                        {
                            idProblema = p.idProblema,
                            inciso = p.inciso,
                            titulo = p.titulo,
                            tiempo = p.tiempo,
                            memoria = p.memoria
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (crudo == null)
                return NotFound(new { mensaje = "El concurso no existe o fue eliminado." });

            var ahora = DateTime.UtcNow;
            var fechaFin = crudo.fechaInicio.AddMinutes(crudo.duracionMinutos);

            string estadoTiempo;
            if (ahora < crudo.fechaInicio) estadoTiempo = "Proximo";
            else if (ahora < fechaFin) estadoTiempo = "Activo";
            else estadoTiempo = "Finalizado";

            bool esPrivado = !string.IsNullOrWhiteSpace(crudo.contrasena);
            bool mostrarProblemas = estadoTiempo != "Proximo"
                                     && (!esPrivado || crudo.yaInscrito);

            var dto = new ConcursoDetalleDto
            {
                idConcurso = crudo.idConcurso,
                nombre = crudo.nombre,
                descripcion = crudo.descripcion,
                codigo = crudo.codigo,
                creador = crudo.creador,
                estadoTiempo = estadoTiempo,
                modalidad = esPrivado ? "Privado" : "Publico",
                fechaInicio = crudo.fechaInicio,
                fechaFin = fechaFin,
                fechaCongelamiento = crudo.minutosCongelamiento > 0
                    ? fechaFin.AddMinutes(-crudo.minutosCongelamiento)
                    : null,
                duracionMinutos = crudo.duracionMinutos,
                minutosCongelamiento = crudo.minutosCongelamiento,
                cantidadParticipantes = crudo.cantidadParticipantes,
                yaInscrito = crudo.yaInscrito,
                segundosRestantes = estadoTiempo == "Activo"
                    ? (int)Math.Max(0, (fechaFin - ahora).TotalSeconds)
                    : null,
                problemas = mostrarProblemas ? crudo.problemas : new List<ProblemaResumenDto>()
            };

            return Ok(dto);
        }

        // Constantes de la clase
        private const int MAXIMO_PROBLEMAS_POR_CONCURSO = 12;
        private const long TAMANO_MAXIMO_POR_ARCHIVO_BYTES = 5 * 1024 * 1024;
        private const long TAMANO_MAXIMO_DESCOMPRIMIDO_TOTAL_BYTES = 600 * 1024 * 1024;
        private const int DURACION_MAXIMA_MINUTOS = 7 * 24 * 60; // 7 días
        private const int MESES_MAXIMOS_A_FUTURO = 12;

        private (bool, string) ValidarDatosConcurso(CrearConcursoDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.codigo))
                return (false, "El código del concurso es obligatorio.");

            var (codigoValido, mensajeCodigo) = ValidarFormatoCodigo(dto.codigo);
            if (!codigoValido)
                return (false, mensajeCodigo);

            if (string.IsNullOrWhiteSpace(dto.nombre))
                return (false, "El nombre del concurso es obligatorio.");

            if (string.IsNullOrWhiteSpace(dto.descripcion))
                return (false, "La descripción del concurso es obligatoria.");

            if (dto.duracionMinutos <= 0)
                return (false, "La duración del concurso debe ser mayor a 0 minutos.");

            if (dto.duracionMinutos > DURACION_MAXIMA_MINUTOS)
                return (false, $"La duración del concurso no puede superar los {DURACION_MAXIMA_MINUTOS / 60 / 24} días.");

            if (dto.minutosCongelamiento < 0)
                return (false, "Los minutos de congelamiento no pueden ser negativos.");

            if (dto.minutosCongelamiento >= dto.duracionMinutos)
                return (false, "Los minutos de congelamiento no pueden ser mayores o iguales a la duración del concurso.");

            if (dto.fechaInicio == default)
                return (false, "La fecha de inicio es obligatoria.");

            // dto.fechaInicio ya llega normalizado a UTC desde Crear()
            if (dto.fechaInicio <= DateTime.UtcNow)
                return (false, "La fecha de inicio debe ser posterior a la fecha actual.");

            if (dto.fechaInicio > DateTime.UtcNow.AddMonths(MESES_MAXIMOS_A_FUTURO))
                return (false, $"La fecha de inicio no puede programarse con más de {MESES_MAXIMOS_A_FUTURO} meses de anticipación.");

            if (dto.archivoZip == null || dto.archivoZip.Length == 0)
                return (false, "Debe adjuntar un archivo ZIP con los casos de prueba.");

            if (!dto.archivoZip.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                return (false, "El archivo adjunto debe tener extensión .zip.");

            const long limiteMB = 100;
            const long limiteBytes = limiteMB * 1024 * 1024;
            if (dto.archivoZip.Length > limiteBytes)
                return (false, $"El archivo ZIP no debe superar los {limiteMB} MB. Tamaño actual: {dto.archivoZip.Length / (1024.0 * 1024.0):F1} MB.");

            if (dto.listaProblemas == null || !dto.listaProblemas.Any())
                return (false, "Debe incluir al menos un problema.");

            if (dto.listaProblemas.Count > MAXIMO_PROBLEMAS_POR_CONCURSO)
                return (false, $"Un concurso no puede tener más de {MAXIMO_PROBLEMAS_POR_CONCURSO} problemas.");

            foreach (var p in dto.listaProblemas)
            {
                if (!char.IsLetter(p.inciso))
                    return (false, $"El inciso '{p.inciso}' no es una letra válida.");
                if (string.IsNullOrWhiteSpace(p.titulo))
                    return (false, $"El problema con inciso '{p.inciso}' debe tener un título.");
                if (p.tiempo <= 0)
                    return (false, $"El tiempo límite del problema '{p.inciso}' debe ser mayor a 0.");
                if (p.memoria <= 0)
                    return (false, $"La memoria límite del problema '{p.inciso}' debe ser mayor a 0.");
            }

            var incisos = dto.listaProblemas.Select(p => char.ToUpperInvariant(p.inciso)).ToList();
            if (incisos.Count != incisos.Distinct().Count())
                return (false, "Hay incisos duplicados en la lista de problemas.");

            return (true, string.Empty);
        }


        private static readonly string[] CategoriasValidas =
            { "div4", "div3", "div2", "div1", "prev", "icpc", "sp" };

        private (bool, string) ValidarFormatoCodigo(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo))
                return (false, "El código del concurso es obligatorio.");

            var partes = codigo.Split('-');

            bool formatoSimple = partes.Length == 2 && partes[0] == "upds"; // minúscula
            bool formatoConCategoria = partes.Length == 3 && partes[0] == "upds"
                                        && CategoriasValidas.Contains(partes[1]); // minúscula

            if (!formatoSimple && !formatoConCategoria)
                return (false, $"El código debe ser UPDS-001 o UPDS-CATEGORIA-001, " +
                                $"donde CATEGORIA es una de: {string.Join(", ", CategoriasValidas).ToUpperInvariant()}.");

            string numeroFinal = partes[^1];
            if (numeroFinal.Length != 3 || !numeroFinal.All(char.IsDigit))
                return (false, "El código debe terminar en 3 dígitos (ej: 001, 002).");

            return (true, string.Empty);
        }

        // =====================================================================
        // Devuelve Dictionary<string, List<string>> — solo nombres de ruta.
        // Nunca guardamos ZipArchiveEntry aquí porque quedaría ligado
        // a un ZipArchive que se cierra al salir de este método.
        // =====================================================================
        private async Task<(bool, string, Dictionary<string, List<string>>)> ValidarEstructuraZipAsync(
            IFormFile archivoZip, List<CrearProblemaDto> listaProblemas)
        {
            var incisosEsperados = listaProblemas
                .Select(p => p.inciso.ToString().ToUpperInvariant())
                .ToHashSet();

            return await Task.Run(() =>
            {
                var mapaCarpetas = new Dictionary<string, List<string>>();
                var nombresOuts = new List<string>();
                var carpetasEnZip = new HashSet<string>();
                long totalDescomprimido = 0;

                using var stream = archivoZip.OpenReadStream();
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

                foreach (var entrada in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entrada.Name)) continue;
                    if (entrada.FullName.Contains("__MACOSX", StringComparison.OrdinalIgnoreCase)) continue;
                    if (entrada.Name.StartsWith(".")) continue;

                    var partes = entrada.FullName.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                    if (partes.Length < 2) continue;

                    string carpeta = partes[partes.Length - 2].ToUpperInvariant();
                    if (carpeta.Length != 1 || !char.IsLetter(carpeta[0]))
                        continue;

                    carpetasEnZip.Add(carpeta);

                    bool esIn = entrada.Name.EndsWith(".in", StringComparison.OrdinalIgnoreCase);
                    bool esOut = entrada.Name.EndsWith(".out", StringComparison.OrdinalIgnoreCase);

                    if (esIn || esOut)
                    {
                        if (entrada.Length > TAMANO_MAXIMO_POR_ARCHIVO_BYTES)
                        {
                            return (false,
                                $"El archivo '{entrada.FullName}' pesa {entrada.Length / (1024.0 * 1024.0):F1} MB, " +
                                $"supera el máximo permitido de {TAMANO_MAXIMO_POR_ARCHIVO_BYTES / (1024 * 1024)} MB por archivo.",
                                mapaCarpetas);
                        }

                        totalDescomprimido += entrada.Length;

                        if (totalDescomprimido > TAMANO_MAXIMO_DESCOMPRIMIDO_TOTAL_BYTES)
                        {
                            return (false,
                                $"El contenido descomprimido del ZIP supera el máximo permitido de " +
                                $"{TAMANO_MAXIMO_DESCOMPRIMIDO_TOTAL_BYTES / (1024 * 1024)} MB.",
                                mapaCarpetas);
                        }
                    }

                    if (esIn)
                    {
                        if (!mapaCarpetas.ContainsKey(carpeta))
                            mapaCarpetas[carpeta] = new List<string>();
                        mapaCarpetas[carpeta].Add(entrada.FullName);
                    }
                    else if (esOut)
                    {
                        nombresOuts.Add(entrada.FullName);
                    }
                }

                var sobrantes = carpetasEnZip.Except(incisosEsperados).ToList();
                if (sobrantes.Any())
                    return (false, $"El ZIP tiene carpetas para incisos no declarados: {string.Join(", ", sobrantes)}", mapaCarpetas);

                var faltantes = incisosEsperados.Except(carpetasEnZip).ToList();
                if (faltantes.Any())
                    return (false, $"Faltan carpetas en el ZIP para los incisos: {string.Join(", ", faltantes)}", mapaCarpetas);

                foreach (var inciso in incisosEsperados)
                {
                    if (!mapaCarpetas.ContainsKey(inciso) || !mapaCarpetas[inciso].Any())
                        return (false, $"La carpeta del inciso '{inciso}' no contiene archivos .in", mapaCarpetas);
                }

                var todosLosIn = mapaCarpetas.Values
                    .SelectMany(lista => lista)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var rutaOut in nombresOuts)
                {
                    string rutaInEsperada = rutaOut.Substring(0, rutaOut.Length - 4) + ".in";

                    if (!todosLosIn.Contains(rutaInEsperada))
                        return (false, $"El archivo '{rutaOut}' no tiene su pareja .in correspondiente.", mapaCarpetas);
                }

                return (true, string.Empty, mapaCarpetas);
            });
        }

        // =====================================================================
        // Recibe los NOMBRES de las entradas .in, abre su propio ZipArchive
        // y busca las entradas dentro de ese archive recién abierto (vivo).
        // =====================================================================
        private async Task<(bool, string, Dictionary<string, List<(string In, string Out)>>)> ValidarCasosPruebaAsync(
            IFormFile archivoZip, Dictionary<string, List<string>> mapaCarpetas)
        {
            var resultado = new Dictionary<string, List<(string, string)>>();

            using var stream = archivoZip.OpenReadStream();
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            foreach (var (inciso, nombresIn) in mapaCarpetas)
            {
                var lista = new List<(string, string)>();

                foreach (var nombreIn in nombresIn)
                {
                    var entradaIn = archive.GetEntry(nombreIn);
                    if (entradaIn == null)
                        return (false, $"No se pudo volver a leer el archivo '{nombreIn}' del ZIP.", resultado);

                    string rutaOutEsperada = nombreIn.Substring(0, nombreIn.Length - 3) + ".out";

                    var entradaOut = archive.Entries.FirstOrDefault(e =>
                        e.FullName.Equals(rutaOutEsperada, StringComparison.OrdinalIgnoreCase));

                    if (entradaOut == null)
                        return (false, $"El archivo '{nombreIn}' no tiene su pareja .out correspondiente.", resultado);

                    string contenidoIn = await LeerContenidoAsync(entradaIn);
                    string contenidoOut = await LeerContenidoAsync(entradaOut);

                    if (string.IsNullOrWhiteSpace(contenidoIn))
                        return (false, $"El archivo '{nombreIn}' está vacío.", resultado);

                    if (string.IsNullOrWhiteSpace(contenidoOut))
                        return (false, $"El archivo '{entradaOut.FullName}' está vacío.", resultado);

                    lista.Add((contenidoIn, contenidoOut));
                }

                if (!lista.Any())
                    return (false, $"El inciso '{inciso}' no tiene casos de prueba válidos.", resultado);

                resultado[inciso] = lista;
            }

            return (true, string.Empty, resultado);
        }


        [Authorize(Roles = "AdministradorConcursos")]
        [HttpGet("mis-creados")]
        public async Task<IActionResult> MisCreados(
            [FromQuery] string filtro = "todos",
            [FromQuery] string? modalidad = null,
            [FromQuery] string? busqueda = null,
            [FromQuery] int pagina = 1,
            [FromQuery] int tamanoPagina = 20)
        {
            var userIdClaim = User.FindFirst("idUsuario")?.Value;
            if (userIdClaim == null)
                return Unauthorized(new { mensaje = "Token inválido" });
            int idUsuarioLogueado = int.Parse(userIdClaim);

            if (pagina < 1) pagina = 1;
            if (tamanoPagina < 1 || tamanoPagina > 50) tamanoPagina = 20;

            var ahora = DateTime.UtcNow;

            // Base: solo concursos no borrados, creados por este usuario
            IQueryable<Concurso> queryBase = _context.Concursos
                .Where(c => c.estado == "Activo" && c.idUsuarioCreador == idUsuarioLogueado);

            if (!string.IsNullOrWhiteSpace(busqueda))
                queryBase = queryBase.Where(c => EF.Functions.ILike(c.codigo, $"%{busqueda}%"));

            if (!string.IsNullOrWhiteSpace(modalidad))
            {
                bool quierePrivados = modalidad.Equals("privado", StringComparison.OrdinalIgnoreCase);
                bool quierePublicos = modalidad.Equals("publico", StringComparison.OrdinalIgnoreCase);

                if (quierePrivados)
                    queryBase = queryBase.Where(c => c.contrasena != null && c.contrasena != "");
                else if (quierePublicos)
                    queryBase = queryBase.Where(c => c.contrasena == null || c.contrasena == "");
            }

            // El resumen de conteos se calcula sobre queryBase (respeta busqueda/modalidad,
            // pero NO el filtro de pestaña — así el usuario ve "cuántos hay en cada estado"
            // sin importar en cuál pestaña esté parado ahora mismo)
            var resumen = new ResumenConteoDto
            {
                activos = await queryBase.CountAsync(c =>
                    ahora >= c.fechaInicio && ahora < c.fechaInicio.AddMinutes(c.duracionMinutos)),
                proximos = await queryBase.CountAsync(c => ahora < c.fechaInicio),
                finalizados = await queryBase.CountAsync(c =>
                    ahora >= c.fechaInicio.AddMinutes(c.duracionMinutos))
            };

            // A partir de aquí sí aplicamos el filtro de pestaña, para la lista paginada
            IQueryable<Concurso> query = filtro?.ToLowerInvariant() switch
            {
                "activos" => queryBase.Where(c =>
                    ahora >= c.fechaInicio && ahora < c.fechaInicio.AddMinutes(c.duracionMinutos)),
                "proximos" => queryBase.Where(c => ahora < c.fechaInicio),
                "finalizados" => queryBase.Where(c =>
                    ahora >= c.fechaInicio.AddMinutes(c.duracionMinutos)),
                _ => queryBase
            };

            var total = await query.CountAsync();

            query = query
                .OrderBy(c => ahora < c.fechaInicio ? 1
                            : (ahora < c.fechaInicio.AddMinutes(c.duracionMinutos) ? 0 : 2))
                .ThenBy(c => c.fechaInicio);

            var crudos = await query
                .Skip((pagina - 1) * tamanoPagina)
                .Take(tamanoPagina)
                .Select(c => new
                {
                    c.codigo,
                    c.nombre,
                    c.contrasena,
                    c.fechaInicio,
                    c.duracionMinutos,
                    cantidadProblemas = c.Problemas.Count(p => p.estado == "Activo"),
                    cantidadParticipantes = c.Participantes.Count(pc => pc.estado == "Activo")
                })
                .ToListAsync();

            var paginaDatos = crudos.Select(c =>
            {
                var fechaFin = c.fechaInicio.AddMinutes(c.duracionMinutos);
                string estadoTiempo = ahora < c.fechaInicio ? "Proximo"
                    : ahora < fechaFin ? "Activo" : "Finalizado";

                return new ConcursoAdminItemDto
                {
                    codigo = c.codigo,
                    nombre = c.nombre,
                    estadoTiempo = estadoTiempo,
                    modalidad = string.IsNullOrWhiteSpace(c.contrasena) ? "Publico" : "Privado",
                    fechaInicio = c.fechaInicio,
                    duracionMinutos = c.duracionMinutos,
                    cantidadProblemas = c.cantidadProblemas,
                    cantidadParticipantes = c.cantidadParticipantes
                };
            }).ToList();

            return Ok(new ConcursosAdminPaginadosDto
            {
                total = total,
                pagina = pagina,
                tamanoPagina = tamanoPagina,
                resumen = resumen,
                concursos = paginaDatos
            });
        }
        private static async Task<string> LeerContenidoAsync(ZipArchiveEntry entrada)
        {
            using var reader = new StreamReader(entrada.Open());
            return await reader.ReadToEndAsync();
        }
        [Authorize(Roles = "Usuario")]
        [HttpGet("dashboard/{codigo}")]
        public async Task<IActionResult> Dashboard(string codigo)
        {
            var userIdClaim = User.FindFirst("idUsuario")?.Value;
            if (userIdClaim == null)
                return Unauthorized(new { mensaje = "Token inválido" });
            int idUsuarioLogueado = int.Parse(userIdClaim);

            codigo = codigo?.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(codigo))
                return BadRequest(new { mensaje = "El código del concurso es obligatorio." });

            var crudo = await _context.Concursos
                .Where(c => c.codigo == codigo && c.estado == "Activo")
                .Select(c => new
                {
                    c.idConcurso,
                    c.codigo,
                    c.nombre,
                    c.contrasena,
                    c.fechaInicio,
                    c.duracionMinutos,
                    c.minutosCongelamiento,
                    c.urlSetProblemas,
                    c.idUsuarioCreador,
                    cantidadParticipantes = c.Participantes.Count(p => p.estado == "Activo"),
                    yaInscrito = c.Participantes.Any(p =>
                        p.idUsuario == idUsuarioLogueado && p.estado == "Activo"),
                    problemas = c.Problemas
                        .Where(p => p.estado == "Activo")
                        .OrderBy(p => p.inciso)
                        .Select(p => new { p.idProblema, p.inciso, p.titulo, p.tiempo, p.memoria })
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (crudo == null)
                return NotFound(new { mensaje = "El concurso no existe o fue eliminado." });

            var ahora = DateTime.UtcNow;
            var fechaFin = crudo.fechaInicio.AddMinutes(crudo.duracionMinutos);

            string estadoTiempo = ahora < crudo.fechaInicio ? "Proximo"
                : ahora < fechaFin ? "Activo" : "Finalizado";

            bool esPrivado = !string.IsNullOrWhiteSpace(crudo.contrasena);
            bool esCreador = crudo.idUsuarioCreador == idUsuarioLogueado;

            bool puedeVer = estadoTiempo != "Proximo" && (!esPrivado || crudo.yaInscrito || esCreador);
            if (!puedeVer)
                return BadRequest(new { mensaje = "Todavía no puedes ver el dashboard de este concurso." });

            var idsProblemas = crudo.problemas.Select(p => p.idProblema).ToList();

            var envios = await _context.Envios
                .Where(e => e.idUsuario == idUsuarioLogueado && idsProblemas.Contains(e.idProblema))
                .OrderByDescending(e => e.fechaEnvio)
                .Select(e => new { e.idProblema, e.resultado, e.fechaEnvio })
                .ToListAsync();

            var enviosPorProblema = envios
                .GroupBy(e => e.idProblema)
                .ToDictionary(g => g.Key, g => g.ToList());

            var problemasDto = crudo.problemas.Select(p =>
            {
                bool tieneEnvios = enviosPorProblema.TryGetValue(p.idProblema, out var listaEnvios);
                int intentos = tieneEnvios ? listaEnvios!.Count : 0;

                bool aceptado = tieneEnvios && listaEnvios!.Any(e => e.resultado == VeredictosEnvio.Aceptado);

                string estado = intentos == 0
                    ? "Sin intentar"
                    : aceptado
                        ? VeredictosEnvio.Aceptado
                        : listaEnvios![0].resultado; // el más reciente, ya viene ordenado desc

                return new ProblemaDashboardItemDto
                {
                    inciso = p.inciso,
                    titulo = p.titulo,
                    tiempo = p.tiempo,
                    memoria = p.memoria,
                    intentos = intentos,
                    estado = estado,
                    resuelto = aceptado
                };
            }).ToList();

            return Ok(new ConcursoDashboardDto
            {
                codigo = crudo.codigo,
                nombre = crudo.nombre,
                estadoTiempo = estadoTiempo,
                cantidadParticipantes = crudo.cantidadParticipantes,
                fechaFin = fechaFin,
                minutosCongelamiento = crudo.minutosCongelamiento,
                segundosRestantes = estadoTiempo == "Activo"
                    ? (int)Math.Max(0, (fechaFin - ahora).TotalSeconds)
                    : null,
                urlSetProblemas = crudo.urlSetProblemas,
                problemasResueltos = problemasDto.Count(p => p.resuelto),
                totalProblemas = problemasDto.Count,
                intentosTotales = problemasDto.Sum(p => p.intentos),
                problemas = problemasDto
            });
        }
    }

}