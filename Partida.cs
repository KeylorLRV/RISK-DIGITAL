using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using UnityEngine.SceneManagement;
using System.Linq;
using System;
using TMPro;
using System.Runtime.InteropServices.WindowsRuntime;
using Unity.Networking.Transport; // Para NetworkConnection

public class Partida : MonoBehaviour
{
    // Enums
    public enum EstadoPartida { EnCurso, Finalizada }
    public enum FaseTurno { Refuerzos, Ataque, Planeacion }

    // Atributos
    public static Partida instance;
    public EstadoPartida estado;
    public FaseTurno faseActual;
    public Jugador jugadorEnTurno;
    public ListaArray<Jugador> jugadores;
    public Jugador ejercitoNeutral;
    public Mapa mapa;
    public Jugador localPlayer;
    public int contadorRonda;
    private DistributionManager distributionManager;
    public int tropasDisponiblesParaRefuerzo;
    public PlayerInfoUI1 playerInfoUI1; // Referencia al UI del jugador
    public PlayerInfoUI2 playerInfoUI2; // Referencia al UI del jugador
    public CombateManager combateManager; // Referencia al CombateManager
    public TextMeshProUGUI jugadoractual;

    // Variables para la lógica de UI y selección
    public Territorio territorioSeleccionadoParaRefuerzo;
    public Territorio territorioSeleccionadoParaAtaqueOrigen; // Renombrado para claridad
    public Territorio territorioSeleccionadoParaAtaqueDestino; // Nuevo para el territorio a atacar
    public Territorio territorioSeleccionadoParaMovimientoOrigen;
    public Territorio territorioSeleccionadoParaMovimientoDestino;

    // Referencia al AttackPanel
    public AttackPanel attackPanel;
    public DadoManager dado;
    public InfoGUI infoGUI;



    public int Refuerzos; // Refuerzos calculados al inicio del turno

    // Atributos del GameManager original que se mantienen si son relevantes para la Partida
    public string attackedTerritoryName;
    public bool battleHasEnded;
    public bool battleWon;
    public bool isJoined = false;  // Inicial false; true después de NetSyncAssignedPlayers.
    public bool IsJoined { get { return isJoined; } }

    [System.Serializable]
    public class SaveData
    {
        public ListaArray<Territorio> savedTerritorios = new ListaArray<Territorio>();
        public ListaArray<Jugador> savedJugadores = new ListaArray<Jugador>();
        public int currentJugadorIndex;
        public int currentContadorRonda;
        public EstadoPartida currentEstadoPartida;
        public FaseTurno currentFaseTurno;
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"Partida duplicada detectada: {this.name}, destruyendo instancia.");
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        jugadores = new ListaArray<Jugador>();

