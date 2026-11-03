using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace RAIZA.Models
{
    [Table("notificacion")]
    public class Notificacion
    {
        [Key]
        [Column("id")]
        public int Idnotificacion { get; set; }

        [Column("tipo_notificacion")]
        public string tiponotificacion { get; set; } = string.Empty;

        [Column("mensaje")]
        public string mensaje { get; set; } = string.Empty;

        [Column("estado_leido")]
        [JsonRequired]
        public bool estadoleido { get; set; }

        // Posible typo de "fechaenvio"; se deja igual para no romper el resto del código.
        [Column("fecha_envio")]
        [JsonRequired]
        public DateTime fechaenvivo { get; set; }

        [Column("id_usuario")]
        [JsonRequired]
        public int idusuario { get; set; }
    }
}