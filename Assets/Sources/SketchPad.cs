using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Byn.Awrtc;
using Byn.Awrtc.Unity;

public class SketchPad : MonoBehaviour {

  private LineRenderer[] lineRenderer;
  private Stack<LineRenderer>[] undoStack;
  private bool drawingEnabled;

  public Material therapistPenMaterial;
  public Material patientPenMaterial;

  private Camera activeCamera;

  private struct DrawState {
    public bool first;
    public DateTime startTime;
    public Vector3 lastPoint;
  }

  private DrawState drawState;
  public Texture2D cursorTexture;

  // settings
  // TODO: where is this scaling coming from??
  private float lineWidth = 3 * 0.005f;

  private enum Action {
    AddPoint,
    DiscardLine, 
    EndOfLine,   
    UndoLastLine,
    EraseAll,
    Toggle  
  }

  [Serializable]
  private struct Message {
    public byte action;
    public float x, y;
    public byte target;
  }

  private LineRenderer currentLine {
    get {
      return lineRenderer[(int)Globals.role];
    }
  }

  public void SendToggleMessage(Role target) {
    var msg = new Message();
    msg.action = (byte)Action.Toggle;
    msg.target = (byte)target;
    SendMessage(msg);
  }

  public void SendEraseMessage(Role target) {
    var msg = new Message();
    msg.action = (byte)Action.EraseAll;
    msg.target = (byte)target;
    SendMessage(msg);
  }

  public void HandleMessage(byte[] bytes) {
    var header = Packet.GetHeader(bytes);
    var msg = Packet.Deserialize<Message>(bytes);
    var senderIndex = (int)header.sender;

    LineRenderer line;
    Stack<LineRenderer> stack;

    switch ((Action)msg.action) {

      case Action.DiscardLine:
        line = lineRenderer[senderIndex];
        if (line) {
          Destroy(line.gameObject, 0);
          lineRenderer[senderIndex] = null;
        }
        break;

      case Action.EndOfLine:
        line = lineRenderer[senderIndex];
        if (line == null) Globals.FatalError("end of line on invalid line index");
        stack = undoStack[senderIndex];
        stack.Push(line);
        lineRenderer[senderIndex] = null;
        break;

      case Action.UndoLastLine:
        stack = undoStack[senderIndex];
        if (stack.Count > 0) {
          line = stack.Pop();
          if (line == null) Globals.FatalError("failed to pop empty line");
          Destroy(line.gameObject, 0);
        }
        break;

      case Action.EraseAll:
        stack = undoStack[msg.target];
        while (stack.Count > 0) {
          line = stack.Pop();
          if (line == null) Globals.FatalError("failed to pop empty line");
          Destroy(line.gameObject, 0);
        }
        break;

      case Action.Toggle:
        // toggle the panel if the target is us
        if ((byte)Globals.role == msg.target) {
          Toggle();
        }
        break;

      case Action.AddPoint:
        // TODO: take this from the sketch pad z
        // pos.z = -1.0f;//gameObject.transform.localPosition.z;
        PushPoint(new Vector3(msg.x, msg.y, -1.0f), header.sender);
        break;

      default:
        Globals.FatalError("invalid action");
        break;
    }

  }

  private void PushPoint(Vector3 pos, Role sender) {
    
    // get line for sender
    var line = lineRenderer[(int)sender];

    // create new line
    if (line == null) {
      var parent = gameObject;
      var obj = new GameObject("Line");
      obj.transform.parent = parent.transform;
      obj.layer = 5;
      // obj.hideFlags = HideFlags.HideInHierarchy;

      line = obj.AddComponent<LineRenderer>();
      line.SetWidth(lineWidth, lineWidth);
      // line.SetColors(color, color);
      if (sender == Role.Therapist) {
        line.material = therapistPenMaterial;
      } else {
        line.material = patientPenMaterial;
      }
      line.useWorldSpace = true;

      line.positionCount = 1;
      line.SetPosition(0, pos);

      lineRenderer[(int)sender] = line;
    }

    line.positionCount = line.positionCount + 1;
    line.SetPosition(line.positionCount - 1, pos);
  }

  private void SendPoint(Vector3 pos) {
    var msg = new Message();
    msg.action = (byte)Action.AddPoint;
    msg.x = pos.x;
    msg.y = pos.y;
    SendMessage(msg);
  }

  private void SendMessage(Message msg) {
    var header = new MessageHeader(MessageHeader.HEADER_PROTOCOL_SKETCH);
    Globals.mainApp.SendMessage(Packet.Serialize(msg, header));
  }

  private void Update() {  

    // undo last line
    if (drawingEnabled && Input.GetKeyDown(KeyCode.Z)) {
      var msg = new Message();
      msg.action = (byte)Action.UndoLastLine;
      SendMessage(msg);
    }

    // add points to line
    if (drawingEnabled && Globals.mainApp.HasConnectedEndPoint()) {
      if (Input.GetMouseButton(0)) {

        // first draw point
        if (!drawState.first) {
          drawState.startTime = DateTime.Now;
          drawState.first = true;
        }

        var pos = activeCamera.ScreenToWorldPoint(Input.mousePosition);
        if (drawState.lastPoint != pos) {
          SendPoint(pos);
          drawState.lastPoint = pos;
        }
      } else if (Input.GetMouseButtonUp(0)) {

        // discard line if mouse was not down long enough (i.e. click)
        var endTime = (DateTime.Now - drawState.startTime).TotalMilliseconds / 1000;
        if (endTime > 0.15) {
          // the current line renderer may be empty so ignore these lines
          if (currentLine != null) {
            var msg = new Message();
            msg.action = (byte)Action.EndOfLine;
            SendMessage(msg);
          }
        } else {
          var msg = new Message();
          msg.action = (byte)Action.DiscardLine;
          SendMessage(msg);
        }

        // reset draw state
        drawState.first = false;
      }
    }

  }

  public void Toggle() {
    if (drawingEnabled) {
      Disable();
    } else {
      Enable();
    }
  }

  public bool IsEnabled() {
    return drawingEnabled;
  }

  public void Enable() {
    drawingEnabled = true;
    Globals.mainApp.BroadcastNotification("toggle_focus");
    if (!cursorTexture) Globals.FatalError("sketch pad cursor texture not found");
    Cursor.SetCursor(cursorTexture, new Vector2(12, 52), CursorMode.Auto);
  }

  public void Disable() {
    drawingEnabled = false;
    Globals.mainApp.BroadcastNotification("toggle_focus");
    Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
  }

  public void Start() {
    // allocate lines for patient/therapist
    var maxLines = Enum.GetNames(typeof(Role)).Length;
    lineRenderer = new LineRenderer[maxLines];
    undoStack = new Stack<LineRenderer>[maxLines];

    undoStack[(int)Role.Therapist] = new Stack<LineRenderer>();
    undoStack[(int)Role.Patient] = new Stack<LineRenderer>();

    activeCamera = GameUtils.FindByComponent<Camera>("Camera");
  }

}