using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyCollision : MonoBehaviour
   

{
    [SerializeField] IntVariable _playerCurrentHP;
    [SerializeField] private int _enemyHP = 2;
    [SerializeField] public PlayerHealth playerHealthComponent;



        
    
    // Start is called before the first frame update
    void Start()
    {
      playerHealthComponent =  GameObject.Find("Player").GetComponent<PlayerHealth>();  
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
       if (other.gameObject.CompareTag("Player"))
        {
            playerHealthComponent.PlayerHit();
            
        }

        if (other.gameObject.CompareTag("Bullet"))
        {
            _enemyHP--;

            if(_enemyHP <= 0)
            {
                Destroy(gameObject);
            }
             
                
        }
    }
}
