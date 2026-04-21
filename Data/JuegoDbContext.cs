using Microsoft.EntityFrameworkCore;
using Juego.Models;

namespace Juego.Data
{
    public class JuegoDbContext : DbContext
    {
        public JuegoDbContext(DbContextOptions<JuegoDbContext> options) : base(options) { }

        public DbSet<Ronda> Rondas { get; set; }
        public DbSet<Partida> Partidas { get; set; }
        public DbSet<Jugador> Jugadores { get; set; }
        public DbSet<Turno> Turnos { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Ronda>(entity =>
            {
                entity.HasIndex(r => r.CodigoInvitacion).IsUnique();
                
                entity.HasMany(r => r.Jugadores)
                      .WithOne(j => j.Ronda)
                      .HasForeignKey(j => j.RondaId)
                      .OnDelete(DeleteBehavior.Cascade);
                      
                entity.HasMany(r => r.Partidas)
                      .WithOne(p => p.Ronda)
                      .HasForeignKey(p => p.RondaId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Partida>(entity =>
            {
                entity.HasMany(p => p.Turnos)
                      .WithOne(t => t.Partida)
                      .HasForeignKey(t => t.PartidaId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Turno>(entity =>
            {
                entity.HasOne(t => t.Jugador)
                      .WithMany()
                      .HasForeignKey(t => t.JugadorId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
