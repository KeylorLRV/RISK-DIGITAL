using System;                    // Para Func<T, bool>, NonSerialized, Array, Reflection
using System.Reflection;         // Para GetProperty en reflection
using UnityEngine;               // Para Debug.Log


[System.Serializable]
public class Par<TKey, TValue>
{
    public TKey Clave { get; set; }  // Propiedad pública para la clave (ej: Territorio)
    public TValue Valor { get; set; } // Propiedad pública para el valor (ej: ListaArray<Territorio>)

    // Constructor por defecto
    public Par()
    {
        Clave = default(TKey);
        Valor = default(TValue);
    }

    // Constructor con parámetros
    public Par(TKey clave, TValue valor)
    {
        Clave = clave;
        Valor = valor;
    }

    // Sobrescribe ToString para depuración (opcional, pero útil)
    public override string ToString()
    {
        return $"Par[Clave: {Clave?.ToString() ?? "null"}, Valor: {Valor?.ToString() ?? "null"}]";
    }

    // Método Equals para comparación (usa Equals de las propiedades)
    public override bool Equals(object obj)
    {
        if (obj is Par<TKey, TValue> other)
        {
            return Clave != null && Clave.Equals(other.Clave) && 
                   Valor != null && Valor.Equals(other.Valor);
        }
        return false;
    }

    // HashCode para uso en diccionarios (basado en clave)
    public override int GetHashCode()
    {
        return (Clave?.GetHashCode() ?? 0) ^ (Valor?.GetHashCode() ?? 0);
    }
}


[System.Serializable]
public class ListaArray<T>
{
    [NonSerialized]  // Evita serialización profunda en arrays anidados
    private T[] array;
    private int cantidad;
    private int capacidad;

    // Constructor
    public ListaArray()
    {
        capacidad = 4;
        array = new T[capacidad];
        cantidad = 0;
    }
    public bool EsVacia()
    {
        return Contar() == 0;
    }


    // Método para contar elementos
    public int Contar()
    {
        return cantidad;
    }

    // Método para obtener elemento por índice
    public T Obtener(int indice)
    {
        if (indice < 0 || indice >= cantidad)
        {
            Debug.LogError("Índice fuera de rango: " + indice);
            return default(T);
        }
        return array[indice];
    }

    // Método para agregar elemento
    public void Agregar(T elemento)
    {
        if (cantidad >= capacidad)
        {
            // Redimensionar array
            capacidad *= 2;
            T[] nuevoArray = new T[capacidad];
            Array.Copy(array, nuevoArray, cantidad);
            array = nuevoArray;
        }
        array[cantidad] = elemento;
        cantidad++;
    }

    // Método para eliminar por índice
    public void Eliminar(int indice)
    {
        if (indice < 0 || indice >= cantidad)
        {
            Debug.LogError("Índice fuera de rango: " + indice);
            return;
        }
        for (int i = indice; i < cantidad - 1; i++)
        {
            array[i] = array[i + 1];
        }
        cantidad--;
        array[cantidad] = default(T); // Limpia el último slot
    }

    // Método para eliminar un elemento específico (por valor)
    public void EliminarElemento(T elemento)
    {
        for (int i = 0; i < cantidad; i++)
        {
            if (array[i] != null && array[i].Equals(elemento))
            {
                Eliminar(i);
                return; // Elimina el primero encontrado
            }
        }
    }

    // Método para verificar si contiene un elemento
    public bool Contiene(T elemento)
    {
        for (int i = 0; i < cantidad; i++)
        {
            if (array[i] != null && array[i].Equals(elemento))
            {
                return true;
            }
        }
        return false;
    }

    // Método para imprimir (para depuración)
    public void Imprimir()
    {
        string output = "Lista: [";
        for (int i = 0; i < cantidad; i++)
        {
            output += (array[i] != null ? array[i].ToString() : "null");
            if (i < cantidad - 1) output += ", ";
        }
        output += "]";
        Debug.Log(output);
    }

    // Método genérico para buscar con predicado
    public T Buscar(Func<T, bool> predicado)
    {
        for (int i = 0; i < Contar(); i++)
        {
            if (predicado(array[i]))
            {
                return array[i];
            }
        }
        return default(T);
    }

