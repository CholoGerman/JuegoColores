// ============================================
//  JUEGO DE COLORES - Client-side Game Logic
// ============================================

// Color hex map para visuales en los chips
const COLOR_HEX_MAP = {
    'rojo': '#E74C3C', 'azul': '#3498DB', 'verde': '#2ECC71',
    'amarillo': '#F1C40F', 'naranja': '#E67E22', 'morado': '#9B59B6',
    'rosa': '#FF69B4', 'celeste': '#87CEEB', 'negro': '#2C3E50',
    'blanco': '#FFFFFF', 'gris': '#95A5A6', 'marrón': '#8B4513',
    'turquesa': '#1ABC9C', 'dorado': '#FFD700', 'violeta': '#8B00FF',
    'fucsia': '#FF00FF', 'coral': '#FF7F50', 'lavanda': '#E6E6FA',
    'esmeralda': '#50C878', 'salmón': '#FA8072', 'salmon': '#FA8072',
    'carmesí': '#DC143C', 'magenta': '#FF00FF', 'cian': '#00FFFF',
    'cyan': '#00FFFF', 'oliva': '#808000', 'crema': '#FFFDD0',
    'borgoña': '#800020', 'ocre': '#CC7722', 'ámbar': '#FFBF00',
    'ambar': '#FFBF00', 'jade': '#00A86B', 'cobalto': '#0047AB',
    'chocolate': '#D2691E', 'canela': '#D2691E', 'arena': '#C2B280',
    'marfil': '#FFFFF0', 'perla': '#EAE0C8', 'lila': '#C8A2C8',
    'malva': '#E0B0FF', 'granate': '#800000', 'carmín': '#960018',
    'carmin': '#960018', 'púrpura': '#800080', 'purpura': '#800080',
    'añil': '#4B0082', 'anil': '#4B0082', 'zafiro': '#0F52BA',
    'rubí': '#E0115F', 'rubi': '#E0115F', 'aguamarina': '#7FFFD4',
    'menta': '#98FF98', 'lima': '#00FF00', 'melocotón': '#FFDAB9',
    'melocoton': '#FFDAB9', 'frambuesa': '#E30B5C', 'cereza': '#DE3163',
    'vino': '#722F37', 'burdeos': '#800020', 'tinto': '#722F37',
    'cobre': '#B87333', 'bronce': '#CD7F32', 'grafito': '#383838',
    'pizarra': '#708090', 'ceniza': '#B2BEB5', 'carbón': '#36454F',
    'carbon': '#36454F', 'nieve': '#FFFAFA', 'miel': '#EB9605',
    'caramelo': '#FFD59A', 'limón': '#FFF44F', 'limon': '#FFF44F',
    'mostaza': '#FFDB58', 'oro': '#FFD700', 'café': '#6F4E37',
    'cafe': '#6F4E37', 'sepia': '#704214', 'tomate': '#FF6347',
    'berenjena': '#614051', 'orquídea': '#DA70D6', 'orquidea': '#DA70D6',
    'índigo': '#4B0082', 'indigo': '#4B0082', 'bermellón': '#E34234',
    'bermellon': '#E34234', 'plateado': '#C0C0C0', 'beige': '#F5F5DC',
    'ciruela': '#8E4585', 'topacio': '#FFC87C', 'durazno': '#FFDAB9',
    'cerezo': '#DE3163', 'hueso': '#E3DAC9', 'canario': '#FFEF00',
    'tostado': '#CD853F', 'caoba': '#C04000', 'bermejo': '#D2042D',
    'calabaza': '#FF7518', 'zanahoria': '#ED9121', 'albaricoque': '#FBCEB1',
    'arcilla': '#B66A50', 'ladrillo': '#CB4154', 'mandarina': '#FF8243',
    'sangre': '#8A0303', 'amatista': '#9966CC', 'cielo': '#87CEEB',
    'mar': '#006994', 'océano': '#006994', 'oceano': '#006994',
    'marino': '#000080', 'eléctrico': '#7DF9FF', 'electrico': '#7DF9FF',
    'neón': '#39FF14', 'neon': '#39FF14', 'bosque': '#228B22',
    'musgo': '#8A9A5B', 'hierba': '#7CFC00', 'pistacho': '#93C572',
    'manzana': '#8DB600', 'kiwi': '#8EE53F', 'selva': '#29AB87',
    'terracota': '#E2725B', 'caqui': '#F0E68C', 'siena': '#A0522D'
};

