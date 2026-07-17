using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace UPDSjudgeB.Models
{
    public class Rol
    {
        [Key]
        public int idRol { get; set; }
        public string nombre { get; set; }
        public List<UsuarioRol> UsuarioRoles { get; set; }
        
    }
}