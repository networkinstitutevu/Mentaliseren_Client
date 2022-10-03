using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CamMove : MonoBehaviour
{
    Keyboard kb;
    float turnRate = 15f;
    Quaternion targetRotation = Quaternion.identity;
    int currentAngle = 1;
    bool startRotation = false;
    [SerializeField] float firstAngle = 330f;
    [SerializeField] float secondAngle = 30f;
    bool startTestSecond = false;
    [SerializeField] float defaultAngle = 0f;
    void Start()
    {
        kb = InputSystem.GetDevice<Keyboard>();
        targetRotation = Quaternion.Euler(new Vector3(0f, firstAngle, 0f));
    }

    void Update()
    {
        if(kb.cKey.wasPressedThisFrame && !startRotation)
        {
            startRotation = true;
        }
        if(startRotation)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnRate * Time.deltaTime);
            //print("--" + transform.rotation.eulerAngles.y);
            if(currentAngle == 1 && transform.rotation.eulerAngles.y < (firstAngle +1f))
            {
                //print("*** Going secondAngle");
                currentAngle++; //2
                targetRotation = Quaternion.Euler(new Vector3(0f, secondAngle, 0f));
            }
            if(currentAngle == 2 && transform.rotation.eulerAngles.y < 50f) { startTestSecond = true; }
            if(currentAngle == 2 && startTestSecond && transform.rotation.eulerAngles.y > (secondAngle - 1f))
            {
                //print("*** Going defaultAngle");
                currentAngle++; //3
                targetRotation = Quaternion.Euler(new Vector3(0f, defaultAngle, 0f));
            }
            if(currentAngle == 3 && transform.rotation.eulerAngles.y < (defaultAngle +1f) && transform.rotation.eulerAngles.y > (defaultAngle -1f))
            {
                startRotation = false;
                currentAngle = 1;
                targetRotation = Quaternion.Euler(new Vector3(0f, firstAngle, 0f));
                //print("*** Rotations done");
            }
        }
    }
}
