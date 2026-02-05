using UnityEngine;

public class GameManager : MonoBehaviour
{

     public float maxMoney;
    
    [SerializeField]
    public  MoneyBar moneyBar;
    public float money;
    
    
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
