using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace UPDSjudgeB.Models
{
    public class Lenguaje
    {
        [Key]
        public int idLenguaje { get; set; }
        public int idJudge0 { get; set; }
        public string nombre { get; set; }
        public string extension { get; set; }
        public string estado { get; set; }

        public List<Envio> Envios { get; set; }

    }
}