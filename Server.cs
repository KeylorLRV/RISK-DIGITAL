using System;
using System.Collections;
using System.Collections.Generic;  // NECESARIO: Para Dictionary<NetworkConnection, Jugador>.
using Unity.Collections;
using UnityEngine;
using Unity.Networking.Transport;

public class Server : MonoBehaviour
{
    #region Singleton
    public static Server Instance { get; set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Server duplicado detectado. Destruyendo instancia extra.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    #endregion

    public NetworkDriver driver;
    private NativeList<NetworkConnection> connections;

    public bool isActive = false;
    private bool gameStarted = false;  // Flag para evitar múltiples inicios de partida.

    // MODIFICADO: Cambiado a 15s para keep-alives proactivos (envía a todos los clientes cada 15s).
    private const float keepAliveTickRate = 15.0f;
    private float lastKeepAlive;

    public Action connectionDropped;

    // Mapeo de conexiones a jugadores (público para acceso desde Partida).
    public Dictionary<NetworkConnection, Jugador> connectionToPlayerMap = new Dictionary<NetworkConnection, Jugador>();

    // Índice para asignar jugadores por orden de conexión.
    private int nextPlayerIndex = 0;

        public void Init(ushort port)
    {
        if (isActive)
        {
            Debug.LogWarning("Server ya inicializado. Ignorando llamada a Init().");
            return;
        }

        // NUEVO: Cleanup previo si driver existe (previene "Address in use" por binds viejos).
        if (driver.IsCreated)
        {
            Debug.LogWarning("[Server] Driver existente detectado. Forzando shutdown previo.");
            Shutdown();  // Limpia driver, connections, etc.
        }

        driver = NetworkDriver.Create();
        NetworkEndpoint endpoint = NetworkEndpoint.AnyIpv4;
        endpoint.Port = port;

        // Error handling en Bind (línea ~55).
        var bindError = driver.Bind(endpoint);
        if (bindError != 0)
        {
            Debug.LogError($"[Server] Fallo al bind puerto {port}: Address in use (error {bindError}). Posibles causas: Puerto ocupado, firewall, o instancia previa no cerrada. Intenta otro puerto.");
            
            // Opcional: Intenta puerto random (ej. 7777 + Random.Range(1,100)).
            ushort altPort = (ushort)(port + UnityEngine.Random.Range(1, 100));
            endpoint.Port = altPort;
            bindError = driver.Bind(endpoint);
            if (bindError != 0)
            {
                Debug.LogError($"[Server] También falló bind en puerto alternativo {altPort}. Cerrando driver.");
                driver.Dispose();
                driver = default;  // Nullify.
                return;  // No setear isActive.
            }
            else
            {
                Debug.LogWarning($"[Server] Bind exitoso en puerto alternativo {altPort}. Actualizando logs.");
                port = altPort;  // Usa el nuevo puerto para Listen.
            }
        }

        driver.Listen();
        Debug.Log($"[Server] Escuchando en puerto {port} (bind exitoso).");

        connections = new NativeList<NetworkConnection>(2, Allocator.Persistent);
        isActive = true;

        // ... resto de tu código (limpiar mapeo, nextPlayerIndex, gameStarted, lastKeepAlive, RegisterToEvent).
        connectionToPlayerMap.Clear();
        nextPlayerIndex = 0;
        gameStarted = false;
        lastKeepAlive = Time.time;

        RegisterToEvent();
        Debug.Log("[Server] Inicializado correctamente con keep-alives cada " + keepAliveTickRate + "s.");
    }

    // En Shutdown(): Fortalece dispose (agrega al final si no está).
    public void Shutdown()
    {
        UnregisterToEvent();

        if (isActive || driver.IsCreated)  // NUEVO: Chequea driver incluso si !isActive.
        {
            // ... tu código existente (desconectar conexiones, limpiar mapeo).

            // Dispose incondicional.
            if (driver.IsCreated)
            {
                driver.Dispose();
                Debug.Log("[Server] Driver disposed correctamente.");
            }
            if (connections.IsCreated)
            {
                connections.Dispose();
                Debug.Log("[Server] Connections list disposed.");
            }

            isActive = false;
            gameStarted = false;
        }
        else
        {
            Debug.Log("[Server] Shutdown: Nada que limpiar (no activo).");
        }
    }


    

    public void OnDestroy()
    {
        Shutdown();
    }

    public void Update()
    {
        if (!isActive || !driver.IsCreated)
            return;

        KeepAlive();

        driver.ScheduleUpdate().Complete();
        AcceptNewConnections();  // Acepta primero (nuevas conexiones).
        UpdateMessagePump();     // Procesa mensajes (join, ping, sync).
        CleanupConnections();    // Limpia al final (después de procesamiento).
    }


    // MODIFICADO: Ajustado para 15s y log de broadcast (envía keep-alives proactivos a todos los clientes).
    private void KeepAlive()
    {
        if (Time.time - lastKeepAlive > keepAliveTickRate)
        {
            lastKeepAlive = Time.time;
            int activeCount = 0;
            for (int i = 0; i < connections.Length; i++)
            {
                if (connections[i].IsCreated) activeCount++;
            }
            Broadcast(new NetKeepAlive());
            Debug.Log($"[Server] Keep-alive broadcast enviado a {activeCount} clientes activos (cada {keepAliveTickRate}s).");
        }
    }

    private void CleanupConnections()
    {
        for (int i = 0; i < connections.Length; i++)
        {
            if (!connections[i].IsCreated)
            {
                // Limpiar mapeo al desconectar.
                if (connectionToPlayerMap.ContainsKey(connections[i]))
                {
                    Jugador disconnectedPlayer = connectionToPlayerMap[connections[i]];
                    if (Partida.instance != null)
                        Partida.instance.jugadores.EliminarElemento(disconnectedPlayer);
                    connectionToPlayerMap.Remove(connections[i]);
                    Debug.Log($"Jugador {disconnectedPlayer.alias} desconectado y removido del mapeo.");
                }

                connections.RemoveAtSwapBack(i);
                --i;
            }
        }
    }

    private void AcceptNewConnections()
    {
        NetworkConnection c;
        while ((c = driver.Accept()) != default(NetworkConnection))
        {
            connections.Add(c);
            Debug.Log($"Nueva conexión aceptada. Total: {connections.Length}");

            // MODIFICADO: Remover llamada prematura a StartGameForConnectedClients().
            // Ahora, espera a que el cliente envíe NetJoinGame para confirmar y asignar jugador.
            // Solo actualiza UI básica (sin iniciar partida).
            if (GameUI.Instance != null && GameUI.Instance.board != null)
            {
                
                Debug.Log("Nueva conexión aceptada - Esperando confirmación de join para iniciar partida.");
            }

            // Opcional: Envía un mensaje de bienvenida inmediato (sin lógica de partida).
            // SendToClient(c, new NetWelcome());  // Si tienes NetWelcome para handshake.
        }
    }

    // Corutina para delay en inicio (evita loops si el host se conecta localmente).
    private IEnumerator IniciarPartidaConDelay(float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);
        if (connections.Length >= 2 && !gameStarted)
        {
            StartGameForConnectedClients();
        }
    }

