using System;
using UnityEngine;

public class CombateManager : MonoBehaviour
{
    public Dice[] dadosAtacante; // Asignar en inspector, máximo 3 dados
    public Dice[] dadosDefensor; // Asignar en inspector, máximo 2 dados

    private int[] resultadosAtacante;
    private int[] resultadosDefensor;

    private int dadosAtacanteRestantes;
    private int dadosDefensorRestantes;

    private Partida partida;

    private bool combateEnCurso = false;

    void Awake()
    {
        partida = Partida.instance;

        // Suscribir eventos una sola vez
        for (int i = 0; i < dadosAtacante.Length; i++)
        {
            int index = i;
            dadosAtacante[i].OnDiceRolled += (resultado) =>
            {
                if (!combateEnCurso) return; // Ignorar si no hay combate activo
                resultadosAtacante[index] = resultado;
                dadosAtacanteRestantes--;
                RevisarSiTermino();
            };
        }

        for (int i = 0; i < dadosDefensor.Length; i++)
        {
            int index = i;
            dadosDefensor[i].OnDiceRolled += (resultado) =>
            {
                if (!combateEnCurso) return; // Ignorar si no hay combate activo
                resultadosDefensor[index] = resultado;
                dadosDefensorRestantes--;
                RevisarSiTermino();
            };
        }
    }

    public void IniciarCombate(int cantidadAtacante, int cantidadDefensor)
    {
        if (combateEnCurso)
        {
            Debug.LogWarning("Combate ya en curso, ignorando nuevo inicio.");
            return;
        }

        if (cantidadAtacante < 1 || cantidadAtacante > 3 || cantidadDefensor < 1 || cantidadDefensor > 2)
        {
            Debug.LogError("Cantidad de dados inválida. Atacante: 1-3, Defensor: 1-2.");
            return;
        }

        combateEnCurso = true;

        resultadosAtacante = new int[cantidadAtacante];
        resultadosDefensor = new int[cantidadDefensor];

        dadosAtacanteRestantes = cantidadAtacante;
        dadosDefensorRestantes = cantidadDefensor;

        // Lanzar dados atacante
        for (int i = 0; i < cantidadAtacante; i++)
        {
            dadosAtacante[i].RollDice();
        }

        // Lanzar dados defensor
        for (int i = 0; i < cantidadDefensor; i++)
        {
            dadosDefensor[i].RollDice();
        }
    }

    private void RevisarSiTermino()
    {
        if (dadosAtacanteRestantes == 0 && dadosDefensorRestantes == 0)
        {
            if (partida == null)
                partida = Partida.instance;
            if (partida == null)
            {
                Debug.LogError("Partida.instance es null en CombateManager.");
                return;
            }
            Debug.Log("Todos los dados lanzados. Resultados atacante: " + string.Join(",", resultadosAtacante) +
                    " | defensor: " + string.Join(",", resultadosDefensor));
            partida.ResolverCombate(resultadosAtacante, resultadosDefensor);
            combateEnCurso = false;
        }
    }

    
}
