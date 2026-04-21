using Microsoft.AspNetCore.SignalR;
using Juego.Data;
using Juego.Models;
using Juego.Services;

namespace Juego.Hubs
{
    public class GameHub : Hub
    {
        private readonly GameStateManager _gameManager;
        private readonly IServiceProvider _serviceProvider;

        public GameHub(GameStateManager gameManager, IServiceProvider serviceProvider)
        {
            _gameManager = gameManager;
            _serviceProvider = serviceProvider;
        }

        public async Task CrearRonda(string nombreAnfitrion)
        {
            nombreAnfitrion = nombreAnfitrion.Trim();
            if (string.IsNullOrWhiteSpace(nombreAnfitrion))
            {
                await Clients.Caller.SendAsync("Error", "El nombre no puede estar vacío.");
                return;
            }

            using IServiceScope scope = _serviceProvider.CreateScope();
            JuegoDbContext db = scope.ServiceProvider.GetRequiredService<JuegoDbContext>();

            Ronda ronda = new()
            {
                CodigoInvitacion = "TEMP",
                NombreAnfitrion = nombreAnfitrion,
                FechaCreacion = DateTime.UtcNow,
                Estado = EstadoRonda.EsperandoJugadores
            };
            db.Rondas.Add(ronda);
            await db.SaveChangesAsync();

            RoomState state = _gameManager.CrearRonda(nombreAnfitrion, Context.ConnectionId, ronda.Id);

            ronda.CodigoInvitacion = state.CodigoInvitacion;
            await db.SaveChangesAsync();

            Jugador jugadorAnfitrion = new()
            {
                Nombre = nombreAnfitrion,
                ConnectionId = Context.ConnectionId,
                EsAnfitrion = true,
                RondaId = ronda.Id
            };
            db.Jugadores.Add(jugadorAnfitrion);
            await db.SaveChangesAsync();

            state.Jugadores[0].Id = jugadorAnfitrion.Id;

            await Groups.AddToGroupAsync(Context.ConnectionId, state.CodigoInvitacion);

            await Clients.Caller.SendAsync("RondaCreada", new
            {
                codigo = state.CodigoInvitacion,
                nombreAnfitrion = nombreAnfitrion,
                jugadores = state.Jugadores.Select(j => new { j.Id, j.Nombre, j.EsAnfitrion })
            });
        }

