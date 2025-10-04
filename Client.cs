using System;
using System.Collections.Generic;  // Para posibles listas internas (si ListaArray lo necesita).
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.AI;

public class Client : MonoBehaviour
{
    #region Singleton
    public static Client Instance { get; set; }

    private float lastHeartbeatTime = 0f;
    private float pingInterval = 10f;  // Envía ping cada 10s.
    private float timeoutDuration = 45f;  // Subido de 30s para tolerancia.
    private Coroutine pingCoroutine;  // Para manejar pings.
    // NUEVO: Variables de respaldo para reconexión automática (seteadas en Init).
    private string reconnectIP;  // IP del servidor para reconectar.
    private ushort reconnectPort;  // Puerto para reconectar.
    private string reconnectAlias;  // Alias del jugador para reconectar.


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Client duplicado detectado. Destruyendo instancia extra.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    #endregion

    public NetworkDriver driver;
    private NetworkConnection connection;

    private bool isActive = false;
    private bool isInitialized = false;  // Flag para evitar múltiples inicializaciones.

    public Action connectionDropped;
    public bool IsConnected;

    // Variable para almacenar el alias del jugador local (resuelve CS0103).
    private string localPlayerAlias = "Guest";

    public void Init(string ip, ushort port, string playerAlias = "Guest")  // Parámetro con default.
    {
        if (isInitialized)
        {
            Debug.LogWarning("Client ya inicializado. Ignorando llamada a Init().");
            return;
        }

        driver = NetworkDriver.Create();
        NetworkEndpoint endpoint = NetworkEndpoint.Parse(ip, port);

        connection = driver.Connect(endpoint);

        Debug.Log($"Intentando conectar al servidor en {endpoint.Address}:{endpoint.Port} como '{playerAlias}'");

        // Asignar alias local.
        localPlayerAlias = string.IsNullOrEmpty(playerAlias) ? "Guest" : playerAlias;

        isActive = true;
        isInitialized = true;

        RegisterToEvent();
        reconnectIP = ip;
        reconnectPort = port;
        reconnectAlias = playerAlias;

        if (pingCoroutine == null)
        {
            pingCoroutine = StartCoroutine(SendPeriodicPings());
        }
    }

    public void Shutdown()
    {

        // 1. Flags PRIMERO: Bloquea Update() y eventos.
        isActive = false;
        if (pingCoroutine != null)
        {
            StopCoroutine(pingCoroutine);
            pingCoroutine = null;
        }
        isInitialized = false;

        // 2. Desuscribir eventos (tu lógica existente).
        UnregisterToEvent();

        // 3. Cleanup conexión (usa el método actualizado).
        CleanupConnection();

        // 4. Dispose driver SOLO al final (ahora seguro, ya que isActive=false bloquea Update).
        if (driver.IsCreated)
        {
            driver.Dispose();  // Esto deallocará NativeQueue<InternalState>.
            Debug.Log("[Client] Driver disposed correctamente.");
        }

        localPlayerAlias = "Guest";  // Tu reset.
        reconnectIP = null;
        reconnectPort = 0;
        reconnectAlias = null;
        Debug.Log("[Client] Shutdown completado: Todo limpio.");
    }


    public void OnDestroy()
    {
        if (isActive || isInitialized)  // Solo si no ya shutdown.
        {
            Shutdown();
        }
        Debug.Log("[Client] OnDestroy: Cleanup final ejecutado.");
    }

