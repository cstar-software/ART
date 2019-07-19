using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Byn.Awrtc;
using Byn.Awrtc.Unity;

public class Semantic3App : BaseApp {

  // gui
  public UIButton toggleHintsButton;
  public UIButton nextVerbButton;
  public GameObject verbCard;
  public UIInput[] inputFields;
  public GameObject hintPanel;
  public UIInput[] hintFields;
  public UIPopupList[] hintPopups;
  public GameObject[] verbCardTriggers;
  public UIPopupList selectVerbPopup;
  public UIButton continueButton;

  // state
  private int currentVerb;
  private Coroutine moveCardCoroutine;

  // data
  private List<string> verbs;

  // verb database
  private struct VerbInfo {
    public string word;
    public string[] whoValid;
    public string[] whoInvalid;
    public string[] whatValid;
    public string[] whatInvalid;
    public List<string> GetWhoOptions() {
      var list = new List<string>();
      list.Add("---- CORRECT ----");
      foreach (var word in whoValid) {
        list.Add(word);
      }
      list.Add("---- FOIL ----");
      foreach (var word in whoInvalid) {
        list.Add(word);
      }
      return list;
    }
    public List<string> GetWhatOptions() {
      var list = new List<string>();
      list.Add("---- CORRECT ----");
      foreach (var word in whatValid) {
        list.Add(word);
      }
      list.Add("---- FOIL ----");
      foreach (var word in whatInvalid) {
        list.Add(word);
      }
      return list;
    }
    public VerbInfo(string word, string[] whoValid, string[] whoInvalid, string[] whatValid, string[] whatInvalid) {
      this.word = word;
      this.whoValid = whoValid;
      this.whoInvalid = whoInvalid;
      this.whatValid = whatValid;
      this.whatInvalid = whatInvalid;
    }
  }

  private VerbInfo[] hints = new VerbInfo[] {

    new VerbInfo("watch", 
                 new string[] {"babysitter","audience","lifeguard","surveyor","sports fan","referee"},  // who 
                 new string[] {"newspaper","mug","ornament"},         // who X
                 new string[] {"children","swimmers","movie"},        // what
                 new string[] {"badge","pencil","steering wheel"}     // what X
                 ),

    new VerbInfo("chop", 
                 new string[] {"butcher","chef","lumberjack","carpenter"},  // who correct
                 new string[] {"teacher","accountant","cat"},         // who foil
                 new string[] {"trees","meat","vegetables"},        // what correct
                 new string[] {"trash can","stop sign","airplane"}     // what foil
                 ),

    new VerbInfo("wash", 
                 new string[] {"kitchen staff","hairdresser", "janitor","maid","groomer"},  // who correct
                 new string[] {"marker","magazine","mailman"},         // who foil
                 new string[] {"floor","laundry","dog"},        // what correct
                 new string[] {"rain","mail","popcorn"}     // what foil
                 ),

    new VerbInfo("read", 
                 new string[] {"student","musician","chef","police officer","librarian"},  // who correct
                 new string[] {"highlighter","turtle","purse"},         // who foil
                 new string[] {"miranda rights","book","recipe"},        // what correct
                 new string[] {"bowl","buckle","paperclip"}     // what foil
                 ),

    new VerbInfo("deliver", 
                 new string[] {"ups man","florist","minister","mailman","doctor"},  // who correct
                 new string[] {"monkey","astronaut","rock"},         // who foil
                 new string[] {"sermon","baby","mail"},        // what correct
                 new string[] {"leaves","submarine","a play"}     // what foil
                 ),

    new VerbInfo("measure", 
                 new string[] {"pharmacist","tailor","nurse","baker","seamstress"},  // who correct
                 new string[] {"toes","cloud","building"},         // who foil
                 new string[] {"fabric","height","flour"},        // what correct
                 new string[] {"book","phone","grass"}     // what foil
                 ),

    new VerbInfo("throw", 
                 new string[] {"pitcher","bride","quaterback"},  // who correct
                 new string[] {"television","hippopotamus","tree","child"},         // who foil
                 new string[] {"baseball","football","bouquet"},        // what correct
                 new string[] {"table","ghost","sky"}     // what foil
                 ),

    new VerbInfo("eat", 
                 new string[] {"children","monkey","birds","dog","cows"},  // who correct
                 new string[] {"computer","fan","sink"},         // who foil
                 new string[] {"ice cream","seeds","grass"},        // what correct
                 new string[] {"table","pen","brush"}     // what foil
                 ),

    new VerbInfo("drive", 
                 new string[] {"mother","farmer","taxi driver","paramedic","engineer"},  // who correct
                 new string[] {"tractor","pigeon","baby"},         // who foil
                 new string[] {"minivan","ambulance","taxi"},        // what correct
                 new string[] {"wallet","wheel","barn"}     // what foil
                 ),

    // TODO: last deck is unnamed!
    // new VerbInfo("", 
    //              new string[] {},  // who correct
    //              new string[] {},  // who foil
    //              new string[] {},  // what correct
    //              new string[] {}   // what foil
    //              ),

  };