        public async Task UnirseARonda(string codigo, string nombreJugador)
        {
            codigo = codigo.Trim().ToUpperInvariant();
            nombreJugador = nombreJugador.Trim();

            if (string.IsNullOrWhiteSpace(codigo) || string.IsNullOrWhiteSpace(nombreJugador))
            {
                await Clients.Caller.SendAsync("Error", "El código y el nombre son obligatorios.");
                return;
            }

            RoomState? state = _gameManager.ObtenerRonda(codigo);
            if (state == null)
            {
                await Clients.Caller.SendAsync("Error", "No se encontró una sala con ese código.");
                return;
            }

            if (state.Estado == EstadoRonda.Cerrada)
            {
                await Clients.Caller.SendAsync("Error", "Esta ronda ya fue cerrada.");
                return;
            }

            await state.Lock.WaitAsync();
            try
            {
                // Verificar si ya existe el jugador (reconexión)
                var jugadorExistente = state.Jugadores.FirstOrDefault(j => j.Nombre.Equals(nombreJugador, StringComparison.OrdinalIgnoreCase));
                
                if (jugadorExistente != null)
                {
                    // Es reconexión o intento de duplicar
                    jugadorExistente.ConnectionId = Context.ConnectionId;
                    
                    // Actualizar BD
                    using IServiceScope scope = _serviceProvider.CreateScope();
                    JuegoDbContext db = scope.ServiceProvider.GetRequiredService<JuegoDbContext>();
                    var dbJugador = await db.Jugadores.FindAsync(jugadorExistente.Id);
                    if (dbJugador != null)
                    {
                        dbJugador.ConnectionId = Context.ConnectionId;
                        await db.SaveChangesAsync();
                    }

                    await Groups.AddToGroupAsync(Context.ConnectionId, codigo);
                    
                    await Clients.Caller.SendAsync("UnidoAPartida", new
                    {
                        codigo = codigo,
                        nombreAnfitrion = state.NombreAnfitrion,
                        jugadores = state.Jugadores.Select(j => new { j.Id, j.Nombre, j.EsAnfitrion })
                    });
                    
                    // Enviar estado actual al reconectado
                    return;
                }

                if (state.Estado != EstadoRonda.EsperandoJugadores)
                {
                    await Clients.Caller.SendAsync("Error", "La ronda ya está en curso, no puedes unirte como jugador nuevo.");
                    return;
                }

                // Jugador nuevo
                using IServiceScope newScope = _serviceProvider.CreateScope();
                JuegoDbContext newDb = newScope.ServiceProvider.GetRequiredService<JuegoDbContext>();

                Jugador jugador = new()
                {
                    Nombre = nombreJugador,
                    ConnectionId = Context.ConnectionId,
                    RondaId = state.RondaDbId
                };
                newDb.Jugadores.Add(jugador);
                await newDb.SaveChangesAsync();

                PlayerInfo? player = _gameManager.AgregarJugador(codigo, nombreJugador, Context.ConnectionId);
                if (player != null)
                {
                    player.Id = jugador.Id;
                }

                await Groups.AddToGroupAsync(Context.ConnectionId, codigo);

                await Clients.Group(codigo).SendAsync("JugadorUnido", new
                {
                    nombre = nombreJugador,
                    totalJugadores = state.Jugadores.Count,
                    jugadores = state.Jugadores.Select(j => new { j.Id, j.Nombre, j.EsAnfitrion })
                });

                await Clients.Caller.SendAsync("UnidoAPartida", new
                {
                    codigo = codigo,
                    nombreAnfitrion = state.NombreAnfitrion,
                    jugadores = state.Jugadores.Select(j => new { j.Id, j.Nombre, j.EsAnfitrion })
                });
            }
            finally
            {
                state.Lock.Release();
            }
        }

        public async Task IniciarJuego(string codigo)
        {
            codigo = codigo.Trim().ToUpperInvariant();
            RoomState? state = _gameManager.ObtenerRonda(codigo);

            if (state == null)
            {
                await Clients.Caller.SendAsync("Error", "Ronda no encontrada.");
                return;
            }

            var currPlayer = state.Jugadores.FirstOrDefault(j => j.ConnectionId == Context.ConnectionId);
            if (currPlayer == null || !currPlayer.EsAnfitrion)
            {
                await Clients.Caller.SendAsync("Error", "Solo el anfitrión puede iniciar.");
                return;
            }

            if (state.Jugadores.Count < 2)
            {
                await Clients.Caller.SendAsync("Error", "Se necesitan al menos 2 jugadores para iniciar.");
                return;
            }

            await state.Lock.WaitAsync();
            try
            {
                using IServiceScope scope = _serviceProvider.CreateScope();
                JuegoDbContext db = scope.ServiceProvider.GetRequiredService<JuegoDbContext>();

                Partida nuevaPartida = new()
                {
                    RondaId = state.RondaDbId,
                    Estado = EstadoPartida.EnProgreso,
                    HoraInicio = DateTime.UtcNow
                };
                db.Partidas.Add(nuevaPartida);
                
                var ronda = await db.Rondas.FindAsync(state.RondaDbId);
                if (ronda != null && ronda.Estado == EstadoRonda.EsperandoJugadores)
                {
                    ronda.Estado = EstadoRonda.Activa;
                }
                
                await db.SaveChangesAsync();

                _gameManager.IniciarNuevaPartida(codigo, nuevaPartida.Id);
                PlayerInfo? primerJugador = _gameManager.SeleccionarSiguienteTurno(codigo);

                await Clients.Group(codigo).SendAsync("JuegoIniciado", new
                {
                    horaInicio = state.PartidaActual!.HoraInicio!.Value.ToUnixTimeMilliseconds(),
                    jugadorTurnoNombre = primerJugador?.Nombre,
                    jugadorTurnoId = primerJugador?.Id,
                    totalJugadores = state.Jugadores.Count,
                    puntuacion = $"0/{state.Jugadores.Count}",
                    jugadores = state.Jugadores.Select(j => new { j.Id, j.Nombre, j.EsAnfitrion }),
                    coloresBloqueadosGlobales = state.ColoresBloqueadosGlobales.ToList()
                });
            }
            finally
            {
                state.Lock.Release();
            }
        }

