using System.ComponentModel.DataAnnotations;

namespace RAIZA.Models
{
    public class Estudiante
    {
        [Key]
        public int idestudiante { get; set; }

        [Required(ErrorMessage = "Debe indicar si el estudiante es Premium.")]
        [System.Text.Json.Serialization.JsonRequired]
        public bool Espremium { get; set; }

        public DateTime? FechaAcceso { get; set; }
    }
}