using System;
using Unity.Networking.Transport;
using UnityEngine;
using Unity.Collections; // Necesario para DataStreamReader

public enum OpCode
{
    KEEP_ALIVE = 1,
    // Mensajes de Partida
    START_GAME = 2,
    NEXT_TURN = 3,
    NEXT_PHASE = 4,
    UPDATE_PLAYER_INFO = 5, // Para actualizar la UI de los jugadores
    UPDATE_TROOPS_VISUAL = 6, // Para actualizar visualmente las tropas en un territorio

    // Mensajes de Territorio
    SELECT_TERRITORY = 7,
    ADD_TROOPS_REINFORCEMENT = 8,
    MOVE_TROOPS = 9,
    ATTACK_TERRITORY = 10,
    RESOLVE_COMBAT = 11,
    CONQUER_TERRITORY = 12,
    UPDATE_TERRITORY_OWNER = 13,
    UPDATE_TERRITORY_TROOPS = 14,

    // Mensajes de Cartas
    DRAW_CARD = 15,
    EXCHANGE_CARDS = 16,

    // Mensajes de UI/Paneles
    SHOW_ATTACK_PANEL = 17,
    HIDE_ATTACK_PANEL = 18,
    SHOW_DICE_ROLL = 19, // Para mostrar los dados rodando
    HIDE_DICE_ROLL = 20,
    SHOW_TIE_MESSAGE = 21, // Para el mensaje de empate en combate

    // Mensajes de Sincronización Inicial
    SYNC_MAP_DATA = 22, // Para enviar el estado completo del mapa al unirse un cliente
    SYNC_PLAYERS_DATA = 23, // Para enviar la información de todos los jugadores
    SYNC_CURRENT_GAME_STATE = 24, // Para enviar el estado actual de la partida (fase, turno, etc.)

    SYNC_ASSIGNED_PLAYERS = 25,
    JOIN_GAME = 26,
}

