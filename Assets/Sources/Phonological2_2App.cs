using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Byn.Awrtc;
using Byn.Awrtc.Unity;

public class Phonological2_2App : BaseApp {

  [Serializable]
  public struct ParamsMessage {
    public List<int> cards; // list of card indexes to select options from
  }

  // gui
  public UITexture card1Texture;
  public UITexture card2Texture;
  public UIPopupList testPopup;
  public UIInput[] syllableFields;
  public UIInput answerField;
  public UILabel testNameLabelLeft;
  public UILabel testNameLabelRight;
  public UIButton nextPairButton;

  // state
  private int currentTest;
  private Texture2D emptyCardTexture;

  // data
  private class ImageInfo {
    public string name;
    public string extension;
    public int syllables;
    public Texture2D texture;
    public int index;
    public static int imageIndexCounter = 0;
    public bool IsMultiSyllabic() {
      return syllables > 1;
    }
    public string GetFullName() {
      return name+"."+extension;
    }
    public ImageInfo(string name, int syllables, bool advanceCounter = false) {
      this.name = name;
      this.syllables = syllables;
      this.extension = "jpg";

      // increment card index
      if (advanceCounter) {
        this.index = imageIndexCounter;
        imageIndexCounter += 1;
      } else {
        this.index = -1;
      }
    }
  }

  private ImageInfo[] imageInfo;

  private List<ImageInfo> singleSyllabicImages;
  private List<ImageInfo> multiSyllabicImages;

  private class TestSpec {
    public string leftName;
    public string rightName;
    public bool leftMultiSyllabic;
    public bool rightMultiSyllabic;
    public int leftCardIndex;
    public int rightCardIndex;
    public Texture2D leftCardTexture;
    public Texture2D rightCardTexture;
    public string GetDisplayName() {
      return leftName + " - " + rightName;
    }
    public TestSpec(string leftName, string rightName, bool first, bool second) {
      this.leftName = leftName;
      this.rightName = rightName;
      this.leftMultiSyllabic = first;
      this.rightMultiSyllabic = second;
      this.leftCardIndex = -1;
      this.rightCardIndex = -1;
      this.leftCardTexture = null;
      this.rightCardTexture = null;
    }
  }
  private TestSpec[] tests = new TestSpec[] {
    new TestSpec("First Syllable", "First Syllable", true, true),
    new TestSpec("First Syllable", "Last Syllable", true, true),
    new TestSpec("Last Syllable", "Last Syllable", true, true),
    new TestSpec("Last Syllable", "First Syllable", true, true),
    new TestSpec("First Syllable", "First Sound", true, false),
    new TestSpec("Last Syllable", "Last Sound", true, false),
    new TestSpec("First Syllable", "Last Sound", true, false),
    new TestSpec("Last Syllable", "First Sound", true, false)
  };

  private Texture2D[] questionImages;

  private const int INVALID_TEST_INDEX = -1;

  private enum Action { 
    UpdateInput,
    FocusField,
    FocusCard,
    TestChanged,
    NextPair,
    ExposeCard
  }

  [Serializable]
  private struct Message {
    public Action action;
    public string text;
    public int inputIndex;
    public int testIndex;
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

  private UIInput GetInputByIndex(int index) {
    UIInput input = null;
    switch (index) {
      case 1:
        input = syllableFields[0];
        break;
      case 2:
        input = syllableFields[1];
        break;
      case 3:
        input = answerField;
        break;
      default: 
        Globals.FatalError("invalid input index");
        break;
    }
    return input;
  }

  protected override void TabFocus(MonoBehaviour sender) {
    if (Globals.isTherapist) {
      var msg = new Message();
      var obj = sender.gameObject.GetComponent<UIObject>();
      msg.action = Action.FocusField;
      msg.inputIndex = obj.objectID;
      SendMessage(Packet.Serialize(msg));
    }
  }

  public void OnInputChanged(UIInput sender) {
    UpdateInputTimer(sender, delegate(object from) {
        var input = (UIInput)from;
        var obj = input.gameObject.GetComponent<UIObject>();
        var msg = new Message();
        msg.action = Action.UpdateInput;
        msg.text = input.text;
        msg.inputIndex = obj.objectID;
        SendMessage(Packet.Serialize(msg));
        // Debug.Log("got input:"+input.text+" "+msg.inputIndex);
      }
    );
  }

  public void OnSelectCard(UIButton sender) {
    if (Globals.isTherapist && currentTest != INVALID_TEST_INDEX) {
      var obj = sender.GetComponent<UIObject>();
      if (activeFocus != sender) {
        var msg = new Message();
        msg.action = Action.FocusCard;
        msg.inputIndex = obj.objectID;
        SendMessage(Packet.Serialize(msg));

        // expose right card image
        if (obj.objectID == 2) {
          msg = new Message();
          msg.action = Action.ExposeCard;
          SendMessage(Packet.Serialize(msg));
        }

      }
    }
  }

