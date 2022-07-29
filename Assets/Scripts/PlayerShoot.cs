using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerShoot : MonoBehaviour
{
    [SerializeField] GameObject _bulletPrefab;
    [SerializeField] Transform _cannon;
    public Transform bulletGroup;

    private void FireBullet()
    {
        GameObject newBullet = Instantiate(_bulletPrefab, _cannon.position, _cannon.rotation, bulletGroup);   
    }
    
    


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetButton("Fire1"))
        {
            FireBullet();
        }
    }
}
