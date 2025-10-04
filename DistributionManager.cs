using System;
using UnityEngine;

public class DistributionManager : MonoBehaviour
{
    public void DistribuirTropas(ListaArray<Territorio> territorios, int tropasDisponibles = 40)
    {
        int territoriosCount = territorios.Contar();  // Usar Contar() de ListaArray
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
}
