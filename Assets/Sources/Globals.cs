using System;
using UnityEngine;

// TESTING
/*
using System.Runtime.InteropServices;
public class CLibTest {
  [DllImport("/Users/ryanjoseph/Desktop/Work/CLibTest/DerivedData/CLibTest/Build/Products/Debug/libCLibTestDynamic.dylib", EntryPoint="HelloWorld")]
  public static extern void HelloWorld();
}

public class SwiftLibTest {
  [DllImport("/Users/ryanjoseph/Desktop/Work/SwiftLibTest/DerivedData/SwiftLibTest/Build/Products/Debug/libSwiftLibTestDynamic.dylib", EntryPoint="CallFromSwift")]
  public static extern void CallFromSwift();
}
*/

public enum Role {
  Therapist,
  Patient,
  Any,
}

// gameboard names which map to GameObjects
public struct Gameboard {
  public const string Phonological1         = "gameBoard_Phonological1";
  public const string Phonological2         = "gameBoard_Phonological2";
  public const string Phonological2_part2   = "gameBoard_Phonological2_2";
  public const string Phonological3         = "gameBoard_Phonological3";
  public const string Semantic1             = "gameBoard_Semantic1";
  public const string Semantic2             = "gameBoard_Semantic2";
  public const string Semantic3             = "gameBoard_Semantic3";
  public const string Semantic3_part2       = "gameBoard_Semantic3_2";
  public const string Semantic3_part3       = "gameBoard_Semantic3_3";
  public const string Semantic3_part4       = "gameBoard_Semantic3_4";
};

// WARNING: MessageHeader must *not* be serializable
public struct MessageHeader {

  // protocols
  public const byte HEADER_PROTOCOL_APP = 0;
  public const byte HEADER_PROTOCOL_SYSTEM = 1;
  public const byte HEADER_PROTOCOL_RESTORE = 2;
  public const byte HEADER_PROTOCOL_AUDIO = 3;
  public const byte HEADER_PROTOCOL_SKETCH = 4;
  public const byte HEADER_PROTOCOL_MOUSE_INDICATOR = 5;
  public const byte HEADER_PROTOCOL_APP_PARAMS = 6;
  public const byte HEADER_PROTOCOL_CURSOR = 7;

  // fields
  public byte protocol;
  public Role sender;
  public byte appID;
  // public long timestamp;

  // returns true if the header is from the remote endpoint
  public bool IsRemote() {
    return sender != Globals.role;
  }

  public MessageHeader(byte protocol) {
    this.protocol = protocol;
    this.sender = Globals.role;
    this.appID = 0;
    // this.timestamp = DateTime.UtcNow.Ticks;
  }
}

public static class PREFS {
  public const string VIDEO_DEVICE = "videoDevice";
  public const string PATIENT_ID = "patientID";
  public const string ENABLE_AUDIO = "enableAudio";
  public const string ENABLE_VIDEO = "enableVideo";
}

public static class Globals {
  public static bool therapistOnlyTesting = false;
  public static bool simulateMessages = false;
  // high values are worse network
  // TODO: WARNING --- this will totally mess up the random number sequence
  public static int simulatePoorNetworkConditions = 0;
  public static Role role;
  public static string exceptionMessage = "";
  public static int randomSeed = 0;
  public static int appInstanceCount = 0;
  public static MasterPanel mainApp;

  public static bool isPatient {
    get {
      return role == Role.Patient;
    }
  }

  public static bool isTherapist {
    get {
      return role == Role.Therapist;
    }
  }

  // WARNING: always use Globals.GetRandomNumber() to get
  // random numbers so we can audit code for synchronization
  // errors in random number sequences across the network
  public static int randomValueCount = 0;
  public static int GetRandomNumber(int min, int max) {
    randomValueCount += 1;
    // DebugLog.AddEntry("rand: "+randomValueCount);
    return UnityEngine.Random.Range(min, max);
  }

  public static void SetRandomSeed(int newValue) {
    randomSeed = newValue;
    UnityEngine.Random.seed = randomSeed;
  }


  public static int GetNewAppInstanceID() {
    appInstanceCount += 1;
    return appInstanceCount;
  }

  // fatal errors
  public static void FatalError(string msg) {
    throw new Exception("FATAL: " + msg);
  }
  public static void FatalError(bool condition, string msg) {
    if (condition) {
      throw new Exception("FATAL: " + msg);
    }
  }
}

public class ImageStore {

  public static Sprite[] LoadAll(string path) {
    var textures = Resources.LoadAll(path, typeof(Texture2D));

    // sort files by name
    // strip off "img" prefix and compare numbers
    Array.Sort(textures, (a,b) => {
                           int numA = Int32.Parse(a.name.Substring(3));
                           int numB = Int32.Parse(b.name.Substring(3));
                           if (numA > numB) {
                             return 1;
                           } else if (numA < numB) {
                             return -1;
                           } else {
                             return 0;
                           }
                         }
                    );

    var images = new Sprite[textures.Length];
    for (int i = 0; i < textures.Length; i++) {
      var texture = (Texture2D)textures[i];
      images[i] = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0, 0));
    }

    return images;
  }
}
