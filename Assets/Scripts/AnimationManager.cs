using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class AnimationClipOverrides : List<KeyValuePair<AnimationClip, AnimationClip>>
{
    public AnimationClipOverrides(int capacity) : base(capacity) { }

    public AnimationClip this[string name]
    {
        get { return this.Find(x => x.Key.name.Equals(name)).Value; }
        set
        {
            int index = this.FindIndex(x => x.Key.name.Equals(name));
            if (index != -1)
                this[index] = new KeyValuePair<AnimationClip, AnimationClip>(this[index].Key, value);
        }
    }
}

public class AnimationManager : MonoBehaviour
{
    //DEBUG
    //Keyboard kb;

    //General
    int step = 0;
    Vector3 cameraScenePosition = new Vector3(14.364f, 1.36144f, 7.366f);// (14.274f, 1.36144f, 7.293f); //Camera location in the room
    Vector3 cameraCanvasPosition = new Vector3(14.274f,0f,-99f);//(12.68f, 0f, -0.44f); //Camere location in the black fader box
    [SerializeField] GameObject faderBox; //The 3D box around the camera to Fade in and out of black
    [SerializeField] GameObject textCanvas; //Holds the in-between scenes text
    public float fadeTimer = 3f;
    public float fadeSpeed = 3f;
    bool fadeDone = false;
    string introTekst = string.Empty;

    //Scenario management
    [SerializeField] GameObject scenarioHolder;
    int totalScenarios = 0; //Total number of Scenarios that have been definied inside the Scenarios GameObject in the scene
    int numberScenariosToPlay = 0; //Only the total number of scenarios indicated to play by the server
    string currentScenarioName = string.Empty;
    int currentScenario = 0; //The actual Scenario number from the scene scenario objects
    int currentScenarioIndex = 0; //Used for the list, zero-based
    int currentScenarioStep = 0; //Which step in the scenario we are
    List<scenario> thisScenario = new List<scenario>();
    List<GameObject> scenarioObjects = new List<GameObject>();
    [SerializeField] GameObject potato1, potato2, canvasText;
    DateTime startTimer;
    string[] theme; //List of scenario theme names
    string[] themeIntroTxt;
    bool loadScenarios = true;
    string[] scenarioNrs;

    //VoiceOver
    AudioSource voiceOverSource;
    public static bool voiceStarted = false;

    //Animations & Audio LARS
    protected Animator animator;
    protected AnimatorOverrideController animatorOverrideController;
    protected AnimationClipOverrides clipOverrides;
    protected AudioSource larsAudioSource;
    public string neutralBodyAnim = "SitC01";
    public string emotionMotion = "SitC02";  //Name of the animation in state EmotionMotion (that you want to overwrite)
    public string emotionExpression = "SadExpression"; //Name of the animation in state EmotionExpression (that you want to overwrite)
    GameObject question;
    //float qTimer = 5.0f; //Timer in s before the question marker is displayed after Lars finished talking

    //Networking
    int genericResponse = 0; //Use if server sends a generic respons (> 0)
    bool die = false;

    private void Awake()
    {
        UnityEngine.XR.InputTracking.disablePositionalTracking = true;
        UnityEngine.XR.InputTracking.Recenter();
    }

