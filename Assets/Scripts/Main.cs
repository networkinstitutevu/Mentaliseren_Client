using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MLAPI;
using MLAPI.Messaging;

public class Main : NetworkBehaviour
{
    void Start()
    {
        print("Starting client");
        NetworkManager.Singleton.StartClient();
    }

    int step = 0;
    void Update()
    {
        switch(step)
        {
            case 0:
                break;
            case 10:
                break;
            case 20:
                print("Sending back");
                MessageServerRpc("Client at 20");
                BackClientRpc(99, "BOO");
                step = 30;
                break;
            case 30:
                break;
            default:
                break;
        }
    }

    [ClientRpc]
    void PongClientRpc(int somenumber, string sometext)
    {
        step += 10;
        if (step > 20) { step = 0; }
        print("Step changed to " + step);
    }
    [ServerRpc]
    void MessageServerRpc(string theMsg)
    {
        print("sending...");
    }
    [ClientRpc]
    void BackClientRpc(int somenumber, string sometext)
    {
        print("sending..." + somenumber + "-" + sometext);
    }

}