// ============================================
//  STATE
// ============================================

let connection = null;
let gameState = {
    codigo: null,
    esAnfitrion: false,
    miNombre: null,
    miId: null,
    jugadores: [],
    coloresUsados: [], // de esta partida
    coloresBloqueadosGlobales: [], // de la ronda general
    jugadorTurnoId: null,
    jugadorTurnoNombre: null,
    horaInicio: null,
    estadoRonda: 'EsperandoJugadores',
    estadoPartida: '',
    cronometroInterval: null,
    esMiTurno: false,
    esperandoSiguiente: false
};

// ============================================
//  SIGNALR CONNECTION
// ============================================

function initConnection() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/gameHub")
        .withAutomaticReconnect()
        .build();

    // Register event handlers
    connection.on("RondaCreada", onRondaCreada);
    connection.on("UnidoAPartida", onUnidoAPartida);
    connection.on("JugadorUnido", onJugadorUnido);
    connection.on("JugadorDesconectado", onJugadorDesconectado);
    connection.on("JuegoIniciado", onJuegoIniciado);
    connection.on("ColorAgregado", onColorAgregado);
    connection.on("TurnoCambiado", onTurnoCambiado);
    connection.on("JuegoTerminado", onJuegoTerminado);
    connection.on("EstadoActual", onEstadoActual);
    connection.on("RecordGuardado", onRecordGuardado);
    connection.on("RondaCerrada", onRondaCerrada);
    connection.on("Error", onError);

    connection.onreconnected(() => {
        showToast('Reconectado al servidor', 'success');
        if (gameState.codigo) {
            connection.invoke("ObtenerEstado", gameState.codigo, gameState.miNombre || "").catch(console.error);
        }
    });

    connection.onreconnecting(() => {
        showToast('Reconectando...', 'warning');
    });

    return connection.start().catch(err => {
        console.error('SignalR connection error:', err);
        showToast('Error al conectar con el servidor', 'error');
    });
}

// ============================================
//  PAGE ACTIONS (called from HTML)
// ============================================

async function crearPartida() {
    const nombre = document.getElementById('host-name')?.value?.trim();
    if (!nombre) {
        showToast('Ingresa tu nombre', 'error');
        shakeElement(document.getElementById('host-name'));
        return;
    }

    const btn = document.getElementById('btn-crear');
    btn.disabled = true;
    btn.textContent = 'Creando...';

    try {
        if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
            await initConnection();
        }
        gameState.miNombre = nombre;
        gameState.esAnfitrion = true;
        await connection.invoke("CrearRonda", nombre);
    } catch (err) {
        console.error(err);
        showToast('Error al crear partida', 'error');
        btn.disabled = false;
        btn.innerHTML = '<span class="btn-icon">🎮</span> Crear Partida';
    }
}

async function unirseAPartida() {
    const nombre = document.getElementById('join-name')?.value?.trim();
    const codigo = document.getElementById('join-code')?.value?.trim().toUpperCase();

    if (!nombre) {
        showToast('Ingresa tu nombre', 'error');
        shakeElement(document.getElementById('join-name'));
        return;
    }
    if (!codigo || codigo.length < 4) {
        showToast('Ingresa un código de invitación válido', 'error');
        shakeElement(document.getElementById('join-code'));
        return;
    }

    const btn = document.getElementById('btn-unirse');
    btn.disabled = true;
    btn.textContent = 'Uniéndose...';

    try {
        if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
            await initConnection();
        }
        gameState.miNombre = nombre;
        gameState.esAnfitrion = false;
        await connection.invoke("UnirseARonda", codigo, nombre);
    } catch (err) {
        console.error(err);
        showToast('Error al unirse', 'error');
        btn.disabled = false;
        btn.innerHTML = '<span class="btn-icon">🚀</span> Unirse';
    }
}