        Debug.Log($"Partida singleton creado: {this.name}");
    }

    void Start()
    {
        Debug.Log("=== START PARTIDA (Multijugador) ===");

        // 1. Inicialización del Mapa (solo la instancia, no la lógica pesada)
        // El Mapa.instance ya se inicializa en su propio Awake/Start y se hace DontDestroyOnLoad.
        // Aquí solo nos aseguramos de tener la referencia.
        if (Mapa.instance == null)
        {
            Debug.LogError("Mapa.instance es NULL. Asegúrate de que el GO con Mapa esté en la escena y activo.");
            // Considera cargar la escena del mapa si no está presente, o mostrar un error fatal.
            return;
        }
        this.mapa = Mapa.instance;
        Debug.Log("Mapa asignado correctamente: " + mapa.name);

        // 2. Creación de Jugadores (solo los objetos Jugador, sin asignación de territorios)
        // Estos jugadores se crearán en el servidor y se sincronizarán con los clientes.
        // Para el host/servidor, se crean aquí. Para los clientes, se recibirán por red.
        try
        {
            // Estos jugadores se crearán en el servidor. Los clientes los recibirán por red.
            // Para una partida de 2 jugadores + neutral, se pueden predefinir.
            // En un juego real, los jugadores se crearían dinámicamente al conectarse los clientes.
            Jugador jugador1 = new Jugador("Player1", new Color32(0, 0, 255, 255)); // Azul
            Jugador jugador2 = new Jugador("Player2", new Color32(255, 0, 0, 255)); // Rojo
            ejercitoNeutral = new Jugador("Neutral", new Color32(150, 150, 150, 255)); // Gris

            jugadores.Agregar(jugador1);
            jugadores.Agregar(jugador2);
            // El ejército neutral se agrega a la lista de jugadores para facilitar la iteración,
            // pero no será un "jugador en turno" normal.
            jugadores.Agregar(ejercitoNeutral); // Asegúrate de que el neutral esté en la lista para referencias
            Debug.Log("Jugadores iniciales creados: " + jugadores.Contar() + " (incluyendo neutral).");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error creando jugadores iniciales: " + ex.Message);
            return;
        }

        // 3. Inicialización de estado básico de la partida
        contadorRonda = 0;
        estado = EstadoPartida.EnCurso; // O en un estado "EsperandoJugadores"
        faseActual = FaseTurno.Refuerzos; // Se establecerá correctamente al iniciar la partida

        // 4. Manejo de carga de partida (opcional, si quieres permitir cargar partidas multijugador)
        // Para simplificar el inicio multijugador, a menudo se empieza de cero.
        string savePath = Application.persistentDataPath + "/SaveFile.json";
        if (File.Exists(savePath))
        {
            Debug.Log("Archivo de guardado encontrado. Eliminando para depuración en multijugador...");
            File.Delete(savePath); // Eliminar para asegurar un inicio limpio en multijugador
        }

        // La lógica de `Loading()` y `IniciarPartida()` se moverá a un método explícito
        // que el servidor llamará cuando la partida deba comenzar.
        // Por ahora, `jugadorEnTurno` será null hasta que se inicie la partida.
        jugadorEnTurno = null; // No hay jugador en turno hasta que la partida comience.

        // La lógica de `battleHasEnded` y `battleWon` también se manejará dentro del flujo de juego
        // y no en el Start() de la Partida.

        Debug.Log("=== FIN START PARTIDA (Multijugador) ===");
        // El servidor será responsable de llamar a IniciarPartidaMultijugador()
        // cuando todos los clientes estén conectados y listos.
    }

    // Este método será llamado por el SERVIDOR cuando la partida deba comenzar.
    // Por ejemplo, después de que todos los clientes se hayan conectado y el host presione "Iniciar".
    public void IniciarPartidaMultijugador()
    {
        Debug.Log("=== INICIANDO PARTIDA MULTIJUGADOR ===");

        // Asegurarse de que el mapa esté completamente inicializado antes de distribuir territorios
        // La inicialización pesada del mapa (creación de territorios, carga de adyacencias)
        // ya debería haber ocurrido en el Start() de Mapa.cs.
        // Aquí solo verificamos que esté listo.
        if (mapa == null || mapa.territorios.Contar() == 0)
        {
            Debug.LogError("Mapa no inicializado o sin territorios. No se puede iniciar la partida.");
            return;
        }

        estado = EstadoPartida.EnCurso;
        DistribuirTerritoriosIniciales(); // Esta lógica ahora es parte del inicio de la partida.

        // Asignar el primer jugador en turno
        // Asegúrate de que 'jugadores' contenga solo los jugadores activos (no el neutral)
        // o ajusta el índice para que el neutral no sea el primer turno.
        jugadorEnTurno = jugadores.Obtener(0); // Asume que el primer elemento es Player1

        faseActual = FaseTurno.Refuerzos;
        contadorRonda = 1; // La primera ronda comienza aquí.

        Debug.Log("Partida multijugador iniciada. Jugador en turno: " + jugadorEnTurno.alias + ", Fase: " + faseActual);

        // Calcular y asignar refuerzos iniciales al primer jugador
        Refuerzos = CalcularRefuerzos(jugadorEnTurno);
        jugadorEnTurno.RecibirRefuerzos(Refuerzos);
        tropasDisponiblesParaRefuerzo = jugadorEnTurno.tropasDisponibles;

        // Actualizar la visualización del mapa y la UI para todos los clientes
        mapa.TintTerritorios();
        mapa.ActualizarVisualTropasEnMapa();
        ActualizarJugadorEnTurno(jugadorEnTurno); // Actualiza la UI del jugador en turno
        if (jugadoractual != null)
            jugadoractual.text = jugadorEnTurno.alias;

        Saving(); // Guardar el estado inicial de la partida en el servidor

        // Sincronizar el estado inicial con TODOS los clientes
        // El servidor debe enviar mensajes de sincronización completos.
        // 1. Enviar datos de todos los jugadores
        var syncPlayersMsg = new NetSyncPlayersData();
        // Necesitarás llenar syncPlayersMsg.allPlayersData con los datos de tus jugadores
        // (alias, color, etc., excluyendo el neutral si no quieres que los clientes lo vean como un jugador normal)
        // Puedes crear una lista de JugadorSaveData para esto.
        syncPlayersMsg.allPlayersData = new JugadorSaveData[jugadores.Contar()];
        for (int i = 0; i < jugadores.Contar(); i++)
        {
            Jugador j = jugadores.Obtener(i);
            syncPlayersMsg.allPlayersData[i] = new JugadorSaveData
            {
                alias = j.alias,
                colorR = j.color.r,
                colorG = j.color.g,
                colorB = j.color.b,
                colorA = j.color.a
            };
        }
        NetworkSender.SendMessage(syncPlayersMsg);


        // 2. Enviar datos de todos los territorios (propietario, tropas)
        var syncMapMsg = new NetSyncMapData();
        syncMapMsg.allTerritoriesData = new TerritorioSaveData[mapa.territorios.Contar()];
        for (int i = 0; i < mapa.territorios.Contar(); i++)
        {
            Territorio t = mapa.territorios.Obtener(i);
            syncMapMsg.allTerritoriesData[i] = new TerritorioSaveData
            {
                nombre = t.nombre,
                cantidadTropas = t.cantidadTropas,
                propietarioAlias = t.jugadorPropietario?.alias ?? "Neutral"
            };
        }
        NetworkSender.SendMessage(syncMapMsg);

        // 3. Enviar el estado actual del juego (jugador en turno, fase, ronda)
        var syncGameStateMsg = new NetSyncCurrentGameState
        {
            currentPlayerAlias = jugadorEnTurno.alias,
            currentPhase = (int)faseActual,
            currentRound = contadorRonda
        };
        NetworkSender.SendMessage(syncGameStateMsg);

        Debug.Log("=== FIN INICIANDO PARTIDA MULTIJUGADOR ===");
    }

    public void IniciarPartida()
    {
        Debug.Log("Iniciando...");
        estado = EstadoPartida.EnCurso;
        DistribuirTerritoriosIniciales();
        jugadorEnTurno = jugadores.Obtener(0);
        faseActual = FaseTurno.Refuerzos;
        Debug.Log("Partida iniciada. Jugador en turno: " + jugadorEnTurno.alias + ", Fase: " + faseActual);
        Refuerzos = CalcularRefuerzos(jugadorEnTurno);
        jugadorEnTurno.RecibirRefuerzos(Refuerzos);
        tropasDisponiblesParaRefuerzo = jugadorEnTurno.tropasDisponibles;
        mapa.TintTerritorios();
        ActualizarJugadorEnTurno(jugadorEnTurno);
        Saving();
        // Enviar mensaje de inicio de partida
        var startGameMsg = new NetStartGame();
        NetworkSender.SendMessage(startGameMsg);
        // Enviar estado inicial del turno
        var nextTurnMsg = new NetNextTurn
        {
            playerAlias = jugadorEnTurno.alias,
            fase = (int)faseActual,
            roundCounter = contadorRonda
        };
        NetworkSender.SendMessage(nextTurnMsg);
    }

    public void SiguienteTurno()
    {
        VerificarVictoria();

        if (estado == EstadoPartida.Finalizada)
        {
            Debug.Log("¡Partida Finalizada! Ganador: " + jugadorEnTurno.alias);
            // Podrías enviar un mensaje de fin de partida aquí
            return;
        }

        int currentIndex = -1;
        for (int i = 0; i < jugadores.Contar(); i++)
        {
            if (jugadores.Obtener(i) == jugadorEnTurno)
            {
                currentIndex = i;
                break;
            }
        }

        int nextIndex = (currentIndex + 1) % jugadores.Contar();
        jugadorEnTurno = jugadores.Obtener(nextIndex);
        faseActual = FaseTurno.Refuerzos;

        if (nextIndex == 0)
        {
            contadorRonda++;
            Debug.Log("Nueva Ronda: " + contadorRonda);
        }

        Refuerzos = CalcularRefuerzos(jugadorEnTurno);
        jugadorEnTurno.RecibirRefuerzos(Refuerzos);
        tropasDisponiblesParaRefuerzo = jugadorEnTurno.tropasDisponibles;
        Debug.Log("Siguiente turno. Jugador en turno: " + jugadorEnTurno.alias + ", Fase: " + faseActual);
        Mapa.instance.TintTerritorios();
        ActualizarJugadorEnTurno(jugadorEnTurno);
        jugadoractual.text = jugadorEnTurno.alias;
        Saving();

        // Enviar mensaje de siguiente turno
        var nextTurnMsg = new NetNextTurn
        {
            playerAlias = jugadorEnTurno.alias,
            fase = (int)faseActual,
            roundCounter = contadorRonda
        };
        NetworkSender.SendMessage(nextTurnMsg);
    }


    public void SiguienteFase()
    {
        // NUEVO: Guards estrictos al inicio – no avanzar/enviar si no joined o desconectado.
        if (!isJoined)
        {
            Debug.LogWarning("[Partida] No se puede avanzar fase: Jugador no joined (espera sync del servidor).");
            return;  // Salta todo: No procesa switch, saving ni envío.
        }

        if (Client.Instance == null || !Client.Instance.IsConnected)
        {
            Debug.LogWarning("[Partida] No se puede avanzar fase: Cliente desconectado.");
            return;  // Salta todo.
        }

        // Tu switch original (sin cambios): Maneja transiciones de fase.
        switch (faseActual)
        {
            case FaseTurno.Refuerzos:
                faseActual = FaseTurno.Ataque;
                Debug.Log("Fase: Ataque");
                break;
            case FaseTurno.Ataque:
                territorioSeleccionadoParaAtaqueOrigen = null;
                territorioSeleccionadoParaAtaqueDestino = null;
                faseActual = FaseTurno.Planeacion;
                Debug.Log("Fase: Planeación");
                break;
            case FaseTurno.Planeacion:
                Debug.Log("Fin de turno. Pasando al siguiente jugador.");
                SiguienteTurno();
                return;  // Ya envía mensaje en SiguienteTurno – salta saving/envío de fase.
        }

        // Tu código original: Saving y envío (ahora solo si guards pasan y no return).
        Saving();

        // Enviar mensaje de cambio de fase (solo para Refuerzos/Ataque, no Planeacion).
        var nextPhaseMsg = new NetNextPhase
        {
            newPhase = (int)faseActual  // Tu data original.
        };
        NetworkSender.SendMessage(nextPhaseMsg);

        // NUEVO: Log opcional para confirmar (útil para depuración).
        Debug.Log($"[Partida] Fase avanzada a {faseActual} y enviada al servidor (joined: {isJoined}).");
    }


    public void DistribuirTerritoriosIniciales()
    {
        Debug.Log("=== INICIANDO DISTRIBUCIÓN DE TERRITORIOS ===");

        ListaArray<Territorio> todosLosTerritorios = mapa.GetTodosTerritorios();
        if (todosLosTerritorios == null || todosLosTerritorios.Contar() == 0)
        {
            Debug.LogError("todosLosTerritorios es null o vacío! No se puede distribuir.");
            return;
        }
        Debug.Log("Territorios obtenidos: " + todosLosTerritorios.Contar());

        Shuffle(todosLosTerritorios);
        Debug.Log("Lista mezclada correctamente.");

        // Preparar lista de jugadores + neutral
        ListaArray<Jugador> propietariosPotenciales = new ListaArray<Jugador>();
        for (int i = 0; i < jugadores.Contar(); i++)
        {
            Jugador j = jugadores.Obtener(i);
            if (j.territoriosConquistados == null)
                j.territoriosConquistados = new ListaArray<Territorio>();
            else
                j.territoriosConquistados = new ListaArray<Territorio>(); // Limpiar lista

            propietariosPotenciales.Agregar(j);
            Debug.Log("Agregado jugador a potenciales: " + j.alias);
        }
        if (ejercitoNeutral.territoriosConquistados == null)
            ejercitoNeutral.territoriosConquistados = new ListaArray<Territorio>();
        else
            ejercitoNeutral.territoriosConquistados = new ListaArray<Territorio>(); // Limpiar lista

        propietariosPotenciales.Agregar(ejercitoNeutral);
        Debug.Log("Propietarios potenciales: " + propietariosPotenciales.Contar() + " (incluyendo neutral).");

        int propietarioIndex = 0;
        int asignadosPlayer1 = 0, asignadosPlayer2 = 0, asignadosNeutral = 0;
        int nulls = 0;

        // Asignar territorios alternadamente
        for (int i = 0; i < todosLosTerritorios.Contar(); i++)
        {
            Territorio t = todosLosTerritorios.Obtener(i);
            if (t == null)
            {
                Debug.LogWarning("Territorio null en índice " + i + ". Saltando asignación.");
                nulls++;
                continue;
            }

            Jugador p = propietariosPotenciales.Obtener(propietarioIndex);
            if (p == null)
            {
                Debug.LogError("Propietario null en índice " + propietarioIndex + ". Saltando.");
                continue;
            }

            t.jugadorPropietario = p;
            t.cantidadTropas = 1; // Inicialmente 1 tropa
            p.territoriosConquistados.Agregar(t);

            if (p.alias == "Player1") asignadosPlayer1++;
            else if (p.alias == "Player2") asignadosPlayer2++;
            else if (p.alias == "Neutral") asignadosNeutral++;

            Debug.Log($"Asignado territorio {i} ('{t.nombre}') a {p.alias} (tropas: 1). Total para {p.alias}: {p.territoriosConquistados.Contar()}");

            propietarioIndex = (propietarioIndex + 1) % propietariosPotenciales.Contar();
        }

        Debug.Log($"Distribución completada: Player1={asignadosPlayer1}, Player2={asignadosPlayer2}, Neutral={asignadosNeutral}, Nulls={nulls}. Total asignados: {asignadosPlayer1 + asignadosPlayer2 + asignadosNeutral}");

        // Ahora distribuir tropas para cada jugador (excluyendo neutral si quieres)
        for (int i = 0; i < jugadores.Contar(); i++)
        {
            Jugador jugador = jugadores.Obtener(i);
            if (jugador.territoriosConquistados != null && jugador.territoriosConquistados.Contar() > 0)
            {
                DistribuirTropasJugador(jugador.territoriosConquistados, 40);
                Debug.Log($"Tropas distribuidas para {jugador.alias} en {jugador.territoriosConquistados.Contar()} territorios.");
            }
        }

        // Opcional: distribuir tropas para neutral si quieres (puedes usar otro número o no distribuir)
        if (ejercitoNeutral.territoriosConquistados != null && ejercitoNeutral.territoriosConquistados.Contar() > 0)
        {
            DistribuirTropasJugador(ejercitoNeutral.territoriosConquistados, 40);
            Debug.Log($"Tropas distribuidas para Neutral en {ejercitoNeutral.territoriosConquistados.Contar()} territorios.");
        }

        for (int i = 0; i < jugadores.Contar(); i++)
        {
            jugadores.Obtener(i).ReestablecerReferenciasInversas();
        }
        ejercitoNeutral.ReestablecerReferenciasInversas();
        Debug.Log("Referencias inversas reestablecidas.");

        VerificarAsignacionesPostDistribucion();

        Mapa.instance.TintTerritorios();
        Mapa.instance.ActualizarVisualTropasEnMapa();
        Debug.Log("TintTerritorios() llamado después de distribución. Verifica logs de TintTerritorios para propietarios.");
    }

    /// <summary>
    /// Distribuye tropas entre los territorios de un jugador, asignando entre 1 y 3 tropas por territorio,
    /// sin exceder el total de tropas disponibles.
    /// </summary>
    private void DistribuirTropasJugador(ListaArray<Territorio> territorios, int tropasDisponibles)
    {
        int territoriosCount = territorios.Contar();
        int tropasRestantes = tropasDisponibles;

        System.Random rnd = new System.Random();

        for (int i = 0; i < territoriosCount; i++)
        {
            // Tropas máximas que podemos asignar en este territorio para no pasarnos
            // Consideramos que cada territorio debe tener al menos 1 tropa
            int maxAsignar = Mathf.Min(3, tropasRestantes - (territoriosCount - i - 1));
            maxAsignar = Mathf.Max(maxAsignar, 1);

            int tropasAsignar = rnd.Next(1, maxAsignar + 1);

            // Asignamos tropas al territorio
            territorios.Obtener(i).cantidadTropas = tropasAsignar;

            tropasRestantes -= tropasAsignar;

            if (tropasRestantes <= 0 && i < territoriosCount - 1)
            {
                for (int j = i + 1; j < territoriosCount; j++)
                {
                    territorios.Obtener(j).cantidadTropas = 1;
                }
                break;
            }
        }
    }



    private void SincronizarHandlersConTerritoriosGlobales()
    {
        if (mapa == null || mapa.territorioHandlers == null)
        {
            Debug.LogError("mapa o territorioHandlers es null en SincronizarHandlers. Saltando sincronización.");
            return;
        }

        Debug.Log("Sincronizando handlers con territorios globales...");
        int sincronizados = 0;
        int desincronizados = 0;
        int totalHandlers = mapa.territorioHandlers.Contar();

        for (int i = 0; i < totalHandlers; i++)
        {
            GameObject go = mapa.territorioHandlers.Obtener(i);
            if (go == null)
            {
                Debug.LogWarning("GameObject null en territorioHandlers índice " + i + ". Saltando.");
                continue;
            }

            TerritorioHandler handler = go.GetComponent<TerritorioHandler>();
            if (handler == null)
            {
                Debug.LogWarning("TerritorioHandler null en " + go.name + " (índice " + i + "). Saltando.");
                continue;
            }

            if (handler.territorio == null)
            {
                Debug.LogWarning("Territorio null en handler de " + go.name + ". Saltando sincronización.");
                desincronizados++;
                continue;
            }

            Territorio globalTerritorio = mapa.GetTerritorioPorNombre(handler.territorio.nombre);
            if (globalTerritorio != null && globalTerritorio != handler.territorio)
            {
                handler.territorio = globalTerritorio;
                sincronizados++;
                Debug.Log("Sincronizado handler de " + go.name + ": ahora apunta al territorio global '" + globalTerritorio.nombre + "' (propietario: " + (globalTerritorio.jugadorPropietario?.alias ?? "null") + ")");
            }
            else if (globalTerritorio == null)
            {
                desincronizados++;
                Debug.LogWarning("No se encontró territorio global para handler de " + go.name + ". Creando nuevo o saltando...");
            }
            else
            {
                Debug.Log("Handler de " + go.name + " ya sincronizado con global.");
            }
        }

        Debug.Log("Sincronización completada: Procesados " + totalHandlers + " handlers. Sincronizados: " + sincronizados + ", Desincronizados: " + desincronizados);
    }

    private void VerificarAsignacionesPostDistribucion()
    {
        int conPropietario = 0;
        int sinPropietario = 0;
        for (int i = 0; i < mapa.territorios.Contar(); i++)
        {
            Territorio t = mapa.territorios.Obtener(i);
            if (t.jugadorPropietario != null)
            {
                conPropietario++;
                Debug.Log("Territorio '" + t.nombre + "' tiene propietario: " + t.jugadorPropietario.alias);
            }
            else
            {
                sinPropietario++;
                Debug.LogWarning("Territorio '" + t.nombre + "' SIN propietario después de distribución!");
            }
        }
        Debug.Log("Verificación post-distribución: " + conPropietario + " con propietario, " + sinPropietario + " sin propietario.");
    }

    private void Shuffle<T>(ListaArray<T> list)
    {
        System.Random rng = new System.Random();
        int n = list.Contar();
        List<T> temp = new List<T>();
        for (int i = 0; i < n; i++) temp.Add(list.Obtener(i));

        for (int i = n - 1; i > 0; i--)
        {
            int k = rng.Next(i + 1);
            T value = temp[k];
            temp[k] = temp[i];
            temp[i] = value;
        }

        for (int i = 0; i < n; i++) list.Eliminar(0);
        for (int i = 0; i < n; i++) list.Agregar(temp[i]);
    }

    public int CalcularRefuerzos(Jugador j)
    {
        int refuerzosBase = Mathf.Max(3, j.territoriosConquistados.Contar() / 3);
        int bonusContinentes = 0;
        for (int i = 0; i < mapa.continentes.Contar(); i++)
        {
            bonusContinentes += mapa.continentes.Obtener(i).CalcularBonificacion(j);
        }
        return refuerzosBase + bonusContinentes;
    }

    /// <summary>
    /// Inicia un ataque entre dos territorios.
    /// </summary>
    public void Atacar(Territorio atacante, Territorio defensor, int tropasAtacantes, int tropasDefensoras)
    {
        // Validaciones
        if (atacante == null || defensor == null)
        {
            Debug.LogError("Territorios de ataque o defensa nulos.");
            return;
        }
        if (atacante.jugadorPropietario != jugadorEnTurno)
        {
            Debug.LogError("El territorio atacante no pertenece al jugador en turno.");
            return;
        }
        if (defensor.jugadorPropietario == jugadorEnTurno)
        {
            Debug.LogError("No puedes atacar tus propios territorios.");
            return;
        }
        if (!mapa.SonAdyacentes(atacante, defensor))
        {
            Debug.LogError("Los territorios no son adyacentes.");
            return;
        }
        if (atacante.cantidadTropas <= tropasAtacantes) // Debe dejar al menos 1 tropa en el origen
        {
            Debug.LogError("Debes dejar al menos una tropa en el territorio atacante.");
            return;
        }
        if (tropasAtacantes < 1 || tropasAtacantes > 3) // RISK permite 1, 2 o 3 dados de ataque
        {
            Debug.LogError("Número de tropas atacantes inválido (debe ser 1, 2 o 3).");
            return;
        }
        if (tropasAtacantes > atacante.cantidadTropas - 1)
        {
            tropasAtacantes = atacante.cantidadTropas - 1; // Ajustar si el jugador intenta usar más de las disponibles
        }

        Debug.Log(jugadorEnTurno.alias + " ataca " + defensor.nombre + " desde " + atacante.nombre + " con " + tropasAtacantes + " tropas.");


        attackedTerritoryName = defensor.nombre;

        int tropasAtacantesValidas = Mathf.Min(tropasAtacantes, atacante.cantidadTropas - 1);
        int tropasDefensorasValidas = Mathf.Min(tropasDefensoras, defensor.cantidadTropas);

        int tropasAtacanteDados = Mathf.Min(3, tropasAtacantesValidas);
        int tropasDefensorDados = Mathf.Min(2, tropasDefensorasValidas);

        territorioSeleccionadoParaAtaqueOrigen = atacante;
        territorioSeleccionadoParaAtaqueDestino = defensor;

        combateManager.IniciarCombate(tropasAtacanteDados, tropasDefensorDados);

        // Enviar mensaje de ataque
        var attackMsg = new NetAttackTerritory
        {
            attackerTerritoryName = atacante.nombre,
            defenderTerritoryName = defensor.nombre,
            attackingTroops = tropasAtacantes,
            defendingTroops = tropasDefensoras,
        };
        NetworkSender.SendMessage(attackMsg);
    }

    /// <summary>
    /// Mueve tropas entre territorios del mismo jugador.
    /// </summary>
    public void MoverTropas(Territorio origen, Territorio destino, int cantidad)
    {
        // Validaciones
        if (origen == null || destino == null)
        {
            Debug.LogError("Territorios de origen o destino nulos.");
            return;
        }
        if (origen.jugadorPropietario != jugadorEnTurno || destino.jugadorPropietario != jugadorEnTurno)
        {
            Debug.LogError("Ambos territorios deben pertenecer al jugador en turno.");
            return;
        }
        if (origen.cantidadTropas <= cantidad) // Debe dejar al menos 1 tropa en el origen
        {
            Debug.LogError("Debes dejar al menos una tropa en el territorio de origen.");
            return;
        }
        if (cantidad <= 0)
        {
            Debug.LogError("La cantidad de tropas a mover debe ser mayor que cero.");
            return;
        }
        if (!mapa.ExisteRutaSegura(origen, destino, jugadorEnTurno))
        {
            Debug.LogError("No existe una ruta segura entre los territorios de origen y destino.");
            return;
        }

        origen.cantidadTropas -= cantidad;
        destino.cantidadTropas += cantidad;
        Debug.Log(jugadorEnTurno.alias + " movió " + cantidad + " tropas de " + origen.nombre + " a " + destino.nombre);
        mapa.ActualizarVisualTropasEnMapa();
        Saving();

        // Enviar mensaje de movimiento de tropas
        var moveTroopsMsg = new NetMoveTroops
        {
            originTerritory = origen.nombre,
            destinationTerritory = destino.nombre,
            troopsToMove = cantidad,
            playerAlias = jugadorEnTurno.alias
        };
        NetworkSender.SendMessage(moveTroopsMsg);
    }

    /// <summary>
    /// Verifica si algún jugador ha ganado la partida.
    /// </summary>
    public Jugador VerificarVictoria()
    {
        for (int i = 0; i < jugadores.Contar(); i++)
        {
            Jugador j = jugadores.Obtener(i);
            if (j.territoriosConquistados.Contar() == mapa.territorios.Contar())
            {
                estado = EstadoPartida.Finalizada;
                Debug.Log("¡" + j.alias + " ha conquistado todos los territorios y ganado la partida!");
                return j;
            }
        }
        return null;
    }

    /// <summary>
    /// Muestra el panel de ataque con la información del territorio defensor.
    /// </summary>
    public void ShowAttackPanel(string description, Territorio defensor, int tropasAtacantesMax, int tropasDefensoras) // Parámetros actualizados
    {

        attackPanel.gameObject.SetActive(true);


        attackPanel.SetupPanel(description, defensor, tropasDefensoras, tropasAtacantesMax);
        // Usar el nuevo SetupPanel
    }

    /// <summary>
    /// Deshabilita el panel de ataque.
    /// </summary>
    public void DisableAttackPanel()
    {
        attackPanel.gameObject.SetActive(false);
    }

    /// <summary>
    /// Inicia la escena de lucha.
    /// </summary>
    public void StartFight()
    {
        dado.gameObject.SetActive(true);


    }

    // Métodos de Guardado y Carga (adaptados a las nuevas clases)
    public void Saving()
    {
        if (mapa == null)
        {
            Debug.LogError("mapa es NULL en Saving(). No se puede guardar.");
            return;
        }
        if (!isJoined || jugadores.Contar() == 0 || Mapa.instance.territorios.Contar() == 0)
        {
            Debug.LogWarning("[Partida] Saving saltado: Estado incompleto (no joined o datos vacíos).");
            return;
        }

        Debug.Log("=== INICIANDO SAVING ===");

        try
        {
            SaveDataJSON dataJSON = new SaveDataJSON();

            dataJSON.territoriosData = new TerritorioSaveData[mapa.territorios.Contar()];
            for (int i = 0; i < mapa.territorios.Contar(); i++)
            {
                Territorio t = mapa.territorios.Obtener(i);
                if (t != null)
                {
                    string propietarioAlias = (t.jugadorPropietario != null) ? t.jugadorPropietario.alias : "Neutral";
                    dataJSON.territoriosData[i] = new TerritorioSaveData
                    {
                        nombre = t.nombre,
                        cantidadTropas = t.cantidadTropas,
                        propietarioAlias = propietarioAlias
                    };
                }
            }

            dataJSON.jugadoresData = new JugadorSaveData[jugadores.Contar()];
            for (int i = 0; i < jugadores.Contar(); i++)
            {
                Jugador j = jugadores.Obtener(i);
                dataJSON.jugadoresData[i] = new JugadorSaveData
                {
                    alias = j.alias,
                    colorR = j.color.r,
                    colorG = j.color.g,
                    colorB = j.color.b,
                    colorA = j.color.a
                };
            }

            dataJSON.currentJugadorIndex = GetJugadorIndex(jugadorEnTurno);
            dataJSON.currentContadorRonda = contadorRonda;
            dataJSON.currentEstadoPartida = (int)estado;
            dataJSON.currentFaseTurno = (int)faseActual;

            string json = JsonUtility.ToJson(dataJSON, true);
            string savePath = Application.persistentDataPath + "/SaveFile.json";
            File.WriteAllText(savePath, json);

            Debug.Log("Juego guardado exitosamente en: " + savePath);
            Debug.Log("JSON preview: " + json.Substring(0, Math.Min(200, json.Length)));
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error en Saving(): " + ex.Message + "\nStack: " + ex.StackTrace);
        }

        Debug.Log("=== FIN SAVING ===");
    }

    private int GetJugadorIndex(Jugador jugador)
    {
        for (int i = 0; i < jugadores.Contar(); i++)
        {
            if (jugadores.Obtener(i) == jugador) return i;
        }
        return 0;
    }

    public void Loading()
    {
        string savePath = Application.persistentDataPath + "/SaveFile.json";
        if (!File.Exists(savePath))
        {
            Debug.Log("No se encontró archivo de guardado.");
            return;
        }

        if (mapa == null)
        {
            Debug.LogError("mapa es NULL en Loading(). No se puede cargar.");
            return;
        }

        Debug.Log("=== INICIANDO LOADING ===");

        try
        {
            string json = File.ReadAllText(savePath);
            SaveDataJSON dataJSON = JsonUtility.FromJson<SaveDataJSON>(json);

            if (dataJSON == null)
            {
                Debug.LogError("JSON inválido o vacío.");
                return;
            }

            jugadores = new ListaArray<Jugador>();
            foreach (var jData in dataJSON.jugadoresData)
            {
                string alias = string.IsNullOrEmpty(jData.alias) ? "JugadorSinAlias" : jData.alias;
                Color32 color = new Color32(jData.colorR, jData.colorG, jData.colorB, jData.colorA);
                Jugador j = new Jugador(alias, color);
                jugadores.Agregar(j);
                Debug.Log($"Jugador cargado: alias='{alias}', color={color}");
            }

            ejercitoNeutral = new Jugador("Neutral", new Color32(150, 150, 150, 255));
            jugadores.Agregar(ejercitoNeutral);

            // Inicializar listas de territorios conquistados
            for (int i = 0; i < jugadores.Contar(); i++)
            {
                jugadores.Obtener(i).territoriosConquistados = new ListaArray<Territorio>();
            }

            // Restaurar territorios y propietarios
            for (int i = 0; i < dataJSON.territoriosData.Length; i++)
            {
                var tData = dataJSON.territoriosData[i];
                Territorio currentTerritorio = mapa.GetTerritorioPorNombre(tData.nombre);
                if (currentTerritorio != null)
                {
                    currentTerritorio.cantidadTropas = tData.cantidadTropas;

                    Jugador propietarioRestaurado = null;
                    for (int j = 0; j < jugadores.Contar(); j++)
                    {
                        if (jugadores.Obtener(j).alias == tData.propietarioAlias)
                        {
                            propietarioRestaurado = jugadores.Obtener(j);
                            break;
                        }
                    }
                    if (propietarioRestaurado == null && tData.propietarioAlias == "Neutral")
                    {
                        propietarioRestaurado = ejercitoNeutral;
                    }

                    currentTerritorio.jugadorPropietario = propietarioRestaurado;
                    if (propietarioRestaurado != null)
                    {
                        propietarioRestaurado.territoriosConquistados.Agregar(currentTerritorio);
                    }
                }
                else
                {
                    Debug.LogWarning("Territorio no encontrado al cargar: " + tData.nombre);
                }
            }

            // Validar índice de jugador en turno
            int idx = dataJSON.currentJugadorIndex;
            if (idx < 0 || idx >= jugadores.Contar())
            {
                Debug.LogWarning($"Índice de jugador en turno inválido ({idx}), asignando jugador 0 por defecto.");
                idx = 0;
            }
            // Evitar que el jugador neutral sea jugador en turno (si no es deseado)
            if (idx == jugadores.Contar() - 1) // Último jugador es neutral
            {
                Debug.LogWarning("El jugador en turno apunta al jugador neutral, asignando jugador 0 por defecto.");
                idx = 0;
            }

            jugadorEnTurno = jugadores.Obtener(idx);

            if (jugadorEnTurno == null || string.IsNullOrEmpty(jugadorEnTurno.alias))
            {
                Debug.LogError("jugadorEnTurno es null o tiene alias vacío después de cargar. Asignando jugador 0.");
                jugadorEnTurno = jugadores.Obtener(0);
            }

            contadorRonda = dataJSON.currentContadorRonda;
            estado = (EstadoPartida)dataJSON.currentEstadoPartida;
            faseActual = (FaseTurno)dataJSON.currentFaseTurno;

            Debug.Log($"Jugador en turno asignado en Loading: '{jugadorEnTurno.alias}' (índice {idx})");
            Debug.Log($"Ronda: {contadorRonda}, Estado: {estado}, Fase: {faseActual}");

            // Reestablecer referencias inversas y reconstruir mapa
            for (int i = 0; i < jugadores.Contar(); i++)
            {
                jugadores.Obtener(i).ReestablecerReferenciasInversas();
            }
            ejercitoNeutral.ReestablecerReferenciasInversas();
            for (int i = 0; i < mapa.continentes.Contar(); i++)
            {
                mapa.continentes.Obtener(i).ReestablecerReferenciasInversas();
            }
            mapa.ReconstruirAdyacenciasLocales();
            mapa.TintTerritorios();

            Debug.Log("Juego cargado exitosamente. Territorios restaurados: " + dataJSON.territoriosData.Length);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error en Loading(): " + ex.Message + "\nStack: " + ex.StackTrace);
        }

        Debug.Log("=== FIN LOADING ===");
    }


    public void DeleteSaveFile()
    {
        if (File.Exists(Application.persistentDataPath + "/SaveFile.sept"))
        {
            File.Delete(Application.persistentDataPath + "/SaveFile.sept");
            Debug.Log("Archivo de guardado eliminado.");
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    public void SeleccionarTerritorio(Territorio territorio)
    {
        if (faseActual == FaseTurno.Refuerzos)
        {
            if (territorio.jugadorPropietario != jugadorEnTurno)
            {
                Debug.LogWarning("Solo puedes seleccionar tus propios territorios para reforzar.");
                return;
            }
            if (territorioSeleccionadoParaRefuerzo == territorio)
            {
                territorioSeleccionadoParaRefuerzo = null;
                Debug.Log("Territorio de refuerzo deseleccionado: " + territorio.nombre);
            }
            else
            {
                territorioSeleccionadoParaRefuerzo = territorio;
                Debug.Log("Territorio de refuerzo seleccionado: " + territorio.nombre);
            }
            if (tropasDisponiblesParaRefuerzo > 0)
            {
                AgregarTropasRefuerzo(territorio, 1);
            }
            return;
        }

        if (faseActual == FaseTurno.Ataque)
        {
            if (jugadorEnTurno == null)
            {
                Debug.LogError("jugadorEnTurno es NULL. No se puede seleccionar territorio.");
                return;
            }

            Debug.Log($"Jugador en turno: {jugadorEnTurno.alias}. Seleccionando territorio: {territorio.nombre} (Propietario: {(territorio.jugadorPropietario != null ? territorio.jugadorPropietario.alias : "null")})");

            if (territorio.jugadorPropietario == jugadorEnTurno)
            {
                if (territorioSeleccionadoParaAtaqueOrigen == null)
                {
                    if (territorio.cantidadTropas > 1)
                    {
                        territorioSeleccionadoParaAtaqueOrigen = territorio;
                        Debug.Log("Territorio de ataque ORIGEN seleccionado: " + territorio.nombre);
                    }
                    else
                    {
                        Debug.LogWarning("El territorio " + territorio.nombre + " tiene solo 1 tropa. Necesita al menos 2 para atacar.");
                    }
                }
                else if (territorioSeleccionadoParaAtaqueOrigen == territorio)
                {
                    territorioSeleccionadoParaAtaqueOrigen = null;
                    Debug.Log("Territorio de ataque ORIGEN deseleccionado: " + territorio.nombre);
                }
                else
                {
                    territorioSeleccionadoParaAtaqueOrigen = territorio;
                    territorioSeleccionadoParaAtaqueDestino = null;
                    Debug.Log("Territorio de ataque ORIGEN cambiado a: " + territorio.nombre);
                }
            }
            else
            {
                if (territorioSeleccionadoParaAtaqueOrigen == null)
                {
                    Debug.LogWarning("Primero selecciona uno de tus territorios para atacar.");
                    return;
                }

                if (mapa.SonAdyacentes(territorioSeleccionadoParaAtaqueOrigen, territorio))
                {
                    territorioSeleccionadoParaAtaqueDestino = territorio;
                    Debug.Log("Territorio de ataque DESTINO seleccionado: " + territorio.nombre);
                    ShowAttackPanel(
                        "Atacar " + territorio.nombre + " (Tropas: " + territorio.cantidadTropas + ") desde " +
                        territorioSeleccionadoParaAtaqueOrigen.nombre + " (Tus tropas: " + (territorioSeleccionadoParaAtaqueOrigen.cantidadTropas - 1) + ")",
                        territorio,
                        territorio.cantidadTropas,
                        territorioSeleccionadoParaAtaqueOrigen.cantidadTropas - 1
                    );

                }
                else
                {
                    Debug.LogWarning("El territorio " + territorio.nombre + " no es adyacente a " + territorioSeleccionadoParaAtaqueOrigen.nombre);
                    territorioSeleccionadoParaAtaqueDestino = null;
                }
            }
            return;
        }

        if (faseActual == FaseTurno.Planeacion)
        {
            if (jugadorEnTurno == null)
            {
                Debug.LogError("jugadorEnTurno es NULL. No se puede seleccionar territorio.");
                return;
            }

            if (territorio.jugadorPropietario != jugadorEnTurno)
            {
                Debug.LogWarning("Solo puedes seleccionar tus propios territorios para mover tropas.");
                return;
            }
            if (territorioSeleccionadoParaMovimientoOrigen == null)
            {
                // Seleccionamos el territorio origen solo si tiene más de 1 tropa
                if (territorio.cantidadTropas > 1)
                {
                    territorioSeleccionadoParaMovimientoOrigen = territorio;
                    Debug.Log("Territorio de movimiento ORIGEN seleccionado: " + territorio.nombre);
                }
                else
                {
                    Debug.LogWarning("El territorio " + territorio.nombre + " tiene solo 1 tropa. Necesita al menos 2 para mover tropas.");
                }
            }


            if (territorioSeleccionadoParaMovimientoOrigen.nombre == "")
            {
                // Seleccionamos el territorio origen solo si tiene más de 1 tropa
                if (territorio.cantidadTropas > 1)
                {
                    territorioSeleccionadoParaMovimientoOrigen = territorio;
                    Debug.Log("Territorio de movimiento ORIGEN seleccionado: " + territorio.nombre);
                }
                else
                {
                    Debug.LogWarning("El territorio " + territorio.nombre + " tiene solo 1 tropa. Necesita al menos 2 para mover tropas.");
                }
            }
            else
            {
                // Ya hay un territorio origen seleccionado, ahora seleccionamos destino
                Debug.Log($"Territorio origen seleccionado: {territorioSeleccionadoParaMovimientoOrigen.nombre}. Intentando seleccionar destino: {territorio.nombre}");

                // Validar que el destino sea distinto al origen
                if (territorio == territorioSeleccionadoParaMovimientoOrigen)
                {
                    // Deseleccionar origen si se selecciona de nuevo
                    territorioSeleccionadoParaMovimientoOrigen = null;
                    Debug.Log("Territorio de movimiento ORIGEN deseleccionado: " + territorio.nombre);
                    return;
                }

                // Validar ruta segura solo si origen no es null (ya validado, pero por seguridad)
                if (territorioSeleccionadoParaMovimientoOrigen == null)
                {
                    Debug.LogWarning("No hay territorio origen seleccionado para validar ruta segura.");
                    return;
                }

                // Validar ruta segura
                bool rutaSegura = mapa.ExisteRutaSegura(territorioSeleccionadoParaMovimientoOrigen, territorio, jugadorEnTurno);
                Debug.Log($"Ruta segura entre {territorioSeleccionadoParaMovimientoOrigen.nombre} y {territorio.nombre}: {rutaSegura}");

                if (rutaSegura)
                {
                    territorioSeleccionadoParaMovimientoDestino = territorio;
                    Debug.Log("Territorio de movimiento DESTINO seleccionado: " + territorio.nombre);

                    int cantidadAMover = 1; // Aquí puedes cambiar para que sea variable o pedir input

                    MoverTropas(territorioSeleccionadoParaMovimientoOrigen, territorioSeleccionadoParaMovimientoDestino, cantidadAMover);

                    // Limpiar selecciones después de mover
                    territorioSeleccionadoParaMovimientoOrigen = null;
                    territorioSeleccionadoParaMovimientoDestino = null;
                }
                else
                {
                    Debug.LogWarning("No existe una ruta segura entre " + territorioSeleccionadoParaMovimientoOrigen.nombre + " y " + territorio.nombre);
                    territorioSeleccionadoParaMovimientoDestino = null;
                }
            }
            return;
        }
    }


    /// <summary>
    /// Intenta agregar tropas al territorio seleccionado durante la fase de refuerzos.
    /// </summary>
    public void AgregarTropasRefuerzo(Territorio territorio, int cantidad)
    {
        if (faseActual != FaseTurno.Refuerzos)
        {
            Debug.LogWarning("No es la fase de refuerzos. No se pueden agregar tropas.");
            return;
        }

        if (territorio == null)
        {
            Debug.LogError("Territorio nulo en AgregarTropasRefuerzo.");
            return;
        }

        if (territorio.jugadorPropietario != jugadorEnTurno)
        {
            Debug.LogWarning("No puedes reforzar un territorio que no te pertenece.");
            return;
        }

        if (cantidad <= 0)
        {
            Debug.LogWarning("Cantidad de tropas a agregar debe ser mayor que cero.");
            return;
        }

        if (cantidad > tropasDisponiblesParaRefuerzo)
        {
            Debug.LogWarning($"No tienes suficientes tropas para agregar. Disponibles: {tropasDisponiblesParaRefuerzo}, solicitadas: {cantidad}");
            return;
        }

        jugadorEnTurno.UsarTropas(cantidad);
        territorio.cantidadTropas += cantidad;
        tropasDisponiblesParaRefuerzo -= cantidad;
        ActualizarJugadorEnTurno(jugadorEnTurno);
        mapa.ActualizarVisualTropasEnMapa();

        Debug.Log($"{jugadorEnTurno.alias} agregó {cantidad} tropas a {territorio.nombre}. Tropas restantes para refuerzo: {tropasDisponiblesParaRefuerzo}");

        mapa.TintTerritorios();
        Saving();

        // Enviar mensaje de refuerzo
        var addTroopsMsg = new NetAddTroopsReinforcement
        {
            territoryName = territorio.nombre,
            troopsToAdd = cantidad,
            playerAlias = jugadorEnTurno.alias
        };
        NetworkSender.SendMessage(addTroopsMsg);
    }
    void ActualizarJugadorEnTurno(Jugador jugador)
    {
        if (jugador == null)
        {
            Debug.LogError("jugador es NULL en ActualizarJugadorEnTurno(). No se puede actualizar UI.");
            return;
        }
        if (jugador == jugadores.Obtener(0))
        {
            playerInfoUI1.ActualizarInfo(jugador.alias, jugador.color, jugador.tropasDisponibles);
        }
        else if (jugador == jugadores.Obtener(1))
        {
            playerInfoUI2.ActualizarInfo(jugador.alias, jugador.color, jugador.tropasDisponibles);
        }
    }
    public void ResolverCombate(int[] dadosAtacante, int[] dadosDefensor)
    {
        Array.Sort(dadosAtacante);
        Array.Reverse(dadosAtacante);
        Array.Sort(dadosDefensor);
        Array.Reverse(dadosDefensor);

        int comparaciones = Math.Min(dadosAtacante.Length, dadosDefensor.Length);

        for (int i = 0; i < comparaciones; i++)
        {
            if (dadosAtacante[i] > dadosDefensor[i])
            {
                territorioSeleccionadoParaAtaqueDestino.cantidadTropas--;
                Debug.Log("Defensor pierde 1 tropa");
                dado.gameObject.SetActive(false);
                DisableAttackPanel();
                attackPanel.ClosePanel();

            }
            else if (dadosAtacante[i] < dadosDefensor[i])
            {
                territorioSeleccionadoParaAtaqueOrigen.cantidadTropas--;
                Debug.Log("Atacante pierde 1 tropa");
                dado.gameObject.SetActive(false);
                DisableAttackPanel();
                attackPanel.ClosePanel();
            }
            else
            {
                // Empate en los dados: reiniciar el combate lanzando los dados otra vez
                Debug.LogWarning("Empate en la tirada de dados. Re-lanzando dados...");

                // Llamamos nuevamente a Atacar con los mismos parámetros para rodar los dados otra vez
                dado.gameObject.SetActive(false);
                attackPanel.Empate();
                return; // Salir del método para evitar aplicar más lógica actualmente
            }
        }

        // Código existente para conquista si el defensor se queda sin tropas
        if (territorioSeleccionadoParaAtaqueDestino.cantidadTropas <= 0)
        {
            Debug.Log(jugadorEnTurno.alias + " ha conquistado " + territorioSeleccionadoParaAtaqueDestino.nombre);

            Jugador antiguoPropietario = territorioSeleccionadoParaAtaqueDestino.jugadorPropietario;
            if (antiguoPropietario != null)
            {
                antiguoPropietario.territoriosConquistados.EliminarElemento(territorioSeleccionadoParaAtaqueDestino);
            }

            territorioSeleccionadoParaAtaqueDestino.jugadorPropietario = jugadorEnTurno;
            jugadorEnTurno.territoriosConquistados.Agregar(territorioSeleccionadoParaAtaqueDestino);

            // Mover tropas atacantes al territorio conquistado (mínimo 1)
            int tropasAMover = Mathf.Min(territorioSeleccionadoParaAtaqueOrigen.cantidadTropas - 1, dadosAtacante.Length);
            territorioSeleccionadoParaAtaqueOrigen.cantidadTropas -= tropasAMover;
            territorioSeleccionadoParaAtaqueDestino.cantidadTropas += tropasAMover;
            dado.gameObject.SetActive(false);
            DefinirCartaAleatoria();
            if (jugadorEnTurno == jugadores.Obtener(0))
            {
                playerInfoUI1.ActualizarConteosCartas(jugadorEnTurno);
            }
            if (jugadorEnTurno == jugadores.Obtener(1))
            {
                playerInfoUI2.ActualizarConteosCartas(jugadorEnTurno);
            }

            mapa.TintTerritorios();

            Debug.Log($"Movidas {tropasAMover} tropas de {territorioSeleccionadoParaAtaqueOrigen.nombre} a {territorioSeleccionadoParaAtaqueDestino.nombre} después de la conquista.");
        }


        mapa.ActualizarVisualTropasEnMapa();
        Saving();
        var resolveCombatMsg = new NetResolveCombat
        {
            attackerTerritoryName = territorioSeleccionadoParaAtaqueOrigen.nombre,
            defenderTerritoryName = territorioSeleccionadoParaAtaqueDestino.nombre,
            attackerDice1 = dadosAtacante.Length > 0 ? dadosAtacante[0] : 0,
            attackerDice2 = dadosAtacante.Length > 1 ? dadosAtacante[1] : 0,
            attackerDice3 = dadosAtacante.Length > 2 ? dadosAtacante[2] : 0,
            defenderDice1 = dadosDefensor.Length > 0 ? dadosDefensor[0] : 0,
            defenderDice2 = dadosDefensor.Length > 1 ? dadosDefensor[1] : 0,
            defenderLostTroop = territorioSeleccionadoParaAtaqueDestino.cantidadTropas < 1,
            attackerLostTroop = territorioSeleccionadoParaAtaqueOrigen.cantidadTropas < 1,
            territoryConquered = territorioSeleccionadoParaAtaqueDestino.cantidadTropas < 1,
            newOwnerAlias = territorioSeleccionadoParaAtaqueDestino.jugadorPropietario?.alias ?? "",
            troopsMovedAfterConquest = territorioSeleccionadoParaAtaqueDestino.cantidadTropas // o la cantidad que se movió
        };
        NetworkSender.SendMessage(resolveCombatMsg);
    }



    public void DefinirCartaAleatoria()
    {
        // Definir un array o lista con los tipos de cartas posibles (sin comodín si no quieres)
        Jugador.TipoCarta[] tiposCartas = new Jugador.TipoCarta[] {
            Jugador.TipoCarta.Infanteria,
            Jugador.TipoCarta.Caballeria,
            Jugador.TipoCarta.Artilleria
        };

        // Seleccionar aleatoriamente un índice válido
        int indiceAleatorio = UnityEngine.Random.Range(0, tiposCartas.Length);

        // Obtener la carta seleccionada
        Jugador.TipoCarta cartaSeleccionada = tiposCartas[indiceAleatorio];

        // Intentar agregar la carta al jugador en turno
        bool agregado = jugadorEnTurno.AgregarCarta(cartaSeleccionada);

        if (agregado)
        {
            Debug.Log(jugadorEnTurno.alias + " ha recibido una carta: " + cartaSeleccionada.ToString());
        }
        else
        {
            Debug.LogWarning(jugadorEnTurno.alias + " no pudo recibir la carta: " + cartaSeleccionada.ToString());
        }
    }
    public void Inercambio()
    {
        jugadorEnTurno.IntercambiarCartas();
    }
    public void Close()
    {
        dado.gameObject.SetActive(false);
        DisableAttackPanel();
        attackPanel.ClosePanel();
    }
    public void ProcesarSeleccionTerritorio(NetSelectTerritory msg, NetworkConnection sender)
    {
        Debug.Log($"[Partida] Procesando selección de territorio: {msg.territoryName} de jugador {msg.playerId}");

        // Aquí puedes buscar el territorio y validar que el jugador pueda seleccionarlo
        Territorio territorio = mapa.GetTerritorioPorNombre(msg.territoryName);
        if (territorio == null)
        {
            Debug.LogWarning("Territorio no encontrado: " + msg.territoryName);
            return;
        }

        // Validar que el jugador que envió el mensaje es el jugador en turno, etc.
        if (jugadorEnTurno == null)
        {
            Debug.LogWarning("Jugador no es el que tiene el turno o no coincide alias.");
            return;
        }

        // Lógica para seleccionar territorio según fase actual
        SeleccionarTerritorio(territorio);

        // Opcional: enviar respuesta o actualizar estado a todos clientes
        // Server.Instance.Broadcast(new NetSyncCurrentGameState(...));
    }

    // Método para manejar refuerzos desde el cliente
    public void ProcesarAgregarTropasRefuerzo(NetAddTroopsReinforcement msg, NetworkConnection sender)
    {
        Jugador jugadorRemoto = GetJugadorPorConnection(sender);  // NUEVO: Obtener por conexión.
        if (jugadorRemoto == null || jugadorRemoto.alias != msg.playerAlias)
        {
            Debug.LogWarning("Cliente no autorizado.");
            return;
        }
        Debug.Log($"[Partida] Procesando refuerzo en territorio: {msg.territoryName} con {msg.troopsToAdd} tropas por jugador {msg.playerAlias}");

        Territorio territorio = mapa.GetTerritorioPorNombre(msg.territoryName);
        if (territorio == null)
        {
            Debug.LogWarning("Territorio no encontrado: " + msg.territoryName);
            return;
        }

        if (jugadorEnTurno == null || jugadorEnTurno.alias != msg.playerAlias)
        {
            Debug.LogWarning("Jugador no es el que tiene el turno o no coincide alias.");
            return;
        }

        AgregarTropasRefuerzo(territorio, msg.troopsToAdd);

        // Opcional: actualizar estado en clientes
    }

    // Método para manejar movimiento de tropas desde el cliente
    public void ProcesarMovimientoTropas(NetMoveTroops msg, NetworkConnection sender)
    {
        Debug.Log($"[Partida] Procesando movimiento de tropas de {msg.originTerritory} a {msg.destinationTerritory} cantidad {msg.troopsToMove} por jugador {msg.playerAlias}");

        Territorio origen = mapa.GetTerritorioPorNombre(msg.originTerritory);
        Territorio destino = mapa.GetTerritorioPorNombre(msg.destinationTerritory);

        if (origen == null || destino == null)
        {
            Debug.LogWarning("Territorio origen o destino no encontrado.");
            return;
        }

        if (jugadorEnTurno == null || jugadorEnTurno.alias != msg.playerAlias)
        {
            Debug.LogWarning("Jugador no es el que tiene el turno o no coincide alias.");
            return;
        }

        MoverTropas(origen, destino, msg.troopsToMove);

        // Opcional: actualizar estado en clientes
    }

    // Método para manejar ataque desde el cliente
    public void ProcesarAtaqueTerritorio(NetAttackTerritory msg, NetworkConnection sender)
    {
        Debug.Log($"[Partida] Procesando ataque de {msg.attackerTerritoryName} a {msg.defenderTerritoryName} con {msg.attackingTroops} tropas por jugador {jugadorEnTurno}");

        Territorio origen = mapa.GetTerritorioPorNombre(msg.attackerTerritoryName);
        Territorio destino = mapa.GetTerritorioPorNombre(msg.defenderTerritoryName);

        if (origen == null || destino == null)
        {
            Debug.LogWarning("Territorio origen o destino no encontrado.");
            return;
        }

        if (jugadorEnTurno == null)
        {
            Debug.LogWarning("Jugador no es el que tiene el turno o no coincide alias.");
            return;
        }

        Atacar(origen, destino, msg.attackingTroops, destino.cantidadTropas);

        // Opcional: actualizar estado en clientes
    }

    // Método para manejar solicitud de siguiente turno desde el cliente
    public void ProcesarSiguienteTurno(NetNextTurn msg, NetworkConnection sender)
    {
        Debug.Log($"[Partida] Procesando siguiente turno solicitado por jugador {msg.playerAlias}");

        if (jugadorEnTurno == null || jugadorEnTurno.alias != msg.playerAlias)
        {
            Debug.LogWarning("Jugador no es el que tiene el turno o no coincide alias.");
            return;
        }

        SiguienteTurno();

        // Opcional: actualizar estado en clientes
    }

    // Método para manejar solicitud de siguiente fase desde el cliente
    public void ProcesarSiguienteFase(NetNextPhase msg, NetworkConnection sender)
    {
        Debug.Log($"[Partida] Procesando siguiente fase solicitada por jugador {jugadorEnTurno}");

        if (jugadorEnTurno == null)
        {
            Debug.LogWarning("Jugador no es el que tiene el turno o no coincide alias.");
            return;
        }

        SiguienteFase();

        // Opcional: actualizar estado en clientes
    }
    // Métodos para sincronización en cliente (llamados desde handlers de Client.cs)

    // Para inicio de partida
    public void SincronizarInicioPartida()
    {
        Debug.Log("[Cliente] Sincronizando inicio de partida");
        // Actualizar UI inicial, mostrar mapa, etc.
        if (mapa != null)
        {
            mapa.TintTerritorios();
            mapa.ActualizarVisualTropasEnMapa();
        }
        // Opcional: mostrar mensaje de "Partida iniciada"
        if (jugadoractual != null)
            jugadoractual.text = jugadorEnTurno?.alias ?? "Jugador en turno";
    }

    // Para siguiente turno
    public void SincronizarSiguienteTurno(string playerAlias, int fase, int roundCounter)
    {
        Debug.Log($"[Cliente] Sincronizando siguiente turno: {playerAlias}, Fase: {(FaseTurno)fase}, Ronda: {roundCounter}");

        // Buscar y asignar jugador en turno
        jugadorEnTurno = jugadores.Buscar(j => j.alias == playerAlias); // Asume que ListaArray tiene Find o implementa uno
        if (jugadorEnTurno != null)
        {
            faseActual = (FaseTurno)fase;
            contadorRonda = roundCounter;
            tropasDisponiblesParaRefuerzo = jugadorEnTurno.tropasDisponibles;
            ActualizarJugadorEnTurno(jugadorEnTurno);
            if (jugadoractual != null)
                jugadoractual.text = playerAlias;
        }

        if (mapa != null)
        {
            mapa.TintTerritorios();
            mapa.ActualizarVisualTropasEnMapa();
        }

        // Limpiar selecciones si es necesario
        territorioSeleccionadoParaAtaqueOrigen = null;
        territorioSeleccionadoParaAtaqueDestino = null;
        territorioSeleccionadoParaRefuerzo = null;
    }

    // Para siguiente fase
    public void SincronizarSiguienteFase(int newPhase)
    {
        Debug.Log($"[Cliente] Sincronizando siguiente fase: {(FaseTurno)newPhase}");
        faseActual = (FaseTurno)newPhase;

        // Limpiar selecciones según fase
        switch (faseActual)
        {
            case FaseTurno.Ataque:
                territorioSeleccionadoParaAtaqueOrigen = null;
                territorioSeleccionadoParaAtaqueDestino = null;
                break;
            case FaseTurno.Planeacion:
                territorioSeleccionadoParaMovimientoOrigen = null;
                territorioSeleccionadoParaMovimientoDestino = null;
                break;
        }

        // Actualizar UI si es necesario
        if (attackPanel != null && faseActual != FaseTurno.Ataque)
            DisableAttackPanel();
    }

    // Para actualización de tropas visuales (refuerzos, movimientos, etc.)
    public void SincronizarActualizacionTropasVisual(string territoryName, int newTroopsCount, string playerAlias)
    {
        Debug.Log($"[Cliente] Actualizando tropas visuales en {territoryName}: {newTroopsCount} para {playerAlias}");
        Territorio territorio = mapa.GetTerritorioPorNombre(territoryName);
        if (territorio != null)
        {
            territorio.cantidadTropas = newTroopsCount;
            // Actualizar propietario si es necesario (buscar por alias)
            Jugador jugador = jugadores.Buscar(j => j.alias == playerAlias);
            if (jugador != null)
                territorio.jugadorPropietario = jugador;

            mapa.ActualizarVisualTropasEnMapa();
            mapa.TintTerritorios();
        }
    }

    // Para resolución de combate
    public void SincronizarResolucionCombate(NetResolveCombat msg)
    {
        Debug.Log($"[Cliente] Sincronizando resolución de combate: {msg.attackerTerritoryName} vs {msg.defenderTerritoryName}");

        // Actualizar dados en UI si tienes un componente para mostrarlos
        if (dado != null)
        {
            // Ejemplo: mostrar dados (implementa según tu DadoManager)
            dado.gameObject.SetActive(true);

        }

        // Actualizar pérdidas de tropas
        Territorio atacante = mapa.GetTerritorioPorNombre(msg.attackerTerritoryName);
        Territorio defensor = mapa.GetTerritorioPorNombre(msg.defenderTerritoryName);
        if (atacante != null && defensor != null)
        {
            if (msg.defenderLostTroop)
                defensor.cantidadTropas--;
            if (msg.attackerLostTroop)
                atacante.cantidadTropas--;

            // Si hay conquista
            if (msg.territoryConquered)
            {
                defensor.jugadorPropietario = jugadorEnTurno; // Asume que el atacante gana
                defensor.cantidadTropas = msg.troopsMovedAfterConquest;
                Debug.Log($"[Cliente] Territorio {msg.defenderTerritoryName} conquistado por {msg.newOwnerAlias}");
            }

            mapa.ActualizarVisualTropasEnMapa();
            mapa.TintTerritorios();
        }

        // Ocultar paneles de combate
        if (dado != null)
            dado.gameObject.SetActive(false);
        DisableAttackPanel();


    }

    // Para sincronización de datos del mapa (territorios, propietarios, tropas)
    public void SincronizarDatosMapa(NetSyncMapData msg) // Asume que tienes NetSyncMapData con array de territorios
    {
        Debug.Log("[Cliente] Sincronizando datos del mapa");
        // Ejemplo: actualizar todos los territorios con datos del msg
        foreach (var tData in msg.allTerritoriesData) // Asume estructura similar a SaveData
        {
            Territorio territorio = mapa.GetTerritorioPorNombre(tData.nombre);
            if (territorio != null)
            {
                territorio.cantidadTropas = tData.cantidadTropas;
                // Buscar propietario por alias
                Jugador propietario = jugadores.Buscar(j => j.alias == tData.propietarioAlias);
                if (propietario != null)
                    territorio.jugadorPropietario = propietario;
            }
        }
        mapa.TintTerritorios();
        mapa.ActualizarVisualTropasEnMapa();
    }

    // Para sincronización de jugadores
    public void SincronizarDatosJugadores(NetSyncPlayersData msg)  // O usa NetSyncAssignedPlayers si lo implementas.
    {
        Debug.Log("[Cliente] Sincronizando datos de jugadores desde servidor.");

        // Limpiar lista local de jugadores (excluyendo neutral si ya existe).
        if (jugadores != null)
        {
            // Remover solo jugadores humanos; mantener neutral si existe.
            for (int i = jugadores.Contar() - 1; i >= 0; i--)
            {
                if (jugadores.Obtener(i).alias != "Neutral")
                {
                    jugadores.Eliminar(i);
                }
            }
        }
        else
        {
            jugadores = new ListaArray<Jugador>();
        }

        // Recrear jugadores basados en los datos asignados del servidor.
        if (msg.allPlayersData != null)
        {
            foreach (var jData in msg.allPlayersData)
            {
                if (string.IsNullOrEmpty(jData.alias) || jData.alias == "Neutral") continue;  // Saltar neutral o inválidos.

                Color32 color = new Color32(jData.colorR, jData.colorG, jData.colorB, jData.colorA);
                Jugador j = new Jugador(jData.alias, color);
                jugadores.Agregar(j);
                Debug.Log($"Jugador sincronizado: '{j.alias}' con color {color}");
            }
        }

        // NUEVO: Si usas NetSyncAssignedPlayers (recomendado para dinámico), integra localPlayerAlias aquí.
        // Ejemplo (si cambias el parámetro a NetSyncAssignedPlayers msgAssigned):
        // if (msgAssigned.localPlayerAlias != null)
        // {
        //     localPlayer = jugadores.Buscar(j => j.alias == msgAssigned.localPlayerAlias);
        //     Debug.Log($"Jugador local asignado: {localPlayer?.alias}");
        // }

        // NUEVO: Actualizar UI inmediatamente después de sincronizar.
        ActualizarUIJugadores();


    }

    // Para estado actual del juego
    public void SincronizarEstadoActual(NetSyncCurrentGameState msg)
    {
        Debug.Log($"[Cliente] Sincronizando estado actual: Jugador {msg.currentPlayerAlias}, Fase {(FaseTurno)msg.currentPhase}, Ronda {msg.currentRound}");
        SincronizarSiguienteTurno(msg.currentPlayerAlias, msg.currentPhase, msg.currentRound); // Reutiliza método existente
        if (localPlayer != null && jugadorEnTurno.alias == localPlayer.alias)
        {
            // Ej. Activar highlight o notificación "Tu turno".
            Debug.Log("¡Es tu turno!");
            // Si tienes un sistema de notificaciones: ShowNotification("Tu turno!");
        }

        ActualizarUIJugadores();
    }
    public Jugador GetJugadorPorConnection(NetworkConnection conn)
    {
        if (Server.Instance != null && Server.Instance.connectionToPlayerMap.TryGetValue(conn, out Jugador player))
        {
            return player;
        }
        return null;
    }
    public void ActualizarUIJugadores()
    {
        if (jugadores == null || jugadores.Contar() == 0)
        {
            Debug.LogWarning("No hay jugadores para actualizar en UI.");
            return;
        }

        Debug.Log("Actualizando UI de jugadores con datos sincronizados.");

        // Asumiendo que tienes UI fija para 2 jugadores (ajusta si es dinámica).
        // PlayerInfoUI1 para el primer jugador (ej. host o Player1).
        if (playerInfoUI1 != null && jugadores.Contar() >= 1)
        {
            Jugador player1 = jugadores.Obtener(0);  // Primer jugador en la lista sincronizada.
            playerInfoUI1.ActualizarInfo(player1.alias, player1.color, player1.tropasDisponibles);
            Debug.Log($"UI Player1 actualizada: {player1.alias} (color: {player1.color})");
        }

        // PlayerInfoUI2 para el segundo jugador.
        if (playerInfoUI2 != null && jugadores.Contar() >= 2)
        {
            Jugador player2 = jugadores.Obtener(1);  // Segundo jugador en la lista.
            playerInfoUI2.ActualizarInfo(player2.alias, player2.color, player2.tropasDisponibles);
            Debug.Log($"UI Player2 actualizada: {player2.alias} (color: {player2.color})");
        }

        // NUEVO: Si es cliente, resaltar o actualizar UI para el jugador local.
        if (localPlayer != null)
        {
            // Ejemplo: Cambiar color de fondo o mostrar "Tu turno" si es localPlayer == jugadorEnTurno.
            if (localPlayer == jugadorEnTurno)
            {
                // Actualizar texto o animación para "Tu turno".
                if (jugadoractual != null)
                    jugadoractual.text = $"Tu turno: {localPlayer.alias}";
            }

            // Opcional: Si tienes un panel para jugador local, actualízalo específicamente.
            // localPlayerUI.ActualizarInfo(localPlayer.alias, localPlayer.color, localPlayer.tropasDisponibles);
        }

        // Actualizar visuales generales (ej. mapa tiñe colores de jugadores).
        if (mapa != null)
        {
            mapa.TintTerritorios();  // Re-tintea el mapa con los nuevos colores de jugadores.
        }

        // Guardar estado sincronizado localmente (opcional, para persistencia en cliente).
        Saving();  // Si quieres guardar el estado sincronizado en el cliente.
    }

    // Método auxiliar para buscar jugador por alias (útil en otros lugares).
    public Jugador BuscarJugadorPorAlias(string alias)
    {
        if (jugadores == null) return null;
        for (int i = 0; i < jugadores.Contar(); i++)
        {
            if (jugadores.Obtener(i).alias == alias)
                return jugadores.Obtener(i);
        }
        return null;
    }
    /// <summary>
    /// Procesa ataque recibido desde un cliente (fase de ataque).
    /// </summary>
    public void ProcesarAtaque(NetMessage msg, NetworkConnection connection)
    {
        var m = msg as NetAttackTerritory;
        if (m == null)
        {
            Debug.LogWarning("[Partida] Mensaje no es NetAttackTerritory.");
            return;
        }

        // Obtener jugador remoto via conexión (de Server).
        if (Server.Instance == null || !Server.Instance.connectionToPlayerMap.ContainsKey(connection))
        {
            Debug.LogWarning("[Partida] Conexión inválida o sin jugador asignado en ProcesarAtaque.");
            return;
        }
        Jugador player = Server.Instance.connectionToPlayerMap[connection];

        // Validar que sea el turno y fase correcta.
        if (jugadorEnTurno == null || jugadorEnTurno != player || faseActual != FaseTurno.Ataque)
        {
            Debug.LogWarning($"[Partida] Jugador {player.alias} no autorizado para atacar (turno: {jugadorEnTurno?.alias}, fase: {faseActual}).");
            return;
        }

        Debug.Log($"[Partida] Procesando ataque: desde '{m.attackerTerritoryName}' a '{m.defenderTerritoryName}' con {m.attackingTroops} tropas para {player.alias}.");

        // Buscar territorios (usa método existente de Mapa).
        Territorio origen = mapa.GetTerritorioPorNombre(m.attackerTerritoryName);
        Territorio destino = mapa.GetTerritorioPorNombre(m.defenderTerritoryName);
        if (origen == null || destino == null)
        {
            Debug.LogWarning($"[Partida] Territorio origen o destino no encontrado: {m.attackerTerritoryName} o {m.defenderTerritoryName}.");
            return;
        }

        // Validar propiedad (origen debe ser del jugador, destino no).
        if (origen.jugadorPropietario != player)
        {
            Debug.LogWarning($"[Partida] Territorio origen {m.attackerTerritoryName} no pertenece a {player.alias}.");
            return;
        }
        if (destino.jugadorPropietario == player)
        {
            Debug.LogWarning($"[Partida] No puedes atacar tu propio territorio {m.defenderTerritoryName}.");
            return;
        }

        // Validar adyacencia (usa método existente de Mapa).
        if (!mapa.SonAdyacentes(origen, destino))
        {
            Debug.LogWarning($"[Partida] Territorios no adyacentes: {m.attackerTerritoryName} y {m.defenderTerritoryName}.");
            return;
        }

        // Validar tropas suficientes (debe dejar al menos 1 en origen, y attackingTroops entre 1-3).
        int troopsToAttack = Mathf.Min(m.attackingTroops, origen.cantidadTropas - 1);  // Ajusta a max disponible.
        if (origen.cantidadTropas <= 1 || troopsToAttack < 1 || troopsToAttack > 3)
        {
            Debug.LogWarning($"[Partida] Tropas insuficientes o inválidas en origen {m.attackerTerritoryName} (disponibles: {origen.cantidadTropas - 1}, solicitadas: {m.attackingTroops}).");
            return;
        }

        // Delegar a método existente (ya incluye validaciones adicionales, combate, UI, saving y broadcast de NetAttackTerritory).
        Atacar(origen, destino, troopsToAttack, destino.cantidadTropas);

        // Opcional: Broadcast sync adicional si necesitas (ya se envía en Atacar via NetworkSender.SendMessage(NetAttackTerritory)).
        // var syncMsg = new NetAttackResult { attackerName = m.attackerTerritoryName, defenderName = m.defenderTerritoryName, troopsAttacking = troopsToAttack };
        // Server.Instance.Broadcast(syncMsg);
    }
    public void ProcesarRefuerzos(NetMessage msg, NetworkConnection connection)
    {
        var m = msg as NetAddTroopsReinforcement;
        if (m == null)
        {
            Debug.LogWarning("[Partida] Mensaje no es NetAddTroopsReinforcement.");
            return;
        }

        // Obtener jugador remoto via conexión (de Server).
        if (Server.Instance == null || !Server.Instance.connectionToPlayerMap.ContainsKey(connection))
        {
            Debug.LogWarning("[Partida] Conexión inválida o sin jugador asignado en ProcesarRefuerzos.");
            return;
        }
        Jugador player = Server.Instance.connectionToPlayerMap[connection];

        // Validar que sea el turno y fase correcta.
        if (jugadorEnTurno == null || jugadorEnTurno != player || faseActual != FaseTurno.Refuerzos)
        {
            Debug.LogWarning($"[Partida] Jugador {player.alias} no autorizado para refuerzos (turno: {jugadorEnTurno?.alias}, fase: {faseActual}).");
            return;
        }

        Debug.Log($"[Partida] Procesando refuerzos: {m.troopsToAdd} tropas en territorio '{m.territoryName}' para {player.alias}.");

        // Buscar territorio (usa método existente de Mapa).
        Territorio territorio = mapa.GetTerritorioPorNombre(m.territoryName);
        if (territorio == null)
        {
            Debug.LogWarning($"[Partida] Territorio no encontrado: {m.territoryName}.");
            return;
        }

        // Validar propiedad (debe ser del jugador).
        if (territorio.jugadorPropietario != player)
        {
            Debug.LogWarning($"[Partida] Territorio {m.territoryName} no pertenece a {player.alias}.");
            return;
        }

        // Delegar a método existente (ya incluye validaciones de tropas disponibles, UI update, saving y broadcast).
        AgregarTropasRefuerzo(territorio, m.troopsToAdd);

        // Opcional: Broadcast sync adicional si necesitas (ya se envía en AgregarTropasRefuerzo).
        // var syncMsg = new NetTroopsUpdate { territoryName = m.territoryName, newTroops = territorio.cantidadTropas, playerAlias = player.alias };
        // Server.Instance.Broadcast(syncMsg);
    }

    
    



}
