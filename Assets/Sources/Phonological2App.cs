using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Byn.Awrtc;
using Byn.Awrtc.Unity;

public class Phonological2App : BaseApp {

  public struct Card {
    public Sprite sprite;
    public int index;
    public string name;
    public string ToString() {
      return index+"->"+name;
    }
    public Card(Sprite sprite, string name) {
      this.sprite = sprite;
      this.index = 0;
      this.name = name;
    }
  }

  // private
  private Stack<Card> questionCards;
  private Stack<Card>[] answerCards;
  private int totalQuestionCount;
  private List<int> answeredCards;
  private System.Diagnostics.Stopwatch stopWatch;

  // gui
  public UIButton[] answerButtons;
  public UIButton[] stackButtons;
  public UITexture questionImage;
  public UIButton continueButton;
  public UILabel  stopWatchLabel;
  public UIButton stopWatchButton;
  public UIButton resetButton;

  private enum Action {
    ChangeCard,
    Reset
  }

  [Serializable]
  private struct Message {
    public Action action;
    public int answerButton;
    public int stackButton;
    public int randSeed;
  }

  private const int MAX_CARDS_PER_STACK = 100;
  private const int MAX_STACKS = 2;
  private const int INVALID_CARD_INDEX = -1;

  [Serializable]
  private struct RestoreState {
    public int questionCount;
    public int[,] answerCardIndexes;
  }

  public void TestRestoreState() {

    var cardArray = questionCards.ToArray();
    Array.Reverse(cardArray);

    var newCards = new Stack<Card>();

    foreach (var card in cardArray) {
      newCards.Push(card);
    }

    for (int i = 0; i < 2; i++) {
      answerCards[0].Push(newCards.Pop());
      answerCards[1].Push(newCards.Pop());
    }

    var bytes = GetRestoreState();
    RestoreState(bytes);
  }

  private Card FindCardByIndex(int index) {
    var cards = questionCards.ToArray();
    foreach (var card in cards) {
      if (card.index == index) return card;
    }
    Globals.FatalError("no card with index "+index);
    return new Card();
  }

  protected override void HandleRestoreState (byte[] bytes) {
    var state = Packet.Deserialize<RestoreState>(bytes);

    for (int stackIndex = 0; stackIndex < MAX_STACKS; stackIndex++) {
      for (int cardIndex = 0; cardIndex < MAX_CARDS_PER_STACK; cardIndex++) {
        var index = state.answerCardIndexes[stackIndex, cardIndex];
        // end of list
        if (index == INVALID_CARD_INDEX) break;
        var card = FindCardByIndex(index);
        // push card to answer stack
        answerCards[stackIndex].Push(card);
      }
    }

    // rollback the question cards by the count
    for (int i = 0; i < state.questionCount; i++) questionCards.Pop();

    UpdateAnswerImage(0);
    UpdateAnswerImage(1);
    UpdateQuestionImage();
  }

  public override byte[] GetRestoreState() {

    var state = new RestoreState();
    state.questionCount = totalQuestionCount - questionCards.Count;
    state.answerCardIndexes = new int[MAX_STACKS, MAX_CARDS_PER_STACK];
    state.answerCardIndexes.Fill(INVALID_CARD_INDEX, MAX_STACKS, MAX_CARDS_PER_STACK);

    for (int stackIndex = 0; stackIndex < MAX_STACKS; stackIndex++) {
      var answerCardArray = answerCards[stackIndex].ToArray();
      for (int cardIndex = 0; cardIndex < answerCardArray.Length; cardIndex++) {
        var card = answerCardArray[cardIndex];
        state.answerCardIndexes[stackIndex, cardIndex] = card.index;
      }
    }

    var header = new MessageHeader(MessageHeader.HEADER_PROTOCOL_RESTORE);
    header.appID = (byte)this.appID;

    return Packet.Serialize(state, header);
  }

  private void UpdateQuestionImage() {
    if (questionCards.Count > 0) {
      questionImage.mainTexture = questionCards.Peek().sprite.texture;
    } else {
      questionImage.mainTexture = null;
    }
  }

  private void UpdateAnswerImage(int num) {
    var stackTexture = stackButtons[num].GetComponentInChildren<UITexture>();
    if (answerCards[num].Count > 0) {
      stackTexture.mainTexture = answerCards[num].Peek().sprite.texture;
    } else {
      stackTexture.mainTexture = null;
    }
  }

  private void ResetCards() {

    // TODO: get a new random seed and send from therapist
    LoadQuestionImages("Test1");

    UpdateQuestionImage();
    UpdateAnswerImage(0);
    UpdateAnswerImage(1);
  }

