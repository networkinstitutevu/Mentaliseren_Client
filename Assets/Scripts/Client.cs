using System;
using System.Collections;
//using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
//using UnityEngine.XR;
//using UnityEngine.InputSystem;
//using Unity.Collections;
public class Client : MonoBehaviour
{
    string serverReply = "";
    bool waitingForServer = false;
    float pollingInterval = 2.0f;
    DateTime pollingPauseStart;
    [SerializeField] int clientNumber = 1;

    DateTime inactiveStart; //Start time of inactivity
    //float timeOut = 10.0f; //Seconds after <inactiveStart> after which the app will quit
    float waitTimeForButton = 10f; //Seconds to wait after READY for the server to send a button press. After that a problem is assumed
    bool waitForButton = false; //If true we are waiting for a server button. START/POS/NEG/GEN[1-6]
    void Start()
    {
        //XRSettings.eyeTextureResolutionScale = 1.5f;
        PrepServerResponse();
        StartCoroutine(SendToServer(clientNumber + ",CMD,INIT")); //Check if server if ready
        inactiveStart = DateTime.Now;
    }
    private void Update()
    {
        if (!Common.Abort)
        {
            if (waitingForServer && serverReply != "") //Are we waiting for a PHP reply AND there is a reply?
            {
                //print("processing reply");
                waitingForServer = false; //Stop waiting
                ProcessNetworkMessage(serverReply); //Process the message from the PHP script
                serverReply = ""; //Reset the PHP messasge
            }
            if (!waitingForServer && ((DateTime.Now - pollingPauseStart).TotalSeconds > pollingInterval)) //If we're not waiting AND enough time has elapsed
            {
                PrepServerResponse(); //Reset PHP message and start waiting
                StartCoroutine(SendToServer(clientNumber + ",CMD,POLL")); //Poll the PHP for server messsages
            }
            if (!waitingForServer && Common.RdyToSend) //Main script indicates something needs to be send to the server
            {
                Common.RdyToSend = false; //Reset flag
                PrepServerResponse(); //Reset PHP messsage and start waiting for PHP reply
                StartCoroutine(SendToServer(clientNumber + "," + Common.MsgToSend)); //Send to PHP (and server)
            }
            if (waitForButton && (DateTime.Now - inactiveStart).TotalSeconds > waitTimeForButton) //Too long without server button => ABORT
            {
                //print("button time out");
                Common.MsgToSend = "CMD,ABORT"; 
                PrepServerResponse(); 
                StartCoroutine(SendToServer(clientNumber + "," + Common.MsgToSend)); //Send to PHP (and server)
                Common.AbortMSG = "Geen reactie van server. Einde sessie. Zet de VR bril af.";
                Common.Abort = true;
            }
        }
    }
    void PrepServerResponse()
    {
        waitingForServer = true; //Start waiting for web server response
        serverReply = "";
    }
    void ProcessNetworkMessage(string msg)
    {
        DateTime storeTimer = inactiveStart;
        inactiveStart = DateTime.Now;

        //Here the reply from the PHP client.php script is processed
        //Possible replies:
        //CMD,INIT,OK/ERROR - Init command processed OK or ERROR = QUIT
        //CMD,READY,OK/ERROR - Ready message to server processed OK/ERROR
        //CMD,RPS,OK/ERROR - Waiting for ResPonSe message to server processed OK/ERROR
        //CMD,NEW,OK/ERROR - New Scenario message to server processed OK/ERROR
        //CMD,END,OK/ERROR - End session message to server processed OK/ERROR
        //CMD,POLL,ERROR/EMPTY/<line> - Poll returned ERROR (=quit), EMPTY or a response from the server application

        string[] elements = msg.Split(','); //Split into <command> and <value>. [2] to [4] elements
        //[0] = CMD :CMD,INIT,OK
        //[1] = INIT,READY,RPS,NEW,END
        //[2] = OK,ERROR
        //[3] = INIT,OK,PollingInterval
        //[4] = INIT,OK,PollingInterval,ButtonTimeOut
        //[1] = POLL :CMD,POLL,CMD,GEN,3 / CMD,POLL,CMD,START
        //[2] = ERROR,EMPTY,CMD
        //[3] = START,POS,NEG,GEN
        //[4] = [1-6]
        //[0] = TXT
        //[1] = VO,     SC,     STP,    LARS
        //[2] = ERROR,OK

/*        string toshow = "";
        foreach(string ts in elements)
        { toshow += ts + "--"; }
        print("***" + toshow);*/

        switch (elements[0])
        {
            case "CMD":
                switch (elements[1])
                {
                    case "INIT": //INIT server to PHP to check for active server
                        if (elements[2] == "ERROR")
                        {
                            print("ERROR at INIT - no server");
                            AbortProgram();
                        }
                        else
                        {
                            print(msg);
                            pollingInterval = (float)Convert.ToDouble(elements[3]);
                            waitTimeForButton = Convert.ToInt32(elements[4]);
                            Common.QuestionTimeOut = (float)Convert.ToDouble(elements[5]);
                            Common.ScenarioNumbers = elements[6];
                            Common.Connected = true;
                            print("Server is ready. Client will send READY. Polling interval: " + pollingInterval + ". Button timeout: " + waitTimeForButton + ". Scenarios: " + Common.ScenarioNumbers);
                            Common.MsgToSend = "CMD,READY";
                            Common.RdyToSend = true;
                            pollingPauseStart = DateTime.Now; //Reset poll timer
                        }
                        break;
                    case "PAUSE":
                        if(elements[2] == "ERROR")
                        {
                            print("ERROR in PAUSE command");
                            AbortProgram();
                        }
                        //Otherwise do nothing
                        break;
                    case "READY": //READY sent to server
                        if (elements[2] == "ERROR")
                        {
                            print("ERROR at READY");
                            AbortProgram();
                        }
                        else
                        {
                            print("Client is ready. Waiting for server START");
                            pollingPauseStart = DateTime.Now; //Reset poll timer
                            waitForButton = true;
                        }
                        break;
                    case "POLL": //Server response after POLLing - CMD,START/POS/NEG/GEN,[1-6] or TXT,VO/SC/STP/LARS,<value>
                        if (elements[2] == "ERROR")
                        {
                            print("ERROR at POLL");
                            AbortProgram();
                            break;
                        }
                        if (elements[2] == "EMPTY") //No reaction from server, do nothing
                        {
                            print("Poll returned EMPTY");
                            pollingPauseStart = DateTime.Now; //Reset poll timer
                            if (waitForButton)
                            {
                                inactiveStart = storeTimer;
                            }
                            break;
                        }
                        switch (elements[2]) //CMD orTXT
                        {
                            case "CMD":
                                waitForButton = false;
                                switch (elements[3])
                                {
                                    case "START": //Server sends START
                                        Common.StartApplication = true;
                                        break;
                                    case "ENDPAUSE":
                                        Common.Pause = false;
                                        break;
                                    case "POS":
                                        Common.NetworkData = "positive";
                                        break;
                                    case "NEG":
                                        Common.NetworkData = "negative";
                                        break;
                                    case "GEN":
                                        Common.NetworkData = "generic" + elements[4]; //sets generic#
                                        break;
                                }
                                break;
                        }
                        break;
                    case "RPS":
                        if (elements[2] == "ERROR")
                        {
                            print("ERROR at sending RPS");
                            AbortProgram();
                        }
                        else
                        {
                            print("Client sent Waiting for Response to server");
                            pollingPauseStart = DateTime.Now; //Reset poll timer
                            waitForButton = true; //Waiting for panel button from server
                        }
                        break;
                    case "NEW":
                        if (elements[2] == "ERROR")
                        {
                            print("ERROR at sending NEW");
                            AbortProgram();
                        }
                        else
                        {
                            print("Client sent NEw Scenario to server");
                            pollingPauseStart = DateTime.Now; //Reset poll timer
                        }
                        break;
                    case "END":
                        if (elements[2] == "ERROR")
                        {
                            print("ERROR at sending END");
                            AbortProgram();
                        }
                        else
                        {
                            print("Client sent End Sesison to server");
                            pollingPauseStart = DateTime.Now; //Reset poll timer
                        }
                        break;
                    case "ERROR":
                        print("ERROR - server command not found");
                        AbortProgram();
                        break;
                }
                break;
            case "TXT":
                switch (elements[1])
                {
                    case "VO":
                        if (elements[2] == "ERROR")
                        {
                            print("ERROR at sending TXT");
                            AbortProgram();
                        }
                        else
                        {
                            //print("^^^^^Client sent voiceover text to server");
                            pollingPauseStart = DateTime.Now; //Reset poll timer
                        }
                        break;
                    case "SC":
                        if (elements[2] == "ERROR")
                        {
                            print("ERROR at sending SC theme");
                            AbortProgram();
                        }
                        else
                        {
                            //print("Client sent scenario theme to server");
                            pollingPauseStart = DateTime.Now; //Reset poll timer
                        }
                        break;
                    case "STP":
                        if (elements[2] == "ERROR")
                        {
                            print("ERROR at sending STP");
                            AbortProgram();
                        }
                        else
                        {
                            //print("Client sent step number to server");
                            pollingPauseStart = DateTime.Now; //Reset poll timer
                        }
                        break;
                    case "LARS":
                        if (elements[2] == "ERROR")
                        {
                            print("ERROR at sending LARS");
                            AbortProgram();
                        }
                        else
                        {
                            //print("Client sent Lars text to server");
                            pollingPauseStart = DateTime.Now; //Reset poll timer
                        }
                        break;
                }
                break;
        }
    }
    IEnumerator SendToServer(string theCommand)
    {
        print("Sending: " + theCommand);
        string dataPush = "https://www.techlabs.nl/experiments/mentaliserenv2/client.php?cmd=" + theCommand;
        UnityWebRequest www = UnityWebRequest.Get(dataPush);// 
        www.timeout = 30; //Set generic 30s timeout for connection to the server
        yield return www.SendWebRequest();
        if (www.result == UnityWebRequest.Result.Success) //Not succes when timed out
        {
            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.Log(www.error);

            }
            else
            {
                serverReply = www.downloadHandler.text;
                //print("RAW: " + serverReply);
            }
        }
        else
        {
            Common.Abort = true; //Stop all
        }
    }
    void AbortProgram()
    {
        Common.Abort = true;
/*#if UNITY_EDITOR
        // Application.Quit() does not work in the editor so
        // UnityEditor.EditorApplication.isPlaying need to be set to false to end the game
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif*/
    }

    private void OnApplicationQuit()
    {
        Common.MsgToSend = "CMD,ABORT";
        PrepServerResponse();
        StartCoroutine(SendToServer(clientNumber + "," + Common.MsgToSend)); //Send to PHP (and server)
    }
}
