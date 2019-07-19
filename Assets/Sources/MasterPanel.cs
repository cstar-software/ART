using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Android;
using Byn.Awrtc;
using Byn.Awrtc.Unity;

public class MasterPanel : MonoBehaviour {

    // consts
    public const float CLIENT_RETRY_DELAY = 1.0f;

    // gui
    public GameObject sideBar;
    public GameObject selectTestButton;
    public GameObject debugLogButton;
    public GameObject mainMenuPanel;
    public GameObject selectTestPanel;
    public UILabel statusMessageLabel;
    public GameObject localVideo;
    public GameObject remoteVideo;
    public GameObject gameboardPanel;
    public UIToggle toggleCursorButton;
    public GameObject therapistControlPanel;

    // external gui elements
    public GameObject[] gameBoards;       // array of all gameboards
    public GameObject debugLog;           // debug log panel
    public SketchPad sketchPad;           // sketch pad panel
    public GameObject mouseIndicator;     // sprite used for mouse clicks indicators
    public UITexture focusRingImage;      // image to place behind gui elements during focus
    public GameObject therapistCursor;    // sprite used for therapists cursor
    public UIButton recordButton;

    // audio & recording
    public AudioClip mouseClickSound;

    // private 
    private ICall caller = null;
    private Account account;
    private Texture2D localVideoTexture = null;
    private Texture2D remoteVideoTexture = null;
    private List<BaseApp> apps = new List<BaseApp>();
    private BaseApp currentApp = null;
    private NetworkConfig networkConfig = null;
    private MediaConfig mediaConfig = null;
    private bool localVideoFlipped = false;
    private bool remoteVideoFlipped = false;
    private long cursorFrameCount = 0;
    private Vector3 lastCursorPosition;
    private bool showTherapistsCursor;
    private Camera camera;
    private System.Diagnostics.Process recordProcess;

    public struct Account {
      public string address;
      public string server;
      public string server2;
      public string user;
      public string password;
      public string url;
    }

    private enum AppActions {
      Open,
      Close,
      Login,
      FlipTherapistVideo,
      FlipPatientVideo,
      HandShake
    }

    [Serializable]
    private struct SystemMessage {
      public int appID;
      public AppActions action;
      public int randomSeed;
      public SystemMessage(AppActions action) {
        this.appID = 0;
        this.action = action;
        this.randomSeed = Globals.randomSeed;
      }
      public SystemMessage(int appID, AppActions action) {
        this.appID = appID;
        this.action = action;
        this.randomSeed = Globals.randomSeed;
      }
    }

    [Serializable]
    private struct CursorMessage {
      public float x, y;
    }

    private NetworkConfig CreateNetworkConfig(Account account) {
      NetworkConfig config = new NetworkConfig();
      config.IceServers.Add(new IceServer(account.server, account.user, account.password));
      config.IceServers.Add(new IceServer(account.server2));
      config.SignalingUrl = account.url;
      return config;
    }

    private MediaConfig CreateMediaConfig() {
      MediaConfig config = new MediaConfig();

      config.Audio = PlayerPrefs.GetInt(PREFS.ENABLE_AUDIO) == 1 ? true : false;
      config.Video = PlayerPrefs.GetInt(PREFS.ENABLE_VIDEO) == 1 ? true : false;;
      config.VideoDeviceName = null;
      config.Format = FramePixelFormat.ABGR;
      config.MinWidth = 160;
      config.MinHeight = 140;
      config.MaxWidth = 640;   // 320 640 1280 1920
      config.MaxHeight = 360;  // 180 360 720  1080
      config.IdealWidth = 320;
      config.IdealHeight = 180;
      config.IdealFrameRate = 30; // NOTE: this works on Windows

      return config;
    }

    private Vector3 GetMouseWorldPosition() {
      return camera.ScreenToWorldPoint(Input.mousePosition);
    }

    private IEnumerator AnimateMouseIndicator(Vector3 worldPosition) {
      var obj = Instantiate(mouseIndicator, mouseIndicator.transform.parent, true) as GameObject;
      obj.transform.position = worldPosition;
      obj.hideFlags = HideFlags.HideInHierarchy;

      obj.transform.localScale = new Vector3(5, 5, 1);

      var sprite = obj.GetComponent<UI2DSprite>();
      // var color = sprite.color;
      // color.a = 0;
      sprite.color = new Color(sprite.color.r, sprite.color.g, sprite.color.b, 0);

      // for (int i = 0; i < 20; i++) {
      //   // scale
      //   var newScale = obj.transform.localScale;
      //   newScale.x += 0.40f;
      //   newScale.y += 0.40f;
      //   obj.transform.localScale = newScale;
      //   // opacity
      //   var sprite = obj.GetComponent<UI2DSprite>();
      //   var color = sprite.color;
      //   color.a -= 0.1f;
      //   sprite.color = color;
      //   yield return new WaitForSeconds(0.01f);
      // }

      for (int i = 0; i < 20; i++) {
        // scale
        var newScale = obj.transform.localScale;
        newScale.x -= 0.20f;
        newScale.y -= 0.20f;
        obj.transform.localScale = newScale;
        // opacity
        sprite = obj.GetComponent<UI2DSprite>();
        var color = sprite.color;
        color.a += 0.1f;
        sprite.color = color;
        yield return new WaitForSeconds(0.01f);
      }


      Destroy(obj);

      yield return null;
    }

