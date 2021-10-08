using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputManager : MonoBehaviour
{
    public float DirectionUp;
    public float DirectionRight;
    private float magnitude;
    private Vector3 _DirectionVector;
    public bool InputEnabled = false;
    private float speed=30f;
    public Vector3 DirectionVector
    {
        get
        {
            return _DirectionVector;
        }
    }
    
    void Update()
    {
        if (InputEnabled)
        {
            DirectionUp = Input.GetAxis("Vertical");
            DirectionRight = Input.GetAxis("Horizontal");
            magnitude = Mathf.Sqrt(DirectionUp * DirectionUp + DirectionRight * DirectionRight);
            _DirectionVector=Vector3.Normalize(new Vector3(DirectionRight,0,DirectionUp))*magnitude*Time.deltaTime*speed;
        }
    }
}
