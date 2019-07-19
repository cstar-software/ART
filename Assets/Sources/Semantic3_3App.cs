using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Byn.Awrtc;
using Byn.Awrtc.Unity;

public class Semantic3_3App : BaseApp {

  // gui
  public UIButton continueButton;

  // state
  private string currentVerb;

  [Serializable]
  private struct Message {
  }

  [Serializable]
  private struct RestoreState {
  }

  [Serializable]
  public struct ParamsMessage {
    public string verb;
  }

  protected override void HandleParams(byte[] bytes) {
    var msg = Packet.Deserialize<ParamsMessage>(bytes);

    currentVerb = msg.verb;
  }

  public void OnContinuePressed() {
    Globals.mainApp.OpenApp("gameBoard_Semantic3_4");

    var header = new MessageHeader(MessageHeader.HEADER_PROTOCOL_APP_PARAMS);    
    var msg = new Semantic3_4App.ParamsMessage();
    msg.verb = currentVerb;
    SendMessage(Packet.Serialize(msg, header));
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


  protected override void HandleLoad () {
  }

  protected override void HandleShow() {
    if (Globals.isPatient) {
      continueButton.gameObject.SetActive(false);
    }
  }

  protected override void HandleMessage (byte[] bytes) {
    var msg = Packet.Deserialize<Message>(bytes);
  }

}
