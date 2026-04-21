using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Juego.Models
{
    public class Turno
    {
        [Key]
        public int Id { get; set; }

        public int PartidaId { get; set; }

        public int JugadorId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Color { get; set; } = string.Empty;

        public DateTime Momento { get; set; } = DateTime.UtcNow;

        [ForeignKey("PartidaId")]
        public Partida? Partida { get; set; }

        [ForeignKey("JugadorId")]
        public Jugador? Jugador { get; set; }
    }
}
