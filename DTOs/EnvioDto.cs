using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UPDSjudgeB.DTOs
{

    public class CrearEnvioDto
    {
        public int idProblema { get; set; }
        public string extension { get; set; }      // ".py", ".cpp", ".cs"
        public string codigoFuente { get; set; }
        public string? contrasena { get; set; }     // solo si es upsolving en un concurso privado sin inscripción previa
    }
}