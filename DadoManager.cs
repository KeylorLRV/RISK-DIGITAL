using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DadoManager : MonoBehaviour
{
    
    public Button lanzarButton;

    public Dice dice;

    public void LanzarDado()
    {
        dice.RollDice();
    }
}