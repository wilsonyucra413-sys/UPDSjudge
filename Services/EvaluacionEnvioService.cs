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

        // Status IDs de Judge0 que representan un problema de compilación/ejecución,
        // sin necesidad de comparar stdout
        private const int JUDGE0_STATUS_COMPILATION_ERROR = 6;
        private const int JUDGE0_STATUS_TIME_LIMIT_EXCEEDED = 5;
        // 7 al 12 son variantes de Runtime Error

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

            int memoryLimitKb = problema.memoria * 1024; // asumiendo problema.memoria en MB

            string veredictoFinal = VeredictosEnvio.Aceptado;
            float tiempoMaximo = 0;
            int memoriaMaxima = 0;
            string? ultimoToken = null;
            string? detalleFallo = null;

            // Fail-fast: se detiene en el primer caso que no pase
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
                    // Si Judge0 no responde, no debe guardarse un veredicto falso.
                    // Se relanza para que el controller devuelva un 503 explícito.
                    throw;
                }

                ultimoToken = resultado.Token;

                float tiempoCaso = float.TryParse(resultado.Time, out var t) ? t : 0;
                int memoriaCaso = resultado.Memory ?? 0;

                tiempoMaximo = Math.Max(tiempoMaximo, tiempoCaso);
                memoriaMaxima = Math.Max(memoriaMaxima, memoriaCaso);

                // 1. Error de compilación
                if (!string.IsNullOrWhiteSpace(resultado.CompileOutput))
                {
                    veredictoFinal = VeredictosEnvio.ErrorCompilacion;
                    detalleFallo = resultado.CompileOutput;
                    break;
                }

                // 2. Time Limit Exceeded reportado directo por Judge0
                if (resultado.Status.Id == JUDGE0_STATUS_TIME_LIMIT_EXCEEDED)
                {
                    veredictoFinal = VeredictosEnvio.TiempoExcedido;
                    detalleFallo = $"Caso #{caso.idCasoPrueba}: tiempo excedido.";
                    break;
                }

                // 3. Runtime Error (statuses 7 al 12 en Judge0)
                if (resultado.Status.Id >= 7 && resultado.Status.Id <= 12)
                {
                    veredictoFinal = VeredictosEnvio.ErrorEjecucion;
                    detalleFallo = resultado.Stderr ?? resultado.Message ?? "Error en tiempo de ejecución.";
                    break;
                }

                // 4. Memory Limit Exceeded — Judge0 CE normalmente lo reporta como
                // Runtime Error (SIGSEGV/SIGABRT) más que con un status propio,
                // así que se detecta comparando memoria usada vs límite del problema.
                if (memoriaCaso > memoryLimitKb)
                {
                    veredictoFinal = VeredictosEnvio.MemoriaExcedida;
                    detalleFallo = $"Caso #{caso.idCasoPrueba}: memoria excedida.";
                    break;
                }

                // 5. Comparación real de salida — NUNCA confiar solo en status "Accepted"
                string salidaObtenida = (resultado.Stdout ?? string.Empty).Trim();
                string salidaEsperada = (caso.salida ?? string.Empty).Trim();

                if (!string.Equals(salidaObtenida, salidaEsperada, StringComparison.Ordinal))
                {
                    veredictoFinal = VeredictosEnvio.RespuestaIncorrecta;
                    detalleFallo = $"Caso #{caso.idCasoPrueba}: salida no coincide.";
                    break;
                }

                // Este caso pasó, sigue al siguiente. Si todos pasan, queda "Aceptado".
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