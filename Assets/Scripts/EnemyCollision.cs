using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyCollision : MonoBehaviour
   

{
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
       /* if (collision.gameObject.CompareTag("Player"))
        {
            Destroy(collision.gameObject);
        } */

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