async function iniciarJuego() {
    if (!gameState.esAnfitrion || !gameState.codigo) return;

    const btn = document.getElementById('btn-iniciar');
    if (btn) {
        btn.disabled = true;
        btn.textContent = 'Iniciando...';
    }

    try {
        await connection.invoke("IniciarJuego", gameState.codigo);
    } catch (err) {
        console.error(err);
        showToast('Error al iniciar juego', 'error');
        if (btn) {
            btn.disabled = false;
            btn.innerHTML = '<span class="btn-icon">🚀</span> ¡Iniciar Juego!';
        }
    }
}

async function enviarColor() {
    const input = document.getElementById('color-input');
    const color = input?.value?.trim();

    if (!color) {
        showToast('Escribe un color', 'error');
        shakeElement(input);
        return;
    }

    const btn = document.getElementById('btn-enviar-color');
    btn.disabled = true;

    try {
        await connection.invoke("EnviarColor", gameState.codigo, color);
        input.value = '';
    } catch (err) {
        console.error(err);
        showToast('Error al enviar color', 'error');
    }

    btn.disabled = false;
}

function seleccionarColor(color) {
    const input = document.getElementById('color-input');
    if (input) {
        input.value = color;
        input.focus();
    }
}

async function siguienteTurno() {
    if (!gameState.esAnfitrion || !gameState.codigo) return;

    try {
        await connection.invoke("SiguienteTurno", gameState.codigo);
    } catch (err) {
        console.error(err);
        showToast('Error', 'error');
    }
}

function copiarCodigo() {
    const texto = document.getElementById('codigo-text')?.textContent;
    if (texto) {
        navigator.clipboard.writeText(texto).then(() => {
            showToast('¡Código copiado!', 'success');
        }).catch(() => {
            showToast('No se pudo copiar', 'error');
        });
    }
}

// Interacciones del Overlay de Leaderboard de Partida
async function guardarNombreRecord() {
    const input = document.getElementById('record-name');
    const nombre = input?.value?.trim();

    if (!nombre) {
        shakeElement(input);
        return;
    }

    document.getElementById('btn-guardar-record').disabled = true;
    try {
        await connection.invoke("GuardarRecord", gameState.codigo, nombre);
    } catch (error) {
        showToast("Error al guardar récord", "error");
        document.getElementById('btn-guardar-record').disabled = false;
    }
}

function onRecordGuardado(nombreEquipo) {
    document.getElementById('record-status').style.display = 'block';
    document.getElementById('btn-guardar-record').style.display = 'none';
    document.getElementById('record-name').disabled = true;
    showToast(`Récord guardado para ${nombreEquipo}`, 'success');
}

async function iniciarNuevaPartidaSala() {
    document.getElementById('btn-nueva-partida').disabled = true;
    document.getElementById('btn-cerrar-ronda').disabled = true;
    await iniciarJuego(); // Usa la misma función que el botón Iniciar Juego
}

async function cerrarRonda() {
    if (!confirm('¿Estás seguro que quieres finalizar y cerrar la sala para todos?')) return;
    
    document.getElementById('btn-cerrar-ronda').disabled = true;
    try {
        await connection.invoke("CerrarRonda", gameState.codigo);
    } catch (error) {
        console.error(error);
        document.getElementById('btn-cerrar-ronda').disabled = false;
    }
}

function onRondaCerrada() {
    showToast("La sala ha sido finalizada por el anfitrión.", "info");
    setTimeout(() => {
        window.location.href = "/";
    }, 2000);
}


// ============================================
//  EVENT HANDLERS (from SignalR)
// ============================================

function onRondaCreada(data) {
    gameState.codigo = data.codigo;
    gameState.jugadores = data.jugadores;
    window.location.href = `/Juego/Sala/${data.codigo}`;
}

function onUnidoAPartida(data) {
    gameState.codigo = data.codigo;
    gameState.jugadores = data.jugadores;
    window.location.href = `/Juego/Sala/${data.codigo}`;
}

function onJugadorUnido(data) {
    gameState.jugadores = data.jugadores;
    renderJugadoresSala(data.jugadores);
    document.getElementById('total-jugadores').textContent = data.totalJugadores;
    showToast(`${data.nombre} se unió a la partida`, 'info');
}

