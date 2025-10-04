using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Continente
{
    // Atributos (sin cambios)
    public string nombre;
    public int bonificacion;
    public ListaArray<Territorio> territorios; // Usamos ListaArray

    // Constructor (sin cambios)
    public Continente(string nombre, int bonificacion)
    {
        this.nombre = nombre;
        this.bonificacion = bonificacion;
        this.territorios = new ListaArray<Territorio>();
    }

    // Métodos (sin cambios principales)
    /// <summary>
    /// Verifica si un jugador domina completamente el continente.
    /// </summary>
    public bool EsControladoPor(Jugador jugador)
    {
        if (territorios.EsVacia()) return false;

        for (int i = 0; i < territorios.Contar(); i++)
        {
            if (territorios.Obtener(i).jugadorPropietario != jugador)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Agrega un territorio a la lista de territorios de este continente y establece la referencia inversa.
    /// </summary>
    public void AgregarTerritorio(Territorio territorio)
    {
        territorios.Agregar(territorio);
        territorio.continente = this; // ← Esto se mantiene, pero no se serializa en Territorio
    }

    /// <summary>
    /// Calcula la bonificación de refuerzos para un jugador si controla completamente el continente.
    /// </summary>
    public int CalcularBonificacion(Jugador jugador)
    {
        if (EsControladoPor(jugador))
        {
            return bonificacion;
        }
        else
        {
            return 0;
        }
    }

    // ← NUEVO MÉTODO: Para reestablecer referencias inversas después de serialización/carga
    /// <summary>
    /// Reestablece la referencia 'continente' en todos los territorios de este continente.
    /// Útil después de deserialización para reconstruir el ciclo en runtime.
    /// </summary>
    public void ReestablecerReferenciasInversas()
    {
        for (int i = 0; i < territorios.Contar(); i++)
        {
            territorios.Obtener(i).continente = this;
        }
        Debug.Log("Referencias inversas reestablecidas para continente: " + nombre);
    }
}
