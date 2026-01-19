using UnityEngine;

public class GameManager : MonoBehaviour
{

    public float money,maxMoney=100;
    
    [SerializeField]
    public  MoneyBar moneyBar;
    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        moneyBar.maxMoney = maxMoney;
        moneyBar.setMoney(100);



    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
