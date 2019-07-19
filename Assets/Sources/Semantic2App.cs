using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Byn.Awrtc;
using Byn.Awrtc.Unity;

using GameObjectList = System.Collections.Generic.List<UnityEngine.GameObject>;

public class Semantic2App : BaseApp {

  // gui
  public GameObject genericCard;
  public GameObject therapistControls;

  // card picker
  public CardPicker cardPicker;

  // data
  private GameObjectList[] decks;
  private Sprite[] cardImages;
  public bool matchCards;
  private Stack<CardInfo> undoStack; 

  // consts
  private const float cardScale = 1f; // * ((float)numCards / 7f)
  private const float cardDim = 160f;

  public class CardInfo : MonoBehaviour {
    public Role role;
    public int absoluteIndex;
    public int cardIndex;
  }

  private enum Action {
    AddCard,
    ResetCards,
    RemoveCard,
    UndoCard
  }

  [Serializable]
  private struct Message {
    public Action action;
    public int totalCards;
    public bool matchCards;
    public int cardIndex;
    public int insertIndex;
    public Role role;
  }

  [Serializable]
  private struct RestoreState {
    public int totalCards;
    public bool matchCards;
  }

  protected override void HandleRestoreState (byte[] bytes) {
    var msg = Packet.Deserialize<RestoreState>(bytes);

    // totalCards = msg.totalCards;
    // matchCards = msg.matchCards;
    // reloadedFirstTime = true;
    // ReloadCards();
  }

  public override byte[] GetRestoreState() {
    var msg = new RestoreState();

    // TODO: list of cards removed also!
    // msg.totalCards = totalCards;
    msg.matchCards = matchCards;

    var header = new MessageHeader(MessageHeader.HEADER_PROTOCOL_RESTORE);
    header.appID = (byte)this.appID;

    return Packet.Serialize(msg, header);
  }

  public void OnUndoClicked() {
    if (undoStack.Count > 0) {
      var info = undoStack.Pop();
      Debug.Log("undo:"+info.role+" "+info.cardIndex);
      var msg = new Message();
      msg.action = Action.UndoCard;
      msg.cardIndex = info.absoluteIndex;
      msg.insertIndex = info.cardIndex;
      msg.role = info.role;
      SendMessage(Packet.Serialize(msg));
    }
  }

  public void OnChooseCards() {
    cardPicker.Toggle(new Vector3(0, -105, 0));
  }

  public void ResetCards() {
    foreach (var cards in decks) {
      while (cards.Count > 0) {
        var card = cards[0];
        Destroy(card);
        cards.RemoveAt(0);
      }
    }
  }

  public void OnSelectCardFromPicker(GameObject card) {

    Message msg;

    // TODO: if match cards changed then reset all cards
    if (matchCards != cardPicker.matchCardsToggle.isChecked) {
      Debug.Log("MATCH CHANGED, RESET ALL");
      msg = new Message();
      msg.action = Action.ResetCards;
      SendMessage(Packet.Serialize(msg));
    }
    matchCards = cardPicker.matchCardsToggle.isChecked;

    var info = card.GetComponent<CardInfo>();
    
    msg = new Message();
    msg.action = Action.AddCard;
    msg.cardIndex = info.absoluteIndex;
    msg.matchCards = matchCards;
    msg.role = cardPicker.cardPickerRole;
    SendMessage(Packet.Serialize(msg));
  }

  public void OnClickedCard(GameObject card) {

    // for now patients can't remove cards so the
    // therapist can see their tapping
    if (Globals.isPatient) {
      return;
    }

    // if cards are matched we can search the list for the
    // current role, but if they are not matched then
    // we need to search both lists since we can't assoicate
    // any abitrary data with the gameObject
    var info = card.GetComponent<CardInfo>();
    int cardIndex = CardsForRole(info.role).IndexOf(card);

    var msg = new Message();
    msg.action = Action.RemoveCard;
    msg.cardIndex = cardIndex;
    msg.role = info.role;
    SendMessage(Packet.Serialize(msg));
  }

  private List<GameObject> CardsForRole(Role role) {
    if (role == Role.Therapist) {
      if (matchCards) {
        return decks[0];
      } else {
        return decks[1];
      }
    } else {
      return decks[0];
    }
  }

  private void RepositionCards() {
    if (Globals.isTherapist) {
      if (matchCards) {
        RepositionCardsInRowFormation(Role.Any, new Vector3(0, 0, 0));
      } else {
        RepositionCardsInRowFormation(Role.Therapist, new Vector3(-300, 0, 0));
        RepositionCardsInRowFormation(Role.Patient, new Vector3(100, 0, 0));
      }
    } else {
      // RepositionCardsInRadialFormation(Role.Any);
      RepositionCardsInRowFormation(Role.Any, new Vector3(0, 0, 0));
    }
  }

  // private void RepositionCardsInRadialFormation(Role role) {

  //   var cards = CardsForRole(role);