        public async Task EnviarColor(string codigo, string color)
        {
            codigo = codigo.Trim().ToUpperInvariant();
            color = color.Trim();

            RoomState? state = _gameManager.ObtenerRonda(codigo);
            if (state == null || state.PartidaActual == null)
            {
                await Clients.Caller.SendAsync("Error", "Partida no activa.");
                return;
            }

            if (state.PartidaActual.Estado != EstadoPartida.EnProgreso)
            {
                await Clients.Caller.SendAsync("Error", "La partida no está en progreso.");
                return;
            }

            if (!_gameManager.EsColorValido(color))
            {
                await Clients.Caller.SendAsync("Error", "Color inválido. Revisa la ortografía o usa la paleta.");
                return;
            }

            PlayerInfo? jugadorActual = state.Jugadores.FirstOrDefault(j => j.ConnectionId == Context.ConnectionId);
            if (jugadorActual == null || state.PartidaActual.JugadorTurnoActualId != jugadorActual.Id)
            {
                await Clients.Caller.SendAsync("Error", "No es tu turno.");
                return;
            }

            await state.Lock.WaitAsync();
            try
            {
                string colorNormalizado = _gameManager.NormalizarColor(color);
                string colorKeyLower = color.Trim().ToLowerInvariant();

                bool colorRepetidoHistorial = state.ColoresBloqueadosGlobales.Contains(colorKeyLower);
                bool colorRepetidoActual = state.PartidaActual.ColoresUsadosNombres.Any(c => c.Equals(colorKeyLower, StringComparison.OrdinalIgnoreCase));

                if (colorRepetidoHistorial || colorRepetidoActual)
                {
                    state.PartidaActual.Estado = EstadoPartida.Derrota;
                    double tiempoTotal = (DateTimeOffset.UtcNow - state.PartidaActual.HoraInicio!.Value).TotalSeconds;
                    string puntuacion = $"{state.PartidaActual.TurnosCompletados}/{state.Jugadores.Count}";

                    jugadorActual.ColorDicho = colorNormalizado;
                    jugadorActual.YaTuvoTurno = true;

                    await GuardarPartidaFinalizada(state.PartidaActual.PartidaDbId, EstadoPartida.Derrota, tiempoTotal, puntuacion, null);

                    await Clients.Group(codigo).SendAsync("JuegoTerminado", new
                    {
                        resultado = "derrota",
                        mensaje = $"¡{jugadorActual.Nombre} repitió el color \"{colorNormalizado}\"!",
                        puntuacion = puntuacion,
                        tiempoSegundos = Math.Round(tiempoTotal, 1),
                        colorRepetido = colorNormalizado,
                        motivo = colorRepetidoHistorial ? "¡Ese color ya se usó en una partida anterior en esta misma sala!" : "¡Ese color ya se dijo en esta partida!",
                        resumen = state.Jugadores.Select(j => new { j.Nombre, color = j.ColorDicho ?? "—", j.EsAnfitrion })
                    });
                    return;
                }

                // Correcto: agregarlo
                state.PartidaActual.ColoresUsadosNombres.Add(colorNormalizado);
                state.ColoresBloqueadosGlobales.Add(colorKeyLower); // Se guarda como válido globalmente para las prox.
                
                jugadorActual.ColorDicho = colorNormalizado;
                jugadorActual.YaTuvoTurno = true;
                state.PartidaActual.TurnosCompletados++;

                string puntuacionActual = $"{state.PartidaActual.TurnosCompletados}/{state.Jugadores.Count}";

                using IServiceScope scope = _serviceProvider.CreateScope();
                JuegoDbContext db = scope.ServiceProvider.GetRequiredService<JuegoDbContext>();
                
                db.Turnos.Add(new Turno
                {
                    PartidaId = state.PartidaActual.PartidaDbId,
                    JugadorId = jugadorActual.Id,
                    Color = colorNormalizado,
                    Momento = DateTime.UtcNow
                });
                await db.SaveChangesAsync();

                if (state.PartidaActual.TurnosCompletados >= state.Jugadores.Count)
                {
                    state.PartidaActual.Estado = EstadoPartida.Victoria;
                    double tiempoTotal = (DateTimeOffset.UtcNow - state.PartidaActual.HoraInicio!.Value).TotalSeconds;

                    await GuardarPartidaFinalizada(state.PartidaActual.PartidaDbId, EstadoPartida.Victoria, tiempoTotal, puntuacionActual, null);

                    await Clients.Group(codigo).SendAsync("JuegoTerminado", new
                    {
                        resultado = "victoria",
                        mensaje = "¡VICTORIA! Todos pasaron.",
                        puntuacion = puntuacionActual,
                        tiempoSegundos = Math.Round(tiempoTotal, 1),
                        resumen = state.Jugadores.Select(j => new { j.Nombre, color = j.ColorDicho ?? "—", j.EsAnfitrion }),
                        requiereNombreRecord = true // Le pide al anfitrion un nombre
                    });
                    return;
                }

                await Clients.Group(codigo).SendAsync("ColorAgregado", new
                {
                    color = colorNormalizado,
                    jugadorNombre = jugadorActual.Nombre,
                    puntuacion = puntuacionActual,
                    coloresUsados = state.PartidaActual.ColoresUsadosNombres,
                    turnosCompletados = state.PartidaActual.TurnosCompletados,
                    totalJugadores = state.Jugadores.Count,
                    coloresBloqueadosGlobales = state.ColoresBloqueadosGlobales.ToList()
                });
            }
            finally
            {
                state.Lock.Release();
            }
        }

