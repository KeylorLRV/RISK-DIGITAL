using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AttackPanel : MonoBehaviour
{
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI infoText1;
    public TextMeshProUGUI infoText2;
    public Button attackButton;
    public Button cancelButton;
    public TMP_InputField troopsInputField; // Para tropas atacantes
    public TMP_InputField troopsDefensorInputField; // Para tropas defensoras
    public GameObject empate;

    private Territorio territorioDefensor;
    private Territorio territorioAtacanteOrigen;

    private int tropasAtacantesMax; // Máximo permitido para atacante
    private int tropasDefensorasMax; // Máximo permitido para defensor
    
    public void SetupPanel(string description, Territorio defensor, int tropasDefensoras, int tropasAtacantesMax)
    {
        DesactivarTerritorios();
        Debug.Log("SetupPanel called");
        empate.SetActive(false);
        descriptionText.text = description;
        infoText1.text = "Tropas defensoras disponibles: " + tropasDefensoras.ToString();
        infoText2.text = "Tus tropas disponibles para atacar: " + tropasAtacantesMax.ToString();

        territorioDefensor = defensor;
        territorioAtacanteOrigen = Partida.instance.territorioSeleccionadoParaAtaqueOrigen;

        this.tropasAtacantesMax = Mathf.Min(3, tropasAtacantesMax);
        this.tropasDefensorasMax = Mathf.Min(2, tropasDefensoras);

        attackButton.onClick.RemoveAllListeners();
        cancelButton.onClick.RemoveAllListeners();

        attackButton.onClick.AddListener(OnAttackButtonClicked);
        cancelButton.onClick.AddListener(OnCancelButtonClicked);

        // Configurar input atacante
        if (troopsInputField != null)
        {
            troopsInputField.text = "1";
            troopsInputField.characterLimit = 1;
            troopsInputField.onValidateInput = (string text, int charIndex, char addedChar) =>
            {
                if (!char.IsDigit(addedChar)) return '\0';
                string newText = text.Insert(charIndex, addedChar.ToString());
                if (int.TryParse(newText, out int val))
                {
                    if (val >= 1 && val <= this.tropasAtacantesMax)
                        return addedChar;
                }
                return '\0';
            };
        }

        // Configurar input defensor
        if (troopsDefensorInputField != null)
        {
            troopsDefensorInputField.text = "1";
            troopsDefensorInputField.characterLimit = 1;
            troopsDefensorInputField.onValidateInput = (string text, int charIndex, char addedChar) =>
            {
                if (!char.IsDigit(addedChar)) return '\0';
                string newText = text.Insert(charIndex, addedChar.ToString());
                if (int.TryParse(newText, out int val))
                {
                    if (val >= 1 && val <= this.tropasDefensorasMax)
                        return addedChar;
                }
                return '\0';
            };
        }
    }

    private void OnAttackButtonClicked()
    {
        int tropasAUsar = 1;
        int tropasDefensorasAUsar = 1;

        if (troopsInputField != null && int.TryParse(troopsInputField.text, out int parsedAtacante))
            tropasAUsar = parsedAtacante;

        if (troopsDefensorInputField != null && int.TryParse(troopsDefensorInputField.text, out int parsedDefensor))
            tropasDefensorasAUsar = parsedDefensor;

        if (territorioAtacanteOrigen == null)
        {
            Debug.LogError("Territorio atacante de origen es null al intentar atacar.");
            gameObject.SetActive(false);
            return;
        }

        // Validar atacante
        if (tropasAUsar < 1 || tropasAUsar > tropasAtacantesMax || tropasAUsar >= territorioAtacanteOrigen.cantidadTropas)
        {
            Debug.LogError($"Cantidad de tropas a usar para atacar inválida: {tropasAUsar}. Debe ser entre 1 y {tropasAtacantesMax}, y menor que tropas en origen.");
            return;
        }

        // Validar defensor
        if (tropasDefensorasAUsar < 1 || tropasDefensorasAUsar > tropasDefensorasMax || tropasDefensorasAUsar > territorioDefensor.cantidadTropas)
        {
            Debug.LogError($"Cantidad de tropas defensoras inválida: {tropasDefensorasAUsar}. Debe ser entre 1 y {tropasDefensorasMax}, y no mayor que tropas defensoras.");
            return;
        }

        // Llamar a un método que acepte ambos valores (debes modificar Partida para aceptar tropas defensoras)
        Partida.instance.Atacar(territorioAtacanteOrigen, territorioDefensor, tropasAUsar, tropasDefensorasAUsar);

        gameObject.SetActive(false);
    }

    private void OnCancelButtonClicked()
    {
        ActivarTerritorios();
        gameObject.SetActive(false);
        Partida.instance.territorioSeleccionadoParaAtaqueOrigen = null;
        Partida.instance.territorioSeleccionadoParaAtaqueDestino = null;
    }

    public void Empate()
    {
        empate.SetActive(true);
    }
    private void DesactivarTerritorios()
    {
        if (Mapa.instance == null)
        {
            Debug.LogWarning("Mapa.instance es null en DesactivarTerritorios().");
            return;
        }
        for (int i = 0; i < Mapa.instance.territorioHandlers.Contar(); i++)
        {
            GameObject go = Mapa.instance.territorioHandlers.Obtener(i);
            if (go != null)
                go.SetActive(false);
            else
                Debug.LogWarning("GameObject null en territorioHandlers en DesactivarTerritorios.");

        }
        Debug.Log("Territorios desactivados al mostrar AttackPanel.");
    }
    // Nuevo método para activar todos los territorios en el mapa
    private void ActivarTerritorios()
    {
        if (Mapa.instance == null)
        {
            Debug.LogWarning("Mapa.instance es null en ActivarTerritorios().");
            return;
        }
        for (int i = 0; i < Mapa.instance.territorioHandlers.Contar(); i++)
        {
            GameObject go = Mapa.instance.territorioHandlers.Obtener(i);
            if (go != null)
                go.SetActive(true);
            else
                Debug.LogWarning("GameObject null en territorioHandlers en ActivarTerritorios.");
        }
        Debug.Log("Territorios activados al ocultar AttackPanel.");
    }
    public void ClosePanel()
    {
        gameObject.SetActive(false);
        ActivarTerritorios();
    }
}
