using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerInfoUI2 : MonoBehaviour
{
    public TextMeshProUGUI aliasText;
    public Image colorImage;
    public TextMeshProUGUI troopsText;

    // Método para actualizar la UI con la info del jugadorpublic TextMeshProUGUI textInfanteria;
    public TextMeshProUGUI textInfanteria;
    public TextMeshProUGUI textCaballeria;
    public TextMeshProUGUI textArtilleria;

    public void ActualizarInfo(string alias, Color color, int tropas)
    {
        aliasText.text = alias;
        colorImage.color = color;
        troopsText.text = tropas.ToString();
    }

    public void ActualizarConteosCartas(Jugador jugador)
    {
        if (jugador != null)
        {
            int countInfanteria = jugador.ContarCartasTipo(Jugador.TipoCarta.Infanteria);
            int countCaballeria = jugador.ContarCartasTipo(Jugador.TipoCarta.Caballeria);
            int countArtilleria = jugador.ContarCartasTipo(Jugador.TipoCarta.Artilleria);

            textInfanteria.text = countInfanteria.ToString();
            textCaballeria.text = countCaballeria.ToString();
            textArtilleria.text = countArtilleria.ToString();
        }
    }
}