        public async Task GuardarRecord(string codigo, string nombreEquipo)
        {
             codigo = codigo.Trim().ToUpperInvariant();
             RoomState? state = _gameManager.ObtenerRonda(codigo);
             if (state == null || state.PartidaActual == null) return;
             
             // Solo el anfitrión puede guardarlo
             var currPlayer = state.Jugadores.FirstOrDefault(j => j.ConnectionId == Context.ConnectionId);
             if (currPlayer == null || !currPlayer.EsAnfitrion) return;

             using IServiceScope scope = _serviceProvider.CreateScope();
             JuegoDbContext db = scope.ServiceProvider.GetRequiredService<JuegoDbContext>();
             
             var partidaDb = await db.Partidas.FindAsync(state.PartidaActual.PartidaDbId);
             if (partidaDb != null)
             {
                 partidaDb.NombreEquipoRecord = nombreEquipo.Trim();
                 await db.SaveChangesAsync();
                 
                 await Clients.Group(codigo).SendAsync("RecordGuardado", nombreEquipo);
             }
        }

        public async Task CerrarRonda(string codigo)
        {
             codigo = codigo.Trim().ToUpperInvariant();
             RoomState? state = _gameManager.ObtenerRonda(codigo);
             if (state == null) return;
             
             var currPlayer = state.Jugadores.FirstOrDefault(j => j.ConnectionId == Context.ConnectionId);
             if (currPlayer == null || !currPlayer.EsAnfitrion) return;

             using IServiceScope scope = _serviceProvider.CreateScope();
             JuegoDbContext db = scope.ServiceProvider.GetRequiredService<JuegoDbContext>();
             
             var rondaDb = await db.Rondas.FindAsync(state.RondaDbId);
             if (rondaDb != null)
             {
                 rondaDb.Estado = EstadoRonda.Cerrada;
                 rondaDb.FechaCerrada = DateTime.UtcNow;
                 await db.SaveChangesAsync();
             }

             _gameManager.FinalizarRonda(codigo);
             await Clients.Group(codigo).SendAsync("RondaCerrada");
        }

