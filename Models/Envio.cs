using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UPDSjudgeB.Models
{
    public class Envio
    {
        [Key]
        public int idEnvio { get; set; }
        public string codigo { get; set; }
        public string resultado { get; set; }
        public float tiempo { get; set; }
        public int memoria { get; set; }
        public string token { get; set; } // Token de Judge0
        public string upsolving { get; set; }
        public DateTime fechaEnvio { get; set; }
        public int idUsuario { get; set; }
        public int idProblema { get; set; }
        public int idLenguaje { get; set; }

        [ForeignKey("idUsuario")]
        [JsonIgnore]
        public Usuario Usuario { get; set; }

        [ForeignKey("idProblema")]
        [JsonIgnore]
        public Problema Problema { get; set; }

        [ForeignKey("idLenguaje")]
        [JsonIgnore]
        public Lenguaje Lenguaje { get; set; }
    }
}