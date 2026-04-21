using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Juego.Models
{
    public class Partida
    {
        [Key]
        public int Id { get; set; }

        public int RondaId { get; set; }
        
        [ForeignKey("RondaId")]
        public Ronda? Ronda { get; set; }

        public EstadoPartida Estado { get; set; } = EstadoPartida.EnProgreso;

        public double TiempoSegundos { get; set; } = 0;

        [MaxLength(20)]
        public string Puntuacion { get; set; } = "0/0";

        public DateTime HoraInicio { get; set; } = DateTime.UtcNow;

        [MaxLength(100)]
        public string? NombreEquipoRecord { get; set; } // Nombre ingresado si ganan, para leaderboard

        public List<Turno> Turnos { get; set; } = new();
    }
}
