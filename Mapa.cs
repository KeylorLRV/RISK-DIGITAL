using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.SceneManagement;

public class Mapa : MonoBehaviour
{
    // Atributos
    public static Mapa instance; // Singleton instance
    public ListaArray<GameObject> territorioHandlers = new ListaArray<GameObject>(); // Referencia a los GameObjects con TerritorioHandler
    public ListaArray<Territorio> territorios = new ListaArray<Territorio>(); // Lista de objetos Territorio
    public ListaArray<Continente> continentes = new ListaArray<Continente>(); // Lista de objetos Continente
    public DiccionarioArray<Territorio, ListaArray<Territorio>> grafoAdyacencias = new DiccionarioArray<Territorio, ListaArray<Territorio>>(); // Grafo de adyacencias

    private int contadorIntercambioGlobal = 0; // Índice para la secuencia de Fibonacci
    private int[] fibonacciSequence = { 4, 6, 8, 10, 12, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, 85, 90, 95, 100 }; // Secuencia de Fibonacci para el intercambio de cartas (ejemplo)

    // Referencia al AttackPanel (ahora gestionado por Partida o UIController)
    public GameObject attackPanelUI; // Renombrado para evitar conflicto con la clase AttackPanel

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject); // Asegura que el mapa persista entre escenas
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
        territorioHandlers = new ListaArray<GameObject>();
        GameObject[] gos = GameObject.FindGameObjectsWithTag("Territorio");
        Debug.Log("Awake Mapa: GameObjects con tag 'Territorio' encontrados: " + gos.Length);
        if (gos.Length == 0)
        {
            Debug.LogError("¡0 GameObjects con tag! Asigna 'Territorio' en Inspector o runtime.");
        }
        foreach (var go in gos)
        {
            territorioHandlers.Agregar(go);
        }
    }

    void Start()
    {
        attackPanelUI.SetActive(false); // Ocultar el panel al inicio
        InicializarMapa(); // Llama al método de inicialización pesada
        // La lógica de GameManager.instance.battleHasEnded y Saving/Loading se moverá a Partida
    }

    /// <summary>
    public void InicializarMapa()
    {
        // 1. Encontrar todos los GameObjects de territorios (opcional: para validación)
        GameObject[] goTerritorios = GameObject.FindGameObjectsWithTag("Territorio");
        Debug.Log("Número de Territorios encontrados: " + goTerritorios.Length);

        // 2. Crear continentes (sin cambios – hazlo antes del bucle)
        Continente americaNorte = new Continente("América del Norte", 5); // Ejemplo de bonificación
        Continente sudamerica = new Continente("Sudamérica", 2);
        Continente europa = new Continente("Europa", 5);
        Continente africa = new Continente("África", 3);
        Continente asia = new Continente("Asia", 7);
        Continente australia = new Continente("Australia", 2);

        continentes.Agregar(americaNorte);
        continentes.Agregar(sudamerica);
        continentes.Agregar(europa);
        continentes.Agregar(africa);
        continentes.Agregar(asia);
        continentes.Agregar(australia);

        // 3. ← BUCLE: Crear territorios y asignarlos a continentes por ID numérico
        for (int id = 1; id <= 42; id++) // De 1 a 42 (ajusta si tienes menos/más)
        {
            string nombreGO = "Mapa" + id; // Ej: "Mapa1", "Mapa42"
            GameObject go = GameObject.Find(nombreGO);

            if (go == null)
            {
                Debug.LogWarning("GameObject no encontrado: " + nombreGO + ". Saltando ID " + id);
                continue; // Salta si falta el GO
            }

            // Verificar tag (opcional, para seguridad)
            if (go.CompareTag("Territorio") == false)
            {
                Debug.LogWarning("GameObject " + nombreGO + " no tiene tag 'Territorio'. Saltando.");
                continue;
            }

            // Crear el Territorio (usa el nombre del GO)
            TerritorioHandler handler = go.GetComponent<TerritorioHandler>();
            if (handler == null)
            {
                Debug.LogError("Falta TerritorioHandler en " + nombreGO);
                continue;
            }

            Territorio territorio = handler.territorio; // Asume que ya se inicializó en el handler
            if (territorio == null)
            {
                territorio = new Territorio(nombreGO, null); // Crea nuevo si no existe
                handler.territorio = territorio; // Asigna al handler
            }
            if (territorio.territoriosAdyacentes == null)
            {
                territorio.territoriosAdyacentes = new ListaArray<Territorio>();
                Debug.Log("Inicializada lista de adyacentes para " + nombreGO);
            }
            // ← NUEVA VALIDACIÓN: Asegurar nombre válido (ya lo tenías)
            if (string.IsNullOrEmpty(territorio.nombre))
            {
                Debug.LogError("Territorio creado para " + nombreGO + " tiene nombre inválido. Asignando por defecto.");
                territorio.nombre = nombreGO;
            }

            // ← NUEVA VALIDACIÓN: Asegurar nombre válido inmediatamente después de crear
            if (string.IsNullOrEmpty(territorio.nombre))
            {
                Debug.LogError("Territorio creado para " + nombreGO + " tiene nombre inválido. Asignando por defecto.");
                territorio.nombre = nombreGO; // Corrige inmediatamente
            }

            // Asignar al continente basado en el ID (mapeo de RISK)
            Continente continenteAsignado = AsignarContinentePorID(id);
            if (continenteAsignado != null)
            {
                territorio.continente = continenteAsignado; // Referencia inversa (se reestablecerá después)
                continenteAsignado.AgregarTerritorio(territorio);
                territorios.Agregar(territorio); // Agrega a la lista global de territorios

                Debug.Log("Territorio " + nombreGO + " (ID " + id + ") asignado a " + continenteAsignado.nombre);
            }
            else
            {
                Debug.LogWarning("No se encontró continente para ID " + id + " (" + nombreGO + ")");
            }
        }

        // ← NUEVO: Validar territorios antes de cargar adyacencias
        bool validacionOK = ValidarTerritorios();
        if (!validacionOK)
        {
            Debug.LogWarning("Algunos territorios tienen problemas. Continuando con carga de adyacencias, pero revisa los logs.");
        }

        TextAsset jsonFile = Resources.Load<TextAsset>("AdyacenciasTerritorios");  // Sin la extensión .json
        if (jsonFile != null)
        {
            string jsonContent = jsonFile.text;  // Esto es el string con el JSON real
            Debug.Log("Contenido JSON cargado (longitud: " + jsonContent.Length + " caracteres). Primeros 100 chars: " + jsonContent.Substring(0, Math.Min(100, jsonContent.Length)));
            CargarAdyacenciasDesdeJSON(jsonContent);  // Pasa el contenido, no el nombre
        }
        else
        {
            Debug.LogError("Archivo JSON 'AdyacenciasTerritorios.json' no encontrado en Assets/Resources/. Colócalo allí.");
            // Opcional: Carga manual si es necesario (ver alternativa abajo)
            return;  // Salta la carga de adyacencias si no existe
        }
        grafoAdyacencias.RedimensionarSiNecesario();  // Opcional, para prevenir colisiones futuras

        // 4. Reestablecer referencias inversas de continentes

        for (int i = 0; i < continentes.Contar(); i++)
        {
            continentes.Obtener(i).ReestablecerReferenciasInversas();
        }

        // 6. Reconstruir adyacencias locales y otras inicializaciones
        ReconstruirAdyacenciasLocales();

        // Este bloque de código estaba duplicado y se ha eliminado de aquí.
        // Las referencias inversas de jugadores y neutral se reestablecen en Partida.Loading()
        // y Partida.IniciarPartida() después de la distribución inicial.
        Partida.instance.DistribuirTerritoriosIniciales();
        TintTerritorios();
        Debug.Log("Territorios tiñidos inicialmente (deberían ser grises/neutrales).");


        Debug.Log("Mapa inicializado con " + territorios.Contar() + " territorios y " + continentes.Contar() + " continentes.");
        territorios.Imprimir(); // Imprimir para depuración
    }


    public void CargarAdyacenciasDesdeJSON(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogError("JSON es null o vacío.");
            return;
        }

        Debug.Log("JSON a parsear: " + json);

        RootAdyacenciasJSON root = null;
        try
        {
            root = JsonUtility.FromJson<RootAdyacenciasJSON>(json);
        }
        catch (Exception ex)
        {
            Debug.LogError("Error parseando JSON: " + ex.Message);
            return;
        }

        if (root == null || root.adyacencias == null || root.adyacencias.Length == 0)
        {
            Debug.LogError("JSON parseado pero sin datos en 'adyacencias'.");
            return;
        }

        foreach (var item in root.adyacencias)
        {
            if (item == null || string.IsNullOrEmpty(item.territorio))
            {
                Debug.LogWarning("Item inválido en JSON (territorio null o vacío).");
                continue;
            }

            Territorio territorio = GetTerritorioPorNombre(item.territorio);
            if (territorio == null)
            {
                Debug.LogWarning("Territorio no encontrado: " + item.territorio);
                continue;
            }

            ListaArray<Territorio> listaAdyacentes = new ListaArray<Territorio>();
            if (item.adyacentes != null)
            {
                foreach (string nombreAdyacente in item.adyacentes)
                {
                    if (string.IsNullOrEmpty(nombreAdyacente))
                    {
                        Debug.LogWarning("Nombre de adyacente vacío en " + item.territorio);
                        continue;
                    }

                    Territorio adyacente = GetTerritorioPorNombre(nombreAdyacente);
                    if (adyacente != null)
                    {
                        listaAdyacentes.Agregar(adyacente);
                    }
                    else
                    {
                        Debug.LogWarning("Adyacente no encontrado: " + nombreAdyacente + " para territorio " + item.territorio);
                    }
                }
            }

            AgregarAdyacenciasDeTerritorio(territorio, listaAdyacentes);
        }

        Debug.Log("Carga de adyacencias completada.");
    }



    public void AgregarAdyacenciasDeTerritorio(Territorio territorio, ListaArray<Territorio> adyacentes)
    {
        if (territorio == null || adyacentes == null)
        {
            Debug.LogError("Territorio o lista de adyacentes es null.");
            return;
        }

        if (!grafoAdyacencias.Existe(territorio))
        {
            grafoAdyacencias.Agregar(territorio, new ListaArray<Territorio>());
        }

        ListaArray<Territorio> listaAdyacentesTerritorio = grafoAdyacencias.Obtener(territorio);
        if (listaAdyacentesTerritorio == null)
        {
            Debug.LogError("No se pudo obtener la lista de adyacentes para " + territorio.nombre);
            return;
        }

        for (int i = 0; i < adyacentes.Contar(); i++)
        {
            Territorio adyacente = adyacentes.Obtener(i);
            if (adyacente == null)
            {
                Debug.LogWarning("Adyacente null en lista de " + territorio.nombre);
                continue;
            }

            if (!listaAdyacentesTerritorio.Contiene(adyacente))
            {
                listaAdyacentesTerritorio.Agregar(adyacente);
            }

            if (!territorio.territoriosAdyacentes.Contiene(adyacente))
            {
                territorio.territoriosAdyacentes.Agregar(adyacente);
            }
        }

        Debug.Log("Adyacencias directas agregadas para territorio " + territorio.nombre);
    }




    // ← NUEVO MÉTODO HELPER: Para obtener lista con reintentos
    private ListaArray<Territorio> ObtenerListaConReintento(Territorio territorio, string nombreLista)
    {
        int maxReintentos = 3;
        for (int intento = 1; intento <= maxReintentos; intento++)
        {
            ListaArray<Territorio> lista = grafoAdyacencias.Obtener(territorio);  // Llama a Obtener con logs
            if (lista != null)
            {
                Debug.Log("Obtenida " + nombreLista + " para '" + territorio.nombre + "' en intento " + intento + ".");
                return lista;
            }

            Debug.LogWarning("Reintento " + intento + " para " + nombreLista + ": Lista null para '" + territorio.nombre + "'. Re-agregando...");
            grafoAdyacencias.Agregar(territorio, new ListaArray<Territorio>());  // Re-agrega con logs
        }

        Debug.LogError("Fallaron " + maxReintentos + " reintentos para " + nombreLista + " de '" + territorio.nombre + "'.");
        return null;
    }



    public void ReconstruirAdyacenciasLocales()
    {
        for (int i = 0; i < territorios.Contar(); i++)
        {
            Territorio t = territorios.Obtener(i);
            t.territoriosAdyacentes = new ListaArray<Territorio>(); // Limpia y reinicia
            if (grafoAdyacencias.Existe(t))
            {
                ListaArray<Territorio> adyacentesCentrales = grafoAdyacencias.Obtener(t);
                for (int j = 0; j < adyacentesCentrales.Contar(); j++)
                {
                    t.territoriosAdyacentes.Agregar(adyacentesCentrales.Obtener(j));
                }
            }
        }
        Debug.Log("Adyacencias locales reconstruidas para todos los territorios.");
    }

    /// <summary>
    /// Devuelve una copia de la lista de todos los objetos Territorio.
    /// </summary>
    public ListaArray<Territorio> GetTodosTerritorios()
    {
        Debug.Log("GetTodosTerritorios() llamado. Total en mapa.territorios: " + territorios.Contar());
        ListaArray<Territorio> copia = new ListaArray<Territorio>();
        int nulls = 0;
        for (int i = 0; i < territorios.Contar(); i++)
        {
            Territorio t = territorios.Obtener(i);
            if (t != null)
            {
                copia.Agregar(t);
                Debug.Log("Agregado a copia: " + t.nombre + " (propietario actual: " + (t.jugadorPropietario?.alias ?? "null"));
            }
            else
            {
                nulls++;
                Debug.LogWarning("Territorio null en índice " + i + " de mapa.territorios!");
            }
        }
        Debug.Log("Copia creada: " + copia.Contar() + " territorios válidos (nulls: " + nulls + ")");
        return copia;
    }


    /// <summary>
    /// Busca un territorio por su nombre.
    /// </summary>
    public Territorio GetTerritorioPorNombre(string nombre)
    {
        for (int i = 0; i < territorios.Contar(); i++)
        {
            Territorio t = territorios.Obtener(i);
            if (t != null && t.nombre == nombre)
            {
                return t;
            }
            else if (t == null)
            {
                Debug.LogError("Territorio null en lista global! Índice: " + i);
            }
            else if (t.nombre == null)
            {
                Debug.LogError("Territorio con nombre null en lista global: " + i);
            }
        }
        Debug.LogWarning("No se encontró territorio con nombre: '" + nombre + "'");
        return null;
    }


    /// <summary>
    /// Verifica si dos territorios son adyacentes.
    /// </summary>
    public bool SonAdyacentes(Territorio territorio1, Territorio territorio2)
    {
        if (!grafoAdyacencias.Existe(territorio1)) return false;
        ListaArray<Territorio> adyacentes = grafoAdyacencias.Obtener(territorio1);
        for (int i = 0; i < adyacentes.Contar(); i++)
        {
            if (adyacentes.Obtener(i) == territorio2)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Determina si existe una ruta de territorios controlados por el jugador entre origen y destino.
    /// Implementa un algoritmo BFS (Breadth-First Search).
    /// </summary>
    /// <summary>
    /// Asigna el continente correspondiente basado en el ID del territorio (1-42).
    /// Basado en la estructura estándar de RISK. Ajusta rangos si tu numeración es diferente.
    /// </summary>
    private Continente AsignarContinentePorID(int id)
    {
        switch (id)
        {
            case int n when (n >= 1 && n <= 4):  // América del Norte (4 territorios)
                return continentes.BuscarPorNombre("América del Norte"); // O usa un método para obtener por nombre
            case int n when (n >= 5 && n <= 11): // Sudamérica (7 territorios)
                return continentes.BuscarPorNombre("Sudamérica");
            case int n when (n >= 12 && n <= 19): // África (8 territorios)
                return continentes.BuscarPorNombre("África");
            case int n when (n >= 20 && n <= 32): // Europa (13 territorios)
                return continentes.BuscarPorNombre("Europa");
            case int n when (n >= 33 && n <= 38): // Asia (6 territorios)
                return continentes.BuscarPorNombre("Asia");
            case int n when (n >= 39 && n <= 42): // Australia (4 territorios)
                return continentes.BuscarPorNombre("Australia");
            default:
                Debug.LogWarning("ID " + id + " fuera de rango (1-42). No asignado.");
                return null;
        }
    }

    public bool ExisteRutaSegura(Territorio origen, Territorio destino, Jugador jugador)
    {   
        if (origen == null || destino == null || jugador == null)
        {
            Debug.LogError("Parámetros null en ExisteRutaSegura.");
            return false;
        }
        if (origen == destino) return true;
        if (origen.jugadorPropietario != jugador || destino.jugadorPropietario != jugador) return false;

        Queue<Territorio> queue = new Queue<Territorio>();
        HashSet<Territorio> visited = new HashSet<Territorio>();

        queue.Enqueue(origen);
        visited.Add(origen);

        while (queue.Count > 0)
        {
            Territorio actual = queue.Dequeue();

            if (actual == destino) return true;

            if (grafoAdyacencias.Existe(actual))
            {
                ListaArray<Territorio> adyacentes = grafoAdyacencias.Obtener(actual);
                for (int i = 0; i < adyacentes.Contar(); i++)
                {
                    Territorio vecino = adyacentes.Obtener(i);
                    if (vecino.jugadorPropietario == jugador && !visited.Contains(vecino))
                    {
                        visited.Add(vecino);
                        queue.Enqueue(vecino);
                    }
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Avanza el contador de intercambio global al siguiente valor de la secuencia de Fibonacci.
    /// </summary>
    public void IncrementarContadorIntercambio()
    {
        contadorIntercambioGlobal = Mathf.Min(contadorIntercambioGlobal + 1, fibonacciSequence.Length - 1);
        Debug.Log("Contador de intercambio global incrementado a índice: " + contadorIntercambioGlobal);
    }

    /// <summary>
    /// Devuelve el valor actual del contador de intercambio global.
    /// </summary>
    public int GetValorIntercambioActual()
    {
        return fibonacciSequence[contadorIntercambioGlobal];
    }

    /// <summary>
    /// Tintea los territorios en el mapa según su propietario.
    /// </summary>
    public void TintTerritorios()
    {
        Debug.Log("=== INICIANDO TINTADO DE TERRITORIOS ===");
        Debug.Log("Número de items en territorioHandlers: " + territorioHandlers.Contar());

        int procesados = 0;
        int conPropietario = 0;
        int sinPropietario = 0;
        int handlersNull = 0;


        for (int i = 0; i < territorioHandlers.Contar(); i++)
        {
            GameObject go = territorioHandlers.Obtener(i);
            if (go == null)
            {
                Debug.LogWarning("GameObject null en territorioHandlers índice " + i + ". Saltando.");
                continue;
            }

            TerritorioHandler handler = go.GetComponent<TerritorioHandler>();
            if (handler == null)
            {
                Debug.LogWarning("TerritorioHandler NULL en " + go.name + " (índice " + i + "). Verifica que el componente esté adjunto.");
                handlersNull++;
                continue;  // ← CAMBIO: No crash, solo salta
            }

            procesados++;

            if (handler.territorio == null)
            {
                Debug.LogWarning("Territorio NULL en handler de " + go.name);
                if (handler != null) handler.TintColor(new Color32(150, 150, 150, 255));  // ← VERIFICACIÓN
                sinPropietario++;
                continue;
            }

            Territorio territorio = handler.territorio;
            if (territorio.jugadorPropietario != null)
            {
                Color32 colorJugador = territorio.jugadorPropietario.color;  // Asumiendo Color32; si es Color, convierte
                handler.TintColor(colorJugador);
                Debug.Log("Tiñido " + go.name + " con color de " + territorio.jugadorPropietario.alias + " (R:" + colorJugador.r + " G:" + colorJugador.g + " B:" + colorJugador.b + ")");
                conPropietario++;
            }
            else
            {
                // ← CAMBIO: Verificación antes de llamar
                if (handler != null)
                {
                    handler.TintColor(new Color32(150, 150, 150, 255));
                    Debug.Log("Tiñido " + go.name + " como neutral (sin propietario).");
                }
                else
                {
                    Debug.LogError("Handler null inesperado en else para " + go.name);
                }
                sinPropietario++;
            }
        }

        Debug.Log("=== TINTADO COMPLETADO ===");
        Debug.Log("Procesados: " + procesados + ", Con propietario: " + conPropietario + ", Sin propietario: " + sinPropietario + ", Handlers null: " + handlersNull);
        if (territorioHandlers.Contar() == 0)
        {
            Debug.LogError("¡territorioHandlers vacío! Verifica tags y GameObjects en la escena.");
        }
    }

    /// <summary>
    /// Valida la lista de territorios antes de cargar adyacencias.
    /// Detecta territorios nulos, con nombre inválido o sin continente.
    /// Retorna true si todos son válidos, false si hay problemas.
    /// </summary>
    public bool ValidarTerritorios()
    {
        bool todosValidos = true;
        int totalTerritorios = territorios.Contar();
        int invalidos = 0;

        Debug.Log("=== VALIDANDO TERRITORIOS ===");
        Debug.Log("Total territorios en lista: " + totalTerritorios);

        for (int i = totalTerritorios - 1; i >= 0; i--) // Iterar al revés para eliminar sin problemas de índice
        {
            Territorio t = territorios.Obtener(i);
            if (t == null)
            {
                Debug.LogError("Territorio NULL en índice " + i + ". Eliminando de la lista.");
                territorios.Eliminar(i);
                invalidos++;
                todosValidos = false;
                continue;
            }

            if (string.IsNullOrEmpty(t.nombre))
            {
                Debug.LogError("Territorio en índice " + i + " tiene nombre NULL o vacío. Asignando nombre por defecto.");
                t.nombre = "Territorio_SinNombre_" + i; // Corrige automáticamente
                invalidos++;
                todosValidos = false;
            }

            if (t.continente == null)
            {
                Debug.LogWarning("Territorio '" + t.nombre + "' (índice " + i + ") no tiene continente asignado. Revisa AsignarContinentePorID.");
                todosValidos = false;
            }

            // Opcional: Verificar si el territorio está asociado a un handler (para depuración)
            TerritorioHandler handler = GameObject.Find(t.nombre)?.GetComponent<TerritorioHandler>();
            if (handler == null || handler.territorio != t)
            {
                Debug.LogWarning("Territorio '" + t.nombre + "' no está correctamente ligado a su GameObject/Handler.");
            }

            Debug.Log("Territorio '" + t.nombre + "' validado: OK (continente: " + (t.continente?.nombre ?? "NULL") + ")");
        }

        if (invalidos > 0)
        {
            Debug.LogError("Validación completada: " + invalidos + " territorios inválidos corregidos/eliminados.");
        }
        else
        {
            Debug.Log("Validación completada: Todos los " + totalTerritorios + " territorios son válidos.");
        }

        Debug.Log("=== FIN VALIDACIÓN ===");
        return todosValidos;
    }
    public void ActualizarVisualTropasEnMapa()
    {
        for  (int i = 0; i < territorioHandlers.Contar(); i++)
        {
            GameObject go = territorioHandlers.Obtener(i);
            if (go == null)
            {
                Debug.LogWarning("GameObject null en territorioHandlers índice " + i + ". Saltando.");
                continue;
            }
        
            TerritorioHandler handler = go.GetComponent<TerritorioHandler>();
            if (handler != null)
            {
                handler.ActualizarTropasVisual();
            }
        }
    }



    // Los métodos ShowAttackPanel y DisableAttackPanel se moverán a una clase UIController o Partida.
    // StartFight se moverá a Partida.
}
