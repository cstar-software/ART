using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Byn.Awrtc;
using Byn.Awrtc.Unity;

public class Phonological3App : BaseApp {

  // TODO: make this a slider or something?
  public const float answerPeriodTime = 15.0f;
  public const float startNextTestDelay = 1.5f;

  public Texture[] answerTextures;
  public UILabel summaryLabel;
  public VideoPlayer videoPlayer;
  public UILabel instructionLabel;
  public UITexture answerTexture;
  public UIButton greenButton;
  public UIButton redButton;
  public UIButton startButton;
  public UIButton practiceButton;
  public UIPopupList levelPopupList;
  public UIButton stopButton;
  public UILabel statusLabel;
  public UILabel questionLabelGreen;
  public UILabel questionLabelRed;

  // TODO: move this to base and find a way to subclass it so we can
  // keep patient/therapist only in all sets
  private enum Visibility {
    All =                   1 << 0,
    DuringInstructions =    1 << 1, 
    DuringVideoPlayback =   1 << 2,
    DuringAnswerPeriod =    1 << 3,
    DuringResponsePeriod =  1 << 4,
    TherapistOnly =         1 << 5,
    PatientOnly =           1 << 6,
    PracticeOnly =          1 << 7
  }

  private struct UIVisibility {
    public GameObject obj;
    public Visibility state;
    public UIVisibility(GameObject obj, Visibility state) {
      this.obj = obj;
      this.state = state;
    }
  }
  private UIVisibility[] visibilityStates;

  private enum Answer {
    None,
    Correct,
    Wrong
  }

  private enum Action {
    ShowInstructions,
    StartTrial,
    NextVideo,
    NextTest,
    GiveAnswer,
    ShowAnswerImage,
    StopVideo,
  }

  private struct Test {
    public string file1;  // name of the first video file
    public string file2;  // name of the second video file
    public int answer;    // answer response index (1 = green/correct, 2 = red/wrong)
  }

  private struct TrialState {
    public List<Test> tests;

    public int trialLevel;   // current trial level
    public int testIndex;    // index into tests list
    public int videoFile;    // current video file index to play (0 or 1)
    public Answer answer;    // last answer given for the current test
    public DateTime startTime;
    public bool practice;

    public Test GetCurrentTest() {
      return tests[testIndex];
    }
  }

  [Serializable]
  private struct Message {
    public Action action;
    public int level;
    public Answer answer;
    public bool practice;
  }

  [Serializable]
  private struct RestoreState {
  }

  private TrialState state;
  private DelayedAction answerPeriodAction;

  private struct InstructionText {
    public string title;
    public string text;
    public string question1Description;
    public string question2Description;
    public int alias;
    public InstructionText(string title, string text, string q1Desc, string q2Desc) {
      this.title = title;
      this.text = text;
      this.question1Description = q1Desc;
      this.question2Description = q2Desc;
      this.alias = 0;
    }
    public InstructionText(string title, int alias) {
      this.title = title;
      this.text = "";
      this.question1Description = "";
      this.question2Description = "";
      this.alias = alias;
    }
  }

  private const string instructionPrefix = 
      "In this task you will see two videos at a time.\n\n" +
      "Each video is of a person's mouth saying a single word.\n\n" +
      "Listen closely to both words.\n\n\n";

  private InstructionText[] instructions = new InstructionText[10] {
    // level 1
    new InstructionText("Level 1 - Same Number Syllables Test",
      instructionPrefix + 
      "Press [99ff00]GREEN[-] if they have the same number of syllables.\n\n" +
      "Press [FF0000]RED[-] if they do not.",
      "Same number",
      "Different number"
    ),
    // level 2
    new InstructionText("Level 2 - More Syllables Test",
      instructionPrefix +
      "Press [99ff00]GREEN[-] if the [u][b]FIRST[/b][/u] word has more syllables.\n\n" +
      "Press [FF0000]RED[-] if the [u][b]SECOND[/b][/u] word has more syllables.",
      "First word has more",
      "Second word has more"
    ),
    // level 3
    new InstructionText("Level 3 - Same Sound Test",
      instructionPrefix +
      "Press [99ff00]GREEN[-] if they [u][b]START[/b][/u] with the same sound.\n\n" +
      "Press [FF0000]RED[-] if they do not.",
      "Starts with the same sound",
      "Doesn't start with the same sound"
    ),
    // level 4, 5
    new InstructionText("Level 4 - Same Sound Test", 3),
    new InstructionText("Level 5 - Same Sound Test", 3),
    // level 6
    new InstructionText("Level 6 - Same End Sound Test",
      instructionPrefix +
      "Press [99ff00]GREEN[-] if they [u][b]END[/b][/u] with the same sound.\n\n" +
      "Press [FF0000]RED[-] if they do not.",
      "Ends with the same sound",
      "Doesn't end with the same sound"
    ),
    // level 7, 8
    new InstructionText("Level 7 - Same End Sound Test", 6),
    new InstructionText("Level 8 - Same End Sound Test", 6),
    // level 9
    new InstructionText("Level 9 - Rhyming Test",
      instructionPrefix + 
      "Press [99ff00]GREEN[-] if they rhyme.\n\n" +
      "Press [FF0000]RED[-] if they do not.",
      "Does rhyme",
      "Doesn't rhyme"
    ),
    // level 10
    new InstructionText("Level 10 - Rhyming Test", 9),
  };

