using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Byn.Awrtc;
using Byn.Awrtc.Unity;

public class CardPicker : MonoBehaviour {

  // gui
  public UIGrid cardPickerGrid;
  public GameObject genericPickerCard;
  public UIToggle matchCardsToggle;
  public UIToggle therapistRoleToggle;
  public UIToggle patientRoleToggle;

  // state
  public Role cardPickerRole;
  public Semantic2App app;

  public void PopulateCardPicker(Semantic2App sender, Sprite[] cardImages) {

    app = sender;

    // set default state
    matchCardsToggle.isChecked = true;

    Vector3 nextPos = cardPickerGrid.transform.localPosition;
    float cardWidth = genericPickerCard.GetComponent<UIWidget>().localSize.x;
    float cardHeight = genericPickerCard.GetComponent<UIWidget>().localSize.y;
    float cellMargin = 4;
    int rows = 0;
    int columns = 0;

    int maxRows = 4;
    int maxColumns = (int)cardImages.Length / maxRows;

    for (int i = 0; i < cardImages.Length; i++) {
      var obj = Instantiate(genericPickerCard, cardPickerGrid.gameObject.transform, true) as GameObject;
      
      // advance to next cell
      obj.transform.localPosition = nextPos;
      nextPos.x += cardWidth + cellMargin;
      columns += 1;

      // advance row
      if (columns > maxColumns) {
        nextPos.y += cardHeight + cellMargin;
        nextPos.x = cardPickerGrid.gameObject.transform.localPosition.x;
        columns = 0;
        rows += 1;
      }

      // add card info component
      var info = obj.AddComponent<Semantic2App.CardInfo>();
      info.absoluteIndex = i;

      // set card texture
      var texture = obj.GetComponent<UITexture>();
      texture.mainTexture = cardImages[i].texture;
    }
  }

  public void OnSelectCardFromPicker(GameObject card) {
    if (matchCardsToggle.isChecked || (patientRoleToggle.isChecked || therapistRoleToggle.isChecked)) {
      app.OnSelectCardFromPicker(card);
    } else {
      Debug.Log("select an option");
    }
  }

  public void OnMatchToggle() {
    if (matchCardsToggle.isChecked) {
      patientRoleToggle.isChecked = false;
      therapistRoleToggle.isChecked = false;
    }
    Debug.Log("matchCards:"+matchCardsToggle.isChecked);
  }

  public void OnRoleChanged(UIObject sender) {
    foreach (var name in Enum.GetNames(typeof(Role))) {
      if (name == sender.objectKind) {
        cardPickerRole = sender.objectKind.ToEnum<Role>();
        Debug.Log("changed role:"+cardPickerRole);
        // role toggles are mutally exclusive
        if (cardPickerRole == Role.Therapist) {
          patientRoleToggle.isChecked = false;
        } else {
          therapistRoleToggle.isChecked = false;
        }

        // if either role toggles are checked then disable matching,
        // otherwise matching must be enabled
        if (therapistRoleToggle.isChecked || patientRoleToggle.isChecked) {
          matchCardsToggle.isChecked = false;
        } else {
          matchCardsToggle.isChecked = true;
        }
        
        return;
      }
    }
    Globals.FatalError("invalid role string "+sender.objectKind);
  }

  public void Toggle(Vector3 where) {
    gameObject.transform.localPosition = where;
    gameObject.SetActive(!gameObject.activeSelf);
  }

  public void Start() {
    gameObject.SetActive(false);
  }
}