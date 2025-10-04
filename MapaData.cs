using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AdyacenciaTerritorio
{
    public string territorio;
    public string[] adyacentes;
}
[System.Serializable]
public class AdyacenciasData
{
    public AdyacenciaTerritorio[] adyacencias;
}
[System.Serializable]
public class TerritorioJSON
{
    public string territorio;
    public string[] adyacentes;
}

// Clase auxiliar para parsear array JSON con JsonUtility
[System.Serializable]
public class TerritorioJSONArray
{
    public TerritorioJSON[] territorios;
}

[System.Serializable]
public class RootAdyacenciasJSON
{
    public TerritorioJSON[] adyacencias;
}
// ← NUEVAS CLASES WRAPPER PARA JSON (agrega al final de Partida.cs)
[System.Serializable]
public class SaveDataJSON
{
    public TerritorioSaveData[] territoriosData;
    public JugadorSaveData[] jugadoresData;
    public int currentJugadorIndex;
    public int currentContadorRonda;
    public int currentEstadoPartida;
    public int currentFaseTurno;
}

[System.Serializable]
public class TerritorioSaveData
{
    public string nombre;
    public int cantidadTropas;
    public string propietarioAlias;
}

[System.Serializable]
public class JugadorSaveData
{
    public string alias;
    public byte colorR, colorG, colorB, colorA;  // Color32 para precisión
}