  private struct LogEntry {
    public int level;
    public int test;
    public Answer answer;
    public double responseTime;
    public string CVSRowString() {
      return (level+1)+","+(test+1)+","+answer+","+responseTime;
    }
  }
  private List<LogEntry> logEntries;

  private const string RESOURCES_PATH = "Phonological-Video-Treatment";

  private List<Test> ParseCSV(int level) {

    // make full path to file
    var name = "level"+level+".csv";
    var path = Application.streamingAssetsPath+"/"+RESOURCES_PATH+"/"+name;

    string line;
    int count = 0;
    var tests = new List<Test>();

    StreamReader file =  new StreamReader(path);  
    while((line = file.ReadLine()) != null) {  
      count += 1;

      // skip the first row which is the header
      if (count == 1) continue;

      // split row by token
      string[] parts = line.Split(',');

      var test = new Test();
      test.file1 = parts[0];
      test.file2 = parts[1];
      test.answer = Int32.Parse(parts[2]);
      tests.Add(test);
    }  
    file.Close();  

    return tests;
  }

  protected override void HandleRestoreState (byte[] bytes) {
    var restoreState = Packet.Deserialize<RestoreState>(bytes);
  }

  public override byte[] GetRestoreState() {
    var restoreState = new RestoreState();

    var header = new MessageHeader(MessageHeader.HEADER_PROTOCOL_RESTORE);
    header.appID = (byte)this.appID;

    return Packet.Serialize(restoreState, header);
  } 

  private void UpdateUIState(Visibility forState) {

    // states not ready, bail!
    if (visibilityStates == null) return;

    bool active;
    foreach (var ui in visibilityStates) {
      active = false;

      if ((ui.state & forState) == forState) {
        active = true;
      }

      // always show
      if ((ui.state & Visibility.All) == Visibility.All) {
        active = true;
      }

      // always hide if we're the patient
      if ((ui.state & Visibility.TherapistOnly) == Visibility.TherapistOnly && Globals.isPatient) {
        active = false;
      }

      // always hide if we're the patient
      if ((ui.state & Visibility.PatientOnly) == Visibility.PatientOnly && (Globals.isTherapist && !Globals.therapistOnlyTesting)) {
        active = false;
      }

      // always hide if we're not in practice mode
      if ((ui.state & Visibility.PracticeOnly) == Visibility.PracticeOnly && !state.practice) {
        active = false;
      }

      ui.obj.SetActive(active);
    }
  }

  public void OnStopButton() {
    var msg = new Message();
    msg.action = Action.StopVideo;
    SendMessage(Packet.Serialize(msg));
  }

  public void OnGreenButton() {
    var test = state.GetCurrentTest();
    var msg = new Message();
    msg.action = Action.GiveAnswer;

    if (test.answer == 1) {
      msg.answer = Answer.Correct;
    } else {
      msg.answer = Answer.Wrong;
    }

    SendMessage(Packet.Serialize(msg));

    if (answerPeriodAction != null) {
      answerPeriodAction.Stop(true);
      answerPeriodAction = null;
    }
  }

  public void OnRedButton() {
    var test = state.GetCurrentTest();
    var msg = new Message();
    msg.action = Action.GiveAnswer;

    if (test.answer == 2) {
      msg.answer = Answer.Correct;
    } else {
      msg.answer = Answer.Wrong;
    }

    SendMessage(Packet.Serialize(msg));

    if (answerPeriodAction != null) {
      answerPeriodAction.Stop(true);
      answerPeriodAction = null;
    }
  }

  public void OnStartButton() {
    var msg = new Message();
    msg.action = Action.StartTrial;
    msg.level = state.trialLevel;
    SendMessage(Packet.Serialize(msg));
  }

  public void OnPracticeButton() {
    var msg = new Message();
    msg.action = Action.StartTrial;
    msg.level = state.trialLevel;
    msg.practice = true;
    SendMessage(Packet.Serialize(msg));
  }

