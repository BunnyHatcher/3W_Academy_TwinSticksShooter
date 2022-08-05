using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyCollision : MonoBehaviour
   

{
    [SerializeField] IntVariable _playerCurrentHP;
    [SerializeField] private int _enemyHP = 2;
    [SerializeField] public PlayerHealth playerHP;



        
    
    // Start is called before the first frame update
    void Start()
    {
      playerHP =  GameObject.Find("Player").GetComponent<PlayerHealth>();  
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
       if (other.gameObject.CompareTag("Player"))
        {
            _playerCurrentHP.Value --;
            Debug.Log("Hit");
            if (_playerCurrentHP.Value <= 0)
            {
                
                
                playerHP.GameOver();
            }
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
