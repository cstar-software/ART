using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Byn.Awrtc;
using Byn.Awrtc.Unity;

public class Semantic3_4App : BaseApp {

  // gui
  public UILabel titleLabel;

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

    titleLabel.text = msg.verb;
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
  }

  protected override void HandleMessage (byte[] bytes) {
    var msg = Packet.Deserialize<Message>(bytes);
  }

}