    private void UpdateMessagePump()
    {
        DataStreamReader stream;
        for (int i = 0; i < connections.Length; i++)
        {
            NetworkEvent.Type cmd;
            while ((cmd = driver.PopEventForConnection(connections[i], out stream)) != NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Data)
                {
                    Debug.Log($"[Server] *** Data recibido de conexión {i} (ID: {connections[i].GetHashCode()}). Stream length: {stream.Length} bytes. Llamando OnData...");
                    NetUtility.OnData(stream, connections[i], this);  // Pasa 'this' (Server).
                    Debug.Log($"[Server] OnData completado para conexión {i}. ¿Handler invocado? (ver logs de NetUtility/NetJoinGame). Conexión aún válida: {connections[i].IsCreated}");
                }

                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    Debug.Log($"Cliente {i} desconectado.");
                    connections[i] = default(NetworkConnection);
                    connectionDropped?.Invoke();
                    // No shutdown completo; solo limpiar conexión.
                }
            }
        }
    }

    public void SendToClient(NetworkConnection connection, NetMessage msg)
    {
        if (!connection.IsCreated)
        {
            Debug.LogWarning("No se puede enviar a conexión no creada.");
            return;
        }

        DataStreamWriter writer;
        if (driver.BeginSend(connection, out writer) == 0)
        {
            msg.Serialize(ref writer);
            driver.EndSend(writer);
            Debug.Log($"[Server] Mensaje enviado a conexión {connection.GetHashCode()}: {msg.GetType().Name}");
        }
        else
        {
            Debug.LogWarning("Error al enviar mensaje al cliente.");
        }
    }

    public void Broadcast(NetMessage msg)
    {
        for (int i = 0; i < connections.Length; i++)
        {
            if (connections[i].IsCreated)
            {
                SendToClient(connections[i], msg);
            }
        }
    }

    public void StartGameForConnectedClients()
    {
        

        // Inicializar el mapa COMPLETO antes de la partida.
        if (Mapa.instance != null)
        {
            Mapa.instance.InicializarMapa();
            Debug.Log("Mapa inicializado completamente después de conexiones.");
        }
        else
        {
            Debug.LogError("Mapa.instance es null. No se puede inicializar.");
            return;
        }

        // Ahora sí, iniciar la partida.
        if (Partida.instance != null)
        {
            Partida.instance.IniciarPartidaMultijugador();
            Debug.Log("Partida iniciada para todos los clientes conectados.");
        }
        else
        {
            Debug.LogError("Partida.instance es null. No se puede iniciar la partida.");
        }

        gameStarted = true;  // Marcar como iniciado.
    }

    // Suscripción a eventos S_* (cliente → servidor)
    private void RegisterToEvent()
    {
        // Desuscribir primero para evitar duplicados.
        UnregisterToEvent();

        NetUtility.S_SELECT_TERRITORY += OnSelectTerritoryServer;
        NetUtility.S_ADD_TROOPS_REINFORCEMENT += OnAddTroopsReinforcementServer;
        NetUtility.S_MOVE_TROOPS += OnMoveTroopsServer;
        NetUtility.S_ATTACK_TERRITORY += OnAttackTerritoryServer;
        NetUtility.S_NEXT_TURN += OnNextTurnRequestServer;
        NetUtility.S_NEXT_PHASE += OnNextPhaseRequestServer;

        // Evento para join de jugadores.
        NetUtility.S_JOIN_GAME += OnJoinGameServer;

        // NUEVO: Suscribir handler para keep-alives del cliente (respuesta inmediata a pings).
        NetUtility.S_KEEP_ALIVE += OnKeepAliveServer;

        Debug.Log("[Server] Eventos registrados correctamente (incluyendo C_KEEP_ALIVE).");
    }

    private void UnregisterToEvent()
    {
        NetUtility.S_SELECT_TERRITORY -= OnSelectTerritoryServer;
        NetUtility.S_ADD_TROOPS_REINFORCEMENT -= OnAddTroopsReinforcementServer;
        NetUtility.S_MOVE_TROOPS -= OnMoveTroopsServer;
        NetUtility.S_ATTACK_TERRITORY -= OnAttackTerritoryServer;
        NetUtility.S_NEXT_TURN -= OnNextTurnRequestServer;
        NetUtility.S_NEXT_PHASE -= OnNextPhaseRequestServer;

        // Desuscribir join.
        NetUtility.S_JOIN_GAME -= OnJoinGameServer;

        // NUEVO: Desuscribir handler para keep-alives.
        NetUtility.S_KEEP_ALIVE -= OnKeepAliveServer;

        Debug.Log("[Server] Eventos desregistrados correctamente.");
    }

    // NUEVO: Handler para responder inmediatamente a pings del cliente (NetKeepAlive).
    private void OnKeepAliveServer(NetMessage msg, NetworkConnection connection)
    {
        Debug.Log("[Server] Ping recibido de cliente. Respondiendo inmediatamente...");
        SendToClient(connection, new NetKeepAlive());  // Envía pong al cliente específico.
    }

    // Handler para que los clientes se unan y asignen jugadores.
    // Handler simplificado: Solo registra join y chequea si iniciar (sin crear jugadores dinámicos).
    // Handler simplificado: Cuenta joins y llama a IniciarPartidaConAsignacion con 1 join (para multiplayer).
    private void OnJoinGameServer(NetMessage msg, NetworkConnection connection)
    {
        var m = msg as NetJoinGame;
        if (m == null)
        {
            Debug.LogWarning("[Server] Mensaje recibido no es NetJoinGame. Ignorando.");
            return;
        }
        if (connectionToPlayerMap.ContainsKey(connection))
        {
            Debug.LogWarning("Conexión ya tiene jugador asignado.");
            return;
        }

        // Solo incrementar contador y loguear (jugadores se crean en IniciarPartidaConAsignacion).
        nextPlayerIndex++;
        Debug.Log($"[Server] Join confirmado de conexión {connection.GetHashCode()}. Total joins: {nextPlayerIndex}.");

        // Chequear si hay al menos 1 join para iniciar (host + client).
        if (nextPlayerIndex >= 1 && !gameStarted && connections.Length >= 1)
        {
            Debug.Log("[Server] Join de client confirmado. Iniciando partida con asignación fija (host + client).");
            GameUI.Instance.menuAnimator.SetBool("OnlineMenu", false);
            GameUI.Instance.menuAnimator.SetBool("HostMenu", false);
            GameUI.Instance.menuAnimator.SetBool("GameUI", false);
            GameUI.Instance.menuAnimator.SetBool("GUIII", true);
            GameUI.Instance.board.gameObject.SetActive(true);
            IniciarPartidaConAsignacion();  // Aquí se crean y asignan los jugadores fijos.
        }
        else if (nextPlayerIndex < 1)
        {
            Debug.Log("[Server] Esperando join de client para iniciar.");
        }
        else
        {
            Debug.LogWarning("[Server] Joins recibidos, pero no hay conexiones. Iniciando singleplayer.");
            IniciarPartidaConAsignacion();  // Fuerza singleplayer si no hay conexiones.
        }
    }



    // Método para iniciar partida después de asignar jugadores.
    // Método corregido: Crea jugadores fijos y los asigna por orden de conexión (host=player1, client=player2).
    // Método corregido: Sin restricciones en Length. Siempre crea player1 (host). Agrega player2 solo si hay conexiones.