    public bool HasConnectedEndPoint() {
      return caller != null;
    }

    private void Disconnect() {
      if (caller != null) {
        caller.CallEvent -= HandleCallEvent;
        caller.Dispose();
        caller = null;
        GC.Collect();
        GC.WaitForPendingFinalizers();
      }
    }

    private void Reconnect() {
      SetStatusMessageText("Reconnecting to "+account.address+"...");
      caller.Call(account.address);
    }

    //666 Roger added another argument 
    private void UpdateTexture(GameObject videoObject, bool flipped, ref Texture2D videoTexture, IFrame frame, FramePixelFormat format) {
      if (frame != null) {
          if (videoTexture == null) {
            DebugLog.AddEntry("Video texture: " + frame.Width + "x" + frame.Height + " Format:" + format);
          }
          UnityMediaHelper.UpdateTexture(frame, ref videoTexture);
          videoObject.GetComponent<UITexture>().mainTexture = videoTexture;
          if (flipped) {
            videoObject.transform.rotation = Quaternion.Euler(0, 180f, 180f);
          } else {
            videoObject.transform.rotation = Quaternion.Euler(0, 0, 180f);
          }
        } else {
          // app shutdown. reset values
          videoObject.GetComponent<UITexture>().mainTexture = null;
          videoObject.transform.rotation = Quaternion.Euler(0, 0, 0);
        }
    }

    private void UpdateFrame(FrameUpdateEventArgs frameUpdateEventArgs) {
      if (frameUpdateEventArgs.IsRemote) {
        UpdateTexture(remoteVideo, remoteVideoFlipped, ref remoteVideoTexture, frameUpdateEventArgs.Frame, frameUpdateEventArgs.Format);
      } else {
        UpdateTexture(localVideo, localVideoFlipped, ref localVideoTexture, frameUpdateEventArgs.Frame, frameUpdateEventArgs.Format);
      }
    }

    // pseduo notification center for now
    public void BroadcastNotification(string name) {
      if (currentApp) currentApp.ReceiveNotification(name);
    }

    private void SendInitialState() {
      // if there is an app currently open then 
      // send it's restore state to the client
      if (currentApp != null) {
        DebugLog.AddEntry("send initial state for "+currentApp.GetName());
        var bytes = currentApp.GetRestoreState();
        SendMessage(bytes);
      }
    }

    private void SetStatusMessageText(string text) {
      statusMessageLabel.gameObject.SetActive(text != "");
      statusMessageLabel.text = text;
    }

