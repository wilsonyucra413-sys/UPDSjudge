using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UPDSjudgeB.Models
{
    public class CasoPrueba
    {
        [Key]
        public int idCasoPrueba { get; set; }
        public string entrada { get; set; }
        public string salida { get; set; }
        public string estado { get; set; } 

        public int idProblema { get; set; }
        [ForeignKey("idProblema")]
        [JsonIgnore]
        public Problema Problema { get; set; }

    }
}