    public void Update()
    {
        // CHECKS TEMPRANOS: Salir si no activo o driver no creado.
        if (!isActive || !driver.IsCreated)
        {
            return;
        }

        CheckAlive();  // Puede setear isActive=false si desconecta.

        // DELEGAR TODO A PUMP: No llamar ScheduleUpdate aquí – hazlo dentro si conexión OK.
        try
        {
            UpdateMessagePump();  // Línea 118: Ahora maneja ScheduleUpdate internamente.
        }
        catch (ObjectDisposedException ex)
        {
            Debug.LogError($"[Client] ObjectDisposed en Update (fallback): {ex.Message}. Forzando shutdown.");
            Shutdown();
            return;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Client] Error en Update: {ex.Message}");
        }
    }



    private void CheckAlive()
    {
        if (!connection.IsCreated && isActive)
        {
            Debug.LogWarning("[Client] Conexión perdida (no creada).");
            connectionDropped?.Invoke();
            AttemptReconnect();  // NUEVO: Intenta reconectar en lugar de solo shutdown.
            return;
        }

        // Timeout ajustado: Solo si conectado pero sin actividad.
        if (connection.IsCreated && (Time.time - lastHeartbeatTime) > timeoutDuration)
        {
            Debug.LogWarning("[Client] Timeout de conexión detectado (sin respuesta a heartbeat). Forzando cleanup y reconexión.");
            connectionDropped?.Invoke();
            CleanupConnection();
            AttemptReconnect();  // NUEVO: Intenta reconectar después de cleanup.
            return;
        }
    }


    private void UpdateMessagePump()
    {
        // 1. Check driver (temprano).
        if (!driver.IsCreated)
        {
            Debug.LogWarning("[Client] Pump: Driver no creado. Saltando.");
            return;
        }

        // 2. Check conexión (crítico antes de cualquier update).
        if (!connection.IsCreated)
        {
            Debug.LogWarning("[Client] Pump: Conexión no creada. Saltando procesamiento.");
            return;
        }

        // 3. ScheduleUpdate SOLO si conexión válida (evita reset de cola en estados inconsistentes).
        try
        {
            driver.ScheduleUpdate().Complete();  // Línea ~170 aprox.: Solo aquí, después de checks.
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Client] Error en ScheduleUpdate: {ex.Message}. Posible cierre – limpiando.");
            CleanupConnection();
            return;  // Salir sin loop.
        }

        DataStreamReader stream;
        NetworkEvent.Type cmd;

        // 4. Loop con check EXTRA antes de PopEvent (previene reset si se invalida durante update).
        while (true)
        {
            // CHECK ANTES DE CADA PopEvent: Si conexión se cerró durante ScheduleUpdate, salir.
            if (!connection.IsCreated)
            {
                Debug.LogWarning("[Client] Pump: Conexión invalidada durante loop. Saliendo (posible reset de cola).");
                return;  // No llamar PopEvent – evita el warning de queue reset.
            }

            cmd = connection.PopEvent(driver, out stream);
            if (cmd == NetworkEvent.Type.Empty)
                break;  // Salir del loop si no hay eventos.

            // Procesar evento (tu lógica existente).
            switch (cmd)
            {
                case NetworkEvent.Type.Connect:
                    Debug.Log("[Client] Conexión establecida con el servidor. Enviando mensaje de confirmación (handshake).");

                    // NUEVO: Mensaje de confirmación simple (sin datos de jugador; asignación es en servidor).
                    var confirmMsg = new NetJoinGame("ready");  // Usa constructor con alias.
                    SendToServer(confirmMsg);
                    Debug.Log("[Client] Mensaje de confirmación (NetJoinGame, OpCode=26) enviado. Esperando sync.");

                    Debug.Log("[Client] Mensaje de confirmación (NetJoinGame) enviado. Esperando sync del servidor para activar UI.");

                    // FIX: Enviar y loguear éxito/fallo (asumiendo SendToServer retorna bool; ajusta si no).


                    // Actualiza estado: Ahora IsConnected incluye confirmación.
                    isActive = true;  // O setea en Init, pero confirma aquí.
                    lastHeartbeatTime = Time.time;  // Resetea para heartbeat.

                    // Tu código existente: Inicia pings si no started.
                    if (pingCoroutine == null)
                    {
                        pingCoroutine = StartCoroutine(SendPeriodicPings());
                    }

                    break;

                case NetworkEvent.Type.Data:
                    // FIX: Log específico para Data (ayuda a depurar qué llega).
                    Debug.Log("[Client] Mensaje de datos recibido. Procesando con NetUtility...");

                    // FIX: Pasar 'connection' en lugar de default (mejor para handlers consistentes).
                    NetUtility.OnData(stream, connection);  // Cambiado: Usa connection real.

                    // Opcional: Si sabes el tipo de mensaje, loguea más (ej. después de OnData, chequea si se procesó sync).
                    // Ejemplo: Si tienes un flag global 'syncReceived', loguea "Sync procesado – activando UI."

                    break;

                case NetworkEvent.Type.Disconnect:
                    Debug.Log("[Client] Desconectado del servidor (evento Disconnect).");
                    connectionDropped?.Invoke();
                    CleanupConnection();
                    return;  // Salir del loop.

                default:
                    Debug.LogWarning($"[Client] Evento no manejado: {cmd}");
                    break;
            }
        }
    }




    // En Client.cs: SendToServer mejorado con logs detallados (void, sin cambios en firma).
    public void SendToServer(NetMessage msg)
    {
        string msgName = msg.GetType().Name;  // Captura nombre primero (evita null).
        if (string.IsNullOrEmpty(msgName)) msgName = "Unknown";  // Fallback si falla.

        if (!driver.IsCreated)
        {
            Debug.LogError($"[Client] SendToServer: Driver no creado. No enviando '{msgName}'. Cleanup.");
            CleanupConnection();
            return;
        }

        if (!connection.IsCreated)
        {
            Debug.LogWarning($"[Client] SendToServer: Conexión no creada. '{msgName}' descartado. isActive={isActive}");
            return;
        }

        if (!isActive)
        {
            Debug.LogWarning($"[Client] SendToServer: No activo. '{msgName}' descartado.");
            return;
        }

        DataStreamWriter writer;
        var error = driver.BeginSend(connection, out writer);
        if (error == 0)
        {
            try
            {
                msg.Serialize(ref writer);
                driver.EndSend(writer);
                Debug.Log($"[Client] Mensaje '{msgName}' serializado y enviado exitosamente a servidor.");  // Formato corregido: Sin concatenación rara.
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Client] SendToServer: Error serializando/enviando '{msgName}': {ex.Message}. Descartado.");
            }
        }
        else
        {
            Debug.LogError($"[Client] SendToServer: Error {error} en BeginSend para '{msgName}'. Conexión inestable – Cleanup.");
            CleanupConnection();
        }
    }


    // Nuevo método helper: Limpia conexión de forma segura (llámalo en Disconnect/Shutdown).
    // En Client.cs: Al final de CleanupConnection() (agrega si no está).
    private void CleanupConnection()
    {
        // Tu código existente: 
        // if (connection.IsCreated) connection.Dispose();
        // if (driver.IsCreated) driver.Dispose();
        // etc.

        // NUEVO: Detener coroutine de pings si está corriendo (evita warnings post-cierre).
        if (pingCoroutine != null)
        {
            StopCoroutine(pingCoroutine);
            pingCoroutine = null;
            Debug.Log("[Client] Coroutine de pings detenida en CleanupConnection().");
        }

        // NUEVO: Resetear flags.
        isActive = false;
        lastHeartbeatTime = 0f;

        // Resto de tu código (ej. Invoke events, reset UI).
        Debug.Log("[Client] CleanupConnection completado.");
    }

    // En Client.cs, agrega después de CleanupConnection en CheckAlive o Disconnect.
    private void AttemptReconnect()
    {
        StartCoroutine(ReconnectCoroutine());
    }

    private System.Collections.IEnumerator ReconnectCoroutine()
    {
        Debug.Log("[Client] Intentando reconexión automática...");
        yield return new WaitForSeconds(5f);  // Espera 5s.

        // Asume variables: string reconnectIP, ushort reconnectPort, string reconnectAlias (setea en Init).
        Init(reconnectIP, reconnectPort, reconnectAlias);

        // Si falla después de 3 intentos, notifica manual.
        int attempts = 0;
        while (!connection.IsCreated && attempts < 3)
        {
            yield return new WaitForSeconds(10f);
            Init(reconnectIP, reconnectPort, reconnectAlias);
            attempts++;
        }

        if (connection.IsCreated)
        {
            Debug.Log("[Client] Reconexión exitosa.");
            // Re-suscribir eventos y resetear Partida.
        }
        else
        {
            connectionDropped?.Invoke();  // Falla → UI manual.
        }
    }
    // En Client.cs: Coroutine de pings adaptada con chequeos de seguridad (basada en tu versión original).