function onJugadorDesconectado(data) {
    gameState.jugadores = data.jugadores;
    renderJugadoresSala(data.jugadores);
    document.getElementById('total-jugadores').textContent = data.totalJugadores;
    showToast(`${data.nombre} se desconectó`, 'warning');
}

function onJuegoIniciado(data) {
    gameState.horaInicio = data.horaInicio;
    gameState.jugadorTurnoNombre = data.jugadorTurnoNombre;
    gameState.jugadorTurnoId = data.jugadorTurnoId;
    gameState.jugadores = data.jugadores;
    gameState.estadoRonda = 'Activa';
    gameState.estadoPartida = 'EnProgreso';
    gameState.esperandoSiguiente = false;
    
    if (data.coloresBloqueadosGlobales) {
        gameState.coloresBloqueadosGlobales = data.coloresBloqueadosGlobales;
    }

    // SI ya estamos en la page de partida, simplemente ocultamos el overlay y limpiamos UI
    if (window.location.pathname.toLowerCase().includes('/juego/partida')) {
        document.getElementById('game-over-overlay').style.display = 'none';
        document.getElementById('colores-grid').innerHTML = '<p class="empty-colors" id="empty-colors">Aún no se han dicho colores</p>';
        updatePuntuacion(data.puntuacion);
        startCronometro(data.horaInicio);
        updateTurnDisplay();
        renderJugadoresPartida();
        
        // Reset botones de modal
        if (document.getElementById('btn-nueva-partida')) {
            document.getElementById('btn-nueva-partida').disabled = false;
            document.getElementById('btn-cerrar-ronda').disabled = false;
            
            document.getElementById('record-status').style.display = 'none';
            document.getElementById('btn-guardar-record').style.display = 'block';
            document.getElementById('btn-guardar-record').disabled = false;
            if(document.getElementById('record-name')) {
                document.getElementById('record-name').disabled = false;
                document.getElementById('record-name').value = '';
            }
        }

        renderColoresQuemadosRonda(gameState.coloresBloqueadosGlobales);

    } else {
        // Redirigir si estamos en la recamara (sala)
        window.location.href = `/Juego/Partida/${gameState.codigo}`;
    }
}

function onColorAgregado(data) {
    gameState.coloresUsados = data.coloresUsados;
    gameState.coloresBloqueadosGlobales = data.coloresBloqueadosGlobales;
    gameState.esperandoSiguiente = true;

    renderColoresUsados(data.coloresUsados);
    updatePuntuacion(data.puntuacion);
    updatePaletaUsados(data.coloresUsados, gameState.coloresBloqueadosGlobales);

    // Show who said what
    showToast(`${data.jugadorNombre} dijo "${data.color}"`, 'success');

    // Muestra quemados si existen
    renderColoresQuemadosRonda(gameState.coloresBloqueadosGlobales);

    const inputSection = document.getElementById('color-input-section');
    if (inputSection) inputSection.style.display = 'none';

    if (gameState.esAnfitrion) {
        const siguienteSection = document.getElementById('siguiente-section');
        if (siguienteSection && data.turnosCompletados < data.totalJugadores) {
            siguienteSection.style.display = 'flex';
        }
    }

    updateJugadorChips();
}

function onTurnoCambiado(data) {
    gameState.jugadorTurnoNombre = data.jugadorNombre;
    gameState.jugadorTurnoId = data.jugadorId;
    gameState.esperandoSiguiente = false;

    const siguienteSection = document.getElementById('siguiente-section');
    if (siguienteSection) siguienteSection.style.display = 'none';

    updateTurnDisplay();
    showToast(`Turno de ${data.jugadorNombre}`, 'info');
}

function onJuegoTerminado(data) {
    gameState.estadoPartida = data.resultado;

    if (gameState.cronometroInterval) {
        clearInterval(gameState.cronometroInterval);
        gameState.cronometroInterval = null;
    }

    const cronoTime = document.getElementById('crono-time');
    if (cronoTime) cronoTime.classList.remove('crono-running');

    const inputSection = document.getElementById('color-input-section');
    if (inputSection) inputSection.style.display = 'none';
    const siguienteSection = document.getElementById('siguiente-section');
    if (siguienteSection) siguienteSection.style.display = 'none';

    showGameOverOverlay(data);
}

