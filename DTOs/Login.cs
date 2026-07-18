using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UPDSjudgeB.DTOs
{
    public class LoginRequest
    {
        public string correo { get; set; } = string.Empty;

        public string contrasena { get; set; } = string.Empty;
    }
    public class LoginResponse
    {
        // Token JWT firmado que contiene idUsuario, nombre, correo y roles
        public string token { get; set; } = string.Empty;

        // Fecha y hora UTC en la que expira el token
        public DateTime expiraEn { get; set; }
    }
}