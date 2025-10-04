using System;                    // Para [NonSerialized]
using System.Collections.Generic; // Necesario para List<Territorio>
using UnityEngine;

[System.Serializable]
public class Territorio
{
    // Atributos
    public string nombre; // Corresponde a 'name'
    public int cantidadTropas; // Nuevo atributo
    [NonSerialized]  // Ya lo tienes: Para ciclo con Continente
    public Continente continente; // Referencia al objeto Continente (se reestablece en runtime)
    [NonSerialized]  // Ya lo tienes: Para ciclo con Jugador
    public Jugador jugadorPropietario; // Referencia al objeto Jugador (se reestablece en runtime)
    [NonSerialized]  // ← AGREGADO: Para romper ciclo de adyacencias (grafo)
    public ListaArray<Territorio> territoriosAdyacentes; // Usamos ListaArray para adyacentes (se reconstruye en Mapa)

    // Constructor (actualizado: inicializa la lista vacía)
    public Territorio(string nombre, Continente continente = null)
    {
        this.nombre = string.IsNullOrEmpty(nombre) ? "Territorio_SinNombre" : nombre;  // ← FIX: Evita null/empty
        this.cantidadTropas = 1;
        this.continente = continente;
        this.jugadorPropietario = null;
        this.territoriosAdyacentes = new ListaArray<Territorio>();
    }
     

    // Métodos (actualizado: EsAdyacenteA ahora consulta el grafo central de Mapa en lugar de la lista local)
    /// <summary>
    /// Verifica si este territorio está conquistado por el jugador especificado.
    /// </summary>
    public bool EstaConquistadoPor(Jugador jugador)
    {
        return jugadorPropietario == jugador;
    }

    /// <summary>
    /// Verifica si el territorio pasado por parámetro es adyacente a este territorio.
    /// Ahora consulta el grafo central de Mapa (más eficiente y evita duplicación).
    /// </summary>
    public bool EsAdyacenteA(Territorio territorio)
    {
        if (Mapa.instance == null) return false; // Si el mapa no está inicializado
        return Mapa.instance.SonAdyacentes(this, territorio); // Delega al grafo central
    }

    // ← NUEVO: Sobrescribe Equals para comparación por nombre (necesario para diccionarios)
    public override bool Equals(object obj)
    {
        if (obj == null || GetType() != obj.GetType())
            return false;

        Territorio other = (Territorio)obj;
        // Compara por nombre (único). Si nombre es null, usa string.Empty para consistencia
        string thisName = nombre ?? string.Empty;
        string otherName = other.nombre ?? string.Empty;
        return thisName == otherName;
    }

    // ← NUEVO: Sobrescribe GetHashCode basado en nombre (necesario para diccionarios)
    public override int GetHashCode()
    {
        // Usa el hash del nombre, o 0 si es null (evita excepciones)
        return (nombre != null ? nombre.GetHashCode() : 0);
    }
}
