
using TMPro;
using UnityEngine;

public class GameUI : MonoBehaviour
{
    public static GameUI Instance { get; set; }

    public Server server;
    public Client client;
    

    [SerializeField] private Animator menuAnimator;
    [SerializeField] private TMP_InputField addressInput;

    private void Awake()
    {
        Instance = this;
    }

    //Buttons
    public void OnlineGameButton()
    {
        menuAnimator.SetTrigger("OnlineMenu");
    }

    public void OnlineHostButton()
    {
        server.Init(9000);
        client.Init("127.0.0.1", 9000);
        menuAnimator.SetTrigger("HostMenu");
    }

    public void OnlineConnectButton()
    {
        client.Init(addressInput.text, 9000);
    }

    public void OnlineBackButton()
    {
        menuAnimator.SetTrigger("GameUI");
    }

     public void HostBackButton()
    {
        server.Shutdown();
        client.Shutdown();
        menuAnimator.SetTrigger("OnlineMenu");
    }

    
}