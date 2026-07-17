using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UPDSjudgeB.Models
{
    public class Problema
    {
        [Key]
        public int idProblema { get; set; }
        public char inciso { get; set; }
        public string titulo { get; set; }
        public float tiempo { get; set; }
        public int memoria { get; set; }
        public string estado { get; set; }

        public int idConcurso { get; set; }
        [ForeignKey("idConcurso")]
        [JsonIgnore]
        public Concurso Concurso { get; set; }

        public List<CasoPrueba> CasosPrueba { get; set; }
        public List<Envio> Envios { get; set; }
    }
}