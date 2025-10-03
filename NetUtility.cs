using System;
using Unity.Networking.Transport;
using UnityEngine;

public enum OpCode
{
    KEEP_ALIVE = 1

}

public static class NetUtility
{
    public static void OnData(Unity.Collections.DataStreamReader stream, NetworkConnection cnn, Server server = null)
    {
        NetMessage msg = null;
        var OpCode = (OpCode)stream.ReadByte();
        switch (OpCode)
        {
            case OpCode.KEEP_ALIVE: msg = new NetKeepAlive(); break;
            default:
                Debug.LogError("Message received with not OpCode");
                break;
        }
        if (server != null)
            msg.ReceivedOnServer(cnn);
        else
            msg.ReceivedOnClient();
    }
    public static Action<NetMessage> C_KEEP_ALIVE;
    public static Action<NetMessage, NetworkConnection> S_KEEP_ALIVE;

}