function onEstadoActual(data) {
    gameState.codigo = data.codigo;
    gameState.esAnfitrion = data.esAnfitrion;
    gameState.jugadores = data.jugadores;
    gameState.coloresUsados = data.coloresUsados;
    gameState.coloresBloqueadosGlobales = data.coloresBloqueadosGlobales || [];
    gameState.jugadorTurnoNombre = data.jugadorTurnoNombre;
    gameState.jugadorTurnoId = data.jugadorTurnoId;
    gameState.horaInicio = data.horaInicio;
    gameState.estadoRonda = data.estadoRonda;
    gameState.estadoPartida = data.estadoPartida;

    if (data.estadoRonda === 'Cerrada') {
        showToast("La ronda ha sido cerrada", "warning");
        return;
    }

    if (data.estadoRonda === 'EsperandoJugadores') {
        renderJugadoresSala(data.jugadores);
        if (document.getElementById('total-jugadores')) {
            document.getElementById('total-jugadores').textContent = data.totalJugadores;
        }
        if (data.esAnfitrion) {
            const btnIniciar = document.getElementById('btn-iniciar');
            if (btnIniciar) btnIniciar.style.display = 'inline-flex';
            const waitingText = document.getElementById('waiting-text');
            if (waitingText) waitingText.style.display = 'none';
        }
    } else if (data.estadoRonda === 'Activa' && data.estadoPartida === 'EnProgreso') {
        renderColoresUsados(data.coloresUsados);
        updatePuntuacion(data.puntuacion);
        updatePaletaUsados(data.coloresUsados, gameState.coloresBloqueadosGlobales);
        updateTurnDisplay();
        startCronometro(data.horaInicio);
        renderJugadoresPartida();
        renderColoresQuemadosRonda(gameState.coloresBloqueadosGlobales);
    }
}

function onError(message) {
    // Show user-friendly errors
    if(message === "SALA_NO_ENCONTRADA") {
        showToast("No tienes ninguna sesión abierta o la sala no existe", "error");
        setTimeout(() => { window.location.href = "/"; }, 2000);
        return;
    }
    
    showToast(message, 'error');

    // Re-enable buttons
    const btnCrear = document.getElementById('btn-crear');
    if (btnCrear) {
        btnCrear.disabled = false;
        btnCrear.innerHTML = '<span class="btn-icon">🎮</span> Crear Partida';
    }
    const btnUnirse = document.getElementById('btn-unirse');
    if (btnUnirse) {
        btnUnirse.disabled = false;
        btnUnirse.innerHTML = '<span class="btn-icon">🚀</span> Unirse';
    }
    const btnIniciar = document.getElementById('btn-iniciar');
    if (btnIniciar) {
        btnIniciar.disabled = false;
        btnIniciar.innerHTML = '<span class="btn-icon">🚀</span> ¡Iniciar Juego!';
    }
    const btnEnviar = document.getElementById('btn-enviar-color');
    if (btnEnviar) btnEnviar.disabled = false;
}

// ============================================
//  UI RENDERERS
// ============================================

function renderJugadoresSala(jugadores) {
    const lista = document.getElementById('lista-jugadores');
    if (!lista) return;

    lista.innerHTML = '';
    jugadores.forEach(j => {
        const li = document.createElement('li');
        li.innerHTML = `
            <span class="player-crown">${j.esAnfitrion ? '👑' : '👤'}</span>
            <span>${j.nombre}</span>
            ${j.esAnfitrion ? '<span style="font-size:0.75rem;color:var(--accent-gold);margin-left:auto;">Anfitrión</span>' : ''}
        `;
        lista.appendChild(li);
    });
}

function renderColoresQuemadosRonda(bloqueados) {
    const sect = document.getElementById('quemados-section');
    const span = document.getElementById('quemados-lista');
    if(!sect || !span) return;
    
    if (bloqueados && bloqueados.length > 0) {
        sect.style.display = 'block';
        span.textContent = bloqueados.map(c => c.charAt(0).toUpperCase() + c.slice(1)).join(', ');
    } else {
        sect.style.display = 'none';
        span.textContent = '';
    }
}

