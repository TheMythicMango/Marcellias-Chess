using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameUI : MonoBehaviour
{
    public static GameUI instance { set; get; }

    [SerializeField] private Animator menuAnimator;

    private void Awake()
    {
        instance = this;
    }

    // Butons
    public void OnLocalGame()
    {
        menuAnimator.SetTrigger("InGameManu");
    }
    public void OnOnlineGame()
    {
        menuAnimator.SetTrigger("Online Manu");
    }
    public void onOnlineHost()
    {
        menuAnimator.SetTrigger("Host Menu");
    }
    public void OnOnlineConnect() // 
    {

    }
    public void OnOnlineBack()
    {
        menuAnimator.SetTrigger("Start Menu");
    }
    public void OnHostBack()
    {
        menuAnimator.SetTrigger("Online Manu");
    }
}
