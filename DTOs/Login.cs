using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UPDSjudgeB.DTOs
{
    public class Login
    {

    }
    /// <summary>
    /// DTO de entrada para el endpoint POST /login.
    /// Recibe las credenciales que el cliente envía en el cuerpo de la petición.
    /// </summary>
    public class LoginRequest
    {
        // Correo electrónico del usuario (se usa para buscar en la tabla Usuario)
        public string correo { get; set; } = string.Empty;

        // Contraseña en texto plano (se verificará contra el hash almacenado con BCrypt)
        public string contrasena { get; set; } = string.Empty;
    }
    /// <summary>
    /// DTO de salida para el endpoint POST /login.
    /// Devuelve el token JWT generado tras una autenticación exitosa.
    /// </summary>
    public class LoginResponse
    {
        // Token JWT firmado que contiene idUsuario, nombre, correo y roles
        public string token { get; set; } = string.Empty;

        // Fecha y hora UTC en la que expira el token
        public DateTime expiraEn { get; set; }
    }
}