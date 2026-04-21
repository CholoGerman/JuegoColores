using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Juego.Models
{
    public class Jugador
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Nombre { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? ConnectionId { get; set; }

        public bool EsAnfitrion { get; set; } = false;

        public int RondaId { get; set; }

        [ForeignKey("RondaId")]
        public Ronda? Ronda { get; set; }
    }
}
