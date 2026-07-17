namespace UPDSjudgeB.DTOs
{
    /// <summary>
    /// DTO de entrada para el endpoint POST /api/auth/register.
    /// Recibe los datos del nuevo usuario que desea registrarse.
    /// </summary>
    public class RegisterRequest
    {
        // Nombre completo del usuario
        public string nombre { get; set; } = string.Empty;

        // Correo electrónico (debe ser único en el sistema)
        public string correo { get; set; } = string.Empty;

        // Contraseña en texto plano (se hasheará con BCrypt antes de guardar)
        public string contrasena { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO de salida para el endpoint POST /api/auth/register.
    /// Devuelve confirmación del registro exitoso.
    /// El correo es el identificador único expuesto al cliente (no se devuelve id interno).
    /// </summary>
    public class RegisterResponse
    {
        // Mensaje descriptivo del resultado
        public string mensaje { get; set; } = string.Empty;

        // Correo del usuario registrado (identificador único para consultas)
        public string correo { get; set; } = string.Empty;
    }
}