// Mantiene NetKeepAlive, lastHeartbeatTime, y logs tuyos; agrega chequeos detallados para evitar warnings.
    private System.Collections.IEnumerator SendPeriodicPings()
    {
        // Asume pingInterval es float de clase (ej. private float pingInterval = 5f;). Si no, hardcodea aquí.
        float interval = pingInterval;  // Usa tu variable; cambia a 5f si no existe.

        Debug.Log($"[Client] Iniciando coroutine de pings periódicos (intervalo: {interval}s).");

        while (true)  // Cambiado a while(true) para chequeos explícitos; más controlable que while(isActive && connection.IsCreated).
        {
            // Chequeo ANTES de wait: Si ya falló, para inmediatamente (evita waits innecesarios).
            if (!isActive || !connection.IsCreated || !driver.IsCreated)
            {
                Debug.LogWarning("[Client] SendPeriodicPings: Condiciones inválidas antes de wait (isActive={0}, connection={1}, driver={2}). Parando coroutine.");
                yield break;  // Sale permanentemente de la coroutine.
            }

            // Espera el intervalo (tu lógica original).
            yield return new WaitForSeconds(interval);

            // Chequeo DESPUÉS de wait: Si se invalidó durante espera (ej. Disconnect), para (evita envíos inválidos).
            if (!isActive || !connection.IsCreated || !driver.IsCreated)
            {
                Debug.LogWarning("[Client] SendPeriodicPings: Condiciones inválidas después de wait (isActive={0}, connection={1}, driver={2}). Parando coroutine.");
                yield break;  // Sale permanentemente.
            }

            // Crear y enviar ping (tu lógica original, solo si todo OK).
            var pingMsg = new NetKeepAlive();  // Tu mensaje simple (OpCode.KEEP_ALIVE).
            SendToServer(pingMsg);
            lastHeartbeatTime = Time.time;  // Actualiza al enviar (previene timeout local inmediato).
            Debug.Log("[Client] Ping enviado al servidor (manteniendo conexión).");  // Tu log original.
        }
        // Este log nunca se alcanza (debido a yield break), pero lo mantengo por compatibilidad.
        // Si quieres, muévelo antes de yield break en los warnings.
        
    }




    // Suscripción a eventos C_* (servidor → cliente)
    private void RegisterToEvent()
    {
        // Desuscribir primero para evitar duplicados.
        UnregisterToEvent();

        NetUtility.C_KEEP_ALIVE += OnKeepAlive;

        NetUtility.C_START_GAME += OnStartGameClient;
        NetUtility.C_NEXT_TURN += OnNextTurnClient;
        NetUtility.C_NEXT_PHASE += OnNextPhaseClient;
        NetUtility.C_UPDATE_TROOPS_VISUAL += OnUpdateTroopsVisualClient;
        NetUtility.C_RESOLVE_COMBAT += OnResolveCombatClient;
        NetUtility.C_SYNC_MAP_DATA += OnSyncMapDataClient;
        NetUtility.C_SYNC_PLAYERS_DATA += OnSyncPlayersDataClient;
        NetUtility.C_SYNC_CURRENT_GAME_STATE += OnSyncCurrentGameStateClient;

        // Evento para sync de jugadores asignados.
        NetUtility.C_SYNC_ASSIGNED_PLAYERS += OnSyncAssignedPlayersClient;

        Debug.Log("[Cliente] Eventos registrados correctamente.");
    }

    private void UnregisterToEvent()
    {
        NetUtility.C_KEEP_ALIVE -= OnKeepAlive;

        NetUtility.C_START_GAME -= OnStartGameClient;
        NetUtility.C_NEXT_TURN -= OnNextTurnClient;
        NetUtility.C_NEXT_PHASE -= OnNextPhaseClient;
        NetUtility.C_UPDATE_TROOPS_VISUAL -= OnUpdateTroopsVisualClient;
        NetUtility.C_RESOLVE_COMBAT -= OnResolveCombatClient;
        NetUtility.C_SYNC_MAP_DATA -= OnSyncMapDataClient;
        NetUtility.C_SYNC_PLAYERS_DATA -= OnSyncPlayersDataClient;
        NetUtility.C_SYNC_CURRENT_GAME_STATE -= OnSyncCurrentGameStateClient;

        // Desuscribir sync de jugadores.
        NetUtility.C_SYNC_ASSIGNED_PLAYERS -= OnSyncAssignedPlayersClient;

        Debug.Log("[Cliente] Eventos desregistrados correctamente.");
    }

    private void OnKeepAlive(NetMessage nm)
    {
        lastHeartbeatTime = Time.time;  // CLAVE: Resetea timeout al recibir pong.
        Debug.Log("[Client] Keep alive recibido – conexión estable.");
    }

    private void OnStartGameClient(NetMessage nm)
    {
        var msg = nm as NetStartGame;
        if (msg == null) return;

        Debug.Log("[Cliente] Recibido inicio de partida");
        if (Partida.instance != null && Partida.instance.estado == Partida.EstadoPartida.EnCurso)
        {
            return;  // Guard para evitar re-procesamiento.
        }

        if (Partida.instance != null)
        {
            Partida.instance.SincronizarInicioPartida();
        }
        else
        {
            Debug.LogWarning("[Cliente] Partida.instance es null al recibir inicio de partida");
        }
    }

    private void OnNextTurnClient(NetMessage nm)
    {
        var msg = nm as NetNextTurn;
        if (msg == null) return;

        Debug.Log($"[Cliente] Recibido siguiente turno: {msg.playerAlias}");
        if (Partida.instance != null)
        {
            Partida.instance.SincronizarSiguienteTurno(msg.playerAlias, msg.fase, msg.roundCounter);
        }
    }

    private void OnNextPhaseClient(NetMessage nm)
    {
        var msg = nm as NetNextPhase;
        if (msg == null) return;

        Debug.Log("[Cliente] Recibido siguiente fase");
        if (Partida.instance != null)
        {
            Partida.instance.SincronizarSiguienteFase(msg.newPhase);
        }
    }

    private void OnUpdateTroopsVisualClient(NetMessage msg)
    {
        if (msg is NetUpdateTroopsVisual message)
        {
            string territoryNameStr = message.territoryName.ToString();
            if (string.IsNullOrEmpty(territoryNameStr))
            {
                territoryNameStr = "Territorio Desconocido";
            }
            string playerAliasStr = message.playerAlias.ToString();
            if (string.IsNullOrEmpty(playerAliasStr))
            {
                playerAliasStr = "Jugador Desconocido";
            }
            int troopsCount = message.troopsCount;
            Debug.Log($"Actualizando tropas en {territoryNameStr} para {playerAliasStr}: {troopsCount} tropas.");
            // Integra tu lógica: UpdateTroopsUI(territoryNameStr, playerAliasStr, troopsCount);
        }
        else
        {
            Debug.LogError("Mensaje recibido no es de tipo NetUpdateTroopsVisual.");
        }
    }

    private void OnResolveCombatClient(NetMessage nm)
    {
        var msg = nm as NetResolveCombat;
        if (msg == null) return;

        Debug.Log("[Cliente] Recibida resolución de combate");
        if (Partida.instance != null)
        {
            Partida.instance.SincronizarResolucionCombate(msg);
        }
    }

    private void OnSyncMapDataClient(NetMessage nm)
    {
        var msg = nm as NetSyncMapData;
        if (msg == null) return;

        Debug.Log("[Cliente] Recibida sincronización de mapa");
        if (Partida.instance != null)
        {
            Partida.instance.SincronizarDatosMapa(msg);
        }
    }

    private void OnSyncPlayersDataClient(NetMessage nm)
    {
        var msg = nm as NetSyncPlayersData;
        if (msg == null) return;

        Debug.Log("[Cliente] Recibida sincronización de jugadores");
        if (Partida.instance != null)
        {
            Partida.instance.SincronizarDatosJugadores(msg);
        }
    }

    // Completado: Handler para estado actual del juego (con guard por ronda).
    private void OnSyncCurrentGameStateClient(NetMessage nm)
    {
        var msg = nm as NetSyncCurrentGameState;
        if (msg == null) return;

        Debug.Log($"[Cliente] Estado actual recibido: {msg.currentPlayerAlias} (ronda {msg.currentRound})");

        // Guard para evitar duplicados: Si ya estamos en la misma ronda, ignora.
        if (Partida.instance != null && Partida.instance.contadorRonda == msg.currentRound)
        {
            Debug.Log("[Cliente] Estado ya sincronizado para esta ronda. Ignorando.");
            return;
        }

        if (Partida.instance != null)
        {
            Partida.instance.SincronizarEstadoActual(msg);
        }
        else
        {
            Debug.LogWarning("[Cliente] Partida.instance es null al recibir estado actual");
        }
    }

    // Nuevo: Handler para sync de jugadores asignados dinámicamente (resuelve limpieza de ListaArray).
    private void OnSyncAssignedPlayersClient(NetMessage msg)
    {
        var m = msg as NetSyncAssignedPlayers;
        if (m == null)
        {
            Debug.LogWarning("[Client] Mensaje recibido no es NetSyncAssignedPlayers. Ignorando.");
            return;
        }

        Debug.Log($"[Cliente] Recibido sync de jugadores asignados. Local: {m.localPlayerAlias}. Total: {m.assignedPlayers?.Length ?? 0}");

        if (Partida.instance == null)
        {
            Debug.LogWarning("[Cliente] Partida.instance es null al recibir sync de jugadores asignados");
            return;
        }

        // Limpiar lista local de jugadores humanos (mantener neutral si existe).
        // Usa loop manual ya que ListaArray no tiene EliminarTodos() (resuelve CS1061).
        if (Partida.instance.jugadores != null)
        {
            for (int i = Partida.instance.jugadores.Contar() - 1; i >= 0; i--)
            {
                Jugador j = Partida.instance.jugadores.Obtener(i);
                if (j.alias != "Neutral")  // Solo eliminar humanos; mantener neutral.
                {
                    Partida.instance.jugadores.Eliminar(i);  // Asume que ListaArray tiene Eliminar(int index).
                }
            }
        }
        else
        {
            // Si no existe, inicializar nueva lista.
            Partida.instance.jugadores = new ListaArray<Jugador>();
        }

        // Recrear jugadores basados en los datos asignados del servidor.
        if (m.assignedPlayers != null)
        {
            foreach (var jData in m.assignedPlayers)
            {
                if (string.IsNullOrEmpty(jData.alias) || jData.alias == "Neutral") continue;

                Color32 color = new Color32(jData.colorR, jData.colorG, jData.colorB, jData.colorA);
                Jugador j = new Jugador(jData.alias, color);
                Partida.instance.jugadores.Agregar(j);
                Debug.Log($"Jugador sincronizado: '{j.alias}' con color {color}");
            }
        }

        // Asignar jugador local basado en localPlayerAlias.
        Partida.instance.localPlayer = null;
        if (!string.IsNullOrEmpty(m.localPlayerAlias))
        {
            Partida.instance.localPlayer = Partida.instance.jugadores.Buscar(j => j.alias == m.localPlayerAlias);
            Debug.Log($"Jugador local asignado: {Partida.instance.localPlayer?.alias ?? "No encontrado"}");
        }
        Partida.instance.isJoined = true;
        ActivarUIMultijugador();

        // Actualizar UI con los jugadores sincronizados.
        Partida.instance.ActualizarUIJugadores();

        // Opcional: Si tienes InfoGUI para lobby, actualízalo (comenta si no existe el método).
        // if (Partida.instance.infoGUI != null)
        // {
        //     Partida.instance.infoGUI.ActualizarListaJugadores(Partida.instance.jugadores);
        // }
    }
    // NUEVO/MODIFICADO: Handler para cuando llega sync de jugadores (setea joined Y activa UI/board).


