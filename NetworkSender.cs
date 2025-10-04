using UnityEngine;

public static class NetworkSender
{
    public static bool IsServer = false; // Debes asignar esto según contexto

    /// <summary>
    /// Envía un mensaje por red, usando el canal correcto según si es servidor o cliente.
    /// </summary>
    public static void SendMessage(NetMessage msg)
    {
        if (IsServer)
        {
            // En servidor, normalmente se hace broadcast a todos o a un cliente específico
            Server.Instance.Broadcast(msg);
            // O si quieres enviar a un cliente específico:
            // Server.Instance.SendToClient(clientConnection, msg);
        }
        else
        {
            // En cliente, se envía al servidor
            Client.Instance.SendToServer(msg);
        }
    }
}
