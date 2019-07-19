using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Byn.Awrtc;
using Byn.Awrtc.Unity;

public struct BaseAppParams {
	public ICall caller;
}

public class BaseApp : MonoBehaviour {

	public int appID = -1;
	public int instanceID;
	
	protected ICall caller;
	protected Stats stats;
	private bool didLoad;

	// UI Helpers

	public GameObject panel {
		get { return gameObject; }
	}

	// UInput Focus
	protected MonoBehaviour activeFocus;

  protected void Update() {      
    if (Input.GetKeyDown(KeyCode.Tab)) {
    	if (activeFocus != null && activeFocus is UIInput) {
    		var obj = activeFocus.gameObject.GetComponent<UIObject>();
    		if (obj != null) {
    			TabFocus(obj.nextTabField);
    		}
    	}
    }
  }

	protected virtual void TabFocus(MonoBehaviour sender) {
		// Debug.Log(activeFocus+" TAB TO NEXT: "+sender);
	}

	protected virtual void ClearFocus() {
	  UISprite sprite;
	  if (activeFocus) {
	    sprite = activeFocus.transform.parent.GetComponent<UISprite>();
	    sprite.color = Color.white;
	    activeFocus = null;
	  }
	}

	protected void GiveFocus(MonoBehaviour component) {
	  var sprite = component.transform.parent.GetComponent<UISprite>();
	  Globals.FatalError(sprite == null, "can't give focus to component, no sprite parent");
	  sprite.color = new Color(0f, 0.9f, 0f, 1.0f);
	  activeFocus = component;
	  
	  // if the component is an input then make sure it's selected also
	  if (component is UIInput) {
	  	var input = (UIInput)component;
	  	if (!input.isSelected) input.isSelected = true;
	  }
	}

	// Input delayed actions
	private DelayedAction inputChangedTimer = null;

	private class DelayedInputContext {
		public object sender;
		public DelayedAction.DelayedActionCallback callback;
	}

	private void CommitInputChanges(object sender) {
		var context = (DelayedInputContext)sender;
		inputChangedTimer = null;
		context.callback(context.sender);
	}

	protected void UpdateInputTimer(object sender, DelayedAction.DelayedActionCallback callback) {
	  if (inputChangedTimer == null) {
	  	DelayedInputContext context = new DelayedInputContext();
	  	context.sender = sender;
	  	context.callback = callback;

	    inputChangedTimer = new DelayedAction(0.3f, CommitInputChanges, context);
	  } else {
	  	var context = (DelayedInputContext) inputChangedTimer.context;
	    if (context.sender == sender) {
	      inputChangedTimer.Reset();
	    } else {
	      // fire timer with old context so we don't lose changes
	      // and then restart timer by calling back in
	      inputChangedTimer.Stop(true);
	      UpdateInputTimer(sender, callback);
	    }
	  }
	}

	// public methods

	public static BaseApp LoadWithPanel(GameObject panel, int id) {
	  panel.SetActive(false);
	  var app = panel.GetComponent<BaseApp>();
	  if (app == null) {
	    Globals.FatalError("app can't find script");
	  }
	  app.appID = id;
	  Debug.Log("App did load: "+panel);
	  return app;
	}

	public static BaseApp LoadWithName(string name, int id) {
	  var panel = GameObject.Find(name);
	  if (panel == null) {
	    Globals.FatalError("app can't find panel game object \""+name+"\"");
	  }
	  return LoadWithPanel(panel, id);
	}

	public string GetName() {
		return gameObject.name;
	}

	public virtual void ReceiveNotification(string name) {

		// toggle all box colliders
		// TODO: this is so stupid, move this out to main panel
		// none of this makes sense as it is
		if (name == "toggle_focus") {
			var colliders = FindObjectsOfType<BoxCollider>();
			foreach (var collider in colliders) {
				var obj = collider.gameObject.GetComponent<UIObject>();
				// ignore objects
				if (obj != null && (obj.objectKind == "sketchPadToggle" || obj.objectKind == "sketchPadErase")) {
					continue;
				}
			  collider.enabled = !collider.enabled;
			}
		}

	}

	public void Disconnect() {
		caller = null;
	}

	public void Reconnect(ICall fromCaller) {
		caller = fromCaller;
	}

	public void Show(GameObject parent, BaseAppParams paras) {
		DebugLog.AddEntry("show app "+GetName());

		caller = paras.caller;
		stats = new Stats();
		instanceID = Globals.GetNewAppInstanceID();

		panel.SetActive(true);
    panel.transform.localPosition = parent.transform.localPosition;

    if (!didLoad) {
    	HandleLoad();
    	didLoad = true;
    }

		HandleShow();
	}

	public void Hide () {
		stats.SaveToDisk();
		panel.SetActive(false);
		HandleHide();
	}

	public void ReceiveParams(byte[] bytes) {
		HandleParams(bytes);
	}

	public void ReceiveMessage(byte[] bytes) {
		HandleMessage(bytes);
	}

	public void RestoreState(byte[] bytes) {
		HandleRestoreState(bytes);
	}

	public virtual void ClientIsReady() {
	}

	public virtual byte[] GetRestoreState() {
		return null;
	}

	protected void SendMessage(byte[] bytes, bool simulateMessages = true) {
		caller.Send(bytes, true);
		if (Globals.simulateMessages && simulateMessages) {
			Globals.mainApp.SimulateMessage(bytes);
		}
	}

	// Handlers
	protected virtual void HandleLoad() {}
	protected virtual void HandleRestoreState(byte[] bytes) {}
	protected virtual void HandleMessage(byte[] bytes) {}
	protected virtual void HandleParams(byte[] bytes) {}
	protected virtual void HandleShow() {}
	protected virtual void HandleHide() {}
}