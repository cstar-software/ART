using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class Preferences : MonoBehaviour
{
		public Dropdown cameraDropdown;
		public InputField patientIDField;
		public Toggle enableAudioToggle;
    public Toggle enableVideoToggle;

		public void OnEnableAudioChanged() {
			PlayerPrefs.SetInt(PREFS.ENABLE_AUDIO, enableAudioToggle.isOn ? 1 : 0);
		}

    public void OnEnableVideoChanged() {
      PlayerPrefs.SetInt(PREFS.ENABLE_VIDEO, enableVideoToggle.isOn ? 1 : 0);
    }

		public void OnDeviceChanged() {
			PlayerPrefs.SetInt(PREFS.VIDEO_DEVICE, cameraDropdown.value);
			Debug.Log("video device changed:"+PlayerPrefs.GetInt(PREFS.VIDEO_DEVICE));
		}

		public void OnPatientIDChanged() {
			PlayerPrefs.SetString(PREFS.PATIENT_ID, patientIDField.text);
		}

		public void OnDone() {
			SceneManager.LoadScene(0);
		}

    public void Start() {

    	patientIDField.text = PlayerPrefs.GetString(PREFS.PATIENT_ID);
    	enableAudioToggle.isOn = PlayerPrefs.GetInt(PREFS.ENABLE_AUDIO) == 1 ? true : false;
      enableVideoToggle.isOn = PlayerPrefs.GetInt(PREFS.ENABLE_VIDEO) == 1 ? true : false;

    	// device names
    	WebCamDevice[] devices = WebCamTexture.devices;
    	var deviceNames = new List<string>();
    	foreach (var device in devices) {
    		deviceNames.Add(device.name);
    	}
      cameraDropdown.AddOptions(deviceNames);
      cameraDropdown.value = PlayerPrefs.GetInt(PREFS.VIDEO_DEVICE);
    }

}
