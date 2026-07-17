using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace UPDSjudgeB.Models
{
    public class Concurso
    {
        [Key]
        public int idConcurso { get; set; }
        public string nombre { get; set; }
        public string descripcion { get; set; }
        public DateTime fechaInicio { get; set; }
        public int duracionMinutos { get; set; }
        public string? contrasena { get; set; }
        public string urlSetProblemas { get; set; }
        public int minutosCongelamiento { get; set; }
        public string codigo { get; set; }
        public string estado { get; set; }
        public int idUsuarioCreador { get; set; }
        [ForeignKey("idUsuarioCreador")]
        [JsonIgnore]
        public Usuario Creador { get; set; }
        public List<Problema> Problemas { get; set; }
        public List<ParticipanteConcurso> Participantes { get; set; }

    }
}