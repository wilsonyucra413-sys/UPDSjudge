using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UPDSjudgeB.DTOs
{
    public class UsuarioBusquedaDto
    {
        public int idUsuario { get; set; }
        public string nombre { get; set; }
        public string correo { get; set; }
        public string estado { get; set; }
        public List<string> roles { get; set; } = new();
    }

    public class CambiarRolDto
    {
        public string correo { get; set; }
        public int idRol { get; set; }
    }
}