// NUEVO: Método para activar UI/board (llamado en cliente al joined).
    private void ActivarUIMultijugador()
    {
        if (GameUI.Instance == null)
        {
            Debug.LogError("[Partida] GameUI.Instance null en cliente. No se puede activar UI.");
            return;
        }

        // Tu lógica de UI original (adaptada de Server.AcceptNewConnections).
        GameUI.Instance.menuAnimator.SetBool("OnlineMenu", false);
        GameUI.Instance.menuAnimator.SetBool("HostMenu", false);
        GameUI.Instance.menuAnimator.SetBool("GameUI", false);
        GameUI.Instance.menuAnimator.SetBool("GUIII", true);  // Activa animador para board.

        if (GameUI.Instance.board != null)
        {
            GameUI.Instance.board.gameObject.SetActive(true);  // Muestra board.
            Debug.Log("[Partida] Board activado en cliente (multijugador joined).");
        }
        else
        {
            Debug.LogWarning("[Partida] GameUI.board null. Verifica referencia en inspector.");
        }

        // Opcional: Habilitar botones de juego (fases, etc.).
        // if (GameUI.Instance.botonSiguienteFase != null) GameUI.Instance.botonSiguienteFase.interactable = true;

        // Si tienes infoGUI o status: Actualiza texto (ej. "Conectado como " + localPlayer.alias).
    }

}