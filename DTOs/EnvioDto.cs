using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UPDSjudgeB.DTOs
{

    public class CrearEnvioDto
    {
        public string codigoConcurso { get; set; }
        public char incisoProblema { get; set; }
        public int idLenguaje { get; set; }
        public string codigoFuente { get; set; }
        public string? contrasena { get; set; } 
    }

    public class EnvioResultadoDto
    {
        public int idEnvio { get; set; }
        public string veredicto { get; set; }
        public float tiempo { get; set; }
        public int memoria { get; set; }
        public bool upsolving { get; set; }
        public string? detalle { get; set; } 
    }
}