  private Dictionary<string, VerbInfo> hintsTable;

  private const string RESOURCES_PATH = "Semantic-Test-3";
  private const string FIELD_KIND_WHO = "who_field";
  private const string FIELD_KIND_WHAT = "what_field";
  private const string FIELD_KIND_HINT = "hint_field";

  // messages

  private enum Action { 
    ChangeVerb,
    UpdateInput,
    UpdateHint,
    UpdateHintFromPopup,
    ToggleHintPanel,
    FocusField,
    MoveVerbCard
  }

  [Serializable]
  private struct Message {
    public Action action;
    public string text;
    public int inputIndex;
    public string inputKind;

    public int triggerIndex {
      get { return inputIndex; }
      set { inputIndex = value; }
    }
  }

  [Serializable]
  private struct RestoreState {
  }

  protected override void HandleRestoreState (byte[] bytes) {
    var state = Packet.Deserialize<RestoreState>(bytes);
  }

  public override byte[] GetRestoreState() {
    var state = new RestoreState();

    var header = new MessageHeader(MessageHeader.HEADER_PROTOCOL_RESTORE);
    header.appID = (byte)this.appID;

    return Packet.Serialize(state, header);
  }

  protected override void TabFocus(MonoBehaviour sender) {
    if (Globals.isTherapist) {
      var msg = new Message();
      var obj = sender.gameObject.GetComponent<UIObject>();
      msg.action = Action.FocusField;
      msg.inputIndex = obj.objectID - 1;
      msg.inputKind = obj.objectKind;
      SendMessage(Packet.Serialize(msg));
    }
  }

  protected override void ClearFocus() {
    base.ClearFocus();
    if (activeFocus) {
      // hide hint button until focus is regained
      toggleHintsButton.gameObject.SetActive(false);
    }
  }

  private void HideHintPanel() {
    hintPanel.SetActive(false);
    var label = toggleHintsButton.gameObject.GetComponentInChildren<UILabel>();
    label.text = "Show Hints";
  }

  private void ShowHintPanel() {
    hintPanel.SetActive(true);
    var label = toggleHintsButton.gameObject.GetComponentInChildren<UILabel>();
    label.text = "Hide Hints";
  }

  private IEnumerator AnimateCard(Transform transform, Vector3 destPos) {

    Vector3 startPos = transform.position;
    float totalDistance = Vector3.Distance(startPos, destPos);
    float startTime = Time.time;
    float speed = 0.8f;

    float remaining;
    float delta = 0;

    while (delta < 1) {
      remaining = (Time.time - startTime) * speed;
      delta = remaining / totalDistance;
      transform.position = Vector3.Lerp(startPos, destPos, delta);
      yield return new WaitForFixedUpdate();
    }

    // snap to exact location
    transform.position = destPos;
    moveCardCoroutine = null;

    yield return null;
  }