    // Método para buscar por nombre (usa reflection)
    public T BuscarPorNombre(string nombre)
    {
        return Buscar(item => 
        {
            if (item == null) return false;

            // Primero buscar propiedad
            var prop = item.GetType().GetProperty("nombre");
            if (prop != null && prop.PropertyType == typeof(string))
            {
                string nombreItem = (string)prop.GetValue(item);
                return !string.IsNullOrEmpty(nombreItem) && nombreItem == nombre;
            }

            // Si no hay propiedad, buscar campo público
            var field = item.GetType().GetField("nombre");
            if (field != null && field.FieldType == typeof(string))
            {
                string nombreItem = (string)field.GetValue(item);
                return !string.IsNullOrEmpty(nombreItem) && nombreItem == nombre;
            }

            return false;
        });
    }

}
/// <summary>
/// Diccionario basado en array (hash simple para claves de referencia como Territorio).
/// Usa ListaArray para buckets y Par<TKey, TValue> para pares clave-valor.
/// Serializable para Unity.
/// </summary>
[System.Serializable]
public class DiccionarioArray<TKey, TValue> where TKey : class
{
    [NonSerialized]
    private ListaArray<ListaArray<Par<TKey, TValue>>> buckets;  // ← CAMBIO: Usa Par en lugar de KeyValuePair
    private int numBuckets = 16; // Número inicial de buckets
    private int count = 0;

    public DiccionarioArray()
    {
        buckets = new ListaArray<ListaArray<Par<TKey, TValue>>>();
        for (int i = 0; i < numBuckets; i++)
        {
            buckets.Agregar(new ListaArray<Par<TKey, TValue>>());
        }
    }

    // Hash simple para TKey (usa GetHashCode)
    private int GetHash(TKey key)
    {
        if (key == null) return 0;
        return (key.GetHashCode() & 0x7fffffff) % numBuckets;
    }

    public void Agregar(TKey key, TValue value)
    {
        if (key == null)
        {
            Debug.LogError("Clave null no permitida.");
            return;
        }

        int hash = GetHash(key);
        Debug.Log("AGREGAR: Intentando agregar clave '" + (key is Territorio territorio ? territorio.nombre : key.ToString()) + "' con hash=" + hash + ", numBuckets=" + numBuckets);

        ListaArray<Par<TKey, TValue>> bucket = buckets.Obtener(hash);
        if (bucket == null)
        {
            Debug.LogError("AGREGAR: Bucket null para hash " + hash + "! Diccionario corrupto.");
            return;
        }

        // Verificar si ya existe y actualizar
        for (int i = 0; i < bucket.Contar(); i++)
        {
            Par<TKey, TValue> par = bucket.Obtener(i);
            if (par.Clave != null && par.Clave.Equals(key))
            {
                par.Valor = value;
                Debug.Log("AGREGAR: Valor actualizado para clave existente '" + (key is Territorio territorio2 ? territorio2.nombre : key.ToString()) + "' en bucket " + hash);
                return;
            }
        }

        // Agregar nuevo par si no existe
        Par<TKey, TValue> nuevoPar = new Par<TKey, TValue>(key, value);
        bucket.Agregar(nuevoPar);
        count++;
        Debug.Log("AGREGAR: Nuevo par INSERTADO exitosamente para '" + (key is Territorio territorio3 ? territorio3.nombre : key.ToString()) + "' en bucket " + hash + ". Count ahora: " + count);
    }

    public TValue Obtener(TKey key)
    {
        if (key == null) 
        {
            Debug.LogWarning("OBTENER: Clave null.");
            return default(TValue);
        }
        
        int hash = GetHash(key);
        string keyDesc = (key is Territorio territorio ? territorio.nombre : key.ToString());
        Debug.Log("OBTENER: Buscando clave '" + keyDesc + "' con hash=" + hash);
        
        ListaArray<Par<TKey, TValue>> bucket = buckets.Obtener(hash);
        if (bucket == null)
        {
            Debug.LogError("OBTENER: Bucket null para hash " + hash + ". Diccionario corrupto.");
            return default(TValue);
        }
        
        for (int i = 0; i < bucket.Contar(); i++)
        {
            Par<TKey, TValue> par = bucket.Obtener(i);
            if (par.Clave != null && par.Clave.Equals(key))
            {
                Debug.Log("OBTENER: Clave '" + keyDesc + "' ENCONTRADA en bucket " + hash + ", devolviendo valor.");
                return par.Valor;
            }
        }
        
        Debug.LogWarning("OBTENER: Clave '" + keyDesc + "' NO ENCONTRADA en bucket " + hash + " (bucket tiene " + bucket.Contar() + " elementos).");
        return default(TValue);
    }



