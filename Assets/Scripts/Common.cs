using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Common
{
    static int ppn = 0; //Participant number from settings.txt <MainFlow>
    static string scenario = ""; //Participant number from settings.txt <MainFlow>
    static string scenarioNumbers = ""; //Holds the sceanrios to play from the server settings.txt
    static string node = ""; //Participant number from settings.txt <MainFlow>
    static string filename = "";
    static string decision = "";
    static string currentFe = "";

    static string networkData = "";
    static bool startApplication = false;
    static bool rdyToSend = false;
    static string msgToSend = string.Empty;
    static bool connected = false;
    static bool abort = false;
    static string abortMSG = "";
    static float questionTimeOut = 3.0f;

    static bool pause = false;

    public static int Ppn { get => ppn; set => ppn = value; }
    public static string Scenario { get => scenario; set => scenario = value; }
    public static string Node { get => node; set => node = value; }
    public static string Filename { get => filename; set => filename = value; }
    public static string Decision { get => decision; set => decision = value; }
    public static string CurrentFe { get => currentFe; set => currentFe = value; }
    public static string NetworkData { get => networkData; set => networkData = value; }
    public static bool StartApplication { get => startApplication; set => startApplication = value; }
    public static bool RdyToSend { get => rdyToSend; set => rdyToSend = value; }
    public static string MsgToSend { get => msgToSend; set => msgToSend = value; }
    public static bool Connected { get => connected; set => connected = value; }
    public static bool Abort { get => abort; set => abort = value; }
    public static string AbortMSG { get => abortMSG; set => abortMSG = value; }
    public static string ScenarioNumbers { get => scenarioNumbers; set => scenarioNumbers = value; }
    public static float QuestionTimeOut { get => questionTimeOut; set => questionTimeOut = value; }
    public static bool Pause { get => pause; set => pause = value; }
}
