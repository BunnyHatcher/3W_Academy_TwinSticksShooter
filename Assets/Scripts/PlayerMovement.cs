using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    private Vector3 _movementInput;
    private Vector3 _orientationInput;
    Rigidbody _rigidbody;

    [SerializeField] private float _speed;
    [SerializeField] private float horizontal;
    [SerializeField] private float vertical;

    [SerializeField] private float orientationHorizontal;
    [SerializeField] private float orientationVertical;


    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }



    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        //Input for movement with Left Twinstick
        horizontal = Input.GetAxisRaw("Horizontal_Move"); 
        vertical = Input.GetAxisRaw("Vertical_Move");

        _movementInput = new Vector3 (horizontal, 0, vertical);
        _movementInput.Normalize();
       // Debug.Log(_movementInput);
        
        //Input for turning with right Twinstick
        orientationHorizontal = Input.GetAxis("Horizontal_Turn");
        orientationVertical = Input.GetAxis("Vertical_Turn");

        _orientationInput = new Vector3(orientationHorizontal, 0, orientationVertical);
       // Debug.Log(_orientationInput);

    }

    private void FixedUpdate()
    {
        //Methods for movement
        Vector3 velocity = _movementInput * _speed;
        _rigidbody.velocity = velocity;

        //Methods for turning        
        Quaternion lookRotation = Quaternion.LookRotation(_orientationInput);
        _rigidbody.MoveRotation(lookRotation);
        
        Debug.Log("x :" + orientationHorizontal + "z :" + orientationVertical + "Magnitude: " + _orientationInput.sqrMagnitude );






    }
}
