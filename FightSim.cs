using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FightSim : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(fight());
    }

    IEnumerator fight()
    {
        yield return new WaitForSeconds(2);
        int num = Random.Range(0, 2); // Simulación simple de victoria/derrota
        if (num == 0)
        {
            Partida.instance.battleWon = false;
        }
        else
        {
            Partida.instance.battleWon = true;
        }
        Partida.instance.battleHasEnded = true;
        SceneManager.LoadScene("Demo"); // Volver a la escena principal del mapa
    }
}
