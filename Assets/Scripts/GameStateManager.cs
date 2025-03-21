using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MyBox;
using FMOD.Studio;
using Unity.VisualScripting;

/**
 * This class is the main entry point into the game. 
 *
 * The Game State Manager keeps track of deaths, current section, and
 * manages starting Fmod and the Conductor (rhythm game logic).
 *
 * It also handles restarting the conductor / fmod upon a level failure. 
 */
public class GameStateManager : MyBox.Singleton<GameStateManager>
{
    public enum GameState
    {
        None,
        Intro,
        Tutorial,
        FirstChorus,
        Level1,
        Response1,
        Level2,
        Response2,
        Level3,
        Response3,
        Ending
    }

    private GameState gameState;
    public List<ChartData> charts;
    public int NumFails = 0;


    FMOD.Studio.EVENT_CALLBACK _musicFmodCallback;
    FMOD.Studio.EventInstance _musicEventInstance;

    //FMOD.Studio.TIMELINE_MARKER_PROPERTIES? _prevMarker;
    FMOD.Studio.TIMELINE_MARKER_PROPERTIES? _currMarker;

    HashSet<string> _labelsPassed = new HashSet<string>();
    public HashSet<string> LabelsPassed => _labelsPassed;

    private FMODUnity.StudioEventEmitter emitter;

    [SerializeField] GameObject dialogueManagerObj;
    DialogueTest _dialogueManager;

    private FMODUnity.StudioEventEmitter sfxEmitter;

    void Awake() {
        InitializeSingleton();

        gameState = GameState.None;
    }

    // Start is called before the first frame update
    void Start()
    {
        FMODUnity.StudioEventEmitter[] emitters = GetComponents<FMODUnity.StudioEventEmitter>();

        emitter = emitters[0];
        sfxEmitter = emitters[1];
        
        _musicFmodCallback = new FMOD.Studio.EVENT_CALLBACK(FMODEventCallback);

        //_musicEventInstance.start();

        _dialogueManager = dialogueManagerObj.GetComponent<DialogueTest>();
        _dialogueManager.endNodeSignal.AddListener(NextFmodSection);
    }

    public void StartFMODEvent()
    {
        gameState = GameState.Intro;
        emitter.Play();
        _labelsPassed.Clear();
        _musicEventInstance = emitter.EventInstance;
        _musicEventInstance.setCallback(_musicFmodCallback, FMOD.Studio.EVENT_CALLBACK_TYPE.TIMELINE_BEAT | FMOD.Studio.EVENT_CALLBACK_TYPE.TIMELINE_MARKER);
    }

    [AOT.MonoPInvokeCallback(typeof(FMOD.Studio.EVENT_CALLBACK))]
    static FMOD.RESULT FMODEventCallback(FMOD.Studio.EVENT_CALLBACK_TYPE type, System.IntPtr _event, System.IntPtr parameterPtr)
    {
        if (type == FMOD.Studio.EVENT_CALLBACK_TYPE.TIMELINE_MARKER)
        {
            var game = GameStateManager.Instance;
            var parameter = (FMOD.Studio.TIMELINE_MARKER_PROPERTIES)System.Runtime.InteropServices.Marshal.PtrToStructure(parameterPtr, typeof(FMOD.Studio.TIMELINE_MARKER_PROPERTIES));
            //GameStateManager.Instance._prevMarker = GameStateManager.Instance._currMarker;
            game._currMarker = parameter;
            string labelName = (string) parameter.name;
            UnityEngine.Debug.LogFormat("Marker: {0}", (string)parameter.name);
            
            if (!game.LabelsPassed.Contains(labelName))
            {
                //First time passing label
                game.LabelsPassed.Add(labelName);
                game.NumFails = 0;
                game.emitter.SetParameter("NumFails", game.NumFails);
                //Debug.Log($"Num Fails: {game.NumFails}");
                game.UpdateGameState();
            }
        }

        return FMOD.RESULT.OK;
    }

    // Update is called once per frame
    void Update()
    {
        //if (Input.GetKeyDown(KeyCode.P))
        //{
        //    UpdateGameState();
        //}
    }

    [SerializeField] private TrackMover credits;

    /**
     * CALLED BY FMOD reaching the end of a section.
     *
     * Progresses the current state of the game. Calls responsible managers.
     */
    void UpdateGameState()
    {
        gameState = gameState+1;
        Debug.Log($"SWITCH THE GAME STATE TO {gameState}");
        //gameState = 0;

        switch (gameState)
        {
            case GameState.None: // this is the main menu
                // *** main menu witchcraft
                break;
            case GameState.Intro:
                _dialogueManager.StartNode("Intro");
                break;
            case GameState.Tutorial: // game
                // *** Start Chart 1 
                _dialogueManager.StartNode("Tutorial");
                PlayChart(0);
                break;
            case GameState.FirstChorus: // dialog
                // *** Start Dialog 1
                _dialogueManager.StartNode("Loop1");
                Conductor.Instance.Pause();
                break;
            case GameState.Level1: // game
                // *** Start Chart 2
                PlayChart(1);
                break;
            case GameState.Response1: // dialog
                // *** Start Dialog 2
                _dialogueManager.StartNode("Post1");
                Conductor.Instance.Pause();
                break;
            case GameState.Level2: // game
                // *** Start Chart 3
                PlayChart(2);
                break;
            case GameState.Response2: // dialog
                // *** Start Dialog 3
                _dialogueManager.StartNode("Post2");
                Conductor.Instance.Pause();
                break;
            case GameState.Level3: // game 
                // *** Start Chart 4
                PlayChart(3);
                break;
            case GameState.Response3: // dialog
                // *** Start Dialog 4
                _dialogueManager.StartNode("Post3");
                Conductor.Instance.Pause();
                break;
            case GameState.Ending: // dialog!
                // *** Start Dialog 5

                credits.PutOnScreen();
                break;
        }
    }

    void NextFmodSection()
    {
        Debug.Log("GameStateManager.NextFmodSection(): Fired.");
        // Proceed to next Fmod segment
    }

    private void PlayChart(int index)
    {
        if (index >= charts.Count)
        {
            Debug.LogError($"Chart not found {index}");
            return;
        }

        Conductor.Instance.Play(charts[index]);
    }

    /**
     * Called by the conductor class when the user fails. If the user fails, restart fmod sequence, start game chart, and play fail sfx.
     *
     */
    public void RestartCurrentLevel()
    {
        NumFails++;
        switch(NumFails){
            case 1:
                _dialogueManager.StartNode("Strike1");
                break;
            case 2:
                _dialogueManager.StartNode("Strike2");
                break;
            case 3:
                _dialogueManager.StartNode("Strike3");
                //gameover logic, return to start of game instead of restarting
                StartCoroutine(GameOver());
                break;
            default:
                break;
        }
        // *** Halt current instance of Fmod Timeline, also stop the current instance of game
        gameState--;

        //Update FMOD timeline position
        sfxEmitter.Play();
        emitter.SetParameter("NumFails", NumFails);
        emitter.EventInstance.setTimelinePosition(_currMarker == null ? 0 : _currMarker.Value.position);
        UpdateGameState();
    }

    IEnumerator GameOver() {
        
        GameObject.Find("Game Over").SetActive(true);
        yield return new WaitForSeconds(3);
        Debug.Log("test");
        GameObject.FindObjectOfType<AppManager>().ReloadMainScene();
    }

}

// fmod just continues unless update game state explicitly stops it