// Método corregido: Sin restricciones en Length. Siempre crea player1 (host). Agrega player2 solo si hay conexiones.
// Usa 'for' en lugar de 'foreach' para iterar sobre Partida.instance.jugadores.
    private void IniciarPartidaConAsignacion()
    {
        Debug.Log("Iniciando partida con jugadores fijos: player1 (azul, host local). Player2 (rojo, client) se agregará si hay conexiones.");

        // No hay validaciones estrictas en Length: Procede siempre.
        int numConnections = connections.Length;
        bool hasClient = numConnections > 0;
        if (hasClient)
        {
            Debug.Log($"[Server] {numConnections} conexión(es) detectada(s). Asignando player2 a la primera (connections[0]). Extras ignoradas.");
        }
        else
        {
            Debug.Log("[Server] Sin conexiones. Iniciando como singleplayer (solo player1 + neutral).");
        }

        // Limpiar mapeo y jugadores previos para forzar asignación fija.
        connectionToPlayerMap.Clear();
        nextPlayerIndex = 0;  // Resetear índice por si acaso.

        // Crear jugador host fijo (player1, azul). NO se mapea a conexión (se maneja localmente).
        Jugador player1 = new Jugador("player1", new Color32(0, 0, 255, 255));  // Azul para host.

        // Agregar a Partida.instance.jugadores.
        if (Partida.instance != null)
        {
            // Limpiar jugadores previos en Partida para evitar duplicados.
            // Asume que ListaJugadores tiene un método Limpiar() o similar; si no, implementa:
            // Partida.instance.jugadores.Limpiar();  // O loop: while (Partida.instance.jugadores.Count > 0) Partida.instance.jugadores.EliminarElemento(Partida.instance.jugadores.Obtener(0));

            while (Partida.instance.jugadores.Contar() > 0)
            {
                Partida.instance.jugadores.EliminarElemento(Partida.instance.jugadores.Obtener(0));
            }  // Ajusta si el método se llama diferente.

            Partida.instance.jugadores.Agregar(player1);

            // Si hay al menos una conexión, crear y agregar player2 (rojo) a la primera conexión.
            if (hasClient)
            {
                Jugador player2 = new Jugador("player2", new Color32(255, 0, 0, 255));  // Rojo para client.
                NetworkConnection clientConnection = connections[0];  // Primera conexión (ignora extras).
                connectionToPlayerMap[clientConnection] = player2;
                Partida.instance.jugadores.Agregar(player2);
                Debug.Log($"[Server] player2 (rojo) asignado a client (conexión {clientConnection.GetHashCode()}).");
            }

            // Agregar neutral si no está (al final de la lista).
            if (Partida.instance.ejercitoNeutral == null)
            {
                Partida.instance.ejercitoNeutral = new Jugador("Neutral", new Color32(150, 150, 150, 255));
                Partida.instance.jugadores.Agregar(Partida.instance.ejercitoNeutral);
            }

            // Asignar turno inicial al host (player1).
            Partida.instance.jugadorEnTurno = player1;
        }
        else
        {
            Debug.LogError("Partida.instance es null. No se pueden agregar jugadores.");
            return;  // Única salida temprana: Si no hay Partida.
        }

        Debug.Log($"[Server] Jugadores asignados: player1 (azul, host local). {(hasClient ? "player2 (rojo, client en connections[0])." : "Sin player2 (singleplayer).")} Total: {Partida.instance.jugadores.Contar()}");

        // Preparar y enviar sync: Solo a clients asignados (no al host).
        if (hasClient && connectionToPlayerMap.Count > 0)
        {
            // Lista de jugadores para sync (incluye player1, player2 y neutral).
            JugadorSaveData[] assignedPlayersData = new JugadorSaveData[Partida.instance.jugadores.Contar()];

            // CAMBIO: Usar 'for' en lugar de 'foreach' para iterar (compatible con Obtener(int index)).
            int idx = 0;
            for (int i = 0; i < Partida.instance.jugadores.Contar(); i++)  // Asume .Count; cambia a .Longitud si es necesario.
            {
                Jugador jugador = Partida.instance.jugadores.Obtener(i);  // Obtener por índice.
                assignedPlayersData[idx++] = new JugadorSaveData
                {
                    alias = jugador.alias,
                    colorR = (byte)jugador.color.r,
                    colorG = (byte)jugador.color.g,
                    colorB = (byte)jugador.color.b,
                    colorA = (byte)jugador.color.a
                    // Agrega otros campos si JugadorSaveData los tiene (ej. tropas iniciales = 0).
                };
            }

            // Enviar sync a cada conexión asignada (en este caso, solo player2).
            foreach (var kvp in connectionToPlayerMap)
            {
                var syncMsg = new NetSyncAssignedPlayers
                {
                    assignedPlayers = assignedPlayersData,
                    localPlayerAlias = kvp.Value.alias  // "player2" para el client.
                };
                SendToClient(kvp.Key, syncMsg);
                Debug.Log($"[Server] Sync enviado a '{kvp.Value.alias}' (conexión {kvp.Key.GetHashCode()}) – Debería activar UI en client.");
            }
        }
        else
        {
            Debug.Log("[Server] Sin clients asignados. No se envía sync por red.");
        }

        // Para host: Activa UI local directamente (sin red).
        if (GameUI.Instance != null)
        {
            // Ejemplo: Setea UI para player1 local (ajusta según tu GameUI).
            // GameUI.Instance.SetLocalPlayer("player1", new Color32(0, 0, 255, 255));
            Debug.Log("[Server] UI local activada para host ('player1').");
        }

        // Ahora iniciar el resto de la partida (siempre).
        StartGameForConnectedClients();
    }




    // Handlers para eventos S_* (placeholders funcionales; ajusta según tu Partida.cs).
    private void OnSelectTerritoryServer(NetMessage msg, NetworkConnection connection)
    {
        var m = msg as NetSelectTerritory;
        if (m == null) return;

                if (!connectionToPlayerMap.ContainsKey(connection))
        {
            Debug.LogWarning("Conexión sin jugador asignado intenta seleccionar territorio.");
            return;
        }

        if (Partida.instance != null)
        {
            Partida.instance.ProcesarSeleccionTerritorio(m, connection);
        }
        Debug.Log($"[Server] Territorio seleccionado procesado para {connectionToPlayerMap[connection].alias}.");
    }

    private void OnAddTroopsReinforcementServer(NetMessage msg, NetworkConnection connection)
    {
        var m = msg as NetAddTroopsReinforcement;
        if (m == null) return;

        if (!connectionToPlayerMap.ContainsKey(connection))
        {
            Debug.LogWarning("Conexión sin jugador asignado intenta agregar tropas.");
            return;
        }

        if (Partida.instance != null)
        {
            Partida.instance.ProcesarRefuerzos(m, connection);
        }
        Debug.Log($"[Server] Refuerzos agregados procesados para {connectionToPlayerMap[connection].alias}.");
    }

    private void OnMoveTroopsServer(NetMessage msg, NetworkConnection connection)
    {
        var m = msg as NetMoveTroops;
        if (m == null) return;

        if (!connectionToPlayerMap.ContainsKey(connection))
        {
            Debug.LogWarning("Conexión sin jugador asignado intenta mover tropas.");
            return;
        }

        if (Partida.instance != null)
        {
            Partida.instance.ProcesarMovimientoTropas(m, connection);
        }
        Debug.Log($"[Server] Movimiento de tropas procesado para {connectionToPlayerMap[connection].alias}.");
    }

    private void OnAttackTerritoryServer(NetMessage msg, NetworkConnection connection)
    {
        var m = msg as NetAttackTerritory;
        if (m == null) return;

        if (!connectionToPlayerMap.ContainsKey(connection))
        {
            Debug.LogWarning("Conexión sin jugador asignado intenta atacar territorio.");
            return;
        }

        if (Partida.instance != null)
        {
            Partida.instance.ProcesarAtaque(m, connection);
        }
        Debug.Log($"[Server] Ataque a territorio procesado para {connectionToPlayerMap[connection].alias}.");
    }

    private void OnNextTurnRequestServer(NetMessage msg, NetworkConnection connection)
    {
        var m = msg as NetNextTurn;  // Asume clase NetNextTurn existe (crea stub si falta).
        if (m == null) return;

        if (!connectionToPlayerMap.ContainsKey(connection))
        {
            Debug.LogWarning("Conexión sin jugador asignado intenta siguiente turno.");
            return;
        }

        if (Partida.instance != null)
        {
            Partida.instance.ProcesarSiguienteTurno(m, connection);  // Asume método en Partida (crea si no existe).
        }
        Debug.Log($"[Server] Siguiente turno procesado para {connectionToPlayerMap[connection].alias}.");
    }

    private void OnNextPhaseRequestServer(NetMessage msg, NetworkConnection connection)
    {
        var m = msg as NetNextPhase;
        if (m == null) return;

        if (!connectionToPlayerMap.ContainsKey(connection))
        {
            Debug.LogWarning($"[Server] Conexión sin jugador asignado (ID: {connection.GetHashCode()}) intenta siguiente fase. Ignorando request.");
            
            // Opcional: Envía feedback al cliente (crea NetError si no tienes).
            // var errorMsg = new NetError { message = "Debes unirte primero (envía NetJoinGame)." };
            // SendToClient(connection, errorMsg);
            
            return;
        }

        // Tu código: Procesar si asignado.
        if (Partida.instance != null)
        {
            Partida.instance.ProcesarSiguienteFase(m, connection);
        }
        Debug.Log($"[Server] Siguiente fase procesada para {connectionToPlayerMap[connection].alias}.");
    }

}
