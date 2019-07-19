/* 
 * Copyright (C) 2015 Christoph Kutza
 * 
 * Please refer to the LICENSE file for license information
 */
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

public class MessageList : MonoBehaviour {
    public GameObject uEntryPrefab;
    private RectTransform mOwnTransform;
    private int mMaxMessages = 5000;
    private int mCounter = 0;

    private void Awake() {
      mOwnTransform = this.GetComponent<RectTransform>();
    }

    private void Start() {
      Application.targetFrameRate = 30;
      foreach (var v in mOwnTransform.GetComponentsInChildren<RectTransform>()) {
        if (v != mOwnTransform) {
          v.name = "Element " + mCounter;
          mCounter++;
        }
      }
    }

    public GameObject GetContainer() {
      return gameObject.GetParent().GetParent();
    }

    public void AddTextEntry(string text) {
      GameObject ngp = Instantiate(uEntryPrefab);
      Text t = ngp.GetComponentInChildren<Text>();
      t.text = text;

      RectTransform transform = ngp.GetComponent<RectTransform>();
      transform.SetParent(mOwnTransform, false);

      GameObject go = transform.gameObject;
      go.name = "Element " + mCounter;
      mCounter++;
    }
    
    private void Update() {
      int destroy = mOwnTransform.childCount - mMaxMessages;
      for(int i = 0; i < destroy; i++) {
        var child = mOwnTransform.GetChild(i).gameObject;
        Destroy(child);
      }
    }

}
