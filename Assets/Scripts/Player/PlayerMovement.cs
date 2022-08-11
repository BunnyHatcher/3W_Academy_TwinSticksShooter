using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    private Vector3 _movementInput;
    private Vector3 _orientationInput;
    private Vector3 _currentOrientation;
    [SerializeField] private float _turningSpeed = 0.75f;
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
        // to solve issue where current rotation is a zero vector, which cannot be transformed into a quaternion
        _currentOrientation = new Vector3(1.0f, 0.0f, 0.0f);

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

        //use Lerp to interpolate turning movement between two values and so make it smoother
        _currentOrientation = Vector3.Lerp(_currentOrientation, _orientationInput, _turningSpeed * Time.fixedDeltaTime);

        //Methods for turning
        //Transforming Vector3 into Quaternion
        Quaternion lookRotation = Quaternion.LookRotation(_currentOrientation);
        //Make character turn physically
        _rigidbody.MoveRotation(lookRotation);
        
       // Debug.Log("x :" + orientationHorizontal + "z :" + orientationVertical + "Magnitude: " + _orientationInput.sqrMagnitude );






    }
}
