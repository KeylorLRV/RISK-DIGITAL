// NetSyncMapData.cs
using Unity.Networking.Transport;
using Unity.Collections;
using UnityEngine;
using System.Collections.Generic;

public class NetSyncMapData : NetMessage
{
    // Podrías serializar una lista de TerritorioSaveData (definida en MapaData.cs)
    public TerritorioSaveData[] allTerritoriesData;

    public NetSyncMapData()
    {
        Code = OpCode.SYNC_MAP_DATA;
    }

    public NetSyncMapData(DataStreamReader reader)
    {
        Code = OpCode.SYNC_MAP_DATA;
        Deserialize(ref reader);
    }

    public override void Serialize(ref DataStreamWriter writer)
    {
        base.Serialize(ref writer);
        writer.WriteInt(allTerritoriesData.Length);
        foreach (var tData in allTerritoriesData)
        {
            writer.WriteFixedString4096(tData.nombre);
            writer.WriteInt(tData.cantidadTropas);
            writer.WriteFixedString4096(tData.propietarioAlias);
        }
    }

    public override void Deserialize(ref DataStreamReader reader)
    {
        base.Deserialize(ref reader);
        int count = reader.ReadInt();
        allTerritoriesData = new TerritorioSaveData[count];
        for (int i = 0; i < count; i++)
        {
            allTerritoriesData[i] = new TerritorioSaveData
            {
                nombre = reader.ReadFixedString4096().ToString(),
                cantidadTropas = reader.ReadInt(),
                propietarioAlias = reader.ReadFixedString4096().ToString()
            };
        }
    }

    public override void ReceivedOnClient()
    {
        NetUtility.C_SYNC_MAP_DATA?.Invoke(this);
    }

    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        // El servidor no debería recibir este mensaje, solo enviarlo.
        Debug.LogWarning("Server received NetSyncMapData, which should only be sent to clients.");
    }
}