  private void LoadQuestionImages(string path) {

    // TODO: replace with ImageStore.LoadAll
    var textures = Resources.LoadAll(path, typeof(Texture2D));   

    // load sprites into list
    var list = new List<Card>();
    for (int i = 0; i < textures.Length; i++) {
      var sprite = SpriteUtils.Create((Texture2D)textures[i]);
      var card = new Card(sprite, textures[i].name);
      list.Add(card);
    }

    // randomize list and create new stack
    list = ListUtils.Randomize<Card>(list);
    questionCards = new Stack<Card>(list);

    // alloc answer cards
    answerCards = new Stack<Card>[MAX_STACKS];
    for (int i = 0; i < answerCards.Length; i++) {
      var newList = new Stack<Card>();
      answerCards[i] = newList;
    }

    totalQuestionCount = questionCards.Count;
  }

  public void OnAnswerButtonPressed(int num) {

    // no questions left, bail!
    if (questionCards.Count == 0) return;

    var msg = new Message();
    msg.action = Action.ChangeCard;
    msg.answerButton = num;
    msg.stackButton = -1;
    SendMessage(Packet.Serialize(msg));
  }

  public void OnStackButtonPressed(int num) {

    // stack is empty, bail!
    if (answerCards[num].Count == 0) return;

    var msg = new Message();
    msg.action = Action.ChangeCard;
    msg.answerButton = -1;
    msg.stackButton = num;
    SendMessage(Packet.Serialize(msg));
  }

  public void OnContinueButtonPressed() {

    var nextAppID = Globals.mainApp.OpenApp("gameBoard_Phonological2_2").appID;
    // var nextAppID = 0;

    Debug.Log("Sent params to appID "+nextAppID);

    var header = new MessageHeader(MessageHeader.HEADER_PROTOCOL_APP_PARAMS);
    header.appID = (byte)nextAppID;
    var msg = new Phonological2_2App.ParamsMessage();
    msg.cards = answeredCards;

    SendMessage(Packet.Serialize(msg, header));
  }

  public void OnResetButtonPressed() {
    var msg = new Message();
    msg.action = Action.Reset;
    msg.randSeed = Time.frameCount;
    SendMessage(Packet.Serialize(msg));
    Debug.Log("reset with seed:"+msg.randSeed);
  }

  public void OnStopWatchButtonPressed() {
    if (stopWatch == null) {
      stopWatch = System.Diagnostics.Stopwatch.StartNew();
    } else {
      if (stopWatch.IsRunning) {
        stopWatch.Stop();
      } else {
        stopWatch.Start();
      }
    }
  }

  protected override void HandleLoad () {

    // hide gui for patient
    if (Globals.isPatient) {
      stopWatchButton.gameObject.SetActive(false);
      continueButton.gameObject.SetActive(false);
      resetButton.gameObject.SetActive(false);
    }

    // setup number buttons
    UIEventListener.Get(answerButtons[0].gameObject).onClick += delegate {
      OnAnswerButtonPressed(0);
    };
    UIEventListener.Get(answerButtons[1].gameObject).onClick += delegate {
      OnAnswerButtonPressed(1);
    };

    UIEventListener.Get(stackButtons[0].gameObject).onClick += delegate {
      OnStackButtonPressed(0);
    };
    UIEventListener.Get(stackButtons[1].gameObject).onClick += delegate {
      OnStackButtonPressed(1);
    };

  }

  protected override void HandleHide() {
    stopWatch = null;
  }

  protected override void HandleShow() {
    answeredCards = new List<int>();
    stopWatch = null;
    stopWatchLabel.text = "00:00";

    // always load question images on show because the
    // random seed has been freshly synched from the server
    LoadQuestionImages("Test1");
    UpdateQuestionImage();
  }

  protected override void HandleMessage (byte[] bytes) {
    var msg = Packet.Deserialize<Message>(bytes);

    switch (msg.action) {
      case Action.Reset:
        Globals.SetRandomSeed(msg.randSeed);
        // Globals.randomSeed = msg.randSeed;
        // UnityEngine.Random.seed = Globals.randomSeed;
        ResetCards();
        break;
      case Action.ChangeCard:
        if (msg.answerButton > -1) {
          // pop sprite off question images
          var card = questionCards.Pop();
          UpdateQuestionImage();

          // push sprite to stack at index
          var num = msg.answerButton;
          answerCards[num].Push(card);
          UpdateAnswerImage(num);

          // rememebr the cards we selected
          answeredCards.Add(card.index);

        } else if (msg.stackButton > -1) {
          var num = msg.stackButton;
          // pop sprite off stack at button index
          var card = answerCards[num].Pop();
          UpdateAnswerImage(num);

          // push sprite to question images
          questionCards.Push(card);
          UpdateQuestionImage();

          // remove the answer from the list
          answeredCards.Remove(card.index);
        } else {
          Globals.FatalError("bad message");
        }
        break;
    }
  }

  public void Update() {
    if (stopWatch != null) {
      TimeSpan ts = stopWatch.Elapsed;
      string elapsedTime = String.Format("{0:00}:{1:00}", ts.Minutes, ts.Seconds);
      stopWatchLabel.text = elapsedTime;
    }
  }

}