  public void OnInputFieldClicked(GameObject sender) {
    if (Globals.isTherapist && currentTest != INVALID_TEST_INDEX) {
      var obj = sender.GetComponent<UIObject>();
      var input = GetInputByIndex(obj.objectID);
      // // send focus message if the field changed
      if (activeFocus != input) {
        var msg = new Message();
        msg.action = Action.FocusField;
        msg.inputIndex = obj.objectID;
        SendMessage(Packet.Serialize(msg));
      }
    }
  }

  public void OnTestChanged() {
    var msg = new Message();
    msg.action = Action.TestChanged;
    msg.testIndex = testPopup.items.IndexOf(testPopup.value);
    SendMessage(Packet.Serialize(msg));
  }

  public void OnNextPair() {
    if (Globals.isTherapist && currentTest != INVALID_TEST_INDEX) {
      var msg = new Message();
      msg.action = Action.NextPair;
      SendMessage(Packet.Serialize(msg));
    }
  }

  private void LoadQuestionImages(string path) {
    questionImages = Resources.LoadAll<Texture2D>(path);       
  }

  private void ShuffleCards() {
    singleSyllabicImages = new List<ImageInfo>();
    multiSyllabicImages = new List<ImageInfo>();

    foreach (var info in imageInfo) {
      // build syllable count lists
      if (info.syllables == 1) {
        singleSyllabicImages.Add(info);
      } else {
        multiSyllabicImages.Add(info);
      }
    }
  }

  protected override void HandleLoad () {

    LoadQuestionImages("Test1");

    // get the empty card texture from the card texture upon loading
    emptyCardTexture = (Texture2D)card1Texture.mainTexture;

    // NOTE: loading this here instead of static because
    // it was getting called twice
    imageInfo = new ImageInfo[] {
      new ImageInfo("img15",  2, true),
      new ImageInfo("img16",  1, true),
      new ImageInfo("img17",  2, true),
      new ImageInfo("img18",  1, true),
      new ImageInfo("img19",  1, true),
      new ImageInfo("img20",  2, true),
      new ImageInfo("img22",  1, true),
      new ImageInfo("img24",  1, true),
      new ImageInfo("img26",  2, true),
      new ImageInfo("img28",  1, true),
      new ImageInfo("img30",  2, true),
      new ImageInfo("img32",  2, true),
      new ImageInfo("img35",  2, true),
      new ImageInfo("img39",  1, true),
      new ImageInfo("img41",  3, true),
      new ImageInfo("img45",  1, true),
      new ImageInfo("img47",  1, true),
      new ImageInfo("img51",  1, true),
      new ImageInfo("img56",  1, true),
      new ImageInfo("img58",  1, true),
      new ImageInfo("img60",  1, true),
      new ImageInfo("img62",  2, true),
      new ImageInfo("img64",  1, true),
      new ImageInfo("img66",  3, true),
      new ImageInfo("img71",  3, true),
      new ImageInfo("img73",  4, true),
      new ImageInfo("img75",  1, true),
      new ImageInfo("img77",  3, true),
      new ImageInfo("img79",  4, true),
      new ImageInfo("img81",  2, true),
      new ImageInfo("img84",  2, true),
      new ImageInfo("img86",  4, true),
      new ImageInfo("img88",  3, true),
      new ImageInfo("img90",  1, true),
      new ImageInfo("img92",  1, true),
      new ImageInfo("img94",  2, true),
      new ImageInfo("img97",  2, true),
      new ImageInfo("img99",  2, true),
      new ImageInfo("img101", 2, true),
      new ImageInfo("img103", 2, true),
      new ImageInfo("img105", 2, true),
      new ImageInfo("img107", 2, true),
      new ImageInfo("img110", 1, true),
      new ImageInfo("img112", 1, true),
      new ImageInfo("img114", 1, true),
      new ImageInfo("img116", 1, true),
      new ImageInfo("img118", 1, true),
      new ImageInfo("img120", 1, true),
      new ImageInfo("img123", 1, true),
      new ImageInfo("img125", 1, true),
      new ImageInfo("img129", 3, true),
      new ImageInfo("img131", 1, true),
      new ImageInfo("img133", 3, true),
      new ImageInfo("img135", 2, true),
      new ImageInfo("img138", 1, true),
      new ImageInfo("img140", 1, true),
      new ImageInfo("img142", 1, true),
      new ImageInfo("img144", 2, true),
      new ImageInfo("img146", 1, true),
      new ImageInfo("img148", 2, true)
    };


    // assign textures to images
    foreach (var info in imageInfo) {
      foreach (var texture in questionImages) {
        if (info.name == texture.name) {
          info.texture = texture;
        }
      }
    }

    // populate test popup items
    var items = new List<string>();
    foreach (var test in tests) {
      items.Add(test.GetDisplayName());
    }
    testPopup.items = items;

    // hide gui for patient
    if (Globals.isPatient) {
      testPopup.gameObject.SetActive(false);
      nextPairButton.gameObject.SetActive(false);

      syllableFields[0].MakeReadOnly();
      syllableFields[1].MakeReadOnly();
      answerField.MakeReadOnly();
    }
  }

  protected override void HandleShow() {
    // start with an invalid test
    currentTest = INVALID_TEST_INDEX;

  }

