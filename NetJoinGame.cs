using Unity.Networking.Transport;
using Unity.Collections;
using UnityEngine;

public class NetJoinGame : NetMessage
{
    public string playerAlias = "Guest";  // Alias del jugador (ej. "ready" para confirmación).
    public byte colorR = 0, colorG = 0, colorB = 0, colorA = 255;  // Colores por defecto (transparente para confirmación simple).

    // Constructor para deserialización (llamado en switch de NetUtility).
    public NetJoinGame(DataStreamReader stream)
    {
        try
        {
            // Deserializa resto del stream (después de OpCode ya leído).
            playerAlias = stream.ReadFixedString4096().ToString();  // Asume alias fijo 32 chars; ajusta si variable.
            colorR = stream.ReadByte();
            colorG = stream.ReadByte();
            colorB = stream.ReadByte();
            colorA = stream.ReadByte();
            Debug.Log($"[NetJoinGame] Deserializado: alias='{playerAlias}', colores=({colorR},{colorG},{colorB},{colorA})");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[NetJoinGame] Error en deserialización: {ex.Message}. Usando defaults.");
            playerAlias = "Unknown";
        }
    }

    // Constructor vacío para creación en cliente.
    public NetJoinGame() { }

    // Para confirmación simple (en cliente: new NetJoinGame { playerAlias = "ready" }).
    public NetJoinGame(string alias)
    {
        playerAlias = alias;
    }

    public override void Serialize(ref DataStreamWriter writer)
    {
        writer.WriteByte((byte)OpCode.JOIN_GAME);  // OpCode primero (26 como byte).
        writer.WriteFixedString32(new FixedString32Bytes(playerAlias));  // Alias fijo 32 bytes.
        writer.WriteByte(colorR);
        writer.WriteByte(colorG);
        writer.WriteByte(colorB);
        writer.WriteByte(colorA);
        Debug.Log($"[NetJoinGame] Serializado: alias='{playerAlias}', OpCode=26 (JOIN_GAME).");
    }

    // NUEVO/FIX: Método que invoca el handler S_JOIN_GAME (llamado desde NetUtility.OnData).
    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        Debug.Log($"[NetJoinGame] ReceivedOnServer llamado para conexión {cnn.GetHashCode()}. Invocando S_JOIN_GAME...");
        NetUtility.S_JOIN_GAME?.Invoke(this, cnn);  // Llama OnJoinGameServer en Server.cs.
    }

    // Opcional: Para cliente (si servidor envía join back, raro).
    public override void ReceivedOnClient()
    {
        Debug.Log($"[NetJoinGame] ReceivedOnClient: Join recibido en cliente (alias='{playerAlias}').");
        NetUtility.C_JOIN_GAME?.Invoke(this);  // Si tienes C_JOIN_GAME definido.
    }
}