function renderColoresUsados(colores) {
    const grid = document.getElementById('colores-grid');
    if (!grid) return;

    const emptyMsg = document.getElementById('empty-colors');
    if (emptyMsg) emptyMsg.style.display = colores.length > 0 ? 'none' : 'block';

    grid.innerHTML = '';
    colores.forEach((color, idx) => {
        const jugador = gameState.jugadores?.find(j => {
            const jColor = j.color || j.colorDicho;
            return jColor && jColor.toLowerCase() === color.toLowerCase();
        });

        const hexColor = getColorHex(color);
        const chip = document.createElement('div');
        chip.className = 'color-chip';
        chip.innerHTML = `
            <span class="color-chip-dot" style="background: ${hexColor}"></span>
            <span class="color-chip-name">${color}</span>
            ${jugador ? `<span class="color-chip-player">— ${jugador.nombre}</span>` : ''}
        `;
        grid.appendChild(chip);
    });
}

function updatePuntuacion(puntuacion) {
    const el = document.getElementById('punt-value');
    if (el) el.textContent = puntuacion;
}

function updateTurnDisplay() {
    const turnoNombre = document.getElementById('turno-nombre');
    const turnoBadge = document.getElementById('turno-badge');
    const inputSection = document.getElementById('color-input-section');
    const siguienteSection = document.getElementById('siguiente-section');

    if (turnoNombre) {
        turnoNombre.textContent = gameState.jugadorTurnoNombre || '—';
    }

    const miJugador = gameState.jugadores?.find(j =>
        j.nombre.toLowerCase() === gameState.miNombre?.toLowerCase()
    );

    gameState.esMiTurno = miJugador && gameState.jugadorTurnoId === (miJugador.id || miJugador.Id);

    if (turnoBadge) {
        turnoBadge.style.display = gameState.esMiTurno ? 'block' : 'none';
    }

    if (inputSection) {
        inputSection.style.display = gameState.esMiTurno ? 'block' : 'none';
        if(gameState.esMiTurno) {
            setTimeout(() => document.getElementById('color-input')?.focus(), 100);
        }
    }

    if (siguienteSection) {
        siguienteSection.style.display = 'none'; 
    }

    updateJugadorChips();
}

function updatePaletaUsados(coloresUsados, coloresGlobalesQuemados) {
    const paletteBtns = document.querySelectorAll('.color-btn');
    const usadosLower = coloresUsados.map(c => c.toLowerCase());
    const globalesLower = (coloresGlobalesQuemados || []).map(c => c.toLowerCase());

    paletteBtns.forEach(btn => {
        const color = btn.getAttribute('data-color');
        if (!color) return;
        
        btn.classList.remove('used', 'burned');
        const colorLower = color.toLowerCase();
        
        if (usadosLower.includes(colorLower)) {
            btn.classList.add('used'); // Usado por alguien en ESTA partida
        } else if (globalesLower.includes(colorLower)) {
            btn.classList.add('burned'); // Prohibido por usarlo en partidas anteriores
            // Optional: style it darker or with a strike-through via CSS
            btn.style.opacity = '0.3';
            btn.style.textDecoration = 'line-through';
        } else {
            btn.style.opacity = '1';
            btn.style.textDecoration = 'none';
        }
    });
}

function renderJugadoresPartida() {
    const grid = document.getElementById('jugadores-grid');
    if (!grid || !gameState.jugadores) return;

    grid.innerHTML = '';
    gameState.jugadores.forEach(j => {
        const isActive = gameState.jugadorTurnoId === (j.id || j.Id);
        const isDone = j.yaTuvoTurno || j.colorDicho || j.color;
        const chip = document.createElement('div');
        chip.className = `jugador-chip ${isActive ? 'active-turn' : ''} ${isDone ? 'completed' : ''}`;
        chip.innerHTML = `
            <span class="jugador-chip-icon">${j.esAnfitrion ? '👑' : '👤'}</span>
            <span class="jugador-chip-name">${j.nombre}</span>
            <span class="jugador-chip-status">${isDone ? (j.color || j.colorDicho || '✓') : isActive ? '🎯' : '⏳'}</span>
        `;
        grid.appendChild(chip);
    });
}

function updateJugadorChips() {
    renderJugadoresPartida();
}

