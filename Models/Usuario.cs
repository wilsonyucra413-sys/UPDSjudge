using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace UPDSjudgeB.Models
{
    public class Usuario
    {
        [Key]
        public int idUsuario { get; set; }
        public string nombre { get; set; }
        public string correo { get; set; }
        public string contrasena { get; set; }
        public DateTime fechaRegistro { get; set; }
        public string estado { get; set; }
        public List<UsuarioRol> UsuarioRoles { get; set; }
        public List<Concurso> ConcursosCreados { get; set; }
        public List<ParticipanteConcurso> Participaciones { get; set; }
        public List<Envio> Envios { get; set; }
    }
}