  //   // origin of the circle to distribute cards
  //   var origin = new Vector3(0.0f,0.0f,0.0f);

  //   float sliceAngle = 2f * Mathf.PI / cards.Count;
  //   float radius = 180f * ((float)cards.Count / 6.5f);
  //   if (radius < 150) radius = 150;

  //   // rotate around slices from origin
  //   float currentAngel = 180f * Mathf.Deg2Rad;
  //   foreach (var card in cards) {
  //     card.transform.localPosition = origin + new Vector3(Mathf.Cos(currentAngel), Mathf.Sin(currentAngel), 0f) * radius;
  //     currentAngel += sliceAngle;
  //   }
  // }

  private void RepositionCardsInRowFormation(Role role, Vector3 origin) {
    var cards = CardsForRole(role);
    int cardsPerColumn;
    int cardGroups;
    Vector3 offset;

    if (cards.Count > 6) {
      cardsPerColumn = 4;
    } else {
      cardsPerColumn = 3;
    }
    const int cardMargin = 20;
    int cardColumns = (int)Math.Ceiling((float)cards.Count / (float)cardsPerColumn);

    if (matchCards || Globals.isPatient) {
      cardGroups = 1;
    } else {
      cardGroups = 2;
    }

    // center cards with offset
    if (cardColumns > 1) {
      offset.x = -(((cardDim*(cardColumns-1))+(cardMargin*(cardColumns-1)))/2);
    } else {
      offset.x = 0;
    }

    if (cardGroups > 1) {
      offset.x += 20 * (cardColumns + cardGroups);
    }

    offset.y = -(((cardDim*(cardsPerColumn-1))+(cardMargin*(cardsPerColumn-1)))/2);
    offset.z = 0;

    var pos = origin + offset;

    // arrange cards expanding by column
    var column = 0;
    foreach (var card in cards) {
      card.transform.localPosition = pos;
      pos.y += cardDim + cardMargin;
      column += 1;
      // next column
      if (column == cardsPerColumn) {
        pos.y = origin.y + offset.y;
        pos.x += cardDim + (int)(cardMargin * 1);
        column = 0;
      }
    }

  }

  private void AddCardForRole(Role role, int cardIndex, int atIndex = -1) {
    var cards = CardsForRole(role);
    var obj = Instantiate(genericCard, this.gameObject.transform, true) as GameObject;

    // add card info component
    var info = obj.AddComponent<CardInfo>();
    info.role = role;
    info.absoluteIndex = cardIndex;

    // set card texture
    var texture = obj.GetComponent<UITexture>();
    texture.mainTexture = cardImages[cardIndex].texture;

    // scale card
    var widget = obj.GetComponent<UIWidget>();
    widget.SetDimensions((int)(cardDim * cardScale), (int)(cardDim * cardScale));

    if (atIndex == -1) {
      cards.Add(obj);
    } else {
      cards.Insert(atIndex, obj);
    }
  }

  protected override void HandleLoad() {
    decks = new GameObjectList[2];
    decks[0] = new GameObjectList();
    decks[1] = new GameObjectList();
  }

  protected override void HandleShow() {

    cardImages = ImageStore.LoadAll("Test1");
    undoStack = new Stack<CardInfo>();

    // hide therapist controls for patient
    if (Globals.isPatient) {
      therapistControls.SetActive(false);
    }
    
    cardPicker.PopulateCardPicker(this, cardImages);
  }

  protected override void HandleMessage (byte[] bytes) {
    var msg = Packet.Deserialize<Message>(bytes);

    switch (msg.action) {
      case Action.AddCard:
        Debug.Log("add card "+msg.cardIndex+" for: "+msg.role+" matched:"+msg.matchCards);
        matchCards = msg.matchCards;
        if (matchCards) {
          AddCardForRole(Role.Any, msg.cardIndex);
        } else {
          AddCardForRole(msg.role, msg.cardIndex);
        }
        RepositionCards();
        break;
      case Action.ResetCards:
        ResetCards();
        break;
      case Action.UndoCard:
        AddCardForRole(msg.role, msg.cardIndex, msg.insertIndex);
        RepositionCards();
        break;
      case Action.RemoveCard:
        bool ignore = false;

        // if the patient gets a remove message from the therapists cards
        // then just ignore it because we don't need to update anything
        if (Globals.isPatient && msg.role == Role.Therapist) {
          ignore = true;
        }

        if (!ignore) {
          Debug.Log("remove card "+msg.cardIndex+" for: "+msg.role+" matched:"+msg.matchCards);

          var cards = CardsForRole(msg.role);
          var card = cards[msg.cardIndex];
          if (!card) {
            Globals.FatalError("can't remove invalid card index "+msg.cardIndex+" for role "+msg.role);
          }

          // push to undo stack
          var info = card.GetComponent<CardInfo>();
          info.cardIndex = cards.IndexOf(card);
          undoStack.Push(info);

          cards.Remove(card);
          Destroy(card);
          RepositionCards();
        }
        break;
    }
  }

}