    private void HandleMessage(byte[] bytes) {
      var header = Packet.GetHeader(bytes);
      // DebugLog.AddEntry("header.protocol: "+header.protocol+" sender:"+header.sender);

      switch (header.protocol) {

        case MessageHeader.HEADER_PROTOCOL_SYSTEM: {
          var msg = Packet.Deserialize<SystemMessage>(bytes);

          switch (msg.action) {
            case AppActions.Open:
              CloseCurrentApp();
              // sync the global random seed with the therapist
              // who sends the open message
              Globals.SetRandomSeed(msg.randomSeed);
              ShowApp(msg.appID);
              break;
            case AppActions.Close:
              CloseCurrentApp();
              break;
            case AppActions.HandShake:
              if (currentApp == null) Globals.FatalError("got handshake without active app");
              if (currentApp.appID != msg.appID) Globals.FatalError("handshake appID doesn't match current app (got "+header.appID+" expected "+currentApp.appID+")");
              currentApp.ClientIsReady();
              break;
            case AppActions.Login:
              DebugLog.AddEntry("login patient");
              Globals.randomSeed = msg.randomSeed;
              LoadAppPanels();
              break;
            case AppActions.FlipTherapistVideo:
              if (Globals.isTherapist) {
                localVideoFlipped = !localVideoFlipped;
              } else {
                remoteVideoFlipped = !remoteVideoFlipped;
              }
              break;
            case AppActions.FlipPatientVideo:
              if (Globals.isPatient) {
                localVideoFlipped = !localVideoFlipped;
              } else {
                remoteVideoFlipped = !remoteVideoFlipped;
              }
              break;
          }
          break;
        }

        case MessageHeader.HEADER_PROTOCOL_RESTORE: {
          // restore app state if the message was sent from 
          // the therapist and we are the patient
          if (header.sender == Role.Therapist && Globals.role == Role.Patient) {
            DebugLog.AddEntry("restore app ID "+header.appID);
            
            // show the app first
            if (!IsAppActive(header.appID)) ShowApp(header.appID);

            if (currentApp == null) Globals.FatalError("attempting restore without active app");
            currentApp.RestoreState(bytes);
          }
          break;
        }

        case MessageHeader.HEADER_PROTOCOL_APP: {
          // NOTE: what was this for???
          // && header.sender != Globals.role
          if (currentApp != null) currentApp.ReceiveMessage(bytes);
          break;
        }

        case MessageHeader.HEADER_PROTOCOL_APP_PARAMS: {
          if (currentApp != null) currentApp.ReceiveParams(bytes);
          break;
        }

        case MessageHeader.HEADER_PROTOCOL_SKETCH: {
          sketchPad.HandleMessage(bytes);
          break;
        }

        case MessageHeader.HEADER_PROTOCOL_MOUSE_INDICATOR: {
          var msg = Packet.Deserialize<CursorMessage>(bytes);
          // play clicking sound
          if (header.sender == Role.Patient) {
            AudioSource.PlayClipAtPoint(mouseClickSound, transform.position);
          }
          StartCoroutine(AnimateMouseIndicator(new Vector3(msg.x, msg.y)));
          break;
        }

        case MessageHeader.HEADER_PROTOCOL_CURSOR: {
          var msg = Packet.Deserialize<CursorMessage>(bytes);

          if (msg.x == -1 && msg.y == -1) {
            therapistCursor.SetActive(false);
          } else {
            // convert back to screen coords to apply offset in pixels
            var pos = camera.WorldToScreenPoint(new Vector3(msg.x, msg.y, 1));
            // var offset = new Vector3(-6 * therapistCursor.transform.localScale.x, 28 * therapistCursor.transform.localScale.y, 0);
            var offset = new Vector3(-24 * therapistCursor.transform.localScale.x, 87 * therapistCursor.transform.localScale.y, 0);
            pos -= offset;

            therapistCursor.SetActive(true);
            therapistCursor.transform.position = camera.ScreenToWorldPoint(pos); 
          }
          break;
        }

        default: {
          Globals.FatalError("invalid message protocol: "+header.protocol);
          break;
        }
      }
    }

    private void HandleCallEvent(object sender, CallEventArgs e) {
      switch (e.Type) {

        case CallEventType.CallAccepted: {
          ConnectionId connID = ((CallAcceptedEventArgs)e).ConnectionId;
          DebugLog.AddEntry("CallAccepted: " + connID);
          Globals.therapistOnlyTesting = false;
          // send intial state when clients are accepted
          // note that this even is received when clients
          // are connected to the server so we need to filter
          // out CallAccepted events which are the server
          if (Globals.role == Role.Therapist) {
            SendMessage(new SystemMessage(0, AppActions.Login), false);
            SendInitialState();
          }
          SetStatusMessageText("");
          break;
        }

        case CallEventType.CallEnded: {
          ConnectionId connID = ((CallEndedEventArgs)e).ConnectionId;
          DebugLog.AddEntry("CallEnded: "+connID);
          if (currentApp != null) {
            currentApp.Disconnect();
            if (Globals.role == Role.Patient) CloseCurrentApp();
          }
          remoteVideo.GetComponent<UITexture>().mainTexture = null;
          Disconnect();
          Connect();
          break;
        }

        case CallEventType.ListeningFailed: {
          ErrorEventArgs args = e as ErrorEventArgs;
          // TODO: when do we get this? if the network is down show an error
          DebugLog.AddEntry("ListeningFailed");
          // Globals.FatalError("Listening failed. Is another server running?");
          SetStatusMessageText("Listening failed ("+args.ErrorMessage+")");
          break;
        }

        case CallEventType.ConnectionFailed: {
          ErrorEventArgs args = e as ErrorEventArgs;
          DebugLog.AddEntry("Connection failed error: " + args.ErrorMessage);
          SetStatusMessageText("Connection failed ("+args.ErrorMessage+")");
          if (Globals.role == Role.Patient) {
            Invoke("Reconnect", CLIENT_RETRY_DELAY);
          } else {
            // does this ever happen to the server?
            Globals.FatalError("ConnectionFailed!");
          }
          break;
        }

        case CallEventType.ConfigurationFailed: {
          ErrorEventArgs args = e as ErrorEventArgs;
          DebugLog.AddEntry("Configuration failed error: " + args.ErrorMessage);
          Disconnect();
          break;
        }

        case CallEventType.FrameUpdate: {
          if (e is FrameUpdateEventArgs) UpdateFrame((FrameUpdateEventArgs)e);
          break;
        }

        case CallEventType.DataMessage: {
          var args = e as DataMessageEventArgs;
          HandleMessage(args.Content);
          break;
        }

        // case CallEventType.Message: {
        //   MessageEventArgs args = e as MessageEventArgs;
        //   HandleSystemMessage(args.Content);
        //   break;
        // }

        case CallEventType.WaitForIncomingCall: {
          WaitForIncomingCallEventArgs args = e as WaitForIncomingCallEventArgs;          
          // the server is connected so present the gui
          SetGUIVisible(true);
          // if there is an app already open the pass the new caller
          if (currentApp != null) currentApp.Reconnect(caller);
          break;
        }

      }
    }

