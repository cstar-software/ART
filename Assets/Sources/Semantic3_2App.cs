using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Byn.Awrtc;
using Byn.Awrtc.Unity;

public class Semantic3_2App : BaseApp {

  // gui
  public UILabel titleLabel;
  public UIInput[] inputFields;
  public UIPopupList chooseRowPopup;
  public UIButton continueButton;
  public UIButton nextQuestionButton;
  public GameObject[] questionGroups;

  private enum Action {
    FocusField,
    UpdateInput,
    ChangeRow,
    NextQuestion
  }

  [Serializable]
  public struct ParamsMessage {
    public string[] whos;
    public string[] whats;
    public string verb;
  }

  [Serializable]
  private struct Message {
    public Action action;
    public int index;
    public string text;
  }

  [Serializable]
  private struct RestoreState {
  }

  // state 
  private ParamsMessage currentState;

  public void OnInputFieldChanged(UIInput sender) {
    UpdateInputTimer(sender, delegate(object from) {
        var input = (UIInput)from;
        var obj = sender.gameObject.GetComponent<UIObject>();
        var msg = new Message();
        msg.action = Action.UpdateInput;
        msg.text = input.label.text;
        msg.index = obj.objectID - 1;
        SendMessage(Packet.Serialize(msg));
        // Debug.Log("got input:"+msg.questionText+" "+msg.soundID);
      }
    );
  }

  public void OnInputFieldClicked(UIInput sender) {
    if (Globals.isTherapist) {
      var obj = sender.GetComponent<UIObject>();
      var input = inputFields[obj.objectID - 1];

      // send focus message if the field changed
      if (activeFocus != input) {
        var msg = new Message();
        msg.action = Action.FocusField;
        msg.index = obj.objectID - 1;
        SendMessage(Packet.Serialize(msg));
      }
    }
  }

  protected override void TabFocus(MonoBehaviour sender) {
    if (Globals.isTherapist) {
      var obj = sender.GetComponent<UIObject>();
      var msg = new Message();
      msg.action = Action.FocusField;
      msg.index = obj.objectID - 1;
      SendMessage(Packet.Serialize(msg));
    }
  }

  public void OnNextQuestionPressed() {
    var msg = new Message();
    msg.action = Action.NextQuestion;
    SendMessage(Packet.Serialize(msg));
  }

  public void OnContinuePressed() {
    Globals.mainApp.OpenApp("gameBoard_Semantic3_3");

    var header = new MessageHeader(MessageHeader.HEADER_PROTOCOL_APP_PARAMS);    
    var msg = new Semantic3_3App.ParamsMessage();
    msg.verb = currentState.verb;
    SendMessage(Packet.Serialize(msg, header));
  }

  public void OnChooseRow() {
    var msg = new Message();
    msg.action = Action.ChangeRow;
    msg.index = chooseRowPopup.items.IndexOf(chooseRowPopup.value);
    SendMessage(Packet.Serialize(msg));
  }

  private void ChangeRow(int row) {
    titleLabel.text = currentState.whos[row]+" - "+currentState.verb+" - "+currentState.whats[row];

    // clear inputs
    foreach (var input in inputFields) {
      input.text = "";
    }
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

  protected override void HandleParams(byte[] bytes) {
    currentState = Packet.Deserialize<ParamsMessage>(bytes);

    ChangeRow(0);

    // build rows popup
    var items = new List<string>();
    for (int i = 0; i < 3; i++) {
      items.Add(currentState.whos[i]+" - "+currentState.whats[i]);
    }
    chooseRowPopup.items = items;

  }

  protected override void HandleLoad () {
  }

  protected override void HandleShow() {

    // hide gui for patient
    // TODO: use UIVisiblity once we factor it out for BaseApp
    if (Globals.isPatient) {
      chooseRowPopup.gameObject.SetActive(false);
      continueButton.gameObject.SetActive(false);
      nextQuestionButton.gameObject.SetActive(false);

      foreach (var input in inputFields) {
        input.MakeReadOnly();
      }
    }

    foreach (var group in questionGroups) {
      group.SetActive(false);
    }

    // start with first question shown
    questionGroups[0].SetActive(true);
  }

  protected override void HandleMessage (byte[] bytes) {
    var msg = Packet.Deserialize<Message>(bytes);
    var header = Packet.GetHeader(bytes);

    UIInput input;

    switch (msg.action) {
      case Action.NextQuestion:
        int shown = 0;
        foreach (var group in questionGroups) {
          if (!group.activeSelf) {
            shown += 1;
            group.SetActive(true);
            // give the input focus
            input = group.GetComponentInChildren<UIInput>();
            ClearFocus();
            GiveFocus(input);
            break;
          } else {
            shown += 1;
          }
        }
        // hide the button if all groups are shown
        if (shown == questionGroups.Length) {
          nextQuestionButton.gameObject.SetActive(false);
        }
        break;
      case Action.ChangeRow:
        ChangeRow(msg.index);
        break;
      case Action.UpdateInput:
        if (header.IsRemote()) {
          input = inputFields[msg.index];
          input.text = msg.text;
        }
        break;
      case Action.FocusField:
        ClearFocus();
        input = inputFields[msg.index];
        GiveFocus(input);
        break;
    }

  }

}
