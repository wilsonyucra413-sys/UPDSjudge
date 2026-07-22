using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UPDSjudgeB.DTOs
{

    public class CrearEnvioDto
    {
        public string codigo { get; set; }
        public char inciso { get; set; }
        public string extension { get; set; }
        public string codigoFuente { get; set; }
        public string? contrasena { get; set; }
    }
}