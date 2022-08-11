using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealingBehaviour : MonoBehaviour
{

    protected PlayerHealth playerHealthComponent;

    void Start()
    {
        //playerHealthComponent = GameObject.Find("Player").GetComponent<PlayerHealth>();
    }


    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Player")
        {
            //playerHealthComponent.PlayerHealed();
            other.gameObject.GetComponent<PlayerHealth>().PlayerHealed();
            gameObject.SetActive(false);
        }

        
    }
}
