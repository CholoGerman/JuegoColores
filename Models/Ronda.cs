using System.ComponentModel.DataAnnotations;

namespace Juego.Models
{
    public class Ronda
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(10)]
        public string CodigoInvitacion { get; set; } = string.Empty;

        public EstadoRonda Estado { get; set; } = EstadoRonda.EsperandoJugadores;

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
        public DateTime? FechaCerrada { get; set; }

        [MaxLength(50)]
        public string NombreAnfitrion { get; set; } = string.Empty;

        // Jugadores que se unieron a esta ronda
        public List<Jugador> Jugadores { get; set; } = new();

        // Las múltiples partidas jugadas en esta pista
        public List<Partida> Partidas { get; set; } = new();
    }
}