        public async Task SiguienteTurno(string codigo)
        {
            codigo = codigo.Trim().ToUpperInvariant();
            RoomState? state = _gameManager.ObtenerRonda(codigo);

            if (state == null || state.PartidaActual == null) return;

            var currPlayer = state.Jugadores.FirstOrDefault(j => j.ConnectionId == Context.ConnectionId);
            if (currPlayer == null || !currPlayer.EsAnfitrion) return;

            if (state.PartidaActual.Estado != EstadoPartida.EnProgreso) return;

            await state.Lock.WaitAsync();
            try
            {
                PlayerInfo? jugadorActual = state.Jugadores.FirstOrDefault(j => j.Id == state.PartidaActual.JugadorTurnoActualId);
                if (jugadorActual != null && !jugadorActual.YaTuvoTurno)
                {
                    await Clients.Caller.SendAsync("Error", "El jugador actual aún no ha dicho su color.");
                    return;
                }

                PlayerInfo? siguiente = _gameManager.SeleccionarSiguienteTurno(codigo);
                if (siguiente == null) return;

                await Clients.Group(codigo).SendAsync("TurnoCambiado", new
                {
                    jugadorNombre = siguiente.Nombre,
                    jugadorId = siguiente.Id,
                    turnosCompletados = state.PartidaActual.TurnosCompletados,
                    totalJugadores = state.Jugadores.Count
                });
            }
            finally
            {
                state.Lock.Release();
            }
        }

        public async Task ObtenerEstado(string codigo, string nombreJugador)
        {
            codigo = codigo.Trim().ToUpperInvariant();
            RoomState? state = _gameManager.ObtenerRonda(codigo);

            if (state == null)
            {
                await Clients.Caller.SendAsync("Error", "SALA_NO_ENCONTRADA");
                return;
            }

            // Actualizar la conexión si vino con un nombre (Reconexión explícita post-recarga page)
            if (!string.IsNullOrEmpty(nombreJugador))
            {
                _gameManager.ReconectarJugador(codigo, nombreJugador, Context.ConnectionId);
                await Groups.AddToGroupAsync(Context.ConnectionId, codigo);
            }

            var currPlayer = state.Jugadores.FirstOrDefault(j => j.ConnectionId == Context.ConnectionId) 
                ?? state.Jugadores.FirstOrDefault(j => j.Nombre.Equals(nombreJugador, StringComparison.OrdinalIgnoreCase));

            if (currPlayer == null)
            {
                await Clients.Caller.SendAsync("Error", "No formas parte de esta sala.");
                return;
            }

            string estadoRonda = state.Estado.ToString();
            string estadoPartida = state.PartidaActual != null ? state.PartidaActual.Estado.ToString() : "";

            // Consolidate response into EstadoActual logic
            await Clients.Caller.SendAsync("EstadoActual", new
            {
                codigo = state.CodigoInvitacion,
                estadoRonda = estadoRonda,
                estadoPartida = estadoPartida,
                nombreAnfitrion = state.NombreAnfitrion,
                esAnfitrion = currPlayer.EsAnfitrion,
                jugadores = state.Jugadores.Select(j => new
                {
                    j.Id,
                    j.Nombre,
                    j.EsAnfitrion,
                    color = j.ColorDicho,
                    j.YaTuvoTurno
                }),
                coloresUsados = state.PartidaActual?.ColoresUsadosNombres ?? new List<string>(),
                coloresBloqueadosGlobales = state.ColoresBloqueadosGlobales.ToList(),
                jugadorTurnoNombre = state.PartidaActual?.JugadorTurnoActualNombre,
                jugadorTurnoId = state.PartidaActual?.JugadorTurnoActualId,
                horaInicio = state.PartidaActual?.HoraInicio?.ToUnixTimeMilliseconds(),
                puntuacion = state.PartidaActual != null ? $"{state.PartidaActual.TurnosCompletados}/{state.Jugadores.Count}" : "0/0",
                turnosCompletados = state.PartidaActual?.TurnosCompletados ?? 0,
                totalJugadores = state.Jugadores.Count
            });
        }

        private async Task GuardarPartidaFinalizada(int partidaDbId, EstadoPartida estado, double tiempoTotal, string puntuacion, string? nombreRecord)
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            JuegoDbContext db = scope.ServiceProvider.GetRequiredService<JuegoDbContext>();

            Partida? partida = await db.Partidas.FindAsync(partidaDbId);
            if (partida != null)
            {
                partida.Estado = estado;
                partida.TiempoSegundos = Math.Round(tiempoTotal, 1);
                partida.Puntuacion = puntuacion;
                partida.NombreEquipoRecord = nombreRecord;
                await db.SaveChangesAsync();
            }
        }
    }
}
