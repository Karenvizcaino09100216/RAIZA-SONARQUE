using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace RAIZA.Models
{
    public class ProgresoLeccion
    {
        [Key]
        public int Idprogresoleccion { get; set; }

        [Required(ErrorMessage = "Debe indicar si la lección fue completada.")]
        [JsonRequired]
        public bool completado { get; set; }

        public DateTime? fecha { get; set; }

        [Required(ErrorMessage = "Debe seleccionar un estudiante.")]
        [JsonRequired]
        public int Idestudiante { get; set; }

        [Required(ErrorMessage = "Debe seleccionar una lección.")]
        [JsonRequired]
        public int Idleccion { get; set; }
    }
}