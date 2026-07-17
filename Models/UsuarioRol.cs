using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UPDSjudgeB.Models
{
    public class UsuarioRol
    {
        [Key]
        public int idUsuarioRol { get; set; }

        public int idUsuario { get; set; }
        public int idRol { get; set; }

        [JsonIgnore]
        public Usuario Usuario { get; set; }

        [ForeignKey("idRol")]
        [JsonIgnore]
        public Rol Rol { get; set; }
    }
}