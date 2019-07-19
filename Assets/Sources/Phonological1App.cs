using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Byn.Awrtc;
using Byn.Awrtc.Unity;

public class Phonological1App : BaseApp {

  private enum Action {
    Invalid,
    FocusField,
    HilightCard,
    RemoveHilightCard,
    ChangeQuestion,
    UpdateInput,
    ShowCardName,
    ToggleQuestionVisiblity
  }

  [Serializable]
  private struct Message {
    public Action action;
    public string questionText;
    public string soundID;
    public int questionID;
    public string fieldName;
  }

  [Serializable]
  private struct RestoreState {
    public int questionID;
    public Dictionary<string,string> questions;
  }

  // gui
  public UITexture questionImage;
  public Sprite[] questionImages;
  public UIButton nextButton;
  public UIButton previousButton;
  public UIButton showCardNameButton;
  public UILabel[] questionLabels;
  public UILabel questionNameLabel;
  public UIPopupList selectQuestionPopup;
  public UIPopupList questionVisibilityPopup;
  public GameObject[] questionBoxes;

  // private
  // TODO: this ended up not being needed. just input field indexes instead and kill the dictionary
  private Dictionary<string, UIInput> soundFields;
  private string[] questionNames;
  private string[] cardNames;

  //state
  private int currentQuestionID = 0;

  // tags
  private const string TAG_SOUND_FIELD = "SoundField";

  private int GetQuestionCount() {
    return questionImages.Length;
  }

  private void UpdateGUIState() {
    nextButton.isEnabled = (currentQuestionID < GetQuestionCount() - 1);
    previousButton.isEnabled = (currentQuestionID > 0);
  }

  protected override void HandleRestoreState (byte[] bytes) {
    var state = Packet.Deserialize<RestoreState>(bytes);
    DebugLog.AddEntry("restore app state");

    questionImage.mainTexture = questionImages[state.questionID].texture;

    currentQuestionID = state.questionID;

    foreach (var key in state.questions.Keys) {
      soundFields[key].text = state.questions[key];
    }
    UpdateGUIState();
  }

  protected virtual void LoadQuestionNames() {

    Debug.Log("Load question names for app ID:"+appID);

    switch (GetName()) {
      case Gameboard.Phonological1: {
        questionNames = new string[] { // row 1
                                        "First Sound", 
                                        "Number of Syllables",
                                        "Last Sound",
                                        // row 2
                                        "Rhyming Word",
                                        "Vowel in First Syllable",
                                        "Vowel in Last Syllable"
                                      };
        cardNames = new string[] {
          "Shoulder","Brain","Ankle","Bus","Arm","Breakfast","Egg","Pan","Butter","Leaf","Eyebrow","Elbow","Finger","Heart",
          "Skeleton","Blood","Ear","Mouth","Tongue","Eye","Nose","Airplane","Toe","Bulldozer","Ambulance","Elevator","Crane",
          "Bicycle","Helicopter","Ferry","Train tracks","Motorcycle","Submarine","Train","Tank","Trolley","Bacon","Wheelchair",
          "Cherry","Biscuit","Lettuce","Lemon","Beans","Cut","Drive","Jump","Swim","Stir","Throw","Eat","Deliver","Melt","Tomato",
          "Carry","Straw","Pear","Orange","Onion","Milk","Napkin"
        };
        questionImages = ImageStore.LoadAll("Test1");
        break;
      }
      case Gameboard.Semantic1: {
        questionNames = new string[] { // row 1
                                       "Category", 
                                       "Use",
                                       "Action",
                                       // row 2
                                       "Description",
                                       "Location",
                                       "Association"
                                      };
        cardNames = new string[] {
          "Barn","Belt","Key","Book","Kick","Button","Camel","Letter","Coat","Library","Chef","Lion","Dance","Lawyer",
          "Jail","Drink","Fish","Sing","Fly","Forest","Policeman","Judge","Bird","Bracelet","Laugh","Dancer","Cat",
          "Comb","Lipstick","Cow","Mountain","Stadium","Sheep","Flashlight","Helmet","Shoe","Teacher","Prisoner",
          "Dress","Pyramid","Quarter","Sit","Toothbrush","Shirt","Pool","Shout","Pants","Sleep","Volcano","Snake",
          "Soldier","Spider","Necklace","Suit","Teach","Priest","Tie","Nurse","Smile","Waterfall"
        };                                      
        questionImages = ImageStore.LoadAll("Test2");
        break;
      }
      default: {
        Globals.FatalError("invalid app name");
        break;
      }
    }
  }

