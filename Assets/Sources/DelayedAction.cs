using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DelayedAction {

	// pool  
  private static List<DelayedAction> pool = new List<DelayedAction>();

  public static void ProcessPool() {
  	if (pool.Count > 0) {
      int index = 0;
      while (index < pool.Count) {
        var action = pool[index];
        if (action.Process()) {
          pool.RemoveAt(index);
        } else {
          index += 1;
        }
      }
  	}
  }

	public delegate void DelayedActionCallback(object context);

  private double startTime = -1;
  private double delay = -1;
  private DelayedActionCallback callback = null;
	public object context = null;

  public void Fire() {
    callback(context);
  }

	private bool Process() {
		double diff = Time.time - startTime;
		if (startTime > -1 && diff >= delay) {
			Fire();
			return true;
		} else {
			return false;
		}
	}

	public void Stop(bool trigger = false) {
		startTime = -1;
		pool.Remove(this);
    if (trigger) {
      callback(context);
    }
	}

	public void Reset() {
		startTime = Time.time;
	}

  public object GetTarget() {
    return callback.Target;
  }

  public static void CancelAllActions(object target) {
    var actions = new List<DelayedAction>();
    foreach (var action in pool) {
      if (action.GetTarget() == target) {
        actions.Add(action);
      }
    }
    foreach (var action in actions) {
      Debug.Log("stop action:"+action);
      action.Stop();
    }
  }

  public static DelayedAction InvokeMethod(double delay, DelayedActionCallback callback, object context = null) {
    return new DelayedAction(delay, callback, context);
  }

	public DelayedAction(double delay, DelayedActionCallback callback, object context = null) {
		this.context = context;
		this.callback = callback;
		this.delay = delay;
		this.startTime = Time.time;
		pool.Add(this);
	}
}