  public void OnToggleHintPanel() {
    var msg = new Message();
    msg.action = Action.ToggleHintPanel;
    SendMessage(Packet.Serialize(msg));
  }

  public void OnCardTriggerClicked(UIObject sender) {
    // allow therapist to move verb card
    if (Globals.isTherapist) {
      var msg = new Message();
      msg.action = Action.MoveVerbCard;
      msg.triggerIndex = sender.objectID;
      SendMessage(Packet.Serialize(msg));
    }
  }

  public void OnContinuePressed() {
    Globals.mainApp.OpenApp("gameBoard_Semantic3_2");

    // TODO: does the app ID even matter? do a check to make sure sending
    // an ID of an app not open doesn't work
    var header = new MessageHeader(MessageHeader.HEADER_PROTOCOL_APP_PARAMS);
    // header.appID = (byte)nextAppID;
    
    var msg = new Semantic3_2App.ParamsMessage();

    // TODO: this is only a single verb, do we need to send more than 1?
    msg.verb = verbs[currentVerb];

    msg.whos = new string[3];
    msg.whos[0] = inputFields[0].text;
    msg.whos[1] = inputFields[1].text;
    msg.whos[2] = inputFields[2].text;

    msg.whats = new string[3];
    msg.whats[0] = inputFields[3].text;
    msg.whats[1] = inputFields[4].text;
    msg.whats[2] = inputFields[5].text;

    SendMessage(Packet.Serialize(msg, header));
  }

  public void OnSelectVerb() {
    currentVerb = selectVerbPopup.items.IndexOf(selectVerbPopup.value);

    var msg = new Message();
    msg.action = Action.ChangeVerb;
    msg.text = verbs[currentVerb];
    SendMessage(Packet.Serialize(msg));

    nextVerbButton.gameObject.SetActive(currentVerb < verbs.Count - 1);
  }

  public void OnNextVerb() {
    currentVerb += 1;

    var msg = new Message();
    msg.action = Action.ChangeVerb;
    msg.text = verbs[currentVerb];
    SendMessage(Packet.Serialize(msg));

    nextVerbButton.gameObject.SetActive(currentVerb < verbs.Count - 1);
  }

  public void OnInputChanged(UIInput sender) {
    UpdateInputTimer(sender, delegate(object from) {
        var input = (UIInput)from;
        var obj = input.gameObject.GetComponent<UIObject>();

        var msg = new Message();
        if (obj.objectKind == FIELD_KIND_HINT) {
          msg.action = Action.UpdateHint;
        } else {
          msg.action = Action.UpdateInput;
        }
        msg.text = input.text;
        msg.inputIndex = obj.objectID - 1;
        SendMessage(Packet.Serialize(msg));
        // Debug.Log("got input:"+input.text+" "+msg.inputIndex);
      }
    );
  }

  public void OnHintChanged(GameObject sender) {
    var value = sender.GetComponent<UIPopupList>().value;
    var obj = sender.GetComponent<UIObject>();

    var msg = new Message();
    msg.action = Action.UpdateHintFromPopup;
    msg.text = sender.GetComponent<UIPopupList>().value;
    msg.inputIndex = obj.objectID - 1;
    SendMessage(Packet.Serialize(msg));
  }

  public void OnInputFieldClicked(GameObject sender) {
    if (Globals.isTherapist) {
      var obj = sender.GetComponent<UIObject>();
      var input = inputFields[obj.objectID - 1];

      // send focus message if the field changed
      if (activeFocus != input) {
        var msg = new Message();
        msg.action = Action.FocusField;
        msg.inputIndex = obj.objectID - 1;
        msg.inputKind = obj.objectKind;
        SendMessage(Packet.Serialize(msg));
      }
    }
  }

  protected override void HandleLoad () {

    // TODO: put this into compile time code
    verbs = new List<string>(new string[]{ 
      "throw", 
      "drive",
      "eat",
      "measure",
      "deliver",
      "read",
      "wash",
      "watch",
      "chop"
      });

    // make lookup table for hints
    hintsTable = new Dictionary<string, VerbInfo>();
    foreach (var word in verbs) {
      VerbInfo info;
      foreach (var hint in hints) {
        if (hint.word == word) {
          hintsTable[word] = hint;
          break;
        }
      }
    }
  }

