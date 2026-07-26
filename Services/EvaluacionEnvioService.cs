using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UPDSjudgeB.data;
using UPDSjudgeB.DTOs;
using UPDSjudgeB.Models;
using static UPDSjudgeB.DTOs.Judge0Dto;

namespace UPDSjudgeB.Services
{
    public class EvaluacionEnvioService : IEvaluacionEnvioService
    {
        private readonly ApplicationDbContext _context;
        private readonly IJudge0Service _judge0;

        // Status IDs de Judge0 CE v1.13.1
        private const int JUDGE0_STATUS_ACCEPTED = 3;
        private const int JUDGE0_STATUS_WRONG_ANSWER = 4;
        private const int JUDGE0_STATUS_TIME_LIMIT_EXCEEDED = 5;
        private const int JUDGE0_STATUS_COMPILATION_ERROR = 6;
        // 7 al 12 = variantes de Runtime Error (incluye 11 = NZEC)
        private const int JUDGE0_STATUS_RUNTIME_ERROR_MIN = 7;
        private const int JUDGE0_STATUS_RUNTIME_ERROR_MAX = 12;

        public EvaluacionEnvioService(ApplicationDbContext context, IJudge0Service judge0)
        {
            _context = context;
            _judge0 = judge0;
        }

        public async Task<EnvioResultadoDto> EvaluarYGuardarAsync(
            int idProblema, int idLenguaje, string codigoFuente,
            int idUsuario, bool esUpsolving)
        {
            var problema = await _context.Problemas
                .FirstOrDefaultAsync(p => p.idProblema == idProblema && p.estado == "Activo");

            if (problema == null)
                throw new InvalidOperationException("El problema no existe o fue eliminado.");

            var lenguaje = await _context.Lenguajes
                .FirstOrDefaultAsync(l => l.idLenguaje == idLenguaje && l.estado == "Activo");

            if (lenguaje == null)
                throw new InvalidOperationException("El lenguaje indicado no está soportado.");

            var casosPrueba = await _context.CasosPrueba
                .Where(c => c.idProblema == idProblema && c.estado == "Activo")
                .OrderBy(c => c.idCasoPrueba)
                .ToListAsync();

            if (!casosPrueba.Any())
                throw new InvalidOperationException("Este problema no tiene casos de prueba configurados.");

            int memoryLimitKb = problema.memoria * 1024;

            string veredictoFinal = VeredictosEnvio.Aceptado;
            float tiempoMaximo = 0;
            int memoriaMaxima = 0;
            string? ultimoToken = null;
            string? detalleFallo = null;

            foreach (var caso in casosPrueba)
            {
                Judge0SubmissionResponseDto resultado;
                try
                {
                    resultado = await _judge0.EjecutarAsync(
                        lenguaje.idJudge0, codigoFuente, caso.entrada,
                        problema.tiempo, memoryLimitKb);
                }
                catch (Judge0NoDisponibleException)
                {
                    throw;
                }

                ultimoToken = resultado.Token;

                float tiempoCaso = float.TryParse(resultado.Time, out var t) ? t : 0;
                int memoriaCaso = resultado.Memory ?? 0;

                tiempoMaximo = Math.Max(tiempoMaximo, tiempoCaso);
                memoriaMaxima = Math.Max(memoriaMaxima, memoriaCaso);

                int statusId = resultado.Status?.Id ?? -1;

                // 1. Compilation Error — SOLO por status.id == 6, nunca por compile_output
                //    (GCC puede meter warnings en compile_output aunque el código compile bien)
                if (statusId == JUDGE0_STATUS_COMPILATION_ERROR)
                {
                    veredictoFinal = VeredictosEnvio.ErrorCompilacion;
                    detalleFallo = resultado.CompileOutput ?? "Error de compilación.";
                    break;
                }

                // 2. Time Limit Exceeded
                if (statusId == JUDGE0_STATUS_TIME_LIMIT_EXCEEDED)
                {
                    veredictoFinal = VeredictosEnvio.TiempoExcedido;
                    detalleFallo = $"Caso #{caso.idCasoPrueba}: tiempo excedido.";
                    break;
                }

                // 3. Runtime Error (7-12, incluye NZEC=11, bad_alloc cae aquí también)
                if (statusId >= JUDGE0_STATUS_RUNTIME_ERROR_MIN && statusId <= JUDGE0_STATUS_RUNTIME_ERROR_MAX)
                {
                    veredictoFinal = VeredictosEnvio.ErrorEjecucion;
                    detalleFallo = resultado.Stderr ?? resultado.Message ?? "Error en tiempo de ejecución.";
                    break;
                }

                // 4. Memory Limit Exceeded — SIEMPRE se revisa por comparación real,
                //    sin importar qué status haya devuelto Judge0. Esto cubre el caso
                //    donde el proceso sí terminó dentro del status "Accepted" o similar
                //    pero excedió memoria de forma silenciosa.
                if (memoriaCaso > memoryLimitKb)
                {
                    veredictoFinal = VeredictosEnvio.MemoriaExcedida;
                    detalleFallo = $"Caso #{caso.idCasoPrueba}: memoria excedida ({memoriaCaso} KB > {memoryLimitKb} KB).";
                    break;
                }

                // 5. Ejecución correcta (status 3) -> comparar salida real
                if (statusId == JUDGE0_STATUS_ACCEPTED)
                {
                    string salidaObtenida = (resultado.Stdout ?? string.Empty).TrimEnd('\n', '\r').Trim();
                    string salidaEsperada = (caso.salida ?? string.Empty).TrimEnd('\n', '\r').Trim();

                    if (!string.Equals(salidaObtenida, salidaEsperada, StringComparison.Ordinal))
                    {
                        veredictoFinal = VeredictosEnvio.RespuestaIncorrecta;
                        detalleFallo = $"Caso #{caso.idCasoPrueba}: salida no coincide.";
                        break;
                    }
                    // Este caso pasó, continúa al siguiente
                    continue;
                }

                // 6. status.id == 4 (Wrong Answer directo de Judge0) u otros no contemplados
                if (statusId == JUDGE0_STATUS_WRONG_ANSWER)
                {
                    veredictoFinal = VeredictosEnvio.RespuestaIncorrecta;
                    detalleFallo = $"Caso #{caso.idCasoPrueba}: respuesta incorrecta.";
                    break;
                }

                // 7. Cualquier otro status.id no contemplado explícitamente (13 Internal Error,
                //    14 Exec Format Error, o -1 si no vino status) — no asumimos Aceptado
                veredictoFinal = VeredictosEnvio.ErrorEjecucion;
                detalleFallo = $"Caso #{caso.idCasoPrueba}: estado inesperado de Judge0 (id={statusId}, \"{resultado.Status?.Description}\").";
                break;
            }

            var nuevoEnvio = new Envio
            {
                codigo = codigoFuente,
                resultado = veredictoFinal,
                tiempo = tiempoMaximo,
                memoria = memoriaMaxima,
                token = ultimoToken,
                upsolving = esUpsolving ? "Si" : "No",
                fechaEnvio = DateTime.UtcNow,
                idUsuario = idUsuario,
                idProblema = idProblema,
                idLenguaje = idLenguaje
            };

            _context.Envios.Add(nuevoEnvio);
            await _context.SaveChangesAsync();

            return new EnvioResultadoDto
            {
                idEnvio = nuevoEnvio.idEnvio,
                veredicto = veredictoFinal,
                tiempo = tiempoMaximo,
                memoria = memoriaMaxima,
                upsolving = esUpsolving,
                detalle = veredictoFinal == VeredictosEnvio.Aceptado ? null : detalleFallo
            };
        }
    }
}