  private void StartTrial(Message msg) {

    // create the trial data
    var list = ParseCSV(state.trialLevel);

    // randomize tests
    if (!msg.practice) {
      state.tests = ListUtils.Randomize<Test>(list);
    } else {
      state.tests = new List<Test>();
      // in practice mode we only choose 2 random videos
      for (int i = 0; i < 2; i++) {
        var index = Globals.GetRandomNumber(0, list.Count);
        state.tests.Add(list[index]);
        list.RemoveAt(index);
      }
    }
    // reset trial state
    state.testIndex = 0;
    state.videoFile = 0;
    state.answer = Answer.None;
    state.practice = msg.practice;

    statusLabel.text = state.testIndex+"/"+state.tests.Count;

    PlayVideo(state.GetCurrentTest().file1);
  }

  private void ProcessAnswer(Message msg) {
    state.answer = msg.answer;
    Debug.Log("got answer:"+msg.answer);
  }

  private void NextVideo(Message msg) {
    state.videoFile = 1;
    PlayVideo(state.GetCurrentTest().file2);
  }

  private void TrialEnded() {

    // TODO: how should we get a summary of response times?
    if (!state.practice) {
      double correct = 0;
      double responseTime = 0;
      StreamWriter file = new StreamWriter("trial-log.txt");  
      foreach (var entry in logEntries) {
        file.WriteLine(entry.CVSRowString());
        if (entry.answer == Answer.Correct) {
          correct += 1;
        }
        responseTime += entry.responseTime;
      }
      // total accuracy
      var accuracy = (correct / (double)logEntries.Count) * 100;
      file.WriteLine("accuracy: "+accuracy+"%");
      // total response time
      file.WriteLine("responseTime: "+responseTime.ToString("0.0"));
      
      file.Close();  
    }

    ShowInstructions(state.trialLevel);
  }

  private void NextTest(Message msg) {

    state.testIndex += 1;
    state.answer = Answer.None;

    // update status label
    statusLabel.text = state.testIndex+"/"+state.tests.Count;

    UpdateUIState(Visibility.DuringVideoPlayback);

    // advance to next video
    if (state.testIndex < state.tests.Count) {
      state.videoFile = 0;
      PlayVideo(state.GetCurrentTest().file1);
    } else {
      // all videos are finished so the trial is over
      TrialEnded();
    }
  }

  private void ShowAnswerImage(Message msg) {
    answerTexture.gameObject.SetActive(true);
    if (msg.answer == Answer.Correct) {
      answerTexture.mainTexture = answerTextures[0];
    } else {
      answerTexture.mainTexture = answerTextures[1];
    }
  }

  private void HandleAnswerPeriodEndedTimer(object sender) {
    answerPeriodAction = null;
    VideoDebugLog("answer period ended");

    // show answer image
    var msg = new Message();
    msg.action = Action.ShowAnswerImage;
    msg.answer = state.answer;
    SendMessage(Packet.Serialize(msg));

    // start next test after a delay so the user can see the results
    DelayedAction.InvokeMethod(startNextTestDelay, delegate(object timerSender) {
      msg = new Message();
      msg.action = Action.NextTest;
      SendMessage(Packet.Serialize(msg));
    });

    //UpdateUIState(Visibility.DuringResponsePeriod);

    // add log entry
    var entry = new LogEntry();
    entry.level = state.trialLevel;
    entry.test = state.testIndex;
    entry.answer = state.answer;
    entry.responseTime = (DateTime.Now - state.startTime).TotalMilliseconds / 1000;
    logEntries.Add(entry);

    VideoDebugLog("test ended "+state.testIndex+" you answered "+state.answer+" time "+entry.responseTime);
  }


  private void HandleVideoEnded(VideoPlayer vp) {
    VideoDebugLog("video ended callback:"+state.videoFile);

    if (state.videoFile == 0) {
      if (Globals.role == Role.Patient || Globals.therapistOnlyTesting) {
        var msg = new Message();
        msg.action = Action.NextVideo;
        SendMessage(Packet.Serialize(msg));
      }
    } else {
      UpdateUIState(Visibility.DuringAnswerPeriod);

      if (Globals.role == Role.Patient || Globals.therapistOnlyTesting) {
        // wait for answer after both videos are finished
        if (state.practice) {
          answerPeriodAction = DelayedAction.InvokeMethod(1000.0f, HandleAnswerPeriodEndedTimer);
        } else {
          answerPeriodAction = DelayedAction.InvokeMethod(answerPeriodTime, HandleAnswerPeriodEndedTimer);
        }
        // start counting for response time now
        state.startTime = DateTime.Now;
      }
    }
  }

  private void PlayVideo(string name) {
    VideoDebugLog("play video:"+name);

    var clip = Resources.Load<VideoClip>(RESOURCES_PATH+"/video/"+name);   
    Globals.FatalError(clip == null, "video clip "+name+" is not found.");

    videoPlayer.clip = (VideoClip)clip;
    videoPlayer.Prepare();
    videoPlayer.Play();

    UpdateUIState(Visibility.DuringVideoPlayback);
  }

