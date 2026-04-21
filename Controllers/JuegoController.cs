using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Juego.Data;
using Juego.Models;

namespace Juego.Controllers
{
    public class JuegoController : Controller
    {
        private readonly JuegoDbContext _context;

        public JuegoController(JuegoDbContext context)
        {
            _context = context;
        }

        // GET: /
        public IActionResult Index()
        {
            return View();
        }

        // GET: /Juego/Sala/{codigo}
        [Route("Juego/Sala/{codigo}")]
        public IActionResult Sala(string codigo)
        {
            ViewBag.Codigo = codigo.ToUpperInvariant();
            return View();
        }

        // GET: /Juego/Partida/{codigo}
        [Route("Juego/Partida/{codigo}")]
        public IActionResult Partida(string codigo)
        {
            ViewBag.Codigo = codigo.ToUpperInvariant();
            return View();
        }

        // GET: /Juego/Historial
        public async Task<IActionResult> Historial()
        {
            List<Partida> partidas = await _context.Partidas
                .Include(p => p.Ronda)
                .ThenInclude(r => r.Jugadores)
                .Include(p => p.Turnos)
                .Where(p => p.Estado == EstadoPartida.Victoria || p.Estado == EstadoPartida.Derrota)
                .OrderByDescending(p => p.HoraInicio)
                .Take(50)
                .ToListAsync();

            return View(partidas);
        }

        // GET: /Juego/Leaderboard
        public async Task<IActionResult> Leaderboard()
        {
            List<Partida> records = await _context.Partidas
                .Include(p => p.Ronda)
                .ThenInclude(r => r.Jugadores)
                .Where(p => p.Estado == EstadoPartida.Victoria && !string.IsNullOrEmpty(p.NombreEquipoRecord))
                .OrderBy(p => p.TiempoSegundos) // Menor tiempo es mejor
                .Take(100)
                .ToListAsync();

            return View(records);
        }
    }
}
