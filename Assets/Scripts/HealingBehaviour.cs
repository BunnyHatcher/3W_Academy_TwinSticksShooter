using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealingBehaviour : MonoBehaviour
{

    [SerializeField] IntVariable _playerCurrentHP;
    

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Player")
        {

            _playerCurrentHP.Value++;            
            

            Debug.Log(_playerCurrentHP.Value);

            Destroy(gameObject);
        }

        
    }
}