  private void ShowInstructions(int level) {

    Debug.Log("Show instructions for level "+level);
    state.trialLevel = level;

    var instruction = instructions[level - 1];

    // the title is always from the actual instruction (ignoring aliases)
    var title = instruction.title;

    // inherit from alias
    if (instruction.alias > 0) {
      instruction = instructions[instruction.alias - 1];
    }

    // prepend code to render text black
    instructionLabel.text = "[000000]"+instruction.text;
    summaryLabel.text = title;
    questionLabelGreen.text = instruction.question1Description;
    questionLabelRed.text = instruction.question2Description;

    UpdateUIState(Visibility.DuringInstructions);
  }

  private void VideoDebugLog(string msg) {
    Debug.Log(msg);
  }

  public void OnSelectLevel() {
    var msg = new Message();
    msg.action = Action.ShowInstructions;
    msg.level = levelPopupList.items.IndexOf(levelPopupList.value) + 1;
    SendMessage(Packet.Serialize(msg));
  }

  protected override void HandleLoad () {
    logEntries = new List<LogEntry>();

    // populate levels popup
    var items = new List<string>();
    for (int i = 0; i < 10; i++) {
      items.Add("Level "+(i + 1)+" - "+instructions[i].title);
    }
    levelPopupList.items = items;

    videoPlayer.loopPointReached += HandleVideoEnded;
  }

  protected override void HandleShow() {

    // setup visibility states
    visibilityStates = new UIVisibility[] {
      new UIVisibility(summaryLabel.gameObject,       Visibility.All),
      new UIVisibility(videoPlayer.gameObject,        Visibility.DuringVideoPlayback),
      new UIVisibility(instructionLabel.gameObject,   Visibility.DuringInstructions),
      new UIVisibility(answerTexture.gameObject,      Visibility.DuringResponsePeriod),
      new UIVisibility(greenButton.gameObject,        Visibility.DuringAnswerPeriod /*| Visibility.PatientOnly*/),
      new UIVisibility(redButton.gameObject,          Visibility.DuringAnswerPeriod /*| Visibility.PatientOnly*/),
      new UIVisibility(questionLabelGreen.gameObject, Visibility.DuringAnswerPeriod /*| Visibility.PatientOnly*/ | Visibility.PracticeOnly),
      new UIVisibility(questionLabelRed.gameObject,   Visibility.DuringAnswerPeriod /*| Visibility.PatientOnly*/ | Visibility.PracticeOnly),
      new UIVisibility(startButton.gameObject,        Visibility.DuringInstructions | Visibility.TherapistOnly),
      new UIVisibility(practiceButton.gameObject,     Visibility.DuringInstructions | Visibility.TherapistOnly),
      new UIVisibility(levelPopupList.gameObject,     Visibility.DuringInstructions | Visibility.TherapistOnly),
      new UIVisibility(stopButton.gameObject,         Visibility.DuringVideoPlayback | Visibility.DuringAnswerPeriod | Visibility.DuringResponsePeriod | Visibility.TherapistOnly),
      new UIVisibility(statusLabel.gameObject,        Visibility.DuringVideoPlayback | Visibility.DuringAnswerPeriod | Visibility.DuringResponsePeriod | Visibility.TherapistOnly),
    };

    UpdateUIState(Visibility.All);

    // disable button presses for buttons if we're the therapist
    if (Globals.isTherapist && !Globals.therapistOnlyTesting) {
      var collider = redButton.GetComponent<BoxCollider>();
      collider.enabled = false;
      collider = greenButton.GetComponent<BoxCollider>();
      collider.enabled = false;
    }

    // select the default level
    levelPopupList.value = levelPopupList.items[0];
  }

  protected override void HandleHide() {
    DelayedAction.CancelAllActions(this);
  }

  protected override void HandleMessage (byte[] bytes) {
    var msg = Packet.Deserialize<Message>(bytes);

    switch (msg.action) {
      case Action.ShowInstructions:
        ShowInstructions(msg.level);
        break;
      case Action.StartTrial:
        StartTrial(msg);
        break;
      case Action.NextVideo:
        NextVideo(msg);
        break;
      case Action.GiveAnswer:
        ProcessAnswer(msg);
        break;
      case Action.NextTest:
        NextTest(msg);
        break;
      case Action.ShowAnswerImage:
        UpdateUIState(Visibility.DuringResponsePeriod);
        ShowAnswerImage(msg);
        break;
      case Action.StopVideo:
        videoPlayer.gameObject.SetActive(true);
        videoPlayer.clip = null;
        videoPlayer.Stop();

        DelayedAction.CancelAllActions(this);
        ShowInstructions(state.trialLevel);
        break;
    }

  }

}
