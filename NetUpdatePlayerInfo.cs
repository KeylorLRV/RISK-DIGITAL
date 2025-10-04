using Unity.Networking.Transport;
using Unity.Collections;
using UnityEngine;

public class NetUpdatePlayerInfo : NetMessage
{
    public string playerAlias;
    public float colorR, colorG, colorB, colorA;

    public NetUpdatePlayerInfo()
    {
        Code = OpCode.UPDATE_PLAYER_INFO;
    }

    public NetUpdatePlayerInfo(DataStreamReader reader)
    {
        Code = OpCode.UPDATE_PLAYER_INFO;
        Deserialize(ref reader);
    }

    public override void Serialize(ref DataStreamWriter writer)
    {
        base.Serialize(ref writer);
        writer.WriteFixedString4096(playerAlias);
        writer.WriteFloat(colorR);
        writer.WriteFloat(colorG);
        writer.WriteFloat(colorB);
        writer.WriteFloat(colorA);
    }

    public override void Deserialize(ref DataStreamReader reader)
    {
        base.Deserialize(ref reader);
        playerAlias = reader.ReadFixedString4096().ToString();
        colorR = reader.ReadFloat();
        colorG = reader.ReadFloat();
        colorB = reader.ReadFloat();
        colorA = reader.ReadFloat();
    }

    public override void ReceivedOnClient()
    {
        NetUtility.C_UPDATE_PLAYER_INFO?.Invoke(this);
    }

    public override void ReceivedOnServer(NetworkConnection cnn)
    {
        // Normalmente solo el servidor envía este mensaje, no lo recibe
        Debug.LogWarning("Server received NetUpdatePlayerInfo, which should only be sent to clients.");
    }
}