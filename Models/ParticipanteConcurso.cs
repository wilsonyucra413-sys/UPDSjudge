using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UPDSjudgeB.Models
{
    public class ParticipanteConcurso
    {
        [Key]
        public int idParticipanteConcurso { get; set; }
        public DateTime fechaIngreso { get; set; }
        public string estado { get; set; }
        public int idUsuario { get; set; }
        public int idConcurso { get; set; }

        [ForeignKey("idUsuario")]
        [JsonIgnore]
        public Usuario Usuario { get; set; }

        [ForeignKey("idConcurso")]
        [JsonIgnore]
        public Concurso Concurso { get; set; }

    }
}