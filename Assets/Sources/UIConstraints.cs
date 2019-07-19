using System;
using UnityEngine;
using UnityEngine.UI;

// TODO: unused for now

public class UIConstraints : MonoBehaviour {

  public bool minX;
  public bool minY;
  public bool maxX;
  public bool maxY;
  public bool width;
  public bool height;

  private GameObject parent;

  private void Apply() {
    var newPosition = gameObject.transform.position;

    if (minX) {
      // parentMinX = parent.transform.position.x;
      // newPosition.x = 0;
    }

    gameObject.transform.position = newPosition;
  }

  public void Start() {
    parent = gameObject.transform.parent.gameObject;

    // var pos = gameObject.transform.localPosition;
    // Debug.Log("local pos: "+pos);
    // var right = parent.transform.right;
    // Debug.Log("parent right: "+right);
    // var up = parent.transform.up;
    // Debug.Log("parent up: "+up);

    Apply();
  }

}