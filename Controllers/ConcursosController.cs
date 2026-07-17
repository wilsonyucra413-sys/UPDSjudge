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
        public async Task<IActionResult> Crear([FromForm] CrearConcursoDto dto)
        {
            var userIdClaim = User.FindFirst("idUsuario")?.Value;
            if (userIdClaim == null)
                return Unauthorized(new { mensaje = "Token inválido" });
            int idUsuarioLogueado = int.Parse(userIdClaim);

            var (dtoValido, mensajeDto) = ValidarDatosConcurso(dto);
            if (!dtoValido)
                return BadRequest(new { mensaje = mensajeDto });

            // Validamos duplicado por nombre, ya no por código
            // (el código ahora lo genera el servidor, el cliente ya no lo manda)
            if (await _context.Concursos.AnyAsync(c => c.codigo == dto.codigo && c.estado == "Activo"))
                return BadRequest(new { mensaje = "Ya existe un concurso activo con ese codigo." });

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
                    codigo = nuevoConcurso.codigo, // <-- esto es lo que usa el frontend para todo lo demás
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
        [FromQuery] string? busqueda = null,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = 9)
        {
            var userIdClaim = User.FindFirst("idUsuario")?.Value;
            int idUsuarioLogueado = userIdClaim != null ? int.Parse(userIdClaim) : 0;

            if (pagina < 1) pagina = 1;
            if (tamanoPagina < 1 || tamanoPagina > 50) tamanoPagina = 9;

            var ahora = DateTime.UtcNow; // OJO: fechaInicio debe guardarse también en UTC

            var query = _context.Concursos
                .Where(c => c.estado == "Activo"); // solo no borrados lógicamente

            if (!string.IsNullOrWhiteSpace(busqueda))
                query = query.Where(c => c.nombre.Contains(busqueda));

            // Proyectamos solo lo crudo que necesitamos. El cálculo de estadoTiempo
            // depende de sumar minutos a una fecha, lo cual no todos los providers de
            // EF Core traducen igual a SQL, así que lo resolvemos en memoria después.
            var crudos = await query
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

            var procesados = crudos.Select(c =>
            {
                var fechaFin = c.fechaInicio.AddMinutes(c.duracionMinutos);

                string estadoTiempo;
                if (ahora < c.fechaInicio) estadoTiempo = "Proximo";
                else if (ahora < fechaFin) estadoTiempo = "Activo";
                else estadoTiempo = "Finalizado";

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
            });

            // Filtro por pestaña (Todos / Activos / Próximos / Finalizados)
            procesados = filtro?.ToLowerInvariant() switch
            {
                "activos" => procesados.Where(c => c.estadoTiempo == "Activo"),
                "proximos" => procesados.Where(c => c.estadoTiempo == "Proximo"),
                "finalizados" => procesados.Where(c => c.estadoTiempo == "Finalizado"),
                _ => procesados
            };

            var listaCompleta = procesados
                .OrderBy(c => c.estadoTiempo == "Activo" ? 0 : c.estadoTiempo == "Proximo" ? 1 : 2)
                .ThenBy(c => c.fechaInicio)
                .ToList();

            var total = listaCompleta.Count;
            var paginaDatos = listaCompleta
                .Skip((pagina - 1) * tamanoPagina)
                .Take(tamanoPagina)
                .ToList();
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
                .Where(c => c.idConcurso != 0)
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
        [Authorize(Roles = "Usuario")]
        [HttpGet("detalle/{codigo}")]
        public async Task<IActionResult> Detalle(string codigo)
        {
            var userIdClaim = User.FindFirst("idUsuario")?.Value;
            if (userIdClaim == null)
                return Unauthorized(new { mensaje = "Token inválido" });
            int idUsuarioLogueado = int.Parse(userIdClaim);

            if (string.IsNullOrWhiteSpace(codigo))
                return BadRequest(new { mensaje = "El código del concurso es obligatorio." });

            // Verificación por código, no por id, como pediste
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
        private async Task CompletarDesempenoAsync(
            List<ConcursoListItemDto> items, List<int> idsConcursos, int idUsuario)
        {
            var resueltosPorConcurso = await _context.Envios
                .Where(e => e.idUsuario == idUsuario
                            && e.resultado == "Aceptado"
                            && idsConcursos.Contains(e.Problema.idConcurso))
                .Select(e => new { e.Problema.idConcurso, e.idProblema })
                .Distinct()
                .GroupBy(e => e.idConcurso)
                .Select(g => new { idConcurso = g.Key, cantidad = g.Count() })
                .ToListAsync();

            foreach (var item in items)
            {
                var match = resueltosPorConcurso.FirstOrDefault(r => r.idConcurso == item.idConcurso);
                if (match != null)
                    item.miProblemasResueltos = match.cantidad;
            }
        }

        private (bool, string) ValidarDatosConcurso(CrearConcursoDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.nombre))
                return (false, "El nombre del concurso es obligatorio.");
            if (string.IsNullOrWhiteSpace(dto.descripcion))
                return (false, "La descripción del concurso es obligatoria.");
            if (dto.duracionMinutos <= 0)
                return (false, "La duración del concurso debe ser mayor a 0 minutos.");
            if (dto.minutosCongelamiento < 0)
                return (false, "Los minutos de congelamiento no pueden ser negativos.");
            if (dto.minutosCongelamiento >= dto.duracionMinutos)
                return (false, "Los minutos de congelamiento no pueden ser mayores o iguales a la duración del concurso.");
            if (dto.fechaInicio == default)
                return (false, "La fecha de inicio es obligatoria.");
            if (dto.fechaInicio <= DateTime.Now)
                return (false, "La fecha de inicio debe ser posterior a la fecha actual.");
            if (dto.archivoZip == null || dto.archivoZip.Length == 0)
                return (false, "Debe adjuntar un archivo ZIP con los casos de prueba.");
            if (!dto.archivoZip.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                return (false, "El archivo adjunto debe tener extensión .zip.");
            if (dto.listaProblemas == null || !dto.listaProblemas.Any())
                return (false, "Debe incluir al menos un problema.");

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

        // =====================================================================
        // Ahora devuelve Dictionary<string, List<string>> — solo nombres de ruta.
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
                var carpetasEnZip = new HashSet<string>();

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

                    if (entrada.Name.EndsWith(".in", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!mapaCarpetas.ContainsKey(carpeta))
                            mapaCarpetas[carpeta] = new List<string>();
                        mapaCarpetas[carpeta].Add(entrada.FullName); // solo el nombre
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
        [Authorize(Roles = "Usuario")]
        [HttpPost("unirse")]
        public async Task<IActionResult> Unirse([FromBody] UnirseConcursoDto dto)
        {
            var userIdClaim = User.FindFirst("idUsuario")?.Value;
            if (userIdClaim == null)
                return Unauthorized(new { mensaje = "Token inválido" });
            int idUsuarioLogueado = int.Parse(userIdClaim);

            if (string.IsNullOrWhiteSpace(dto.codigo))
                return BadRequest(new { mensaje = "El código del concurso es obligatorio." });

            var concurso = await _context.Concursos
                .FirstOrDefaultAsync(c => c.codigo == dto.codigo && c.estado == "Activo");

            if (concurso == null)
                return NotFound(new { mensaje = "El concurso no existe o fue eliminado." });

            var ahora = DateTime.UtcNow;
            var fechaFin = concurso.fechaInicio.AddMinutes(concurso.duracionMinutos);

            // No dejamos inscribirse a un concurso que ya terminó
            if (ahora >= fechaFin)
                return BadRequest(new { mensaje = "No puedes inscribirte a un concurso que ya finalizó." });

            // Validación de contraseña solo si el concurso es privado
            bool esPrivado = !string.IsNullOrWhiteSpace(concurso.contrasena);
            if (esPrivado)
            {
                if (string.IsNullOrWhiteSpace(dto.contrasena))
                    return BadRequest(new { mensaje = "Este concurso es privado, debes ingresar la contraseña." });

                // Comparación en texto plano, como acordamos
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

                try
                {
                    await _context.SaveChangesAsync();
                    return Ok(new { mensaje = "Te has vuelto a inscribir al concurso.", codConcurso = concurso.codigo });
                }
                catch (DbUpdateException ex)
                {
                    return BadRequest(new { mensaje = "No se pudo procesar la inscripción.", detalle = ex.Message });
                }
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
        private static async Task<string> LeerContenidoAsync(ZipArchiveEntry entrada)
        {
            using var reader = new StreamReader(entrada.Open());
            return await reader.ReadToEndAsync();
        }
    }

}