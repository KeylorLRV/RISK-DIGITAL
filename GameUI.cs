
using TMPro;
using UnityEngine;

public class GameUI : MonoBehaviour
{
    public static GameUI Instance { get; set; }

    public Server server;
    public Client client;
    public GameObject board;
    
    

    [SerializeField] public Animator menuAnimator;
    [SerializeField] private TMP_InputField addressInput;
    [SerializeField] private TMP_InputField playerNameInput;  

    private void Awake()
    {
        Instance = this;
        board.gameObject.SetActive(false);

    }

    //Buttons
    public void OnlineGameButton()
    {
        menuAnimator.SetBool("HostMenu", false);
        menuAnimator.SetBool("GameUI", false);
        menuAnimator.SetBool("OnlineMenu", true);
    }

    public void OnlineHostButton()
    {
        // Setear el HostMenu visible (sin inicializar cliente local).
        menuAnimator.SetBool("GameUI", false);
        menuAnimator.SetBool("OnlineMenu", false);
        menuAnimator.SetBool("HostMenu", true);
        if (Server.Instance != null && Server.Instance.isActive)
        {
            Debug.Log("[GameUI] Servidor activo detectado. Forzando shutdown antes de nuevo host.");
            Server.Instance.Shutdown();
        }
        // Solo inicializar el SERVIDOR (no el cliente local).
        if (server != null)
        {
            server.Init(9016);
            Debug.Log("Servidor iniciado. Esperando conexiones en localhost:9016. Muestra HostMenu.");
        }
        else
        {
            Debug.LogError("Server no asignado en GameUI.");
        }
        string hostAlias = string.IsNullOrEmpty(playerNameInput?.text) ? "HostPlayer" : playerNameInput.text;
        // NUEVO: Opcional - Mostrar un mensaje en HostMenu como "Esperando jugador 2...".
        // Puedes agregar un TextMeshPro en el HostMenu para esto.
    }
    public void OnlineConnectButton()
    {
        // Solo para clientes reales (jugador 2).
        string ip = string.IsNullOrEmpty(addressInput.text) ? "127.0.0.1" : addressInput.text;
        if (client != null)
        {
            client.Init(ip, 9016);
            Debug.Log($"Cliente iniciando conexión a {ip}:9016");
        }
        else
      {
            Debug.LogError("Client no asignado en GameUI.");
        }
    }

 public void OnlineBackButton()
    {
        // Desactivar menús online y volver al principal.
        menuAnimator.SetBool("OnlineMenu", false);
        menuAnimator.SetBool("HostMenu", false);
        menuAnimator.SetBool("GameUI", true);

        // Si es host, shutdown del servidor.
        if (server != null)
        {
            server.Shutdown();
        }
    }

    public void HostBackButton()
    {
        // Shutdown del servidor y cliente (por si acaso).
        if (server != null) server.Shutdown();
        if (client != null) client.Shutdown();

        // Resetear UI a OnlineMenu.
        menuAnimator.SetBool("HostMenu", false);
        menuAnimator.SetBool("GameUI", false);
        menuAnimator.SetBool("OnlineMenu", true);

        // NUEVO: Ocultar board si está visible.
        if (board != null) board.SetActive(false);
    }

    // NUEVO: Método público para que el Server lo llame cuando inicie la partida.
    public void ActivateGameUI()
    {
        // Transita a la UI del juego (board visible).
        menuAnimator.SetBool("HostMenu", false);  // Oculta HostMenu.
        menuAnimator.SetBool("OnlineMenu", false);
        menuAnimator.SetBool("GameUI", true);     // Activa GameUI si lo tienes.
        
        // Activar board y trigger/bool para animación.
        if (board != null)
        {
            board.SetActive(true);
        }
        if (menuAnimator != null)
        {
            // Usa bool como sugerí antes, o trigger si prefieres.
            menuAnimator.SetTrigger("GUIII");  // O SetBool("BoardVisible", true);
        }

        Debug.Log("UI de juego activada (board visible).");
    }


    
}