    static private void HandleException(string condition, string stackTrace, LogType type) {
      if (type == LogType.Exception) {
        DebugLog.AddEntry(condition);
        DebugLog.AddEntry(stackTrace);
        Globals.exceptionMessage = condition;
      }
    }

    private void OnCallFactoryReady() {
      UnityCallFactory.Instance.RequestLogLevel(UnityCallFactory.LogLevel.Info);
    }

    private void OnCallFactoryFailed(string error) {
        string fullErrorMsg = typeof(CallApp).Name + " can't start. The " + typeof(UnityCallFactory).Name + " failed to initialize with following error: " + error;
        Debug.LogError(fullErrorMsg);
    }

    private void HandleSimulatedMessage(object context) {
      HandleMessage((byte[]) context);
    }

    public void SimulateMessage(byte[] bytes, float delay = 0.0f) {
      if (delay > 0) {
        DelayedAction.InvokeMethod(delay, HandleSimulatedMessage, bytes);
      } else if (Globals.simulatePoorNetworkConditions > 0) {
        // TODO: WARNING --- this will totally mess up the random number sequence
        // delay = Globals.GetRandomNumber(0, Globals.simulatePoorNetworkConditions) / 100;
        // DelayedAction.InvokeMethod(delay, HandleSimulatedMessage, bytes);
      } else {
        HandleMessage(bytes);
      }
    }

    public void SendMessage (byte[] bytes, bool simulate = true, float delay = 0.0f) {
      if (caller == null) Globals.FatalError("server is not running");
      caller.Send(bytes, true);
      if (Globals.simulateMessages && simulate) SimulateMessage(bytes, delay);
    }

    private void SendMessage (SystemMessage msg, bool simulate = true, float delay = 0.0f) {
      SendMessage(Packet.Serialize(msg, new MessageHeader(MessageHeader.HEADER_PROTOCOL_SYSTEM)), simulate, delay);
    }

    private void SetGUIVisible(bool visible) {
      mainMenuPanel.gameObject.SetActive(visible);
    }

    private bool IsAppActive(int id) {
      if (currentApp == null) return false;
      return currentApp.appID == id;
    }

    private void SendCloseCurrentAppMessage() {
      if (currentApp) {
        var msg = new SystemMessage(currentApp.appID, AppActions.Close);
        SendMessage(msg);
      }
    }

    public GameObject FindAppPanel(string name) {
      foreach (var obj in gameBoards) {
        if (obj.name == name) {
          return obj;
        }
      }
      return null;
    }

    // sends message to open app
    public BaseApp OpenApp(string name) {
      var obj = FindAppPanel(name);
      var app = obj.GetComponent<BaseApp>();
      OpenApp(app.appID);
      return app;
    }

    // sends message to open app
    public void OpenApp(int appID) {

      // make sure the server is running
      if (caller == null) Connect();

      var msg = new SystemMessage(appID, AppActions.Open);
      msg.randomSeed = Time.frameCount;
      SendMessage(msg);
    }

    public byte FindAppID(string name) {
      foreach (var app in apps) {
        if (app.name == name) {
          return (byte)app.appID;
        }
      }
      return byte.MaxValue;
    }

    private void CloseCurrentApp() {
      if (currentApp != null) {
        currentApp.Hide();
        currentApp = null;
      }
    }

    private void ShowApp(int appID) {
      if (apps.Count == 0) Globals.FatalError("can't show apps because none are loaded yet");
      if (appID >= apps.Count) Globals.FatalError("app ID "+appID+" is out of range");
      if (!IsAppActive(appID)) ShowApp(apps[appID]);
    }