function showGameOverOverlay(data) {
    const overlay = document.getElementById('game-over-overlay');
    const card = document.getElementById('game-over-card');
    const iconEl = document.getElementById('game-over-icon');
    const titleEl = document.getElementById('game-over-title');
    const messageEl = document.getElementById('game-over-message');
    const motivoEl = document.getElementById('game-over-motivo');
    const puntEl = document.getElementById('final-puntuacion');
    const tiempoEl = document.getElementById('final-tiempo');
    const resumenLista = document.getElementById('resumen-lista');

    // Controls
    const hostActions = document.getElementById('host-actions');
    const guestActions = document.getElementById('guest-actions');
    const recordSection = document.getElementById('record-section');
    const btnVerHistorial = document.getElementById('btn-ver-historial');

    if (!overlay) return;

    const isVictoria = data.resultado === 'victoria';

    card.className = `game-over-card ${isVictoria ? 'victoria' : 'derrota'}`;
    iconEl.textContent = isVictoria ? '🏆' : '💔';
    titleEl.textContent = isVictoria ? '¡VICTORIA!' : '¡Derrota!';
    messageEl.textContent = data.mensaje;
    puntEl.textContent = data.puntuacion;
    tiempoEl.textContent = `${data.tiempoSegundos}s`;
    
    if (motivoEl) {
        motivoEl.textContent = data.motivo || "";
    }

    resumenLista.innerHTML = '';
    if (data.resumen) {
        data.resumen.forEach(j => {
            const item = document.createElement('div');
            item.className = 'resumen-item';
            item.innerHTML = `
                <span class="resumen-nombre">${j.esAnfitrion ? '👑' : '👤'} ${j.nombre}</span>
                <span class="resumen-color">${j.color || '—'}</span>
            `;
            resumenLista.appendChild(item);
        });
    }
    
    // Configurar layout para Host o Invitado
    if (gameState.esAnfitrion) {
        if (hostActions) hostActions.style.display = 'flex';
        if (guestActions) guestActions.style.display = 'none';
        
        if (isVictoria && data.requiereNombreRecord) {
            recordSection.style.display = 'block';
            setTimeout(()=> document.getElementById('record-name')?.focus(), 200);
        } else {
            recordSection.style.display = 'none';
        }
    } else {
        if (hostActions) hostActions.style.display = 'none';
        if (guestActions) guestActions.style.display = 'flex';
        recordSection.style.display = 'none';
    }

    // Hide old return btn if exists
    if(btnVerHistorial && gameState.esAnfitrion) {
        btnVerHistorial.style.display = 'none';
    }

    overlay.style.display = 'flex';

    if (isVictoria) {
        launchConfetti();
    }
}

// ============================================
//  CRONÓMETRO
// ============================================

function startCronometro(horaInicioMs) {
    if (gameState.cronometroInterval) {
        clearInterval(gameState.cronometroInterval);
    }

    const cronoEl = document.getElementById('crono-time');
    if (!cronoEl) return;

    cronoEl.classList.add('crono-running');

    gameState.cronometroInterval = setInterval(() => {
        const now = Date.now();
        const elapsed = Math.floor((now - horaInicioMs) / 1000);
        const mins = Math.floor(elapsed / 60).toString().padStart(2, '0');
        const secs = (elapsed % 60).toString().padStart(2, '0');
        cronoEl.textContent = `${mins}:${secs}`;
    }, 250);
}

// ============================================
//  TOASTS & EFFECTS
// ============================================

function showToast(message, type = 'info') {
    const container = document.getElementById('toast-container');
    if (!container) return;
    const icons = { success: '✅', error: '❌', warning: '⚠️', info: '💬' };
    const toast = document.createElement('div');
    toast.className = `toast toast-${type}`;
    toast.innerHTML = `<span class="toast-icon">${icons[type] || '💬'}</span><span>${message}</span>`;
    container.appendChild(toast);
    setTimeout(() => {
        toast.classList.add('toast-exit');
        setTimeout(() => toast.remove(), 300);
    }, 3500);
}

function shakeElement(el) {
    if (!el) return;
    el.style.animation = 'none';
    el.offsetHeight; 
    el.style.animation = 'shake 0.5s';
    setTimeout(() => { el.style.animation = ''; }, 500);
}

