using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class tester : MonoBehaviour
{
    Keyboard kb;
    [SerializeField] Animator thisAnimator;
    void Start()
    {
        kb = InputSystem.GetDevice<Keyboard>(); //pre def keyboard
    }

    // Update is called once per frame
    void Update()
    {
        if(kb.lKey.wasPressedThisFrame)
        {
            thisAnimator.SetBool("GoAnim", true);
            GetComponent<AudioSource>().Play();
        }
        if (thisAnimator.GetCurrentAnimatorStateInfo(0).IsName("17f-1a"))
        {
            if (thisAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1.0f)
            {
                thisAnimator.SetBool("GoAnim", false);
            }
        }
    }
}