  protected override void HandleLoad () {
    LoadQuestionNames();

    UpdateQuestionVisiblityPopupList();

    // populate card chooser popup
    var items = new List<string>();
    for (int i = 0; i < cardNames.Length; i++) {
      var name = "#"+(i + 1)+" "+cardNames[i];
      items.Add(name);
    }
    selectQuestionPopup.items = items;

  }

  protected override void HandleShow() {

    questionNameLabel.gameObject.SetActive(false);

    // load sound fields sorted by name
    soundFields = new Dictionary<string, UIInput>();
    var objects = GameObject.FindGameObjectsWithTag(TAG_SOUND_FIELD);
    foreach (var obj in objects) {
      var input = obj.GetComponent<UIInput>();
      soundFields[obj.name] = input;
      input.MakeMultiLine();
    }

    // fill text fields with question names
    for (int i = 0; i < questionNames.Length; i++) {
      questionLabels[i].text = questionNames[i];
    }

    // prepare gui for client
    if (Globals.isPatient) {
      selectQuestionPopup.gameObject.SetActive(false);
      showCardNameButton.gameObject.SetActive(false);
      questionVisibilityPopup.gameObject.SetActive(false);
      // make fields read-only
      nextButton.gameObject.SetActive(false);
      previousButton.gameObject.SetActive(false);
      foreach (var field in soundFields.Values) {
        field.MakeReadOnly();

        // inputs have a button component for focus
        // which needs to be disabled
        var button = field.gameObject.GetComponentInChildren<UIButton>();
        button.enabled = false;
      }
    } else {
      // show card button for therapist if we're semantic 1
      if (GetName() == Gameboard.Semantic1) {
        showCardNameButton.gameObject.SetActive(true);
      }
    }

    // send startup message
    var msg = new Message();
    msg.action = Action.ChangeQuestion;
    msg.questionID = 0;
    SendMessage(Packet.Serialize(msg));
  }

  public void ChangeQuestion(int questionID) {

    // show card button for therapist if we're semantic 1
    if (Globals.isTherapist && GetName() == Gameboard.Semantic1) {
      showCardNameButton.gameObject.SetActive(true);
    }

    currentQuestionID = questionID;
    questionImage.mainTexture = questionImages[questionID].texture;
    questionNameLabel.gameObject.SetActive(false);
    foreach (UIInput field in soundFields.Values) {
      field.text = "";
    }
    stats.Push("question", new string[]{questionID.ToString()});
    // stats.PushSession("sound:"+msg.soundID);
    UpdateGUIState();
  }

  protected override void HandleMessage (byte[] bytes) {
    var msg = Packet.Deserialize<Message>(bytes);
    var header = Packet.GetHeader(bytes);

    UISprite sprite;
    UIInput input;

    switch (msg.action) {
      case Action.ToggleQuestionVisiblity:
        questionBoxes[msg.questionID].SetActive(!questionBoxes[msg.questionID].activeSelf);
        UpdateQuestionVisiblityPopupList();
        break;
      case Action.ShowCardName:
        // hide the button now
        showCardNameButton.gameObject.SetActive(false);

        // show card name
        questionNameLabel.text = cardNames[currentQuestionID];
        questionNameLabel.gameObject.SetActive(true);
        break;
      case Action.HilightCard:
        ClearFocus();

        // TODO: standardize this
        sprite = questionImage.transform.parent.GetComponent<UISprite>();
        sprite.color = new Color(0f, 0.9f, 0f, 1.0f);
        break;
      case Action.RemoveHilightCard:
        sprite = questionImage.transform.parent.GetComponent<UISprite>();
        sprite.color = Color.white;
        break;
      case Action.FocusField:
        ClearFocus();
        input = soundFields[msg.fieldName];
        if (!input.isSelected) {
          input.isSelected = true;
        }
        GiveFocus(input);
        UnhilightCard();
        break;
      case Action.ChangeQuestion:
        // Debug.Log("new question: "+msg.questionID);
        ChangeQuestion(msg.questionID);
        UnhilightCard();
        break;
      case Action.UpdateInput:
        if (header.IsRemote()) {
          // Debug.Log("got answer:"+msg.soundID+" = "+msg.questionText);
          input = soundFields[msg.soundID];
          input.text = msg.questionText;
        }
        break;
    }
  }

