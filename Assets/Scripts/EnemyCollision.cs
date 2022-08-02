using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyCollision : MonoBehaviour
   

{
    [SerializeField] IntVariable _playerCurrentHP;
    [SerializeField] private int _enemyHP = 2;

        
    
    // Start is called before the first frame update
    void Start()
    {
        
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

            if (_playerCurrentHP.Value <= 0)
            {
                Destroy(gameObject);
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
