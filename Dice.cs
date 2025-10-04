using UnityEngine;
using UnityEngine.UI;

public class Dice : MonoBehaviour
{
    public Image diceImage; 
    public Sprite[] diceSides; 

    public float animationDuration = 1f;  
    public float spriteChangeInterval = 0.1f; 

    private bool isRolling = false;
    private float animationTimeElapsed = 0f;
    private float changeTimer = 0f;

    public int finalValue = 0;  // Resultado final del dado

    public delegate void DiceRolled(int result);
    public event DiceRolled OnDiceRolled;

    public void RollDice()
    {
        if (!isRolling)
        {
            isRolling = true;
            animationTimeElapsed = 0f;
            changeTimer = 0f;
        }
    }

    private void Update()
    {
        if (isRolling)
        {
            animationTimeElapsed += Time.deltaTime;
            changeTimer += Time.deltaTime;

            if (changeTimer >= spriteChangeInterval)
            {
                changeTimer = 0f;
                int randomSide = Random.Range(0, diceSides.Length);
                diceImage.sprite = diceSides[randomSide];
            }

            if (animationTimeElapsed >= animationDuration)
            {
                isRolling = false;

                int finalSide = Random.Range(0, diceSides.Length);
                diceImage.sprite = diceSides[finalSide];
                finalValue = finalSide + 1;

                OnDiceRolled?.Invoke(finalValue);
                Debug.Log("Dado rodado: " + finalValue);
            }
        }
    }
}
