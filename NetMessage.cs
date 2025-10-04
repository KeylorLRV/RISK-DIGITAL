using Unity.Networking.Transport;
using Unity.Collections; // Necesario para DataStreamReader

public class NetMessage
{
    public OpCode Code { get; set; }

    public virtual void Serialize(ref DataStreamWriter writer)
    {
        writer.WriteByte((byte)Code);
    }
    public virtual void Deserialize(ref DataStreamReader reader)
    {
        // El OpCode ya se leyó en NetUtility.OnData, así que no lo leemos aquí de nuevo.
        // Si un mensaje tiene datos adicionales, se leerían aquí.
    }
    public virtual void ReceivedOnClient()
    {
        // Implementación por defecto, cada mensaje específico la sobrescribirá
    }
    public virtual void ReceivedOnServer(NetworkConnection cnn)
    {
        // Implementación por defecto, cada mensaje específico la sobrescribirá
    }
    
}
