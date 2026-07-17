using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace UPDSjudgeB.DTOs
{

    public class CrearProblemaDto
    {
        public char inciso { get; set; }
        public string titulo { get; set; }
        public float tiempo { get; set; }
        public int memoria { get; set; }
    }

    public class CrearConcursoDto
    {
        public string nombre { get; set; }
        public string descripcion { get; set; }
        public DateTime fechaInicio { get; set; }
        public int duracionMinutos { get; set; }
        public string contrasena { get; set; } = string.Empty;
        public string urlSetProblemas { get; set; }
        public int minutosCongelamiento { get; set; }
        public string codigo { get; set; }
        // Recibiremos la lista de problemas como un JSON String o campos individuales
        // pero para procesar el ZIP, necesitamos que el nombre del problema 
        // coincida con las carpetas A, B, C...
        public List<CrearProblemaDto> listaProblemas { get; set; }

        // El archivo ZIP
        public IFormFile archivoZip { get; set; }
    }
    public class ConcursoListItemDto
    {
        public int idConcurso { get; set; }
        public string nombre { get; set; }
        public string descripcion { get; set; }
        public string codigo { get; set; }

        // Calculados, no vienen directo de la BD
        public string estadoTiempo { get; set; }   // "Proximo" | "Activo" | "Finalizado"
        public string modalidad { get; set; }       // "Publico" | "Privado"

        public DateTime fechaInicio { get; set; }
        public DateTime fechaFin { get; set; }
        public DateTime? fechaCongelamiento { get; set; }
        public int duracionMinutos { get; set; }
        public int minutosCongelamiento { get; set; }

        public int cantidadProblemas { get; set; }
        public int cantidadParticipantes { get; set; }
        public bool yaInscrito { get; set; }

        // Solo tiene valor si estadoTiempo == "Activo"
        public int? segundosRestantes { get; set; }

        // Solo tienen valor si estadoTiempo == "Finalizado" y el usuario participó
        public int? miPuesto { get; set; }
        public int? miProblemasResueltos { get; set; }
    }

    public class ConcursosPaginadosDto
    {
        public int total { get; set; }
        public int pagina { get; set; }
        public int tamanoPagina { get; set; }
        public List<ConcursoListItemDto> concursos { get; set; }
    }
    public class ConcursoDetalleDto
    {
        public int idConcurso { get; set; }
        public string nombre { get; set; }
        public string descripcion { get; set; }
        public string codigo { get; set; }
        public string creador { get; set; }

        public string estadoTiempo { get; set; }   // "Proximo" | "Activo" | "Finalizado"
        public string modalidad { get; set; }       // "Publico" | "Privado"

        public DateTime fechaInicio { get; set; }
        public DateTime fechaFin { get; set; }
        public DateTime? fechaCongelamiento { get; set; }
        public int duracionMinutos { get; set; }
        public int minutosCongelamiento { get; set; }

        public int cantidadParticipantes { get; set; }
        public bool yaInscrito { get; set; }
        public int? segundosRestantes { get; set; } // solo si estadoTiempo == "Activo"

        // Solo se llena si el concurso ya inició (o finalizó) y, si es privado,
        // el usuario está inscrito. Antes de eso, viene vacío.
        public List<ProblemaResumenDto> problemas { get; set; } = new();
    }

    public class ProblemaResumenDto
    {
        public int idProblema { get; set; }
        public char inciso { get; set; }
        public string titulo { get; set; }
        public float tiempo { get; set; }
        public int memoria { get; set; }
    }
    public class UnirseConcursoDto
    {
        public string codigo { get; set; }
        public string? contrasena { get; set; }
    }
}