    private void ShowApp(BaseApp app) {
      if (currentApp != null) Globals.FatalError("already showing an app");

      currentApp = app;

      var paras = new BaseAppParams();
      paras.caller = caller;
      app.Show(gameboardPanel, paras);

      // send a handshake action so we know the client is ready
      if (Globals.isPatient) {
        DebugLog.AddEntry("handshake with therapist for appID "+currentApp.appID);
        SendMessage(new SystemMessage(currentApp.appID, AppActions.HandShake), false);
      }
    }

    private void LoadApp(GameObject panel) {
      var app = BaseApp.LoadWithPanel(panel, apps.Count);
      apps.Add(app);
    }

    private void Connect() {

      DebugLog.AddEntry("connecting...");

      // create network config
      if (networkConfig == null) {
        networkConfig = CreateNetworkConfig(account);
      }

      // setup caller
      caller = UnityCallFactory.Instance.Create(networkConfig);
      if (caller == null) {
          Debug.Log("Failed to create caller");
          return;
      }

      caller.LocalFrameEvents = true;
      caller.CallEvent += HandleCallEvent;

      // setup media config
      if (mediaConfig == null) {
        mediaConfig = CreateMediaConfig();

        // prefer the external video device
        if (UnityCallFactory.Instance.CanSelectVideoDevice()) {
          string[] videoDevices = UnityCallFactory.Instance.GetVideoDevices();

          // show all video device names
          for (int i = 0; i < videoDevices.Length; i++) {
            var name = videoDevices[i];
            DebugLog.AddEntry("video device #"+i+": "+name);
          }

          var preferredDevice = PlayerPrefs.GetInt(PREFS.VIDEO_DEVICE);
          if (preferredDevice < videoDevices.Length) {
            mediaConfig.VideoDeviceName = videoDevices[preferredDevice];
          } else {
            // use defualt device if the preferred device is out of range
            mediaConfig.VideoDeviceName = videoDevices[0];
          }
        } else {
          mediaConfig.VideoDeviceName = UnityCallFactory.Instance.GetDefaultVideoDevice();
        }
        DebugLog.AddEntry("Using video device: " + mediaConfig.VideoDeviceName);
      }

      caller.Configure(mediaConfig);

      // start listening...
      if (Globals.role == Role.Therapist) {
        DebugLog.AddEntry("server listening for connections on " + account.address + "...");
        SetStatusMessageText("Listening for connections on "+account.address+"...");
        caller.Listen(account.address);
      } else {
        DebugLog.AddEntry("client connecting to " + account.address + "...");
        SetStatusMessageText("Connecting to "+account.address+"...");
        caller.Call(account.address);
      }
    }

    public void OnSelectTestPanel() {
      SendCloseCurrentAppMessage();
      selectTestPanel.SetActive(true);
    }

    public void OnToggleDebugLog() {
      DebugLog.Toggle();
    }

    public void OnToggleTherapistSketchPad() {
      sketchPad.SendToggleMessage(Role.Therapist);
    }

    public void OnTogglePatientSketchPad() {
      sketchPad.SendToggleMessage(Role.Patient);
    }

    public void OnEraseTherapistSketchPad() {
      sketchPad.SendEraseMessage(Role.Therapist);
    }

    public void OnErasePatientSketchPad() {
      sketchPad.SendEraseMessage(Role.Patient);
    }

    public void OnToggleCursor() {
      showTherapistsCursor = !showTherapistsCursor;

      // send message to hide cursor
      if (!showTherapistsCursor) {
        var header = new MessageHeader(MessageHeader.HEADER_PROTOCOL_CURSOR);
        var msg = new CursorMessage();
        msg.x = -1;
        msg.y = -1;
        SendMessage(Packet.Serialize(msg, header), true);
      }
    }

    public void OnFlipLocalVideo() {
      if (Globals.isTherapist) {
        localVideoFlipped = !localVideoFlipped;
        // if the therapist flips their video the
        // patient should see the therapists video flipped
        var msg = new SystemMessage(AppActions.FlipTherapistVideo);
        SendMessage(msg, false);
      }
    }

    public void OnFlipRemoteVideo() {
      if (Globals.isTherapist) {
        remoteVideoFlipped = !remoteVideoFlipped;
        // if the therapist flips the patients video the
        // patient should see their video flipped
        var msg = new SystemMessage(AppActions.FlipPatientVideo);
        SendMessage(msg, false);
      }
    }

    private void OnGUI() {

      if (Globals.exceptionMessage != "") {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null) {
          float h = canvas.GetComponent<RectTransform>().rect.height;
          float w = canvas.GetComponent<RectTransform>().rect.width;
          GUI.ModalWindow(0, new Rect(w/2, h/2, 230, 50), DoAlert, "Exception!");
        }
        return;
      }

