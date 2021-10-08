using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    private Transform _cameraTrans;
    public InputManager _InPutMgr;
    void Awake()
    {
        _cameraTrans = this.gameObject.transform;
    }
    void Update()
    {
        _cameraTrans.position += _InPutMgr.DirectionVector;
    }
}
