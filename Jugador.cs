using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Jugador
{
    // Enum para los tipos de cartas (si se usan)
    public enum TipoCarta
    {
        Infanteria,
        Caballeria,
        Artilleria,
        Comodin // Si hay cartas comodín
    }

    // Atributos (sin cambios)
    public string alias;
    public Color32 color; // Usamos Color32 para Unity
    public int tropasDisponibles;
    public ListaArray<TipoCarta> cartas; // Usamos ListaArray
    public ListaArray<Territorio> territoriosConquistados; // Usamos ListaArray

    // Constructor (sin cambios)
    public Jugador(string alias, Color32 color)
    {
        this.alias = alias;
        this.color = color;
        this.tropasDisponibles = 0;
        this.cartas = new ListaArray<TipoCarta>();
        this.territoriosConquistados = new ListaArray<Territorio>();
    }

    // Métodos existentes (RecibirRefuerzos, AgregarCarta, IntercambiarCartas) – sin cambios
    /// <summary>
    /// Suma la cantidad de tropas de refuerzo a las tropas disponibles del jugador.
    /// </summary>
    public void RecibirRefuerzos(int cantidad)
    {
        tropasDisponibles += cantidad;
        Debug.Log(alias + " ha recibido " + cantidad + " refuerzos. Total disponibles: " + tropasDisponibles);
    }
    public void UsarTropas(int cantidad)
    {
        if (cantidad > tropasDisponibles)
        {
            Debug.LogWarning(alias + " no tiene suficientes tropas disponibles. Tropas disponibles: " + tropasDisponibles);
            return;
        }
        tropasDisponibles -= cantidad;
        Debug.Log(alias + " ha usado " + cantidad + " tropas. Tropas restantes: " + tropasDisponibles);
    }

    /// <summary>
    /// Agrega una carta a la mano del jugador.
    /// </summary>
    public bool AgregarCarta(TipoCarta carta)
    {
        if (cartas.Contar() >= 5)
        {
            Debug.LogWarning(alias + " ya tiene 5 cartas. Debe intercambiar antes de recibir una nueva.");
            return false;
        }
        cartas.Agregar(carta);
        Debug.Log(alias + " ha recibido una carta de " + carta.ToString() + ". Total de cartas: " + cartas.Contar());
        

        return true;
    }

    /// <summary>
    /// Intenta intercambiar cartas por tropas.
    /// </summary>
    public int IntercambiarCartas()
    {
        // ... (código existente de IntercambiarCartas, sin cambios)
        // (Mantén el código que te di antes para la lógica de tríos)
        TipoCarta? trioEncontrado = null;
        foreach (TipoCarta tipo in System.Enum.GetValues(typeof(TipoCarta)))
        {
            if (tipo == TipoCarta.Comodin) continue;

            int count = 0;
            for (int i = 0; i < cartas.Contar(); i++)
            {
                if (cartas.Obtener(i) == tipo)
                {
                    count++;
                }
            }
            if (count >= 3)
            {
                trioEncontrado = tipo;
                break;
            }
        }

        bool hasInfanteria = false;
        bool hasCaballeria = false;
        bool hasArtilleria = false;
        for (int i = 0; i < cartas.Contar(); i++)
        {
            if (cartas.Obtener(i) == TipoCarta.Infanteria) hasInfanteria = true;
            if (cartas.Obtener(i) == TipoCarta.Caballeria) hasCaballeria = true;
            if (cartas.Obtener(i) == TipoCarta.Artilleria) hasArtilleria = true;
        }
        bool trioMixto = hasInfanteria && hasCaballeria && hasArtilleria;

        if (trioEncontrado.HasValue || trioMixto)
        {
            // Eliminar las cartas del trío (código existente)
            if (trioEncontrado.HasValue)
            {
                int removedCount = 0;
                for (int i = cartas.Contar() - 1; i >= 0 && removedCount < 3; i--)
                {
                    if (cartas.Obtener(i) == trioEncontrado.Value)
                    {
                        cartas.Eliminar(i);
                        removedCount++;
                    }
                }
            }
            else if (trioMixto)
            {
                for (int i = cartas.Contar() - 1; i >= 0; i--)
                {
                    if (cartas.Obtener(i) == TipoCarta.Infanteria && hasInfanteria)
                    {
                        cartas.Eliminar(i);
                        hasInfanteria = false;
                    }
                    else if (cartas.Obtener(i) == TipoCarta.Caballeria && hasCaballeria)
                    {
                        cartas.Eliminar(i);
                        hasCaballeria = false;
                    }
                    else if (cartas.Obtener(i) == TipoCarta.Artilleria && hasArtilleria)
                    {
                        cartas.Eliminar(i);
                        hasArtilleria = false;
                    }
                }
            }

            int tropasGanadas = Mapa.instance.GetValorIntercambioActual();
            Mapa.instance.IncrementarContadorIntercambio();
            Debug.Log(alias + " ha intercambiado cartas por " + tropasGanadas + " tropas.");
            return tropasGanadas;
        }
        else
        {
            Debug.Log(alias + " no tiene un trío válido para intercambiar.");
            return 0;
        }
    }

    // ← NUEVO MÉTODO: Para reestablecer la referencia 'jugadorPropietario' en todos los territorios conquistados
    /// <summary>
    /// Reestablece la referencia 'jugadorPropietario' en todos los territorios de este jugador.
    /// Útil después de deserialización para reconstruir el ciclo en runtime.
    /// </summary>
    public void ReestablecerReferenciasInversas()
    {
        for (int i = 0; i < territoriosConquistados.Contar(); i++)
        {
            Territorio t = territoriosConquistados.Obtener(i);
            if (t != null)
            {
                t.jugadorPropietario = this;  // Reasigna la referencia inversa
            }
        }
        Debug.Log("Referencias inversas reestablecidas para jugador: " + alias + " (" + territoriosConquistados.Contar() + " territorios)");
    }
    public int ContarCartasTipo(Jugador.TipoCarta tipo)
    {
        int count = 0;
        for (int i = 0; i < cartas.Contar(); i++)
        {
            if (cartas.Obtener(i) == tipo)
                count++;
        }
        return count;
    }
}