      if (!sideBar.activeSelf) {
        GUI.ModalWindow(0, new Rect(20, 20, 230, 100), DoRoleWindow, "Welcome");
      }
    }
    
    private void DoAlert(int windowID) {
      if (GUI.Button(new Rect(10, 20, 100, 20), "Ok")) {
        Globals.exceptionMessage = "";
      }
    }

    private void DoRoleWindow(int windowID) {
      if (GUI.Button(new Rect(10, 20, 100, 20), "Therapist")) {
        RoleChanged(Role.Therapist);
      }
      if (GUI.Button(new Rect(100 + 20, 20, 100, 20), "Patient")) {
        RoleChanged(Role.Patient);
      }
      if (GUI.Button(new Rect(10, 50, 100, 20), "Setup")) {
        SceneManager.LoadScene("Preferences");
      }
    }

    private void RoleChanged(Role role) {

      // create account info
      account = new Account();

      // windows builds connect on a specific address
      // as to not interer with testing on macs
      #if UNITY_STANDALONE_WIN
      account.address = "com.c-star.slptest.win";
      #else
      account.address = "com.c-star.slptest";
      #endif

      account.server = "stun:stun.because-why-not.com:443";
      account.server2 = "stun:stun.l.google.com:19302";
      account.user = "patient_"+PlayerPrefs.GetString(PREFS.PATIENT_ID);
      account.password = "cstar_dont_tell_anyone";
      account.url = "ws://signaling.because-why-not.com/callapp";

      // TODO: testing
      // account.server = "stun:129.252.69.147:443";
      // account.url = "ws://129.252.69.147/callapp";

      DebugLog.AddEntry("Connecting as "+role+"...");
      DebugLog.AddEntry("    account.address: "+account.address);
      DebugLog.AddEntry("    account.user: "+account.user);
      DebugLog.AddEntry("    account.password: "+account.password);

      Globals.role = role;
      Connect();
      sideBar.SetActive(true);

      // the therapist always controls the random seed
      if (role == Role.Therapist) {
        Globals.randomSeed = UnityEngine.Random.seed;
        LoadAppPanels();
      }

      // call after a delay to ensure startup process is complete
      // TODO: but we're not really connected!!
      DelayedAction.InvokeMethod(0.0f, ConnectedAndReady);
    }

    private void OnDestroy() {
    }

    private void LoadAppPanels() {

      // don't double-load
      if (apps.Count > 0) return;

      DebugLog.AddEntry("loading app panels");

      // before loading app panels make sure the
      // the random seed is set to our value
      UnityEngine.Random.seed = Globals.randomSeed;
      DebugLog.AddEntry("random seed: "+Globals.randomSeed);

      foreach (var board in gameBoards) {
        LoadApp(board);
      }
    }

    private void SetupDefaultPreferences() {
      if (!PlayerPrefs.HasKey(PREFS.VIDEO_DEVICE)) PlayerPrefs.SetInt(PREFS.VIDEO_DEVICE, 0);
      if (!PlayerPrefs.HasKey(PREFS.PATIENT_ID)) PlayerPrefs.SetString(PREFS.PATIENT_ID, "tester");
      if (!PlayerPrefs.HasKey(PREFS.ENABLE_AUDIO)) PlayerPrefs.SetInt(PREFS.ENABLE_AUDIO, 1);
      if (!PlayerPrefs.HasKey(PREFS.ENABLE_VIDEO)) PlayerPrefs.SetInt(PREFS.ENABLE_VIDEO, 1);
    }

    private void ConnectedAndReady(object context) {
      DebugLog.AddEntry("connected and ready");
      PrepareRoleSpecific();

      #if UNITY_ANDROID
      debugLogButton.SetActive(true);
      #endif

      // TESTING - open app if we're running the editor as the therapist
      #if UNITY_EDITOR
      if (Globals.isTherapist) {
        OpenApp(Gameboard.Semantic2);
      }
      #endif
    }

    private void PrepareGUIState() {
      sideBar.SetActive(false);
      debugLogButton.SetActive(false);
      SetStatusMessageText("");

      // set default visibility for game boards
      foreach (var obj in gameBoards) {
        obj.SetActive(false);
      }

      SetGUIVisible(false);
    }

    private void PrepareRoleSpecific() {
      if (Globals.isPatient) {
        // disable flipping videos for patient
        localVideo.GetComponent<UIButton>().onClick = null;
        remoteVideo.GetComponent<UIButton>().onClick = null;

        toggleCursorButton.gameObject.SetActive(false);
        therapistControlPanel.SetActive(false);
        recordButton.gameObject.SetActive(false);
      }
    }

    public struct FFMPEGOptions {

      public enum Format {
        avfoundation,
        x11grab,
      }

      public string shouldOverwrite;
      public int threadQueSize;
      public Format cameraFormat;
      // show all devices for format:
      // ffmpeg -f avfoundation -list_devices true -i ""
      public int videoDevice;
      public int audioDevice;
      public float framerate;

      // ffmpeg 4.1 options:
      // see "man ffpeg" for more information
      // static ffmpeg builds: https://ffmpeg.zeranoe.com/builds/
      public string videoSize;
      public string videoCodec;
      public string recQuality;
      public string audioQuality;
      public float audioVolume;
      public string pixelFormat;

      public string MakeCommand(string outputFilename) {
        string command = 
        shouldOverwrite +
        " -thread_queue_size " + threadQueSize +
        " -f " + cameraFormat +
        " -framerate " + framerate +
        " -i " + videoDevice + "\":\"" + audioDevice + "\":\"" +
        // " -i " + videoDevice + "\":\"" +
        " -s " + videoSize +
        " -b:v " + recQuality +
        " -aq " + audioQuality +
        " -filter:a \"volume=" + audioVolume + "\" " + 
        " -vcodec " + videoCodec +
        " -pix_fmt " + pixelFormat +
        " -r " + framerate +
        "  " + "\"" + outputFilename + "\"";

        return command;
      }
    }

    private void TestFFMpeg() {
      /*
        /Users/ryanjoseph/Downloads/ffmpeg-20190506-fec4212-macos64-static/bin/ffmpeg
        ffmpeg -y -f gdigrab -i desktop -f dshow -i audio="Microphone (High Definition Audio Device)" -c:v h264_amf testvid.mp4
      */
      var ffmpeg = "/Users/ryanjoseph/Downloads/ffmpeg-20190506-fec4212-macos64-static/bin/ffmpeg";
      // var args = "-y -f gdigrab -i desktop -f dshow -i audio=\"Microphone (High Definition Audio Device)\" -c:v h264_amf testvid.mp4";

      // show all devices for format:
      // ffmpeg -f avfoundation -list_devices true -i ""

      // TODO: try to open script in the actual terminal! we could use applescript...
      /*
        function run_in_terminal($script) {
          $command = "/usr/bin/osascript <<EOF\n";
          $command .= "tell application \"Terminal\"\n";
          $command .= " if (count of windows) is 0 then\n";
          $command .= "   do script \"$script\"\n";
          $command .= " else\n";
          $command .= "   do script \"$script\" in window 1\n";
          $command .= " end if\n";
          $command .= " activate\n";
          $command .= "end tell\n";
          $command .= "EOF\n";
          exec($command);
        }
      */

      FFMPEGOptions options;

      options.shouldOverwrite = "-y";         // do overwrite if file with same name exists
      options.threadQueSize = 512;            // preallocation
      options.cameraFormat = FFMPEGOptions.Format.avfoundation;
      options.videoDevice = 1;            // macOS only
      options.audioDevice = 0;            // macOS only
      options.videoSize = "640x400";          // output video dimensions
      options.videoCodec = "libx264";         // man ffmpeg for option -vcodec
      options.recQuality = "256k";            // man ffmpeg for option -b 
      options.audioQuality = "64k";           // man ffmpeg for option -aq
      options.audioVolume = 1.0f;            
      // note: required for libx264 to play in QuickTime
      // https://superuser.com/questions/820134/why-cant-quicktime-play-a-movie-file-encoded-by-ffmpeg
      options.pixelFormat = "yuv420p";        // bits per pixel 12
      options.framerate = 30.0f;

      string args = options.MakeCommand("/Users/ryanjoseph/Desktop/testvid.mp4");
      Debug.Log(ffmpeg+" "+args);
    }

    public void OnStartRecording() {

      UILabel label;
      var assetsPath = Application.streamingAssetsPath;

      // stop recording
      if (recordProcess != null) {

        #if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        ProcessUtils.Execute("/usr/bin/killall", "ffmpeg");
        if (!recordProcess.HasExited) {
          recordProcess.Kill();
        }
        #endif

        #if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        var args = "/c"+assetsPath+"\\STOPffmpeg.bat";
        var res = ProcessUtils.Execute("C:\\Windows\\system32\\cmd.exe", args);
        Debug.Log("STOPffmpeg: "+res);
        #endif

        label = recordButton.GetComponentInChildren<UILabel>();
        label.text = "Record";
        recordProcess = null;

        return;
      }

      // TODO: remove this or use it to generate scripts?
      // TestFFMpeg();

      var process = new System.Diagnostics.Process();
      process.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
      process.StartInfo.CreateNoWindow = true;
      process.StartInfo.UseShellExecute = false;

      string outputFilename;

      #if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
      outputFilename = "testvideo.mkv";
      process.StartInfo.FileName = "C:\\Windows\\system32\\cmd.exe";
      process.StartInfo.Arguments = "/c"+assetsPath+"\\STARTffmpeg.bat "+outputFilename;
      #endif

      #if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
      outputFilename = "/Users/ryanjoseph/Desktop/testvid.mp4";
      process.StartInfo.FileName = "/Applications/Utilities/Terminal.app/Contents/MacOS/Terminal";
      // TODO: can't get params on Mac???
      // process.StartInfo.Arguments = assetsPath+"/ffmpeg_record.bash \""+outputFilename+"\"";
      // process.StartInfo.Arguments = assetsPath+"/ffmpeg_record.bash";
      process.StartInfo.Arguments = "/Users/ryanjoseph/Downloads/ffmpeg_record.bash";
      #endif

      // process.EnableRaisingEvents = true;
      process.Start();
      // process.WaitForExit(); 
      // Debug.Log("process exited:"+process.ExitCode);
      Debug.Log("process started: "+process.Id);

      recordProcess = process;

      label = recordButton.GetComponentInChildren<UILabel>();
      label.text = "Stop";
    }

    private void Start() {

      Globals.mainApp = this;
      camera = GameUtils.FindByComponent<Camera>("Camera");

      // Screen.SetResolution(1280, 960, false);

      #if UNITY_ANDROID
      Permission.RequestUserPermission(Permission.Microphone);
      Permission.RequestUserPermission(Permission.Camera);
      #endif

      SetupDefaultPreferences();

      // setup sketchpad
      sketchPad.Disable();

      // setup debug log
      DebugLog.Setup(debugLog.GetComponent<DebugLog>(), new Vector3(206f, 19f, 0.0f));
      DebugLog.Dismiss();
      Application.RegisterLogCallback(HandleException);

      // always receive our own messages
      Globals.simulateMessages = true;

      // setup quality
      QualitySettings.vSyncCount = 0;
      QualitySettings.antiAliasing = 1;
      Application.targetFrameRate = 30;

      UnityCallFactory.EnsureInit(OnCallFactoryReady, OnCallFactoryFailed);

      PrepareGUIState();

      // choose role
      Globals.therapistOnlyTesting = true;
      #if UNITY_EDITOR
      RoleChanged(Role.Therapist);
      #endif  
    }

    private void Update() {      

      // toggle debug log
      if (Input.GetKeyDown(KeyCode.Escape)) {
        DebugLog.Toggle();
      }

      // toggle sketch pad
      // if (Input.GetKeyDown(KeyCode.Tab) && sketchPad) {
      //   toggleSketchpadButton.value = !toggleSketchpadButton.value;
      // }

      // toggle therapists cursor
      if (Input.GetKeyDown(KeyCode.Equals) && Globals.isTherapist) {
        toggleCursorButton.value = !toggleCursorButton.value;
      }

      var mousePosition = GetMouseWorldPosition();

      // animate mouse down
      if (HasConnectedEndPoint() && Input.GetMouseButtonDown(0)) {

        // send mouse click indicator message
        bool show;
        if (Globals.isTherapist && showTherapistsCursor) {
          // TODO: maybe make this an option? the cursor sprite is good enough for now I think
          show = false;
        } else if (Globals.isPatient) {
          // show mouse indicator for patient unless we're in sketch pad mode
          show = !sketchPad.IsEnabled();
        } else {
          show = false;
        }

        if (show) {
          var header = new MessageHeader(MessageHeader.HEADER_PROTOCOL_MOUSE_INDICATOR);
          var msg = new CursorMessage();
          msg.x = mousePosition.x;
          msg.y = mousePosition.y;
          SendMessage(Packet.Serialize(msg, header), true);
        }
      }

      // show therapists cursor
      if (showTherapistsCursor) {
        if (cursorFrameCount > 5 && lastCursorPosition != mousePosition) {

          var header = new MessageHeader(MessageHeader.HEADER_PROTOCOL_CURSOR);
          var msg = new CursorMessage();
          msg.x = mousePosition.x;
          msg.y = mousePosition.y;
          SendMessage(Packet.Serialize(msg, header), true);

          // cursorFrameCount = 0;
          lastCursorPosition = GetMouseWorldPosition();
        }
        cursorFrameCount += 1;
      }

      DelayedAction.ProcessPool();

      // TESTING
      // if (audioStreamByteCount > 0) {
      //   debugLogStatus.text = "audio stream: "+MemUtils.GetHumanReadableFileSize(audioStreamByteCount);
      //   debugLogStatus.gameObject.SetActive(true);
      //   audioStreamByteCount = 0;
      // }

      // Debug.Log((int)1/Time.deltaTime);
      if (caller != null) caller.Update();
    }
}