const shakeStyle = document.createElement('style');
shakeStyle.textContent = `
    @keyframes shake {
        0%, 100% { transform: translateX(0); }
        20% { transform: translateX(-8px); }
        40% { transform: translateX(8px); }
        60% { transform: translateX(-6px); }
        80% { transform: translateX(6px); }
    }
`;
document.head.appendChild(shakeStyle);

function launchConfetti() {
    const colors = ['#6c5ce7', '#00cec9', '#fd79a8', '#fdcb6e', '#00b894', '#e74c3c', '#a29bfe', '#fab1a0'];
    for (let i = 0; i < 80; i++) {
        const piece = document.createElement('div');
        piece.className = 'confetti-piece';
        piece.style.left = Math.random() * 100 + 'vw';
        piece.style.width = (Math.random() * 10 + 5) + 'px';
        piece.style.height = (Math.random() * 10 + 5) + 'px';
        piece.style.background = colors[Math.floor(Math.random() * colors.length)];
        piece.style.animationDuration = (Math.random() * 2 + 2) + 's';
        piece.style.animationDelay = (Math.random() * 1.5) + 's';
        piece.style.borderRadius = Math.random() > 0.5 ? '50%' : '2px';
        document.body.appendChild(piece);
        setTimeout(() => piece.remove(), 5000);
    }
}

function createParticles() {
    const container = document.getElementById('particles');
    if (!container) return;

    const colors = ['#6c5ce7', '#00cec9', '#fd79a8', '#fdcb6e', '#00b894'];
    for (let i = 0; i < 25; i++) {
        const particle = document.createElement('div');
        particle.className = 'particle';
        particle.style.left = Math.random() * 100 + 'vw';
        particle.style.width = (Math.random() * 8 + 4) + 'px';
        particle.style.height = particle.style.width;
        particle.style.background = colors[Math.floor(Math.random() * colors.length)];
        particle.style.animationDuration = (Math.random() * 10 + 8) + 's';
        particle.style.animationDelay = (Math.random() * 10) + 's';
        container.appendChild(particle);
    }
}

function getColorHex(colorName) {
    return COLOR_HEX_MAP[colorName.toLowerCase().trim()] || '#888888';
}

document.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') {
        if (document.getElementById('host-name') === document.activeElement) crearPartida();
        if (document.getElementById('join-code') === document.activeElement ||
            document.getElementById('join-name') === document.activeElement) unirseAPartida();
        if (document.getElementById('color-input') === document.activeElement) enviarColor();
        if (document.getElementById('record-name') === document.activeElement) guardarNombreRecord();
    }
});

// ============================================
//  PAGE INITIALIZATION
// ============================================

document.addEventListener('DOMContentLoaded', () => {
    createParticles();
    
    if (window.SALA_CODIGO || window.PARTIDA_CODIGO) {
        const codigo = window.SALA_CODIGO || window.PARTIDA_CODIGO;
        gameState.codigo = codigo;

        const sessionData = sessionStorage.getItem('gameState_' + codigo);
        if (sessionData) {
            const saved = JSON.parse(sessionData);
            gameState.miNombre = saved.miNombre;
            gameState.esAnfitrion = saved.esAnfitrion;
        }

        initConnection().then(() => {
            // El backend necesita nombre de jugador para reconectar el ID si cambió
            connection.invoke("ObtenerEstado", codigo, gameState.miNombre || "").catch(console.error);
        });
    }
});

// Wrappers para guardar sesion
const originalOnRondaCreada = onRondaCreada;
onRondaCreada = function(data) {
    gameState.codigo = data.codigo;
    sessionStorage.setItem('gameState_' + data.codigo, JSON.stringify({
        miNombre: gameState.miNombre,
        esAnfitrion: true
    }));
    originalOnRondaCreada(data);
};

const originalOnUnidoAPartida = onUnidoAPartida;
onUnidoAPartida = function(data) {
    gameState.codigo = data.codigo;
    sessionStorage.setItem('gameState_' + data.codigo, JSON.stringify({
        miNombre: gameState.miNombre,
        esAnfitrion: false
    }));
    originalOnUnidoAPartida(data);
};
