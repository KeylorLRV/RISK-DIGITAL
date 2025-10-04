using TMPro;  // Necesario para TextMeshPro
using UnityEngine;

[RequireComponent(typeof(PolygonCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class TerritorioHandler : MonoBehaviour
{
    public Territorio territorio; // Referencia al objeto Territorio
    
    // Referencia al componente TextMeshProUGUI para mostrar tropas (debe asignarse desde el Inspector)
    public TextMeshProUGUI tropasText;

    private SpriteRenderer sprite;
    private Color32 hoverColor;
    private Color32 oldColor;

    void Awake()
    {
        gameObject.tag = "Territorio";
        Debug.Log("Tag 'Territorio' asignado a " + gameObject.name + " en runtime.");

        sprite = GetComponent<SpriteRenderer>();
        if (sprite == null)
        {
            Debug.LogError("¡SpriteRenderer faltante en " + gameObject.name + "! Agrega uno para ver colores.");
            return;
        }
        Debug.Log("SpriteRenderer inicializado para " + gameObject.name + ". Color inicial: " + sprite.color);

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

    void OnDrawGizmos()
    {
        if (territorio != null)
        {
            string safeName = string.IsNullOrEmpty(name) ? "Territorio_Default" : name;
            territorio.nombre = safeName;
        }
        this.tag = "Territorio";
    }

    void OnMouseEnter()
    {
        if (sprite == null) return;
        oldColor = sprite.color;

        if (territorio.jugadorPropietario != null && territorio.jugadorPropietario == Partida.instance.jugadorEnTurno)
        {
            hoverColor = oldColor;
        }
        else
        {
            hoverColor = new Color32(oldColor.r, oldColor.g, oldColor.b, 189);
        }
        sprite.color = hoverColor;
    }

    void OnMouseExit()
    {
        if (sprite == null) return;
        sprite.color = oldColor;
    }

    public void TintColor(Color32 color)
    {
        if (sprite == null)
        {
            Debug.LogError("Sprite null en TintColor para " + gameObject.name + ". No se puede aplicar color: " + color);
            return;
        }
        sprite.color = color;
        Debug.Log("Color aplicado a " + gameObject.name + ": R=" + color.r + " G=" + color.g + " B=" + color.b + " A=" + color.a);
    }

    public void TintColor(Color color)
    {
        TintColor((Color32)color);
    }

    void OnMouseUpAsButton()
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
