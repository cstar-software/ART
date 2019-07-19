using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Byn.Awrtc;
using Byn.Awrtc.Unity;


public struct RGBColor {
  public float r, g, b;
}


public class DemoApp : BaseApp {


  private struct Message {
    public MessageHeader header;
    public RGBColor color;
    public int index;
  }

  // gui
  public Image questionImage;
  public Button nextButton;
  public Text answers;

  protected override void HandleShow() {
    questionImage.color = new Color(1, 0, 0);
    Debug.Log("demo app is ready");
  }

  protected override void HandleMessage (byte[] bytes) {
    var msg = Packet.Deserialize<Message>(bytes);
    Debug.Log("got msg index: " + msg.index);
  }

  public void OnEndEdit(GameObject sender) {
    Debug.Log("OnEndEdit:"+sender);
  }
  
  public void DoClick() {
    var msg = new Message();
    msg.color = new RGBColor();
    msg.index = 128;
    SendMessage(Packet.Serialize(msg));
  }

}