    void Start()
    {
        Common.AbortMSG = "";

        //kb = InputSystem.GetDevice<Keyboard>();

        animator = GetComponent<Animator>(); //Get Lars animator
        animatorOverrideController = new AnimatorOverrideController(animator.runtimeAnimatorController); //Create a new animation override
        animator.runtimeAnimatorController = animatorOverrideController; //Make that override the animation controller
        clipOverrides = new AnimationClipOverrides(animatorOverrideController.overridesCount); //Create a new list of animation clip pairs
        animatorOverrideController.GetOverrides(clipOverrides); //Get the current set
        DoBaseOverides();

        larsAudioSource = GetComponent<AudioSource>();
        voiceOverSource = Camera.main.GetComponent<AudioSource>();

        question = GameObject.Find("QuestionQuad");
        question.SetActive(false);

        ////GetPPN();
        totalScenarios = scenarioHolder.transform.childCount; //Get number of children (=scenarios) in the special scenario holder
        //SetObjects(); //Get the scenarios

        //Camera needs to have 0% aplha faderBox to view intro text - default
        Camera.main.transform.position = cameraCanvasPosition; //Move camera to canvas room to show intro text and wait for network connection
        introTekst = canvasText.GetComponent<TMP_Text>().text; //Remember the intro tekst from the original entry in the Editor
        canvasText.GetComponent<TMP_Text>().text = "Een momentje geduld..."; //Set new

        //OVRManager.display.displayFrequency = 72.0f;
        //XRSettings.eyeTextureResolutionScale = 1.4f;
    }
    void Update()
    {
        if(Common.Connected && loadScenarios) //If INIT is received, the scenario list is in too. Process it
        {
            loadScenarios = false; //Do only once
            SetScenarios();
        }
        if (Common.StartApplication)
        {
            step = 3;
            Common.StartApplication = false;
        }
        if(Common.Abort && !die)
        {
            die = true;
            step = 2000; //Show end and quit
        }
        switch (step)
        {
            case 0: //Wait for connection and START button from server
                break;
            case 3: //Play intro audio and display text
                Common.MsgToSend = "TXT,VO,Introductie tekst";
                Common.RdyToSend = true;
                PlayAudio("Audio/Algemeen/intro", false); //play intro audio
                canvasText.GetComponent<TMP_Text>().text = introTekst;//Display intro text
                step = 5;
                break;
            case 5: //Wait here
                if (Common.Connected && !voiceOverSource.isPlaying)
                {
                    Common.MsgToSend = "TXT,VO,"; //Let server know the Intro VoiceOver is done
                    Common.RdyToSend = true;
                    canvasText.GetComponent<TMP_Text>().text = "Pauze..."; //step 8 was here!
                    step = 6;
                }
                break;
            case 6:
                Common.Pause = true;
                Common.MsgToSend = "CMD,PAUSE";
                Common.RdyToSend = true;
                step = 7;
                break;
            case 7: //Wait for server to send ENDPAUSE
                if(!Common.Pause)
                {
                    step = 8;
                }
                break;
            case 8:
                canvasText.GetComponent<TMP_Text>().text = "Uitleg situatie...";
                step = 10;
                break;
            case 10: //Get all the info for this scenario to play audio and animations
                ReadScenario(currentScenarioIndex); //Read scenario
                canvasText.GetComponent<TMP_Text>().text = themeIntroTxt[currentScenario];//"Uitleg situatie..." + theme[currentScenario];
                step = 100;
                break;
            case 100: //Start VoiceOver
                if (!Common.RdyToSend)
                {
                    Common.MsgToSend = "TXT,VO,Scenario uitleg"; //Let server know the VoiceOver is playing
                    Common.RdyToSend = true;
                    PlayAudio("Audio/Scenario" + currentScenario + "/voiceover", false);
                    step = 200;
                }
                break;
            case 200: //Wait for VoiceOver to finish
                if(!voiceOverSource.isPlaying)
                {
                    Common.MsgToSend = "TXT,VO,"; //Let server know the VoiceOver has stopped playing
                    Common.RdyToSend = true;
                    step = 250;
                }
                break;
            case 250: //Fade to black
                print("250: Fade to black");
                StartCoroutine(FadeInOut(false)); //First fade the faderBox to black
                step = 260;
                break;
            case 260: //Wait for fade
                if(fadeDone)
                {
                    fadeDone = false;
                    step = 270;
                }
                break;
            case 270: //Move camera to scene and fade to transparant
                Camera.main.transform.position = cameraScenePosition; //Move camera into scene
                textCanvas.SetActive(false); //Text canvas off - it's camera based so always visible unless turned off!
                print("250: Fade to transparant");
                StopAllCoroutines();
                StartCoroutine(FadeInOut(true)); //Fade to transparant
                step = 280;
                break;
            case 280: //Wait for fade
                if (fadeDone)
                {
                    fadeDone = false;
                    step = 300;
                }
                break;
            case 300: //Setup next animation & audio. Let server know next step
                Common.NetworkData = ""; //reset
                question.SetActive(false); //Make sure to turn off the question marker
                Common.MsgToSend = "TXT,STP," + thisScenario[currentScenarioStep].stap; //Send current animation step to server
                Common.RdyToSend = true;
                step = 350;
                break;
            case 350: //Load & Play animation and audio Lars from thisScenario List (using scenario class)
                if (!Common.RdyToSend)
                {
                    PlayAnimation(); //Load and start animation and audio for Lars
                    CheckSpecials(); //Check to see if there is anything special for this scenario - step
                    step = 400;
                }
                break;
            case 400: //Wait for audio (and animation) to finish
                if (!larsAudioSource.isPlaying)// && animator.GetCurrentAnimatorStateInfo(0).normalizedTime != 0 && !animator.GetAnimatorTransitionInfo(0).IsUserName("transitionRunning"))
                {
                    //CHANGE
                    //Remove next IF to show question mark after audio only!
                    if (animator.GetCurrentAnimatorStateInfo(0).IsName("NeutralMotion"))
                    {
                        Common.MsgToSend = "TXT,LARS,";
                        Common.RdyToSend = true;
                        step = 450; //Do next step in scenario
                    }
                }
                break;
            case 450:
                if (thisScenario[currentScenarioStep].negativeOption == "END") //This was the last step => go to next scenario
                {
                    if(currentScenario == 5) //Do extra pause at the end!
                    {
                        startTimer = DateTime.Now; //Get current time for Question marker
                        step = 830;
                    }
                    else
                    {
                        step = 850;
                    }
                    break;
                }
                Common.MsgToSend = "CMD,RPS"; //Let server know to show the reaction buttons
                Common.RdyToSend = true;
                startTimer = DateTime.Now; //Get current time for Question marker
                step = 500;
                break;
            case 500: //Wait for server response
                if((DateTime.Now - startTimer).TotalSeconds > Common.QuestionTimeOut) //Waited too long, display question marker
                {
                    question.SetActive(true);
                }
                if (Common.NetworkData == "negative") //negative
                {
                    print("Received <negative> from server");
                    //find the <negative> step in the steps of the current scenario
                    int stepIndex = -1;
                    foreach (scenario nextStep in thisScenario)
                    {
                        stepIndex++;
                        if (thisScenario[currentScenarioStep].negativeOption == nextStep.stap)
                        {
                            currentScenarioStep = stepIndex;
                            break; //Out of ForEach
                        }
                    }
                    step = 300; //Do the next step
                    break;
                }
                if (Common.NetworkData == "positive") //positive
                {
                    print("Received <positive> from server");
                    //find the <positive> step in the steps of the current scenario
                    int stepIndex = -1;
                    foreach (scenario nextStep in thisScenario)
                    {
                        stepIndex++;
                        if (thisScenario[currentScenarioStep].positiveOption == nextStep.stap)
                        {
                            currentScenarioStep = stepIndex;
                            break; //Out of ForEach
                        }
                    }
                    step = 300; //Do the next step
                    break;
                }
                if (Common.NetworkData.IndexOf("generic") != -1) //genericN or <algemeen> => equals positive step
                {
                    print("Received <generic> from server");
                    //find the <positive> step in the steps of the current scenario
                    int stepIndex = -1;
                    foreach (scenario nextStep in thisScenario)
                    {
                        stepIndex++;
                        if (thisScenario[currentScenarioStep].positiveOption == nextStep.stap)
                        {
                            currentScenarioStep = stepIndex;
                            genericResponse = Convert.ToInt32(Common.NetworkData.Substring(7));//1; //number to be set by server
                            print("Generic response number " + genericResponse);
                            break; //Out of ForEach
                        }
                    }
                    step = 300; //Do the next step
                    break;
                }
                break;
            case 830: //Special catch for scenario 5 where we need a pauzed time before ending!
                if ((DateTime.Now - startTimer).TotalSeconds > 5f) //Waited too long, display question marker
                {
                    step = 850;
                }
                break;
            case 850: //Fade out
                StartCoroutine(FadeInOut(false)); //Fade to black
                canvasText.GetComponent<TMP_Text>().text = "";
                textCanvas.SetActive(true); //Text canvas off - it's camera based so always visible unless turned off!
                step = 860;
                break;
            case 860: //Wait for fade
                if (fadeDone)
                {
                    fadeDone = false;
                    step = 900;
                }
                break;
            case 900: //Get next scenario ready and run
                Camera.main.transform.position = cameraCanvasPosition; //Move to canvas box, leave faderBox transparant
                scenarioObjects[currentScenarioIndex].SetActive(false); //Turn scenario objects off
                currentScenarioIndex++;
                if (currentScenarioIndex == numberScenariosToPlay)
                {
                    step = 1000; //END
                    Common.MsgToSend = "CMD,END"; //Let server know this is the end of the application
                    Common.RdyToSend = true;
                    canvasText.GetComponent<TMP_Text>().text = "Einde session. Zet de VR-bril af.";
                    StartCoroutine(FadeInOut(true));
                    break;
                }
                currentScenarioStep = 0; //reset
                Common.MsgToSend = "CMD,NEW"; //Let server know this scenario has ended, ready for a new one
                Common.RdyToSend = true;
                canvasText.GetComponent<TMP_Text>().text = "Uitleg situatie...";
                StartCoroutine(FadeInOut(true));
                startTimer = DateTime.Now;
                step = 990; //Pause then Do next scenario
                break;
            case 990:
                if((DateTime.Now - startTimer).TotalSeconds > 2.0f)
                {
                    step = 10; //Start next scenario
                }
                break;
            case 1000: //END
                print(" *** DONE *** ");
#if UNITY_EDITOR
                // Application.Quit() does not work in the editor so
                // UnityEditor.EditorApplication.isPlaying need to be set to false to end the game
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                break;
                //There was an ERROR in communication, application will quit
            case 2000: //Fade to black
                print("2000: Fade to black");
                StartCoroutine(FadeInOut(false)); //First fade the faderBox to black
                step = 2100;
                break;
            case 2100: //Wait for fade
                if (fadeDone)
                {
                    fadeDone = false;
                    step = 2200;
                }
                break;
            case 2200: //Move camera to scene and fade to transparant
                Camera.main.transform.position = cameraCanvasPosition; //Move camera into canvas
                textCanvas.SetActive(true); //Text canvas off - it's camera based so always visible unless turned off!
                if (Common.AbortMSG == "")
                {
                    Common.AbortMSG = "Fout opgetreden bij communicatie met de server. Zet de VR bril af.";
                }
                canvasText.GetComponent<TMP_Text>().text = Common.AbortMSG;
                print("2200: Fade to transparant");
                StopAllCoroutines();
                StartCoroutine(FadeInOut(true)); //Fade to transparant
                step = 2300;
                break;
            case 2300: //Wait for fade
                if (fadeDone)
                {
                    fadeDone = false;
                    step = 2400;
                    startTimer = DateTime.Now;
                }
                break;
            case 2400:
                if ((DateTime.Now - startTimer).TotalSeconds > 5.0f)
                {
                    step = 1000; //Quit
                }
                break;
        }
    }
    void PlayAnimation()
    {
        //Test for <genericResponse > 0 for generic answers and animations
        //stap,bodyAnim,FaceExp,bodyNeu, options
        print("Loading anim for step: " + thisScenario[currentScenarioStep].stap);
        if (genericResponse > 0)
        {
            //print("This will be a generic response!");
            print("206 Anim to load: " + "Animations/Algemeen/A" + genericResponse + "f");
            AnimationClip aniClip = Resources.Load<AnimationClip>("Animations/Algemeen/A" + genericResponse + "f"); //Get Body Animation
            clipOverrides[emotionMotion] = aniClip; //Override the default Emotion body animation [SitC02]
            //print("209 Anim to load: " + "Animations/Algemeen/A" + genericResponse + "f"); //Can be DELETEd?
            aniClip = Resources.Load<AnimationClip>("Animations/Algemeen/A" + genericResponse + "f"); //Get Facial experession animation
            clipOverrides[emotionExpression] = aniClip; //Override the default Emotion facial animation [Sadexpression]
            if (thisScenario[currentScenarioStep].bodyNeutral != "")
            {
                //print("214 Anim to load: Animations/" + thisScenario[currentScenarioStep].bodyNeutral);
                aniClip = Resources.Load<AnimationClip>("Animations/" + thisScenario[currentScenarioStep].bodyNeutral); //Get Neutral bodyanimation
            }
            else
            {
                //print("219 Anim to load: " + "Animations/SitC01");
                aniClip = Resources.Load<AnimationClip>("Animations/SitC01"); //Get default Neutral Body Animation
            }
            clipOverrides[neutralBodyAnim] = aniClip; //Override the default Neutral body animation [SitC01]
            switch(genericResponse)
            {
                case 1:
                    Common.MsgToSend = "TXT,LARS,Ik begijp je niet."; //Send current spoken text to server
                    break;
                case 2:
                    Common.MsgToSend = "TXT,LARS,Ik weet het niet hoor."; //Send current spoken text to server
                    break;
                case 3:
                    Common.MsgToSend = "TXT,LARS,Ik volg je niet helemaal."; //Send current spoken text to server
                    break;
                default:
                    Common.MsgToSend = "TXT,LARS,Algemene reactie default"; //Send current spoken text to server
                    break;
            }
            Common.RdyToSend = true;
        }
        else
        {
            print("226 Anim to load: " + "Animations/" + currentScenarioName + "/" + thisScenario[currentScenarioStep].bodyAnim);
            AnimationClip aniClip = Resources.Load<AnimationClip>("Animations/" + currentScenarioName + "/" + thisScenario[currentScenarioStep].bodyAnim); //Get Body Animation
            clipOverrides[emotionMotion] = aniClip; //Override the default Emotion body animation [SitC02]
            //print("229 Anim to load: " + "Animations/" + currentScenarioName + "/" + thisScenario[currentScenarioStep].faceExp);
            aniClip = Resources.Load<AnimationClip>("Animations/" + currentScenarioName + "/" + thisScenario[currentScenarioStep].faceExp); //Get Facial experession animation
            clipOverrides[emotionExpression] = aniClip; //Override the default Emotion facial animation [Sadexpression]
            if (thisScenario[currentScenarioStep].bodyNeutral != "")
            {
                //print("234 Anim to load: Animations/" + thisScenario[currentScenarioStep].bodyNeutral);
                aniClip = Resources.Load<AnimationClip>("Animations/" + thisScenario[currentScenarioStep].bodyNeutral); //Get Neutral bodyanimation
            }
            else
            {
                //print("239 Anim to load: " + "Animations/SitC01");
                aniClip = Resources.Load<AnimationClip>("Animations/SitC01"); //Get default Neutral Body Animation
            }
            clipOverrides[neutralBodyAnim] = aniClip; //Override the default Neutral body animation [SitC01]
            Common.MsgToSend = "TXT,LARS," + thisScenario[currentScenarioStep].SimonText; //Send current spoken text to server
            Common.RdyToSend = true;
        }
        animatorOverrideController.ApplyOverrides(clipOverrides);

        animator.SetTrigger("Emotional"); //Play animation
        if (genericResponse > 0)
        {
            PlayAudio("Audio/Algemeen/A" + genericResponse + "f", true); //Play audio from Lars
            genericResponse = 0; //reset
        }
        else
        {
            PlayAudio("Audio/" + currentScenarioName + "/" + thisScenario[currentScenarioStep].faceExp, true); //Play audio from Lars
        }
    }
    void PlayAudio(string theAudioPfile, bool Lars)
    {
        print("Playing audio: " + theAudioPfile);
        AudioClip audioClip = (AudioClip)Resources.Load(theAudioPfile); //Load audio file
        if (!Lars) //If VoiceOver
        {
            voiceOverSource.clip = audioClip; //Push clip into source
            voiceOverSource.Play(); //play
        }
        else
        {
            larsAudioSource.clip = audioClip;
            larsAudioSource.Play();
        }

    }
    void AudioIsPlaying(bool Lars)
    {
        if(!Lars)
        {

        }
        else
        {

        }
    }
    void CheckSpecials()
    {
        if(currentScenario == 2) //Potatoes
        {
            potato1.GetComponent<Animator>().SetTrigger("trigger");
            potato2.GetComponent<Animator>().SetTrigger("trigger");
        }
    }
    void ReadScenario(int scenarioNumber)
    {
        thisScenario.Clear();
        print("Reading scenario index: " + scenarioNumber);
        //print("File name: " + @"Scenarios\" + scenarioHolder.transform.GetChild(scenarioNumber).name + ".txt");
        print("File name: " + @"Scenarios\" + scenarioObjects[scenarioNumber].transform.name + ".txt");
        //currentScenarioName = scenarioHolder.transform.GetChild(scenarioNumber).name;
        currentScenarioName = scenarioObjects[scenarioNumber].transform.name;
        var textFile = Resources.Load<TextAsset>(@"Scenarios\" + currentScenarioName); //Get the text file from the Resources independent of platform
        currentScenario = Convert.ToInt32(currentScenarioName.Substring(currentScenarioName.IndexOf("o") + 1));
        print("Reading scenario " + currentScenario + " : " + currentScenarioName);
        string[] steps = textFile.text.Split(new[] { System.Environment.NewLine }, System.StringSplitOptions.None); //Split on NewLine for each step
        foreach (string thisStep in steps)
        {
            string[] theParts = thisStep.Split(','); //Separate the elements of this step
            if (thisStep.Length > 0) //SKip any empty line, prob at the end
            {
                //Now push all the elements of each step into the List
                thisScenario.Add(new scenario { stap = theParts[0], bodyAnim = theParts[1], faceExp = theParts[2], bodyNeutral = theParts[3], negativeOption = theParts[4], neutralOption = theParts[5], positiveOption = theParts[6], SimonText = theParts[7] });
            }
        }
        scenarioObjects[scenarioNumber].SetActive(true); //Turn scenario objects for this scenatio on
        Common.MsgToSend = "TXT,SC," + theme[currentScenario]; //Ready to send the <scenario> command to the Server with the scenario theme
        Common.RdyToSend = true; //Tell the client code to send the message
    }
    void SetScenarios()
    {
        //Get the common.scenarionumbers
        scenarioNrs = Common.ScenarioNumbers.Split('-'); //split on '-'
        bool foundIt = false;
        //Find the scenario objects inside scenarioHolder
        foreach(string thisScenario in scenarioNrs)
        {
            foundIt = false;
            for (int i = 0; i < totalScenarios; i++)
            {
                if(scenarioHolder.transform.GetChild(i).gameObject.name.Substring(8) == thisScenario) //Use this scenario?
                {
                    scenarioObjects.Add(scenarioHolder.transform.GetChild(i).gameObject); //Add each to the list of scneario objects
                    numberScenariosToPlay++;
                    foundIt = true;
                }
            }
            if(!foundIt)
            {
                //error
                print("^^^Scenario in server list is not present in Client! ABORT");
                Common.Abort = true;
                Common.AbortMSG = "Fout in scenariolijst van de server. Zet de VR bril af.";
            }
        }
        for (int i = 0; i < totalScenarios; i++) //Disbale all the scenario objects
        {
            scenarioHolder.transform.GetChild(i).gameObject.SetActive(false); //Turn each off to start with
        }
        //Load theme names

        theme = new string[25]; //Create array for all the scenarios - now default to 25
        themeIntroTxt = new string[25];
        var textFile = Resources.Load<TextAsset>(@"Scenarios\scenarios"); //Get the themes text file from the Resources independent of platform
        string[] lines = textFile.text.Split(new[] { System.Environment.NewLine }, System.StringSplitOptions.None); //Split on NewLine for each step
        foreach (string thisLine in lines)
        {
            if (thisLine != "")
            {
                string[] elements = thisLine.Split(';'); //[0] = scenario number, [1] = scenario theme
                //print("^ " + elements[0] + " - " + elements[1]);
                theme[Convert.ToInt32(elements[0])] = elements[1]; //Create the list of themes - NOTE: list is 1-based
                themeIntroTxt[Convert.ToInt32(elements[0])] = elements[2];
            }
        }
    }
    void SetObjects()
    {
        for(int i = 0; i < totalScenarios; i++) //Run through all the scenerio objects inside the Scenarios GameObject
        {
            scenarioObjects.Add(scenarioHolder.transform.GetChild(i).gameObject); //Add each to the list of scneario objects
            scenarioObjects[i].SetActive(false); //Turn each off to start with
        }
        theme = new string[25]; //Create array for all the scenarios - now default to 25
        var textFile = Resources.Load<TextAsset>(@"Scenarios\scenarios"); //Get the themes text file from the Resources independent of platform
        string[] lines = textFile.text.Split(new[] { System.Environment.NewLine }, System.StringSplitOptions.None); //Split on NewLine for each step
        foreach(string thisLine in lines)
        {
            if (thisLine != "")
            {
                string[] elements = thisLine.Split(','); //[0] = scenario number, [1] = scenario theme
                //print("^ " + elements[0] + " - " + elements[1]);
                theme[Convert.ToInt32(elements[0])] = elements[1]; //Create the list of themes - NOTE: list is 1-based
            }
        }
    }
    void DoBaseOverides()
    {
        AnimationClip aniClip = Resources.Load<AnimationClip>("Animations/SitC02"); //Get default Body Animation
        clipOverrides[emotionMotion] = aniClip; //Set the default Emotion body animation [SitC02]
        aniClip = Resources.Load<AnimationClip>("Animations/SadExpression"); //Get default Facial experession animation
        clipOverrides[emotionExpression] = aniClip; //Set the default Emotion facial animation [Sadexpression]
        aniClip = Resources.Load<AnimationClip>("Animations/SitC01"); //Get default Neutral Body Animation
        clipOverrides[neutralBodyAnim] = aniClip; //Override the default Neutral body animation [SitC01]

        animatorOverrideController.ApplyOverrides(clipOverrides);
    }
    IEnumerator FadeInOut(bool fadeIn) //If TRUE then fade to transparant, if FALSE fade to opaque = black
    {
        var material = faderBox.GetComponent<MeshRenderer>().material;
        if(fadeIn)
        {
            while (material.color.a != 0f)
            {
                var newAlpha = Mathf.MoveTowards(material.color.a, 0f, fadeSpeed * Time.deltaTime);
                material.color = new Color(material.color.r, material.color.g, material.color.b, newAlpha);
                yield return null;
            }
            fadeDone = true;
        }
        else
        {
            while (material.color.a != 1f)
            {
                var newAlpha = Mathf.MoveTowards(material.color.a, 1f, fadeSpeed * Time.deltaTime);
                material.color = new Color(material.color.r, material.color.g, material.color.b, newAlpha);
                yield return null;
            }
            fadeDone = true;
        }
    }
}

class scenario
{
    public string theme { get; set; }
    public string stap { get; set; }
    public string bodyAnim { get; set; }
    public string faceExp { get; set; }
    public string bodyNeutral { get; set; }
    public string negativeOption { get; set; }
    public string neutralOption { get; set; }
    public string positiveOption { get; set; }
    public string SimonText { get; set; }
}