    public bool Existe(TKey key)
    {
        if (key == null) return false;
        int hash = GetHash(key);
        ListaArray<Par<TKey, TValue>> bucket = buckets.Obtener(hash);
        for (int i = 0; i < bucket.Contar(); i++)
        {
            if (bucket.Obtener(i).Clave != null && bucket.Obtener(i).Clave.Equals(key))
            {
                return true;
            }
        }
        return false;
    }


    public void Eliminar(TKey key)
    {
        if (key == null) return;
        int hash = GetHash(key);
        ListaArray<Par<TKey, TValue>> bucket = buckets.Obtener(hash);
        for (int i = 0; i < bucket.Contar(); i++)
        {
            if (bucket.Obtener(i).Clave != null && bucket.Obtener(i).Clave.Equals(key))
            {
                bucket.Eliminar(i);
                count--;
                return;
            }
        }
    }

    public int Contar()
    {
        return count;
    }

    // Método para imprimir (debug) – corregido para usar ToString de Par
    public void Imprimir()
    {
        Debug.Log("Diccionario con " + count + " elementos:");
        for (int b = 0; b < numBuckets; b++)
        {
            ListaArray<Par<TKey, TValue>> bucket = buckets.Obtener(b);
            if (bucket.Contar() > 0)
            {
                string bucketStr = "Bucket " + b + ": [";
                for (int i = 0; i < bucket.Contar(); i++)
                {
                    bucketStr += bucket.Obtener(i).ToString();
                    if (i < bucket.Contar() - 1) bucketStr += ", ";
                }
                bucketStr += "]";
                Debug.Log(bucketStr);
            }
        }
    }
    // ← NUEVO: Redimensiona buckets si hay muchas colisiones (llamar después de muchas Agregar)
    public void RedimensionarSiNecesario()
    {
        if (count > numBuckets * 2)
        {
            int oldNumBuckets = numBuckets;
            numBuckets *= 2;
            ListaArray<ListaArray<Par<TKey, TValue>>> newBuckets = new ListaArray<ListaArray<Par<TKey, TValue>>>();
            for (int i = 0; i < numBuckets; i++)
            {
                newBuckets.Agregar(new ListaArray<Par<TKey, TValue>>());
            }
            
            // Rehash todos los elementos
            // Nota: Para simplicidad, itera sobre buckets viejos y re-agrega
            for (int b = 0; b < oldNumBuckets; b++)
            {
                ListaArray<Par<TKey, TValue>> oldBucket = buckets.Obtener(b);
                for (int i = 0; i < oldBucket.Contar(); i++)
                {
                    Par<TKey, TValue> par = oldBucket.Obtener(i);
                    if (par.Clave != null)
                    {
                        int newHash = GetHash(par.Clave);
                        ListaArray<Par<TKey, TValue>> newBucket = newBuckets.Obtener(newHash);
                        newBucket.Agregar(par);
                    }
                }
            }
            buckets = newBuckets;
            Debug.Log("Buckets redimensionados a " + numBuckets + " para " + count + " elementos.");
        }
    }

}

[System.Serializable]
public class Cola<T>
{
    private ListaArray<T> elementos;

    public Cola()
    {
        elementos = new ListaArray<T>();
    }

    public void Encolar(T item)
    {
        elementos.Agregar(item);
    }

    public T Desencolar()
    {
        if (elementos.Contar() == 0)
        {
            Debug.LogWarning("Cola vacía.");
            return default(T);
        }
        T item = elementos.Obtener(0);
        elementos.Eliminar(0);
        return item;
    }

    public int Contar()
    {
        return elementos.Contar();
    }

    public bool EstaVacia()
    {
        return elementos.Contar() == 0;
    }
}

/// <summary>
/// Pila<T> basada en ListaArray<T> (LIFO).
/// </summary>
[System.Serializable]
public class Pila<T>
{
    private ListaArray<T> elementos;

    public Pila()
    {
        elementos = new ListaArray<T>();
    }

    public void Empujar(T item)
    {
        elementos.Agregar(item);
    }

    public T Sacar()
    {
        if (elementos.Contar() == 0)
        {
            Debug.LogWarning("Pila vacía.");
            return default(T);
        }
        T item = elementos.Obtener(elementos.Contar() - 1);
        elementos.Eliminar(elementos.Contar() - 1);
        return item;
    }

    public int Contar()
    {
        return elementos.Contar();
    }

    public bool EstaVacia()
    {
        return elementos.Contar() == 0;
    }
}
