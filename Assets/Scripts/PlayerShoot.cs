using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerShoot : MonoBehaviour
{
    [SerializeField] GameObject _bulletPrefab;
    [SerializeField] Transform _cannon;
    public Transform bulletGroup;

    [SerializeField] float __bulletSpeed;

    //Setting shot intervall
    [SerializeField] float _delayBetweenShots = 0.2f;
    private float _nextShotTime;





    private void FireBullet()
    {
        GameObject newBullet = Instantiate(_bulletPrefab, _cannon.position, _cannon.rotation, bulletGroup);   
        newBullet.GetComponent<Bullet>().Shoot(__bulletSpeed);
    }

    private void Awake()
    {
        _nextShotTime = Time.time;
    }


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetAxisRaw("Fire1")!=0 && Time.time >= _nextShotTime)
        //the !=0 helps turn this from an axis into a positive/negative input
        {
            FireBullet();
            _nextShotTime = _delayBetweenShots + Time.time;
        }


    }
}
