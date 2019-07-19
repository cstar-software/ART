using System;
using UnityEngine;
using UnityEngine.UI;

public class DebugLog : MonoBehaviour {

  // shared instances
  private static DebugLog _SharedDebugLog = null;
  public static DebugLog Shared {
    get {
      if (_SharedDebugLog == null) Globals.FatalError("debug log not ready!");
      return _SharedDebugLog;
    }
  }

  public UITextList textList;

  // methods

  public static void Setup(DebugLog log, Vector3 localPosition) {
    _SharedDebugLog = log;
    Shared.gameObject.transform.localPosition = localPosition;
  }

  public static void Toggle() {
    if (!Shared.gameObject.activeSelf) {
      Present();
    } else {
      Dismiss();
    }
  }

  public static void Dismiss() {
    Shared.gameObject.SetActive(false);
  }

  public static void Present() {
    Shared.gameObject.SetActive(true);
  }

  public static void AddEntry(string message) {
    Debug.Log(message);
    Shared.textList.Add(message);
  }

}
