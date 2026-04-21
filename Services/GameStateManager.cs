using System.Collections.Concurrent;
using Juego.Models;

namespace Juego.Services
{
    public class PlayerInfo
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? ConnectionId { get; set; }
        public string? ColorDicho { get; set; }
        public int? OrdenTurno { get; set; }
        public bool YaTuvoTurno { get; set; } = false;
        public bool EsAnfitrion { get; set; } = false;
    }

    public class PartidaState
    {
        public int PartidaDbId { get; set; }
        public DateTimeOffset? HoraInicio { get; set; }
        public int TurnosCompletados { get; set; } = 0;
        public int? JugadorTurnoActualId { get; set; }
        public string? JugadorTurnoActualNombre { get; set; }
        public EstadoPartida Estado { get; set; } = EstadoPartida.EnProgreso;

        // Colores usados exlusivamente en esta partida (para listarlos)
        public List<string> ColoresUsadosNombres { get; set; } = new();
    }

    public class RoomState
    {
        public int RondaDbId { get; set; }
        public string CodigoInvitacion { get; set; } = string.Empty;
        public EstadoRonda Estado { get; set; } = EstadoRonda.EsperandoJugadores;
        public List<PlayerInfo> Jugadores { get; set; } = new();
        public string NombreAnfitrion { get; set; } = string.Empty;
        
        // Histórico de colores usados que fueron VÁLIDOS, para que no se repitan en futuras partidas de esta ronda
        public HashSet<string> ColoresBloqueadosGlobales { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        // La partida que se está jugando en este momento, si es que hay una
        public PartidaState? PartidaActual { get; set; }

        public SemaphoreSlim Lock { get; set; } = new(1, 1);
    }

    public class GameStateManager
    {
        private readonly ConcurrentDictionary<string, RoomState> _rondas = new();
        private static readonly Random _random = new();

        // Lista maestra de colores válidos
        private static readonly HashSet<string> _coloresValidos = new(StringComparer.OrdinalIgnoreCase)
        {
            "rojo", "azul", "verde", "amarillo", "naranja", "morado", "rosa", "blanco",
            "negro", "gris", "marrón", "celeste", "violeta", "turquesa", "dorado",
            "plateado", "fucsia", "beige", "coral", "lavanda", "carmesí", "escarlata",
            "índigo", "indigo", "magenta", "cian", "cyan", "oliva", "crema", "salmón",
            "salmon", "borgoña", "burgundy", "terracota", "ocre", "ámbar", "ambar",
            "esmeralda", "jade", "cobalto", "cerezo", "chocolate", "canela", "arena",
            "hueso", "marfil", "perla", "lila", "malva", "ciruela", "granate", "carmín",
            "carmin", "bermellón", "bermellon", "púrpura", "purpura", "añil", "anil",
            "zafiro", "rubí", "rubi", "topacio", "aguamarina", "menta", "lima",
            "chartreuse", "durazno", "melocotón", "melocoton", "frambuesa",
            "cereza", "vino", "burdeos", "tinto", "cobre", "bronce",
            "titanio", "grafito", "pizarra", "ceniza", "carbón", "carbon",
            "nieve", "algodón", "algodon", "trigo", "miel", "caramelo",
            "canario", "limón", "limon", "mostaza", "oro", "otoño", "bronce",
            "caqui", "khaki", "siena", "tostado", "café", "cafe", "caoba",
            "bermejo", "sepia", "desierto", "piel",
            "mandarina", "calabaza", "zanahoria", "albaricoque",
            "arcilla", "ladrillo", "herrumbre", "óxido", "oxido",
            "tomate", "sangre", "rubor",
            "baya", "grosella", "uva", "berenjena", "amatista",
            "orquídea", "orquidea", "glicina", "heliotropo",
            "iris", "pervinca", "aciano", "cielo", "mar", "océano", "oceano",
            "petróleo", "petroleo", "azulón", "azulon", "prusia", "marino",
            "nocturno", "medianoche", "eléctrico", "electrico", "neón", "neon",
            "aqua", "hierbabuena", "eucalipto", "pino", "bosque",
            "musgo", "helecho", "hierba", "pradera",
            "pistacho", "manzana", "kiwi", "loro", "abeto", "aceituna",
            "pistache", "alga", "trébol", "trebol", "selva", "jungla"
        };

        public bool EsColorValido(string color)
        {
            string colorNormalizado = color.Trim().ToLowerInvariant();
            return _coloresValidos.Contains(colorNormalizado);
        }

        public string NormalizarColor(string color)
        {
            string normalizado = color.Trim();
            if (normalizado.Length > 0)
            {
                normalizado = char.ToUpperInvariant(normalizado[0]) + normalizado.Substring(1).ToLowerInvariant();
            }
            return normalizado;
        }

        public string GenerarCodigo()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            string codigo;
            do
            {
                codigo = new string(Enumerable.Range(0, 6)
                    .Select(_ => chars[_random.Next(chars.Length)])
                    .ToArray());
            } while (_rondas.ContainsKey(codigo));
            return codigo;
        }

        public RoomState CrearRonda(string nombreAnfitrion, string connectionId, int rondaDbId)
        {
            string codigo = GenerarCodigo();
            RoomState state = new()
            {
                RondaDbId = rondaDbId,
                CodigoInvitacion = codigo,
                NombreAnfitrion = nombreAnfitrion,
                Jugadores = new List<PlayerInfo>
                {
                    new PlayerInfo
                    {
                        Id = 1,
                        Nombre = nombreAnfitrion,
                        ConnectionId = connectionId,
                        EsAnfitrion = true
                    }
                }
            };
            _rondas[codigo] = state;
            return state;
        }

        public RoomState? ObtenerRonda(string codigo)
        {
            _rondas.TryGetValue(codigo.ToUpperInvariant(), out RoomState? state);
            return state;
        }

        public PlayerInfo? AgregarJugador(string codigo, string nombre, string connectionId)
        {
            RoomState? state = ObtenerRonda(codigo);
            if (state == null || state.Estado != EstadoRonda.EsperandoJugadores)
                return null;

            int newId = state.Jugadores.Count + 1;
            PlayerInfo player = new()
            {
                Id = newId,
                Nombre = nombre,
                ConnectionId = connectionId
            };
            state.Jugadores.Add(player);
            return player;
        }

        public PlayerInfo? ReconectarJugador(string codigo, string nombreViejo, string newConnectionId)
        {
            RoomState? state = ObtenerRonda(codigo);
            if (state == null) return null;

            PlayerInfo? player = state.Jugadores.FirstOrDefault(j => j.Nombre.Equals(nombreViejo, StringComparison.OrdinalIgnoreCase));
            if (player != null)
            {
                player.ConnectionId = newConnectionId;
            }
            return player;
        }

        public void IniciarNuevaPartida(string codigo, int partidaDbId)
        {
            RoomState? state = ObtenerRonda(codigo);
            if (state == null) return;

            state.Estado = EstadoRonda.Activa;

            // Resetear jugadores para la nueva partida
            foreach(var j in state.Jugadores)
            {
                j.YaTuvoTurno = false;
                j.ColorDicho = null;
                j.OrdenTurno = null;
            }

            state.PartidaActual = new PartidaState
            {
                PartidaDbId = partidaDbId,
                HoraInicio = DateTimeOffset.UtcNow,
                Estado = EstadoPartida.EnProgreso
            };
        }

        public PlayerInfo? SeleccionarSiguienteTurno(string codigo)
        {
            RoomState? state = ObtenerRonda(codigo);
            if (state == null || state.PartidaActual == null) return null;

            List<PlayerInfo> disponibles = state.Jugadores
                .Where(j => !j.YaTuvoTurno)
                .ToList();

            if (disponibles.Count == 0) return null;

            PlayerInfo elegido = disponibles[_random.Next(disponibles.Count)];
            elegido.OrdenTurno = state.PartidaActual.TurnosCompletados + 1;
            
            state.PartidaActual.JugadorTurnoActualId = elegido.Id;
            state.PartidaActual.JugadorTurnoActualNombre = elegido.Nombre;
            return elegido;
        }

        public void FinalizarRonda(string codigo)
        {
            RoomState? state = ObtenerRonda(codigo);
            if (state != null)
            {
                state.Estado = EstadoRonda.Cerrada;
            }
        }

        public IEnumerable<RoomState> ObtenerRondasActivas()
        {
            return _rondas.Values;
        }
    }
}
