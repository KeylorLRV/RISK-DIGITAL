using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using UnityEngine.SceneManagement;
using System.Linq;
using System;
using TMPro;

public class Partida : MonoBehaviour
{
    // Enums
    public enum EstadoPartida { EnCurso, Finalizada }
    public enum FaseTurno { Refuerzos, Ataque, Planeacion }

    // Atributos
    public static Partida instance;
    public EstadoPartida estado;
    public FaseTurno faseActual;
    public Jugador jugadorEnTurno;
    public ListaArray<Jugador> jugadores;
    public Jugador ejercitoNeutral;
    public Mapa mapa;
    public int contadorRonda;
    private DistributionManager distributionManager;
    public int tropasDisponiblesParaRefuerzo;
    public PlayerInfoUI1 playerInfoUI1; // Referencia al UI del jugador
    public PlayerInfoUI2 playerInfoUI2; // Referencia al UI del jugador
    public CombateManager combateManager; // Referencia al CombateManager
    public TextMeshProUGUI jugadoractual;

    // Variables para la lógica de UI y selección
    public Territorio territorioSeleccionadoParaRefuerzo;
    public Territorio territorioSeleccionadoParaAtaqueOrigen; // Renombrado para claridad
    public Territorio territorioSeleccionadoParaAtaqueDestino; // Nuevo para el territorio a atacar
    public Territorio territorioSeleccionadoParaMovimientoOrigen;
    public Territorio territorioSeleccionadoParaMovimientoDestino;

    // Referencia al AttackPanel
    public AttackPanel attackPanel;
    public DadoManager dado;
    public InfoGUI infoGUI;



    public int Refuerzos; // Refuerzos calculados al inicio del turno

    // Atributos del GameManager original que se mantienen si son relevantes para la Partida
    public string attackedTerritoryName;
    public bool battleHasEnded;
    public bool battleWon;

    [System.Serializable]
    public class SaveData
    {
        public ListaArray<Territorio> savedTerritorios = new ListaArray<Territorio>();
        public ListaArray<Jugador> savedJugadores = new ListaArray<Jugador>();
        public int currentJugadorIndex;
        public int currentContadorRonda;
        public EstadoPartida currentEstadoPartida;
        public FaseTurno currentFaseTurno;
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"Partida duplicada detectada: {this.name}, destruyendo instancia.");
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        jugadores = new ListaArray<Jugador>();