public static class NetUtility
{
    public static void OnData(DataStreamReader stream, NetworkConnection cnn, Server server = null)
    {
        if (stream.Length < 1)
        {
            Debug.LogWarning("[NetUtility] Stream vacío en OnData. Ignorando.");
            return;
        }

        try
        {
            var readerOpCode = stream.ReadByte();  // Lee OpCode como byte raw.
            var opCode = (OpCode)readerOpCode;  // Cast a enum para switch.
            Debug.Log($"[NetUtility] OnData: OpCode leído = {readerOpCode} ({opCode}), Stream total: {stream.Length} bytes, Conexión: {cnn.GetHashCode()}, Server: {(server != null ? "Sí" : "No")}");

            NetMessage msg = null;
            switch (opCode)
            {
                case OpCode.KEEP_ALIVE:
                    Debug.Log("[NetUtility] Case KEEP_ALIVE (1). Creando NetKeepAlive...");
                    msg = new NetKeepAlive(stream);
                    break;
                case OpCode.START_GAME:
                    msg = new NetStartGame(stream);
                    break;
                case OpCode.NEXT_TURN:
                    msg = new NetNextTurn(stream);
                    break;
                case OpCode.NEXT_PHASE:
                    msg = new NetNextPhase(stream);
                    break;
                case OpCode.UPDATE_PLAYER_INFO:
                    msg = new NetUpdatePlayerInfo(stream);
                    break;
                case OpCode.UPDATE_TROOPS_VISUAL:
                    msg = new NetUpdateTroopsVisual(stream);
                    break;
                case OpCode.SELECT_TERRITORY:
                    msg = new NetSelectTerritory(stream);
                    break;
                case OpCode.ADD_TROOPS_REINFORCEMENT:
                    msg = new NetAddTroopsReinforcement(stream);
                    break;
                case OpCode.MOVE_TROOPS:
                    msg = new NetMoveTroops(stream);
                    break;
                case OpCode.ATTACK_TERRITORY:
                    msg = new NetAttackTerritory(stream);
                    break;
                case OpCode.RESOLVE_COMBAT:
                    msg = new NetResolveCombat(stream);
                    break;
                case OpCode.CONQUER_TERRITORY:
                    msg = new NetConquerTerritory(stream);
                    break;
                case OpCode.UPDATE_TERRITORY_OWNER:
                    msg = new NetUpdateTerritoryOwner(stream);
                    break;
                case OpCode.UPDATE_TERRITORY_TROOPS:
                    msg = new NetUpdateTerritoryTroops(stream);
                    break;
                case OpCode.DRAW_CARD:
                    msg = new NetDrawCard(stream);
                    break;
                case OpCode.EXCHANGE_CARDS:
                    msg = new NetExchangeCards(stream);
                    break;
                case OpCode.SHOW_ATTACK_PANEL:
                    msg = new NetShowAttackPanel(stream);
                    break;
                case OpCode.HIDE_ATTACK_PANEL:
                    msg = new NetHideAttackPanel(stream);
                    break;
                case OpCode.SHOW_DICE_ROLL:
                    msg = new NetShowDiceRoll(stream);
                    break;
                case OpCode.HIDE_DICE_ROLL:
                    msg = new NetHideDiceRoll(stream);
                    break;
                case OpCode.SHOW_TIE_MESSAGE:
                    msg = new NetShowTieMessage(stream);
                    break;
                case OpCode.SYNC_MAP_DATA:
                    msg = new NetSyncMapData(stream);
                    break;
                case OpCode.SYNC_PLAYERS_DATA:
                    msg = new NetSyncPlayersData(stream);
                    break;
                case OpCode.SYNC_CURRENT_GAME_STATE:
                    msg = new NetSyncCurrentGameState(stream);
                    break;
                case OpCode.SYNC_ASSIGNED_PLAYERS:
                    msg = new NetSyncAssignedPlayers(stream);
                    break;
                case OpCode.JOIN_GAME:
                    Debug.Log("[NetUtility] *** Case JOIN_GAME (26) detectado! *** Creando NetJoinGame...");
                    msg = new NetJoinGame(stream);
                    Debug.Log("[NetUtility] NetJoinGame creado exitosamente.");
                    break;
                
                default:
                    Debug.LogError($"[NetUtility] OpCode desconocido: {readerOpCode} ({opCode}). Ignorando mensaje de {stream.Length + 1} bytes totales.");
                    return;  // Sale temprano sin invocar handler (stream se ignora automáticamente por UTP).
            }

            if (msg == null)
            {
                Debug.LogError("[NetUtility] msg es null después de switch. Posible fallo en constructor/deserialización (ej. exception en new Msg(stream)).");
                return;
            }

            Debug.Log($"[NetUtility] Mensaje '{msg.GetType().Name}' creado exitosamente. Invocando handler...");
            
            if (server != null)
            {
                msg.ReceivedOnServer(cnn);  // Aquí se invoca S_JOIN_GAME (o S_KEEP_ALIVE, etc.) si implementado en la clase Msg.
                Debug.Log("[NetUtility] ReceivedOnServer completado para servidor.");
            }
            else
            {
                msg.ReceivedOnClient();
                Debug.Log("[NetUtility] ReceivedOnClient completado para cliente.");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[NetUtility] Error general en OnData: {ex.Message}. Stack: {ex.StackTrace}. Stream perdido – posible fallo en ReadByte o switch.");
        }
    }


    // Acciones para el Cliente
    public static Action<NetMessage> C_KEEP_ALIVE;
    public static Action<NetMessage> C_START_GAME;
    public static Action<NetMessage> C_NEXT_TURN;
    public static Action<NetMessage> C_NEXT_PHASE;
    public static Action<NetMessage> C_UPDATE_PLAYER_INFO;
    public static Action<NetMessage> C_UPDATE_TROOPS_VISUAL;
    public static Action<NetMessage> C_SELECT_TERRITORY; // El cliente recibe la confirmación de selección
    public static Action<NetMessage> C_ADD_TROOPS_REINFORCEMENT;
    public static Action<NetMessage> C_MOVE_TROOPS;
    public static Action<NetMessage> C_ATTACK_TERRITORY; // El cliente recibe la orden de iniciar el combate
    public static Action<NetMessage> C_RESOLVE_COMBAT;
    public static Action<NetMessage> C_CONQUER_TERRITORY;
    public static Action<NetMessage> C_UPDATE_TERRITORY_OWNER;
    public static Action<NetMessage> C_UPDATE_TERRITORY_TROOPS;
    public static Action<NetMessage> C_DRAW_CARD;
    public static Action<NetMessage> C_EXCHANGE_CARDS;
    public static Action<NetMessage> C_SHOW_ATTACK_PANEL;
    public static Action<NetMessage> C_HIDE_ATTACK_PANEL;
    public static Action<NetMessage> C_SHOW_DICE_ROLL;
    public static Action<NetMessage> C_HIDE_DICE_ROLL;
    public static Action<NetMessage> C_SHOW_TIE_MESSAGE;
    public static Action<NetMessage> C_SYNC_MAP_DATA;
    public static Action<NetMessage> C_SYNC_PLAYERS_DATA;
    public static Action<NetMessage> C_SYNC_CURRENT_GAME_STATE;
    public static Action<NetMessage> C_SYNC_ASSIGNED_PLAYERS;
    public static Action<NetMessage> C_JOIN_GAME;  // Para si servidor envía join al cliente.


    // Acciones para el Servidor
    public static Action<NetMessage, NetworkConnection> S_JOIN_GAME;   
  
    public static Action<NetMessage, NetworkConnection> S_KEEP_ALIVE;
    public static Action<NetMessage, NetworkConnection> S_START_GAME; // Un cliente puede pedir iniciar el juego (ej. host)
    public static Action<NetMessage, NetworkConnection> S_NEXT_TURN;
    public static Action<NetMessage, NetworkConnection> S_NEXT_PHASE;
    public static Action<NetMessage, NetworkConnection> S_SELECT_TERRITORY;
    public static Action<NetMessage, NetworkConnection> S_ADD_TROOPS_REINFORCEMENT;
    public static Action<NetMessage, NetworkConnection> S_MOVE_TROOPS;
    public static Action<NetMessage, NetworkConnection> S_ATTACK_TERRITORY;
    public static Action<NetMessage, NetworkConnection> S_RESOLVE_COMBAT; // El servidor resuelve y notifica el resultado
    public static Action<NetMessage, NetworkConnection> S_CONQUER_TERRITORY;
    public static Action<NetMessage, NetworkConnection> S_DRAW_CARD;
    public static Action<NetMessage, NetworkConnection> S_EXCHANGE_CARDS;
    // Los mensajes de UI (SHOW_ATTACK_PANEL, HIDE_ATTACK_PANEL, SHOW_DICE_ROLL, etc.) generalmente solo se envían del servidor al cliente
    // Los mensajes de sincronización inicial (SYNC_MAP_DATA, SYNC_PLAYERS_DATA, SYNC_CURRENT_GAME_STATE) también son del servidor al cliente
}
