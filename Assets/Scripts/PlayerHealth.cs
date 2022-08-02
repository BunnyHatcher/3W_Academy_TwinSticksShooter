using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] IntVariable _playerStartHP;
    [SerializeField] IntVariable _playerCurrentHP;

    private void Awake()
    {
        _playerCurrentHP = _playerStartHP;
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