        Debug.Log($"Partida singleton creado: {this.name}");
    }

    void Start()
    {
        Debug.Log("=== START PARTIDA ===");

        if (Mapa.instance == null)
        {
            Debug.LogError("Mapa.instance es NULL. Asegúrate de que el GO con Mapa esté en la escena y activo.");
            return;
        }
        this.mapa = Mapa.instance;
        Debug.Log("Mapa asignado correctamente: " + mapa.name + ". Territorios disponibles: " + mapa.territorios.Contar());

        try
        {
            Jugador jugador1 = new Jugador("Player1", new Color32(0, 0, 255, 255)); // Azul
            Jugador jugador2 = new Jugador("Player2", new Color32(255, 0, 0, 255)); // Rojo
            ejercitoNeutral = new Jugador("Neutral", new Color32(150, 150, 150, 255)); // Gris

            jugadores.Agregar(jugador1);
            jugadores.Agregar(jugador2);
            Debug.Log("Jugadores creados: " + jugadores.Contar() + " jugadores + neutral.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error creando jugadores: " + ex.Message);
            return;
        }

        contadorRonda = 0;
        estado = EstadoPartida.EnCurso;

        string savePath = Application.persistentDataPath + "/SaveFile.json";
        if (File.Exists(savePath))
        {
            Debug.Log("Archivo de guardado encontrado. Eliminando para depuración...");
            File.Delete(savePath);
        }

        try
        {
            Loading();
            Debug.Log("Loading() completado.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error en Loading(): " + ex.Message + ". Continuando con nueva partida.");
        }

        if (jugadorEnTurno == null)
        {
            Debug.Log("Iniciando nueva partida...");

            IniciarPartida();
            Debug.Log("Nueva partida iniciada correctamente.");
        }
        else
        {
            Debug.Log("Partida cargada. Jugador en turno: " + jugadorEnTurno.alias + ", Fase: " + faseActual);
            mapa.TintTerritorios();
        }

        if (battleHasEnded)
        {
            TerritorioHandler attackedTerritoryHandler = GameObject.Find(attackedTerritoryName)?.GetComponent<TerritorioHandler>();
            if (attackedTerritoryHandler != null)
            {
                if (battleWon)
                {
                    // Lógica de conquista:
                    // 1. El territorio defensor cambia de propietario al jugador en turno.
                    Territorio defensorConquistado = mapa.GetTerritorioPorNombre(attackedTerritoryName);
                    if (defensorConquistado != null)
                    {
                        Jugador antiguoPropietario = defensorConquistado.jugadorPropietario;
                        if (antiguoPropietario != null)
                        {
                            antiguoPropietario.territoriosConquistados.EliminarElemento(defensorConquistado);
                            Debug.Log($"Territorio {defensorConquistado.nombre} removido de {antiguoPropietario.alias}.");
                        }

                        defensorConquistado.jugadorPropietario = jugadorEnTurno;
                        jugadorEnTurno.territoriosConquistados.Agregar(defensorConquistado);
                        defensorConquistado.cantidadTropas = 1; // Mínimo 1 tropa al conquistar

                        // Mover tropas del atacante al defensor (ejemplo: 1 tropa por defecto)
                        if (territorioSeleccionadoParaAtaqueOrigen != null && territorioSeleccionadoParaAtaqueOrigen.cantidadTropas > 1)
                        {
                            int tropasAMover = 1; // Puedes hacer esto interactivo con UI
                            territorioSeleccionadoParaAtaqueOrigen.cantidadTropas -= tropasAMover;
                            defensorConquistado.cantidadTropas += tropasAMover;
                            Debug.Log($"Movidas {tropasAMover} tropas de {territorioSeleccionadoParaAtaqueOrigen.nombre} a {defensorConquistado.nombre} después de la conquista.");
                        }
                        Debug.Log($"{jugadorEnTurno.alias} ha conquistado {defensorConquistado.nombre}!");
                    }
                    mapa.TintTerritorios();
                }
                else
                {
                    Debug.Log(jugadorEnTurno.alias + " ha perdido la batalla por " + attackedTerritoryHandler.territorio.nombre + ".");
                }
            }
            battleHasEnded = false;
            battleWon = false;
            // Limpiar selecciones después de la batalla
            territorioSeleccionadoParaAtaqueOrigen = null;
            territorioSeleccionadoParaAtaqueDestino = null;
        }

        Debug.Log("=== FIN START PARTIDA ===");

        try
        {
            Saving();
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error en Saving() inicial: " + ex.Message + ". Continuando sin guardar.");
        }
    }

    public void IniciarPartida()
    {
        Debug.Log("Iniciando...");
        estado = EstadoPartida.EnCurso;
        DistribuirTerritoriosIniciales();
        jugadorEnTurno = jugadores.Obtener(0);

        faseActual = FaseTurno.Refuerzos;
        Debug.Log("Partida iniciada. Jugador en turno: " + jugadorEnTurno.alias + ", Fase: " + faseActual);
        Refuerzos = CalcularRefuerzos(jugadorEnTurno);
        jugadorEnTurno.RecibirRefuerzos(Refuerzos);
        tropasDisponiblesParaRefuerzo = jugadorEnTurno.tropasDisponibles;
        mapa.TintTerritorios();
        ActualizarJugadorEnTurno(jugadorEnTurno);
        Saving();
    }

    public void SiguienteTurno()
    {
        VerificarVictoria();
        

        if (estado == EstadoPartida.Finalizada)
        {
            Debug.Log("¡Partida Finalizada! Ganador: " + jugadorEnTurno.alias);
            return;
        }

        int currentIndex = -1;
        for (int i = 0; i < jugadores.Contar(); i++)
        {
            if (jugadores.Obtener(i) == jugadorEnTurno)
            {
                currentIndex = i;
                break;
            }
        }

        int nextIndex = (currentIndex + 1) % jugadores.Contar();
        jugadorEnTurno = jugadores.Obtener(nextIndex);
        faseActual = FaseTurno.Refuerzos;

        if (nextIndex == 0)
        {
            contadorRonda++;
            Debug.Log("Nueva Ronda: " + contadorRonda);
        }
        

        Refuerzos = CalcularRefuerzos(jugadorEnTurno);
        jugadorEnTurno.RecibirRefuerzos(Refuerzos);
        tropasDisponiblesParaRefuerzo = jugadorEnTurno.tropasDisponibles;
        Debug.Log("Siguiente turno. Jugador en turno: " + jugadorEnTurno.alias + ", Fase: " + faseActual);
        Mapa.instance.TintTerritorios();
        ActualizarJugadorEnTurno(jugadorEnTurno);
        jugadoractual.text = jugadorEnTurno.alias;
        Saving();
    }


    public void SiguienteFase()
    {
        switch (faseActual)
        {
            case FaseTurno.Refuerzos:
                faseActual = FaseTurno.Ataque;
                Debug.Log("Fase: Ataque");
                break;
            case FaseTurno.Ataque:
                // Limpiar selecciones de ataque al cambiar de fase
                territorioSeleccionadoParaAtaqueOrigen = null;
                territorioSeleccionadoParaAtaqueDestino = null;
                faseActual = FaseTurno.Planeacion;
                Debug.Log("Fase: Planeación");
                break;
            case FaseTurno.Planeacion:
                Debug.Log("Fin de turno. Pasando al siguiente jugador.");
                SiguienteTurno();
                break;
        }
        Saving();
    }

    public void DistribuirTerritoriosIniciales()
    {
        Debug.Log("=== INICIANDO DISTRIBUCIÓN DE TERRITORIOS ===");

        ListaArray<Territorio> todosLosTerritorios = mapa.GetTodosTerritorios();
        if (todosLosTerritorios == null || todosLosTerritorios.Contar() == 0)
        {
            Debug.LogError("todosLosTerritorios es null o vacío! No se puede distribuir.");
            return;
        }
        Debug.Log("Territorios obtenidos: " + todosLosTerritorios.Contar());

        Shuffle(todosLosTerritorios);
        Debug.Log("Lista mezclada correctamente.");

        // Preparar lista de jugadores + neutral
        ListaArray<Jugador> propietariosPotenciales = new ListaArray<Jugador>();
        for (int i = 0; i < jugadores.Contar(); i++)
        {
            Jugador j = jugadores.Obtener(i);
            if (j.territoriosConquistados == null)
                j.territoriosConquistados = new ListaArray<Territorio>();
            else
                j.territoriosConquistados = new ListaArray<Territorio>(); // Limpiar lista

            propietariosPotenciales.Agregar(j);
            Debug.Log("Agregado jugador a potenciales: " + j.alias);
        }
        if (ejercitoNeutral.territoriosConquistados == null)
            ejercitoNeutral.territoriosConquistados = new ListaArray<Territorio>();
        else
            ejercitoNeutral.territoriosConquistados = new ListaArray<Territorio>(); // Limpiar lista

        propietariosPotenciales.Agregar(ejercitoNeutral);
        Debug.Log("Propietarios potenciales: " + propietariosPotenciales.Contar() + " (incluyendo neutral).");

        int propietarioIndex = 0;
        int asignadosPlayer1 = 0, asignadosPlayer2 = 0, asignadosNeutral = 0;
        int nulls = 0;

        // Asignar territorios alternadamente
        for (int i = 0; i < todosLosTerritorios.Contar(); i++)
        {
            Territorio t = todosLosTerritorios.Obtener(i);
            if (t == null)
            {
                Debug.LogWarning("Territorio null en índice " + i + ". Saltando asignación.");
                nulls++;
                continue;
            }

            Jugador p = propietariosPotenciales.Obtener(propietarioIndex);
            if (p == null)
            {
                Debug.LogError("Propietario null en índice " + propietarioIndex + ". Saltando.");
                continue;
            }

            t.jugadorPropietario = p;
            t.cantidadTropas = 1; // Inicialmente 1 tropa
            p.territoriosConquistados.Agregar(t);

            if (p.alias == "Player1") asignadosPlayer1++;
            else if (p.alias == "Player2") asignadosPlayer2++;
            else if (p.alias == "Neutral") asignadosNeutral++;

            Debug.Log($"Asignado territorio {i} ('{t.nombre}') a {p.alias} (tropas: 1). Total para {p.alias}: {p.territoriosConquistados.Contar()}");

            propietarioIndex = (propietarioIndex + 1) % propietariosPotenciales.Contar();
        }

        Debug.Log($"Distribución completada: Player1={asignadosPlayer1}, Player2={asignadosPlayer2}, Neutral={asignadosNeutral}, Nulls={nulls}. Total asignados: {asignadosPlayer1 + asignadosPlayer2 + asignadosNeutral}");

        // Ahora distribuir tropas para cada jugador (excluyendo neutral si quieres)
        for (int i = 0; i < jugadores.Contar(); i++)
        {
            Jugador jugador = jugadores.Obtener(i);
            if (jugador.territoriosConquistados != null && jugador.territoriosConquistados.Contar() > 0)
            {
                DistribuirTropasJugador(jugador.territoriosConquistados, 40);
                Debug.Log($"Tropas distribuidas para {jugador.alias} en {jugador.territoriosConquistados.Contar()} territorios.");
            }
        }

        // Opcional: distribuir tropas para neutral si quieres (puedes usar otro número o no distribuir)
        if (ejercitoNeutral.territoriosConquistados != null && ejercitoNeutral.territoriosConquistados.Contar() > 0)
        {
            DistribuirTropasJugador(ejercitoNeutral.territoriosConquistados, 40);
            Debug.Log($"Tropas distribuidas para Neutral en {ejercitoNeutral.territoriosConquistados.Contar()} territorios.");
        }

        for (int i = 0; i < jugadores.Contar(); i++)
        {
            jugadores.Obtener(i).ReestablecerReferenciasInversas();
        }
        ejercitoNeutral.ReestablecerReferenciasInversas();
        Debug.Log("Referencias inversas reestablecidas.");

        VerificarAsignacionesPostDistribucion();

        Mapa.instance.TintTerritorios();
        Mapa.instance.ActualizarVisualTropasEnMapa();
        Debug.Log("TintTerritorios() llamado después de distribución. Verifica logs de TintTerritorios para propietarios.");
    }

    /// <summary>
    /// Distribuye tropas entre los territorios de un jugador, asignando entre 1 y 3 tropas por territorio,
    /// sin exceder el total de tropas disponibles.
    /// </summary>
    private void DistribuirTropasJugador(ListaArray<Territorio> territorios, int tropasDisponibles)
    {
        int territoriosCount = territorios.Contar();
        int tropasRestantes = tropasDisponibles;

        System.Random rnd = new System.Random();

        for (int i = 0; i < territoriosCount; i++)
        {
            // Tropas máximas que podemos asignar en este territorio para no pasarnos
            // Consideramos que cada territorio debe tener al menos 1 tropa
            int maxAsignar = Mathf.Min(3, tropasRestantes - (territoriosCount - i - 1));
            maxAsignar = Mathf.Max(maxAsignar, 1);

            int tropasAsignar = rnd.Next(1, maxAsignar + 1);

            // Asignamos tropas al territorio
            territorios.Obtener(i).cantidadTropas = tropasAsignar;

            tropasRestantes -= tropasAsignar;

            if (tropasRestantes <= 0 && i < territoriosCount - 1)
            {
                for (int j = i + 1; j < territoriosCount; j++)
                {
                    territorios.Obtener(j).cantidadTropas = 1;
                }
                break;
            }
        }
    }



    private void SincronizarHandlersConTerritoriosGlobales()
    {
        if (mapa == null || mapa.territorioHandlers == null)
        {
            Debug.LogError("mapa o territorioHandlers es null en SincronizarHandlers. Saltando sincronización.");
            return;
        }

        Debug.Log("Sincronizando handlers con territorios globales...");
        int sincronizados = 0;
        int desincronizados = 0;
        int totalHandlers = mapa.territorioHandlers.Contar();

        for (int i = 0; i < totalHandlers; i++)
        {
            GameObject go = mapa.territorioHandlers.Obtener(i);
            if (go == null)
            {
                Debug.LogWarning("GameObject null en territorioHandlers índice " + i + ". Saltando.");
                continue;
            }

            TerritorioHandler handler = go.GetComponent<TerritorioHandler>();
            if (handler == null)
            {
                Debug.LogWarning("TerritorioHandler null en " + go.name + " (índice " + i + "). Saltando.");
                continue;
            }

            if (handler.territorio == null)
            {
                Debug.LogWarning("Territorio null en handler de " + go.name + ". Saltando sincronización.");
                desincronizados++;
                continue;
            }

            Territorio globalTerritorio = mapa.GetTerritorioPorNombre(handler.territorio.nombre);
            if (globalTerritorio != null && globalTerritorio != handler.territorio)
            {
                handler.territorio = globalTerritorio;
                sincronizados++;
                Debug.Log("Sincronizado handler de " + go.name + ": ahora apunta al territorio global '" + globalTerritorio.nombre + "' (propietario: " + (globalTerritorio.jugadorPropietario?.alias ?? "null") + ")");
            }
            else if (globalTerritorio == null)
            {
                desincronizados++;
                Debug.LogWarning("No se encontró territorio global para handler de " + go.name + ". Creando nuevo o saltando...");
            }
            else
            {
                Debug.Log("Handler de " + go.name + " ya sincronizado con global.");
            }
        }

        Debug.Log("Sincronización completada: Procesados " + totalHandlers + " handlers. Sincronizados: " + sincronizados + ", Desincronizados: " + desincronizados);
    }

    private void VerificarAsignacionesPostDistribucion()
    {
        int conPropietario = 0;
        int sinPropietario = 0;
        for (int i = 0; i < mapa.territorios.Contar(); i++)
        {
            Territorio t = mapa.territorios.Obtener(i);
            if (t.jugadorPropietario != null)
            {
                conPropietario++;
                Debug.Log("Territorio '" + t.nombre + "' tiene propietario: " + t.jugadorPropietario.alias);
            }
            else
            {
                sinPropietario++;
                Debug.LogWarning("Territorio '" + t.nombre + "' SIN propietario después de distribución!");
            }
        }
        Debug.Log("Verificación post-distribución: " + conPropietario + " con propietario, " + sinPropietario + " sin propietario.");
    }

    private void Shuffle<T>(ListaArray<T> list)
    {
        System.Random rng = new System.Random();
        int n = list.Contar();
        List<T> temp = new List<T>();
        for (int i = 0; i < n; i++) temp.Add(list.Obtener(i));

        for (int i = n - 1; i > 0; i--)
        {
            int k = rng.Next(i + 1);
            T value = temp[k];
            temp[k] = temp[i];
            temp[i] = value;
        }

        for (int i = 0; i < n; i++) list.Eliminar(0);
        for (int i = 0; i < n; i++) list.Agregar(temp[i]);
    }

    public int CalcularRefuerzos(Jugador j)
    {
        int refuerzosBase = Mathf.Max(3, j.territoriosConquistados.Contar() / 3);
        int bonusContinentes = 0;
        for (int i = 0; i < mapa.continentes.Contar(); i++)
        {
            bonusContinentes += mapa.continentes.Obtener(i).CalcularBonificacion(j);
        }
        return refuerzosBase + bonusContinentes;
    }

    /// <summary>
    /// Inicia un ataque entre dos territorios.
    /// </summary>
    public void Atacar(Territorio atacante, Territorio defensor, int tropasAtacantes, int tropasDefensoras)
    {
        // Validaciones
        if (atacante == null || defensor == null)
        {
            Debug.LogError("Territorios de ataque o defensa nulos.");
            return;
        }
        if (atacante.jugadorPropietario != jugadorEnTurno)
        {
            Debug.LogError("El territorio atacante no pertenece al jugador en turno.");
            return;
        }
        if (defensor.jugadorPropietario == jugadorEnTurno)
        {
            Debug.LogError("No puedes atacar tus propios territorios.");
            return;
        }
        if (!mapa.SonAdyacentes(atacante, defensor))
        {
            Debug.LogError("Los territorios no son adyacentes.");
            return;
        }
        if (atacante.cantidadTropas <= tropasAtacantes) // Debe dejar al menos 1 tropa en el origen
        {
            Debug.LogError("Debes dejar al menos una tropa en el territorio atacante.");
            return;
        }
        if (tropasAtacantes < 1 || tropasAtacantes > 3) // RISK permite 1, 2 o 3 dados de ataque
        {
            Debug.LogError("Número de tropas atacantes inválido (debe ser 1, 2 o 3).");
            return;
        }
        if (tropasAtacantes > atacante.cantidadTropas - 1)
        {
            tropasAtacantes = atacante.cantidadTropas - 1; // Ajustar si el jugador intenta usar más de las disponibles
        }

        Debug.Log(jugadorEnTurno.alias + " ataca " + defensor.nombre + " desde " + atacante.nombre + " con " + tropasAtacantes + " tropas.");

        attackedTerritoryName = defensor.nombre;
        // Validar que tropas elegidas no excedan tropas disponibles
        int tropasAtacantesValidas = Mathf.Min(tropasAtacantes, atacante.cantidadTropas - 1); // siempre debe quedar al menos 1 tropa
        int tropasDefensorasValidas = Mathf.Min(tropasDefensoras, defensor.cantidadTropas);

        int tropasAtacanteDados = Mathf.Min(3, tropasAtacantesValidas);
        int tropasDefensorDados = Mathf.Min(2, tropasDefensorasValidas);

        Debug.Log($"{jugadorEnTurno.alias} ataca {defensor.nombre} desde {atacante.nombre} con {tropasAtacanteDados} dados. Defensor usa {tropasDefensorDados} dados.");

        territorioSeleccionadoParaAtaqueOrigen = atacante;
        territorioSeleccionadoParaAtaqueDestino = defensor;

        combateManager.IniciarCombate(tropasAtacanteDados, tropasDefensorDados);

    }

    /// <summary>
    /// Mueve tropas entre territorios del mismo jugador.
    /// </summary>
    public void MoverTropas(Territorio origen, Territorio destino, int cantidad)
    {
        // Validaciones
        if (origen == null || destino == null)
        {
            Debug.LogError("Territorios de origen o destino nulos.");
            return;
        }
        if (origen.jugadorPropietario != jugadorEnTurno || destino.jugadorPropietario != jugadorEnTurno)
        {
            Debug.LogError("Ambos territorios deben pertenecer al jugador en turno.");
            return;
        }
        if (origen.cantidadTropas <= cantidad) // Debe dejar al menos 1 tropa en el origen
        {
            Debug.LogError("Debes dejar al menos una tropa en el territorio de origen.");
            return;
        }
        if (cantidad <= 0)
        {
            Debug.LogError("La cantidad de tropas a mover debe ser mayor que cero.");
            return;
        }
        if (!mapa.ExisteRutaSegura(origen, destino, jugadorEnTurno))
        {
            Debug.LogError("No existe una ruta segura entre los territorios de origen y destino.");
            return;
        }

        origen.cantidadTropas -= cantidad;
        destino.cantidadTropas += cantidad;
        Debug.Log(jugadorEnTurno.alias + " movió " + cantidad + " tropas de " + origen.nombre + " a " + destino.nombre);
        mapa.ActualizarVisualTropasEnMapa();
        Saving();
    }

    /// <summary>
    /// Verifica si algún jugador ha ganado la partida.
    /// </summary>
    public Jugador VerificarVictoria()
    {
        for (int i = 0; i < jugadores.Contar(); i++)
        {
            Jugador j = jugadores.Obtener(i);
            if (j.territoriosConquistados.Contar() == mapa.territorios.Contar())
            {
                estado = EstadoPartida.Finalizada;
                Debug.Log("¡" + j.alias + " ha conquistado todos los territorios y ganado la partida!");
                return j;
            }
        }
        return null;
    }

    /// <summary>
    /// Muestra el panel de ataque con la información del territorio defensor.
    /// </summary>
    public void ShowAttackPanel(string description, Territorio defensor, int tropasAtacantesMax, int tropasDefensoras) // Parámetros actualizados
    {

        attackPanel.gameObject.SetActive(true);


        attackPanel.SetupPanel(description, defensor, tropasDefensoras, tropasAtacantesMax);
        // Usar el nuevo SetupPanel
    }

    /// <summary>
    /// Deshabilita el panel de ataque.
    /// </summary>
    public void DisableAttackPanel()
    {
        attackPanel.gameObject.SetActive(false);
    }

    /// <summary>
    /// Inicia la escena de lucha.
    /// </summary>
    public void StartFight()
    {
        dado.gameObject.SetActive(true);


    }

    // Métodos de Guardado y Carga (adaptados a las nuevas clases)
    public void Saving()
    {
        if (mapa == null)
        {
            Debug.LogError("mapa es NULL en Saving(). No se puede guardar.");
            return;
        }

        Debug.Log("=== INICIANDO SAVING ===");

        try
        {
            SaveDataJSON dataJSON = new SaveDataJSON();

            dataJSON.territoriosData = new TerritorioSaveData[mapa.territorios.Contar()];
            for (int i = 0; i < mapa.territorios.Contar(); i++)
            {
                Territorio t = mapa.territorios.Obtener(i);
                if (t != null)
                {
                    string propietarioAlias = (t.jugadorPropietario != null) ? t.jugadorPropietario.alias : "Neutral";
                    dataJSON.territoriosData[i] = new TerritorioSaveData
                    {
                        nombre = t.nombre,
                        cantidadTropas = t.cantidadTropas,
                        propietarioAlias = propietarioAlias
                    };
                }
            }

            dataJSON.jugadoresData = new JugadorSaveData[jugadores.Contar()];
            for (int i = 0; i < jugadores.Contar(); i++)
            {
                Jugador j = jugadores.Obtener(i);
                dataJSON.jugadoresData[i] = new JugadorSaveData
                {
                    alias = j.alias,
                    colorR = j.color.r,
                    colorG = j.color.g,
                    colorB = j.color.b,
                    colorA = j.color.a
                };
            }

            dataJSON.currentJugadorIndex = GetJugadorIndex(jugadorEnTurno);
            dataJSON.currentContadorRonda = contadorRonda;
            dataJSON.currentEstadoPartida = (int)estado;
            dataJSON.currentFaseTurno = (int)faseActual;

            string json = JsonUtility.ToJson(dataJSON, true);
            string savePath = Application.persistentDataPath + "/SaveFile.json";
            File.WriteAllText(savePath, json);

            Debug.Log("Juego guardado exitosamente en: " + savePath);
            Debug.Log("JSON preview: " + json.Substring(0, Math.Min(200, json.Length)));
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error en Saving(): " + ex.Message + "\nStack: " + ex.StackTrace);
        }

        Debug.Log("=== FIN SAVING ===");
    }

    private int GetJugadorIndex(Jugador jugador)
    {
        for (int i = 0; i < jugadores.Contar(); i++)
        {
            if (jugadores.Obtener(i) == jugador) return i;
        }
        return 0;
    }

    public void Loading()
    {
        string savePath = Application.persistentDataPath + "/SaveFile.json";
        if (!File.Exists(savePath))
        {
            Debug.Log("No se encontró archivo de guardado.");
            return;
        }

        if (mapa == null)
        {
            Debug.LogError("mapa es NULL en Loading(). No se puede cargar.");
            return;
        }

        Debug.Log("=== INICIANDO LOADING ===");

        try
        {
            string json = File.ReadAllText(savePath);
            SaveDataJSON dataJSON = JsonUtility.FromJson<SaveDataJSON>(json);

            if (dataJSON == null)
            {
                Debug.LogError("JSON inválido o vacío.");
                return;
            }

            jugadores = new ListaArray<Jugador>();
            foreach (var jData in dataJSON.jugadoresData)
            {
                string alias = string.IsNullOrEmpty(jData.alias) ? "JugadorSinAlias" : jData.alias;
                Color32 color = new Color32(jData.colorR, jData.colorG, jData.colorB, jData.colorA);
                Jugador j = new Jugador(alias, color);
                jugadores.Agregar(j);
                Debug.Log($"Jugador cargado: alias='{alias}', color={color}");
            }

            ejercitoNeutral = new Jugador("Neutral", new Color32(150, 150, 150, 255));
            jugadores.Agregar(ejercitoNeutral);

            // Inicializar listas de territorios conquistados
            for (int i = 0; i < jugadores.Contar(); i++)
            {
                jugadores.Obtener(i).territoriosConquistados = new ListaArray<Territorio>();
            }

            // Restaurar territorios y propietarios
            for (int i = 0; i < dataJSON.territoriosData.Length; i++)
            {
                var tData = dataJSON.territoriosData[i];
                Territorio currentTerritorio = mapa.GetTerritorioPorNombre(tData.nombre);
                if (currentTerritorio != null)
                {
                    currentTerritorio.cantidadTropas = tData.cantidadTropas;

                    Jugador propietarioRestaurado = null;
                    for (int j = 0; j < jugadores.Contar(); j++)
                    {
                        if (jugadores.Obtener(j).alias == tData.propietarioAlias)
                        {
                            propietarioRestaurado = jugadores.Obtener(j);
                            break;
                        }
                    }
                    if (propietarioRestaurado == null && tData.propietarioAlias == "Neutral")
                    {
                        propietarioRestaurado = ejercitoNeutral;
                    }

                    currentTerritorio.jugadorPropietario = propietarioRestaurado;
                    if (propietarioRestaurado != null)
                    {
                        propietarioRestaurado.territoriosConquistados.Agregar(currentTerritorio);
                    }
                }
                else
                {
                    Debug.LogWarning("Territorio no encontrado al cargar: " + tData.nombre);
                }
            }

            // Validar índice de jugador en turno
            int idx = dataJSON.currentJugadorIndex;
            if (idx < 0 || idx >= jugadores.Contar())
            {
                Debug.LogWarning($"Índice de jugador en turno inválido ({idx}), asignando jugador 0 por defecto.");
                idx = 0;
            }
            // Evitar que el jugador neutral sea jugador en turno (si no es deseado)
            if (idx == jugadores.Contar() - 1) // Último jugador es neutral
            {
                Debug.LogWarning("El jugador en turno apunta al jugador neutral, asignando jugador 0 por defecto.");
                idx = 0;
            }

            jugadorEnTurno = jugadores.Obtener(idx);

            if (jugadorEnTurno == null || string.IsNullOrEmpty(jugadorEnTurno.alias))
            {
                Debug.LogError("jugadorEnTurno es null o tiene alias vacío después de cargar. Asignando jugador 0.");
                jugadorEnTurno = jugadores.Obtener(0);
            }

            contadorRonda = dataJSON.currentContadorRonda;
            estado = (EstadoPartida)dataJSON.currentEstadoPartida;
            faseActual = (FaseTurno)dataJSON.currentFaseTurno;

            Debug.Log($"Jugador en turno asignado en Loading: '{jugadorEnTurno.alias}' (índice {idx})");
            Debug.Log($"Ronda: {contadorRonda}, Estado: {estado}, Fase: {faseActual}");

            // Reestablecer referencias inversas y reconstruir mapa
            for (int i = 0; i < jugadores.Contar(); i++)
            {
                jugadores.Obtener(i).ReestablecerReferenciasInversas();
            }
            ejercitoNeutral.ReestablecerReferenciasInversas();
            for (int i = 0; i < mapa.continentes.Contar(); i++)
            {
                mapa.continentes.Obtener(i).ReestablecerReferenciasInversas();
            }
            mapa.ReconstruirAdyacenciasLocales();
            mapa.TintTerritorios();

            Debug.Log("Juego cargado exitosamente. Territorios restaurados: " + dataJSON.territoriosData.Length);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error en Loading(): " + ex.Message + "\nStack: " + ex.StackTrace);
        }

        Debug.Log("=== FIN LOADING ===");
    }


    public void DeleteSaveFile()
    {
        if (File.Exists(Application.persistentDataPath + "/SaveFile.sept"))
        {
            File.Delete(Application.persistentDataPath + "/SaveFile.sept");
            Debug.Log("Archivo de guardado eliminado.");
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    public void SeleccionarTerritorio(Territorio territorio)
    {
        if (faseActual == FaseTurno.Refuerzos)
        {
            if (territorio.jugadorPropietario != jugadorEnTurno)
            {
                Debug.LogWarning("Solo puedes seleccionar tus propios territorios para reforzar.");
                return;
            }
            if (territorioSeleccionadoParaRefuerzo == territorio)
            {
                territorioSeleccionadoParaRefuerzo = null;
                Debug.Log("Territorio de refuerzo deseleccionado: " + territorio.nombre);
            }
            else
            {
                territorioSeleccionadoParaRefuerzo = territorio;
                Debug.Log("Territorio de refuerzo seleccionado: " + territorio.nombre);
            }
            if (tropasDisponiblesParaRefuerzo > 0)
            {
                AgregarTropasRefuerzo(territorio, 1);
            }
            return;
        }

        if (faseActual == FaseTurno.Ataque)
        {
            if (jugadorEnTurno == null)
            {
                Debug.LogError("jugadorEnTurno es NULL. No se puede seleccionar territorio.");
                return;
            }

            Debug.Log($"Jugador en turno: {jugadorEnTurno.alias}. Seleccionando territorio: {territorio.nombre} (Propietario: {(territorio.jugadorPropietario != null ? territorio.jugadorPropietario.alias : "null")})");

            if (territorio.jugadorPropietario == jugadorEnTurno)
            {
                if (territorioSeleccionadoParaAtaqueOrigen == null)
                {
                    if (territorio.cantidadTropas > 1)
                    {
                        territorioSeleccionadoParaAtaqueOrigen = territorio;
                        Debug.Log("Territorio de ataque ORIGEN seleccionado: " + territorio.nombre);
                    }
                    else
                    {
                        Debug.LogWarning("El territorio " + territorio.nombre + " tiene solo 1 tropa. Necesita al menos 2 para atacar.");
                    }
                }
                else if (territorioSeleccionadoParaAtaqueOrigen == territorio)
                {
                    territorioSeleccionadoParaAtaqueOrigen = null;
                    Debug.Log("Territorio de ataque ORIGEN deseleccionado: " + territorio.nombre);
                }
                else
                {
                    territorioSeleccionadoParaAtaqueOrigen = territorio;
                    territorioSeleccionadoParaAtaqueDestino = null;
                    Debug.Log("Territorio de ataque ORIGEN cambiado a: " + territorio.nombre);
                }
            }
            else
            {
                if (territorioSeleccionadoParaAtaqueOrigen == null)
                {
                    Debug.LogWarning("Primero selecciona uno de tus territorios para atacar.");
                    return;
                }

                if (mapa.SonAdyacentes(territorioSeleccionadoParaAtaqueOrigen, territorio))
                {
                    territorioSeleccionadoParaAtaqueDestino = territorio;
                    Debug.Log("Territorio de ataque DESTINO seleccionado: " + territorio.nombre);
                    ShowAttackPanel(
                        "Atacar " + territorio.nombre + " (Tropas: " + territorio.cantidadTropas + ") desde " +
                        territorioSeleccionadoParaAtaqueOrigen.nombre + " (Tus tropas: " + (territorioSeleccionadoParaAtaqueOrigen.cantidadTropas - 1) + ")",
                        territorio,
                        territorio.cantidadTropas,
                        territorioSeleccionadoParaAtaqueOrigen.cantidadTropas - 1
                    );

                }
                else
                {
                    Debug.LogWarning("El territorio " + territorio.nombre + " no es adyacente a " + territorioSeleccionadoParaAtaqueOrigen.nombre);
                    territorioSeleccionadoParaAtaqueDestino = null;
                }
            }
            return;
        }

        if (faseActual == FaseTurno.Planeacion)
        {
            if (jugadorEnTurno == null)
            {
                Debug.LogError("jugadorEnTurno es NULL. No se puede seleccionar territorio.");
                return;
            }

            if (territorio.jugadorPropietario != jugadorEnTurno)
            {
                Debug.LogWarning("Solo puedes seleccionar tus propios territorios para mover tropas.");
                return;
            }
            if (territorioSeleccionadoParaMovimientoOrigen == null)
            {
                // Seleccionamos el territorio origen solo si tiene más de 1 tropa
                if (territorio.cantidadTropas > 1)
                {
                    territorioSeleccionadoParaMovimientoOrigen = territorio;
                    Debug.Log("Territorio de movimiento ORIGEN seleccionado: " + territorio.nombre);
                }
                else
                {
                    Debug.LogWarning("El territorio " + territorio.nombre + " tiene solo 1 tropa. Necesita al menos 2 para mover tropas.");
                }
            }


            if (territorioSeleccionadoParaMovimientoOrigen.nombre == "")
            {
                // Seleccionamos el territorio origen solo si tiene más de 1 tropa
                if (territorio.cantidadTropas > 1)
                {
                    territorioSeleccionadoParaMovimientoOrigen = territorio;
                    Debug.Log("Territorio de movimiento ORIGEN seleccionado: " + territorio.nombre);
                }
                else
                {
                    Debug.LogWarning("El territorio " + territorio.nombre + " tiene solo 1 tropa. Necesita al menos 2 para mover tropas.");
                }
            }
            else
            {
                // Ya hay un territorio origen seleccionado, ahora seleccionamos destino
                Debug.Log($"Territorio origen seleccionado: {territorioSeleccionadoParaMovimientoOrigen.nombre}. Intentando seleccionar destino: {territorio.nombre}");

                // Validar que el destino sea distinto al origen
                if (territorio == territorioSeleccionadoParaMovimientoOrigen)
                {
                    // Deseleccionar origen si se selecciona de nuevo
                    territorioSeleccionadoParaMovimientoOrigen = null;
                    Debug.Log("Territorio de movimiento ORIGEN deseleccionado: " + territorio.nombre);
                    return;
                }

                // Validar ruta segura solo si origen no es null (ya validado, pero por seguridad)
                if (territorioSeleccionadoParaMovimientoOrigen == null)
                {
                    Debug.LogWarning("No hay territorio origen seleccionado para validar ruta segura.");
                    return;
                }

                // Validar ruta segura
                bool rutaSegura = mapa.ExisteRutaSegura(territorioSeleccionadoParaMovimientoOrigen, territorio, jugadorEnTurno);
                Debug.Log($"Ruta segura entre {territorioSeleccionadoParaMovimientoOrigen.nombre} y {territorio.nombre}: {rutaSegura}");

                if (rutaSegura)
                {
                    territorioSeleccionadoParaMovimientoDestino = territorio;
                    Debug.Log("Territorio de movimiento DESTINO seleccionado: " + territorio.nombre);

                    int cantidadAMover = 1; // Aquí puedes cambiar para que sea variable o pedir input

                    MoverTropas(territorioSeleccionadoParaMovimientoOrigen, territorioSeleccionadoParaMovimientoDestino, cantidadAMover);

                    // Limpiar selecciones después de mover
                    territorioSeleccionadoParaMovimientoOrigen = null;
                    territorioSeleccionadoParaMovimientoDestino = null;
                }
                else
                {
                    Debug.LogWarning("No existe una ruta segura entre " + territorioSeleccionadoParaMovimientoOrigen.nombre + " y " + territorio.nombre);
                    territorioSeleccionadoParaMovimientoDestino = null;
                }
            }
            return;
        }
    }


    /// <summary>
    /// Intenta agregar tropas al territorio seleccionado durante la fase de refuerzos.
    /// </summary>
    public void AgregarTropasRefuerzo(Territorio territorio, int cantidad)
    {
        if (faseActual != FaseTurno.Refuerzos)
        {
            Debug.LogWarning("No es la fase de refuerzos. No se pueden agregar tropas.");
            return;
        }

        if (territorio == null)
        {
            Debug.LogError("Territorio nulo en AgregarTropasRefuerzo.");
            return;
        }

        if (territorio.jugadorPropietario != jugadorEnTurno)
        {
            Debug.LogWarning("No puedes reforzar un territorio que no te pertenece.");
            return;
        }

        if (cantidad <= 0)
        {
            Debug.LogWarning("Cantidad de tropas a agregar debe ser mayor que cero.");
            return;
        }

        if (cantidad > tropasDisponiblesParaRefuerzo)
        {
            Debug.LogWarning($"No tienes suficientes tropas para agregar. Disponibles: {tropasDisponiblesParaRefuerzo}, solicitadas: {cantidad}");
            return;
        }


        jugadorEnTurno.UsarTropas(cantidad);
        territorio.cantidadTropas += cantidad;
        tropasDisponiblesParaRefuerzo -= cantidad;
        ActualizarJugadorEnTurno(jugadorEnTurno);
        mapa.ActualizarVisualTropasEnMapa();



        Debug.Log($"{jugadorEnTurno.alias} agregó {cantidad} tropas a {territorio.nombre}. Tropas restantes para refuerzo: {tropasDisponiblesParaRefuerzo}");

        mapa.TintTerritorios();
        Saving();
    }
    void ActualizarJugadorEnTurno(Jugador jugador)
    {
        if (jugador == null)
        {
            Debug.LogError("jugador es NULL en ActualizarJugadorEnTurno(). No se puede actualizar UI.");
            return;
        }
        if (jugador == jugadores.Obtener(0))
        {
            playerInfoUI1.ActualizarInfo(jugador.alias, jugador.color, jugador.tropasDisponibles);
        }
        else if (jugador == jugadores.Obtener(1))
        {
            playerInfoUI2.ActualizarInfo(jugador.alias, jugador.color, jugador.tropasDisponibles);
        }
    }
    public void ResolverCombate(int[] dadosAtacante, int[] dadosDefensor)
    {
        Array.Sort(dadosAtacante);
        Array.Reverse(dadosAtacante);
        Array.Sort(dadosDefensor);
        Array.Reverse(dadosDefensor);

        int comparaciones = Math.Min(dadosAtacante.Length, dadosDefensor.Length);

        for (int i = 0; i < comparaciones; i++)
        {
            if (dadosAtacante[i] > dadosDefensor[i])
            {
                territorioSeleccionadoParaAtaqueDestino.cantidadTropas--;
                Debug.Log("Defensor pierde 1 tropa");
                dado.gameObject.SetActive(false);
                DisableAttackPanel();
                attackPanel.ClosePanel();
            }
            else if (dadosAtacante[i] < dadosDefensor[i])
            {
                territorioSeleccionadoParaAtaqueOrigen.cantidadTropas--;
                Debug.Log("Atacante pierde 1 tropa");
                dado.gameObject.SetActive(false);
                DisableAttackPanel();
                attackPanel.ClosePanel();
            }
            else
            {
                // Empate en los dados: reiniciar el combate lanzando los dados otra vez
                Debug.LogWarning("Empate en la tirada de dados. Re-lanzando dados...");

                // Llamamos nuevamente a Atacar con los mismos parámetros para rodar los dados otra vez
                dado.gameObject.SetActive(false);
                attackPanel.Empate();
                return; // Salir del método para evitar aplicar más lógica actualmente
            }
        }

        // Código existente para conquista si el defensor se queda sin tropas
        if (territorioSeleccionadoParaAtaqueDestino.cantidadTropas <= 0)
        {
            Debug.Log(jugadorEnTurno.alias + " ha conquistado " + territorioSeleccionadoParaAtaqueDestino.nombre);

            Jugador antiguoPropietario = territorioSeleccionadoParaAtaqueDestino.jugadorPropietario;
            if (antiguoPropietario != null)
            {
                antiguoPropietario.territoriosConquistados.EliminarElemento(territorioSeleccionadoParaAtaqueDestino);
            }

            territorioSeleccionadoParaAtaqueDestino.jugadorPropietario = jugadorEnTurno;
            jugadorEnTurno.territoriosConquistados.Agregar(territorioSeleccionadoParaAtaqueDestino);

            // Mover tropas atacantes al territorio conquistado (mínimo 1)
            int tropasAMover = Mathf.Min(territorioSeleccionadoParaAtaqueOrigen.cantidadTropas - 1, dadosAtacante.Length);
            territorioSeleccionadoParaAtaqueOrigen.cantidadTropas -= tropasAMover;
            territorioSeleccionadoParaAtaqueDestino.cantidadTropas += tropasAMover;
            dado.gameObject.SetActive(false);
            DefinirCartaAleatoria();
            if (jugadorEnTurno == jugadores.Obtener(0))
            {
                playerInfoUI1.ActualizarConteosCartas(jugadorEnTurno);
            }
            if (jugadorEnTurno == jugadores.Obtener(1))
            {
                playerInfoUI2.ActualizarConteosCartas(jugadorEnTurno);
            }

            mapa.TintTerritorios();

            Debug.Log($"Movidas {tropasAMover} tropas de {territorioSeleccionadoParaAtaqueOrigen.nombre} a {territorioSeleccionadoParaAtaqueDestino.nombre} después de la conquista.");
        }

        mapa.ActualizarVisualTropasEnMapa();
        Saving();
    }


    public void DefinirCartaAleatoria()
    {
        // Definir un array o lista con los tipos de cartas posibles (sin comodín si no quieres)
        Jugador.TipoCarta[] tiposCartas = new Jugador.TipoCarta[] {
            Jugador.TipoCarta.Infanteria,
            Jugador.TipoCarta.Caballeria,
            Jugador.TipoCarta.Artilleria
        };

        // Seleccionar aleatoriamente un índice válido
        int indiceAleatorio = UnityEngine.Random.Range(0, tiposCartas.Length);

        // Obtener la carta seleccionada
        Jugador.TipoCarta cartaSeleccionada = tiposCartas[indiceAleatorio];

        // Intentar agregar la carta al jugador en turno
        bool agregado = jugadorEnTurno.AgregarCarta(cartaSeleccionada);

        if (agregado)
        {
            Debug.Log(jugadorEnTurno.alias + " ha recibido una carta: " + cartaSeleccionada.ToString());
        }
        else
        {
            Debug.LogWarning(jugadorEnTurno.alias + " no pudo recibir la carta: " + cartaSeleccionada.ToString());
        }
    }
    public void Inercambio()
    {
        jugadorEnTurno.IntercambiarCartas();
    }




}
