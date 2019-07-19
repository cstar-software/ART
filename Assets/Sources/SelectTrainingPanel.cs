using System;
using UnityEngine;
using UnityEngine.UI;

public class SelectTrainingPanel : MonoBehaviour {

  private void Close() {
    gameObject.SetActive(false);
  }

  public void OnTest1Button() {
    Close();
    Globals.mainApp.OpenApp(Gameboard.Phonological1);
  }

  public void OnTest2Button() {
    Close();
    Globals.mainApp.OpenApp(Gameboard.Phonological2);
  }

  public void OnTest3Button() {
    Close();
    Globals.mainApp.OpenApp(Gameboard.Phonological3);
  }

  public void OnTest4Button() {
    Close();
    Globals.mainApp.OpenApp(Gameboard.Semantic1);
  }

  public void OnTest5Button() {
    Close();
    Globals.mainApp.OpenApp(Gameboard.Semantic2);
  }

  public void OnTest6Button() {
    Close();
    Globals.mainApp.OpenApp(Gameboard.Semantic3);
  }


  public void Start() {
    gameObject.SetActive(false);
  }

}