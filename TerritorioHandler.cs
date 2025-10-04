using TMPro;  // Necesario para TextMeshPro
using UnityEngine;
using UnityEngine.UI;        // Necesario para Image
using UnityEngine.EventSystems;  // Necesario para IPointerHandlers

[RequireComponent(typeof(Image))]
public class TerritorioHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Territorio territorio; // Referencia al objeto Territorio
    
    // Referencia al componente TextMeshProUGUI para mostrar tropas (debe asignarse desde el Inspector)
    public TextMeshProUGUI tropasText;

    private Image image;
    private Color oldColor;

    void Awake()
    {
        gameObject.tag = "Territorio";
        Debug.Log("Tag 'Territorio' asignado a " + gameObject.name + " en runtime.");

        image = GetComponent<Image>();
        if (image == null)
        {
            Debug.LogError("¡Image faltante en " + gameObject.name + "! Agrega un componente Image para ver colores.");
            return;
        }
        Debug.Log("Image inicializado para " + gameObject.name + ". Color inicial: " + image.color);

        if (territorio == null)
        {
            string safeName = string.IsNullOrEmpty(name) ? "Territorio_Default" : name;
            territorio = new Territorio(safeName);
            Debug.Log("Territorio creado en handler para " + safeName);
        }
    }

    void Start()
    {
        ActualizarTropasVisual();
    }

    // Métodos para interacciones UI (hover y click)
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (image == null) return;
        oldColor = image.color;

        if (territorio.jugadorPropietario != null && territorio.jugadorPropietario == Partida.instance.jugadorEnTurno)
        {
            // Si es del jugador actual, no cambiar color (mantener hoverColor como oldColor)
            image.color = oldColor;
        }
        else
        {
            // Para otros, aplicar transparencia (alpha = 189/255 ≈ 0.74)
            Color hoverColor = new Color(oldColor.r, oldColor.g, oldColor.b, 189f / 255f);
            image.color = hoverColor;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (image == null) return;
        image.color = oldColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Partida.instance != null)
        {
            Partida.instance.SeleccionarTerritorio(this.territorio);
        }
        else
        {
            Debug.LogError("Partida.instance es NULL. No se puede seleccionar el territorio.");
        }
    }

    public void TintColor(Color32 color)
    {
        if (image == null)
        {
            Debug.LogError("Image null en TintColor para " + gameObject.name + ". No se puede aplicar color: " + color);
            return;
        }
        image.color = (Color)color;
        Debug.Log("Color aplicado a " + gameObject.name + ": R=" + color.r + " G=" + color.g + " B=" + color.b + " A=" + color.a);
    }

    public void TintColor(Color color)
    {
        if (image == null)
        {
            Debug.LogError("Image null en TintColor para " + gameObject.name + ". No se puede aplicar color: " + color);
            return;
        }
        image.color = color;
        Debug.Log("Color aplicado a " + gameObject.name + ": R=" + color.r + " G=" + color.g + " B=" + color.b + " A=" + color.a);
    }

    /// <summary>
    /// Actualiza el texto en UI con la cantidad de tropas actuales del territorio.
    /// Debe llamarse cada vez que cambie la cantidad de tropas.
    /// </summary>
    public void ActualizarTropasVisual()
    {
        if (territorio == null)
        {
            Debug.LogWarning("Territorio es null en ActualizarTropasVisual de " + gameObject.name);
            return;
        }

        if (tropasText == null)
        {
            Debug.LogWarning("tropasText no asignado en " + gameObject.name);
            return;
        }

        tropasText.text = territorio.cantidadTropas.ToString();
    }
}
