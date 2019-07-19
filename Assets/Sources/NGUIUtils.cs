using System;
using UnityEngine;
using UnityEngine.UI;

public static class NGUIUtils {
  public static void MakeReadOnly(this UIInput input) {
    input.enabled = false;
    input.onChange = null;
    input.caretColor = new Color(0f, 0f, 0f, 0f);
  }

  public static void MakeMultiLine(this UIInput input, int maxLineCount = 4) {
    var label = input.gameObject.GetComponentInChildren<UILabel>();
    label.maxLineCount = maxLineCount;
    label.overflowMethod = UILabel.Overflow.ShrinkContent;
  }


}