  protected override void HandleShow() {

    currentVerb = 0;

    // prepare gui state
    hintPanel.SetActive(false);
    toggleHintsButton.gameObject.SetActive(false);

    var label = verbCard.GetComponentInChildren<UILabel>();
    label.text = verbs[currentVerb];

    // populate verbs popup
    selectVerbPopup.items = verbs;

    // hide gui for patient
    if (Globals.isPatient) {
      nextVerbButton.gameObject.SetActive(false);
      selectVerbPopup.gameObject.SetActive(false);
      continueButton.gameObject.SetActive(false);
      
      foreach (var popup in hintPopups) {
        popup.gameObject.SetActive(false);
      }
      foreach (var input in hintFields) {
        input.MakeReadOnly();
      }
      foreach (var input in inputFields) {
        input.MakeReadOnly();
      }
    }

    foreach (var input in hintFields) {
      input.MakeMultiLine();
    }

    foreach (var input in inputFields) {
      input.MakeMultiLine();
    }
  }

  protected override void HandleMessage (byte[] bytes) {
    var msg = Packet.Deserialize<Message>(bytes);
    var header = Packet.GetHeader(bytes);

    UILabel label;
    UIInput input;

    switch (msg.action) {
      case Action.UpdateInput:
        if (header.IsRemote()) {
          input = inputFields[msg.inputIndex];
          input.text = msg.text;
        }
        break;

      case Action.UpdateHintFromPopup:
        hintFields[msg.inputIndex].text = msg.text;
        break;

      case Action.UpdateHint:
        if (header.IsRemote()) {
          hintFields[msg.inputIndex].text = msg.text;
        }
        break;

      case Action.FocusField:
        input = inputFields[msg.inputIndex];
        input.isSelected = true;
        ClearFocus();
        GiveFocus(input);

        // clear hint fields
        foreach (var hintInput in hintFields) {
          hintInput.text = "";
        }

        // populate hint popups
        var hint = hintsTable[verbs[currentVerb]];
        if (msg.inputKind == FIELD_KIND_WHO) {
          hintPopups[0].items = hint.GetWhoOptions();
          hintPopups[1].items = hint.GetWhoOptions();
          hintPopups[2].items = hint.GetWhoOptions();
        } else if (msg.inputKind == FIELD_KIND_WHAT) {
          hintPopups[0].items = hint.GetWhatOptions();
          hintPopups[1].items = hint.GetWhatOptions();
          hintPopups[2].items = hint.GetWhatOptions();
        } else {
          Globals.FatalError("invalid field kind");
        }

        // show the toggle hints button for therapist
        // if there is a focused field
        if (Globals.isTherapist) {
          toggleHintsButton.gameObject.SetActive(true);
        }

        break;

      case Action.ChangeVerb:
        label = verbCard.GetComponentInChildren<UILabel>();
        label.text = msg.text;

        // hide hint panel and reset text
        HideHintPanel();
        foreach (var hintInput in hintFields) {
          hintInput.text = "";
        }

        // clear input fields
        foreach (var i in inputFields) {
          i.text = "";
        }

        // clear active focus so the hint panel gets reset
        ClearFocus();

        break;

      case Action.ToggleHintPanel:
        if (hintPanel.activeSelf) {
          HideHintPanel();
        } else {
          ShowHintPanel();
        }
        break;

      case Action.MoveVerbCard:
        var collider = verbCardTriggers[msg.triggerIndex - 1].GetComponent<BoxCollider>();
        var dest = collider.bounds.center;

        if (moveCardCoroutine != null) StopCoroutine(moveCardCoroutine);

        moveCardCoroutine = StartCoroutine(AnimateCard(verbCard.transform, dest));
        break;
    }
  }

}
