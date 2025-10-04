using System;
using Unity.Networking.Transport;
using Unity.Collections;
using UnityEngine;

[System.Serializable]
public class NetSyncAssignedPlayers : NetMessage
{
    public JugadorSaveData[] assignedPlayers;  // Array con jugadores asignados (sin neutral).
    public string localPlayerAlias;  // Alias del jugador local para este cliente.

    // Constructor: Asigna el Code específico (resuelve CS0103 en línea 11).
    public NetSyncAssignedPlayers()
    {
        Code = OpCode.SYNC_ASSIGNED_PLAYERS;  // CORREGIDO: Usa 'Code' en lugar de 'opCode'.
    }
    public NetSyncAssignedPlayers(DataStreamReader reader)
    {
        Code = OpCode.SYNC_ASSIGNED_PLAYERS;  // CORREGIDO: Usa 'Code' en lugar de 'opCode'.
        Deserialize(ref reader);
    }


    // Serializar: Llama base (escribe Code) + datos específicos.
    public override void Serialize(ref DataStreamWriter writer)
    {
        base.Serialize(ref writer);  // Escribe el Code (de la base; resuelve cualquier referencia a opCode).

        // Serializar array: Longitud + elementos.
        writer.WriteInt(assignedPlayers?.Length ?? 0);
        if (assignedPlayers != null)
        {
            foreach (var jData in assignedPlayers)
            {
                writer.WriteFixedString4096(jData.alias);
                writer.WriteByte(jData.colorR);
                writer.WriteByte(jData.colorG);
                writer.WriteByte(jData.colorB);
                writer.WriteByte(jData.colorA);
            }
        }

        // Serializar alias local al final.
        writer.WriteFixedString4096(localPlayerAlias ?? string.Empty);
    }

    // Deserializar: Lee solo datos específicos (Code ya se leyó en NetUtility y asignó a la propiedad).
    public override void Deserialize(ref DataStreamReader reader)
    {
        // Deserializar array: Leer longitud + elementos.
        int length = reader.ReadInt();
        assignedPlayers = new JugadorSaveData[length];
        for (int i = 0; i < length; i++)
        {
            assignedPlayers[i] = new JugadorSaveData
            {
                alias = reader.ReadFixedString4096().ToString(),
                colorR = reader.ReadByte(),
                colorG = reader.ReadByte(),
                colorB = reader.ReadByte(),
                colorA = reader.ReadByte()
            };
        }

        // Leer alias local.
        localPlayerAlias = reader.ReadFixedString4096().ToString();
    }

    // Opcional: Manejo de recepción en cliente (puedes invocarlo desde NetUtility o Client.cs).
    public override void ReceivedOnClient()
    {
        Debug.Log($"[NetSyncAssignedPlayers] Recibido en cliente: Local '{localPlayerAlias}', {assignedPlayers?.Length ?? 0} jugadores.");
        // Aquí puedes agregar lógica inmediata, ej. llamar directamente a Partida.instance.ActualizarUIJugadores().
        // Por ahora, vacío; usa el evento C_SYNC_ASSIGNED_PLAYERS en Client.cs para el manejo principal.
    }

    // No necesita ReceivedOnServer, ya que es un mensaje servidor → cliente.
}
