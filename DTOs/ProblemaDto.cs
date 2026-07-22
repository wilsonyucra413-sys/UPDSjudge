using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UPDSjudgeB.DTOs
{
    public class ProblemaDto
    {
        public class ProblemaDashboardItemDto
        {
            public char inciso { get; set; }
            public string titulo { get; set; }
            public float tiempo { get; set; }
            public int memoria { get; set; }
            public int intentos { get; set; }
            public string estado { get; set; } // "Sin intentar" | "Aceptado" | "Respuesta incorrecta"
            public bool resuelto { get; set; }
        }

        public class ConcursoDashboardDto
        {
            public string codigo { get; set; }
            public string nombre { get; set; }
            public string estadoTiempo { get; set; }
            public int cantidadParticipantes { get; set; }
            public DateTime fechaFin { get; set; }              // <-- agregado lo calcula frontend
            public int minutosCongelamiento { get; set; }        // <-- agregado lo calcula frontend
            public int? segundosRestantes { get; set; }
            public string urlSetProblemas { get; set; }
            public int problemasResueltos { get; set; }
            public int totalProblemas { get; set; }
            public int intentosTotales { get; set; }
            public List<ProblemaDashboardItemDto> problemas { get; set; } = new();
        }
    }
    public static class VeredictosEnvio
    {
        public const string Aceptado = "Accepted";
        public const string RespuestaIncorrecta = "Wrong Answer";
        public const string ErrorCompilacion = "Compilation Error";
        public const string ErrorEjecucion = "Runtime Error";
        public const string TiempoExcedido = "Time Limit Exceeded";
        public const string MemoriaExcedida = "Memory Limit Exceeded";
    }
}