  private void UnhilightCard() {
    var msg = new Message();
    msg.action = Action.RemoveHilightCard;
    SendMessage(Packet.Serialize(msg));
  }

  public void OnSelectQuestionImage() {
    if (Globals.isTherapist) {
      var msg = new Message();
      msg.action = Action.HilightCard;
      SendMessage(Packet.Serialize(msg));
    }
  }

  public void OnShowCardName() {
    var msg = new Message();
    msg.action = Action.ShowCardName;
    SendMessage(Packet.Serialize(msg));
  }

  private void UpdateQuestionVisiblityPopupList() {
    var items = new List<string>();
    for (int i = 0; i < questionNames.Length; i++) {
      var name = questionNames[i];
      if (!questionBoxes[i].activeSelf) name = "* "+name;
      items.Add(name);
    }
    questionVisibilityPopup.items = items;
  }

  public void OnChangeQuestionVisibility() {
    var msg = new Message();
    msg.action = Action.ToggleQuestionVisiblity;
    msg.questionID = questionVisibilityPopup.items.IndexOf(questionVisibilityPopup.value);
    SendMessage(Packet.Serialize(msg));
  }

  public void OnSelectQuestion() {
    var msg = new Message();
    msg.action = Action.ChangeQuestion;
    msg.questionID = selectQuestionPopup.items.IndexOf(selectQuestionPopup.value);
    SendMessage(Packet.Serialize(msg));
  }

  public void OnNextQuestion() {
    if (currentQuestionID < GetQuestionCount()) {
      var msg = new Message();
      msg.action = Action.ChangeQuestion;
      msg.questionID = currentQuestionID + 1;
      SendMessage(Packet.Serialize(msg));
    }
  }

  public void OnPreviousQuestion() {
    if (currentQuestionID > 0) {
      var msg = new Message();
      msg.action = Action.ChangeQuestion;
      msg.questionID = currentQuestionID - 1;
      SendMessage(Packet.Serialize(msg));
    }
  }

  public void OnSoundValueChanged(UIInput sender) {
    UpdateInputTimer(sender, delegate(object from) {
        var input = (UIInput)from;
        var msg = new Message();
        msg.action = Action.UpdateInput;
        msg.questionText = input.label.text;
        msg.soundID = input.name;
        msg.questionID = currentQuestionID;
        SendMessage(Packet.Serialize(msg));
        // Debug.Log("got input:"+msg.questionText+" "+msg.soundID);
      }
    );
  }

  protected override void TabFocus(MonoBehaviour sender) {
    if (Globals.isTherapist) {
      var msg = new Message();
      msg.action = Action.FocusField;
      msg.fieldName = sender.name;
      SendMessage(Packet.Serialize(msg));
    }
  }

  public void OnInputFieldClicked(GameObject sender) {
    if (Globals.isTherapist) {
      var msg = new Message();
      msg.action = Action.FocusField;
      msg.fieldName = sender.name;
      SendMessage(Packet.Serialize(msg));
    }
  }

  public override byte[] GetRestoreState() {
    var msg = new RestoreState();

    msg.questionID = currentQuestionID;
    msg.questions = new Dictionary<string, string>();
    foreach (var key in soundFields.Keys) {
      msg.questions[key] = soundFields[key].text;
    }

    var header = new MessageHeader(MessageHeader.HEADER_PROTOCOL_RESTORE);
    header.appID = (byte)this.appID;
    return Packet.Serialize(msg, header);
  }

}