  private ImageInfo GetRandomCard(int except, bool multiSyllabic) {
    int index = -1;
    ImageInfo info;

    while (true) {
      if (multiSyllabic) {
        if (multiSyllabicImages.Count == 0) {
          Globals.FatalError("GetRandomCard failed");
          break;
        }
        index = Globals.GetRandomNumber(0, multiSyllabicImages.Count);
        info = multiSyllabicImages[index];
        if (info.index != except) {
          return info;
        } else if (info.index == except && multiSyllabicImages.Count == 1) {
          Globals.FatalError("GetRandomCard failed, not enough cards");
          break;
        }
      } else {
        if (singleSyllabicImages.Count == 0) {
          Globals.FatalError("GetRandomCard failed");
          break;
        }
        index = Globals.GetRandomNumber(0, singleSyllabicImages.Count);
        info = singleSyllabicImages[index];
        if (info.index != except) {
          return info;
        } else if (info.index == except && singleSyllabicImages.Count == 1) {
          Globals.FatalError("GetRandomCard failed, not enough cards");
          break;
        }
      }
    }

    return new ImageInfo("",0);
  }

  private void AdvanceToNextPair(TestSpec test) {

    // Debug.Log("current pair: "+test.leftCardIndex+"/"+test.rightCardIndex+" imageInfo:"+imageInfo.Length);

    // remove previous pair
    if (test.leftCardIndex != -1 && test.rightCardIndex != -1) {
      if (imageInfo[test.leftCardIndex].IsMultiSyllabic()) {
        multiSyllabicImages.Remove(imageInfo[test.leftCardIndex]);
      } else {
        singleSyllabicImages.Remove(imageInfo[test.leftCardIndex]);
      }

      if (imageInfo[test.rightCardIndex].IsMultiSyllabic()) {
        multiSyllabicImages.Remove(imageInfo[test.rightCardIndex]);
      } else {
        singleSyllabicImages.Remove(imageInfo[test.rightCardIndex]);
      }
    }

    // TODO: we can infer leftMultiSyllabic using card index
    var card1 = GetRandomCard(-1, test.leftMultiSyllabic);
    var card2 = GetRandomCard(card1.index, test.rightMultiSyllabic);

    card1Texture.mainTexture = card1.texture;
    card2Texture.mainTexture = emptyCardTexture;

    test.leftCardIndex = card1.index;
    test.rightCardIndex = card2.index;
    test.leftCardTexture = card1.texture;
    test.rightCardTexture = card2.texture;

    syllableFields[0].text = "";
    syllableFields[1].text = "";
    answerField.text = "";
  }

  protected override void HandleParams(byte[] bytes) {
    var msg = Packet.Deserialize<ParamsMessage>(bytes);
    Debug.Log("Phonological2_2App got params #"+msg.cards.Count);

    singleSyllabicImages = new List<ImageInfo>();
    multiSyllabicImages = new List<ImageInfo>();

    foreach (var info in imageInfo) {
      // build syllable count lists
      if (info.syllables == 1) {
        singleSyllabicImages.Add(info);
      } else {
        multiSyllabicImages.Add(info);
      }
      // assign texture to image info
      foreach (var texture in questionImages) {
        if (info.name == texture.name) {
          info.texture = texture;
        }
      }
    }

  }

  protected override void HandleMessage (byte[] bytes) {
    var msg = Packet.Deserialize<Message>(bytes);
    UIInput input;
    TestSpec test;

    switch (msg.action) {

      case Action.ExposeCard:
        test = tests[currentTest];
        card2Texture.mainTexture = test.rightCardTexture;
        break;

      case Action.FocusCard:
        ClearFocus();
        if (msg.inputIndex == 1) {
          GiveFocus(card1Texture);
        } else if (msg.inputIndex == 2) {
          GiveFocus(card2Texture);
        }
        break;

      case Action.FocusField:
        input = GetInputByIndex(msg.inputIndex);
        input.isSelected = true;
        ClearFocus();
        GiveFocus(input);
        break;

      case Action.NextPair:
        test = tests[currentTest];
        AdvanceToNextPair(test);
        ClearFocus();
        
        // test testspec to see if there are enough cards
        var validNextPair = ((test.leftMultiSyllabic && multiSyllabicImages.Count > 2)    ||
           (!test.leftMultiSyllabic && singleSyllabicImages.Count > 2)   ||
           (test.rightMultiSyllabic && multiSyllabicImages.Count > 2)    ||
           (!test.rightMultiSyllabic && singleSyllabicImages.Count > 2));

        if (Globals.isTherapist) {
          nextPairButton.gameObject.SetActive(validNextPair);
        }

        break;

      case Action.TestChanged:

        currentTest = msg.testIndex;
        test = tests[currentTest];

        ShuffleCards();
        AdvanceToNextPair(test);

        if (Globals.isTherapist) {
          nextPairButton.gameObject.SetActive(true);
        }

        testNameLabelLeft.text = test.leftName;
        testNameLabelRight.text = test.rightName;
        break;

      case Action.UpdateInput:
        input = GetInputByIndex(msg.inputIndex);
        input.text = msg.text;
        break;
    }

  }

}
