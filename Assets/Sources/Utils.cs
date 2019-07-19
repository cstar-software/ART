using System;
using System.IO;
using System.Timers;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.UI;

public static class EnumExtensions {
  public static T ToEnum<T>(this string value) {
    return (T) Enum.Parse(typeof(T), value, true);
  }
}

public static class ListUtils {

  public static List<T> Randomize<T>(T[] list) {
    return Randomize<T>(new List<T>(list));
  }

  public static List<T> Randomize<T>(List<T> list) {
    var randomizedList = new List<T>();
    while (list.Count > 0) {
      int index;
      if (list.Count > 1) {
        // remove random item from list and add to randomized list
        index = Globals.GetRandomNumber(0, list.Count);
        randomizedList.Add(list[index]);
        list.RemoveAt(index);
      } else {
        // if the list only has one item then insert at a random
        // location so the last item isn't always added the end of
        // list, thus creating a predicable pattern
        index = Globals.GetRandomNumber(0, randomizedList.Count);
        randomizedList.Insert(index, list[0]);
        list.RemoveAt(0);
      }
    }
    return randomizedList;
  }
}


public static class SpriteUtils {
  public static Sprite Create(Texture2D texture) {
    return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0, 0));
  }
}

public static class ArrayExtensions {
  public static void Fill<T>(this T[] arr, T value) {
    for (int i = 0; i < arr.Length; i++) {
      arr[i] = value;
    }
  }
  public static void Fill<T>(this T[,] arr, T value, int lengthX, int lengthY) {
    for (int y = 0; y < lengthY; y++) {
      for (int x = 0; x < lengthX; x++) {
        arr[x,y] = value;
      }
    }
  }
}

public static class GameObjectExtensions {
  public static void SetImage(this Button button, Sprite sprite) {
    var image = button.GetComponent<Image>();
    image.sprite = sprite;
  }
  public static GameObject GetParent(this GameObject obj) {
    return obj.transform.parent.gameObject;
  }
}   

public static class GameUtils {
  public static T FindByComponent<T>(string name) {
  	var	obj = GameObject.Find(name);
  	if (obj != null) {
  		return obj.GetComponent<T>();
  	} else {
  		return default(T);
  	}
  }
}   

// TODO: can't figure this out right now...
// public static class DebugUtils {
//   public static void Print<T>(ICollection<T> collection) {
//     Debug.Log("collection.Count:"+collection.Count);
//     var array = collection.ToArray();
//     for (int i = 0; i < array.Length; i++) {
//       Debug.Log(i+":"+array[i]);
//     }
//   }
// }   

public static class ProcessUtils {
  public static string Execute(string program, string args = "") {
		var info = new System.Diagnostics.ProcessStartInfo(); 
		info.FileName = program;
    info.Arguments = args;
		info.UseShellExecute = false; 
		info.RedirectStandardOutput = true;
    info.RedirectStandardError = true;
		var process = System.Diagnostics.Process.Start(info); 
		var res = process.StandardOutput.ReadToEnd(); 
    process.WaitForExit(); 
    return res;
  }
  public static System.Diagnostics.Process Open(string program, string args = "") {
    var info = new System.Diagnostics.ProcessStartInfo(); 
    info.FileName = program;
    info.Arguments = args;
    info.UseShellExecute = false; 
    info.CreateNoWindow = true;
    info.RedirectStandardError = true;
    info.RedirectStandardOutput = true;
    var process = System.Diagnostics.Process.Start(info); 
    return process;
  }
}   

public static class FileUtils {
  public static string ReadTextFile(string name) {
    var path = Application.streamingAssetsPath+"/"+name;
    StreamReader file =  new StreamReader(path);  
    var contents = file.ReadToEnd();
    file.Close(); 
    return contents;
  }
}   


public class MemUtils {

	static readonly string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
	public static string GetHumanReadableFileSize(Int64 value) {
    if (value < 0) { return "-" + GetHumanReadableFileSize(-value); }
    if (value == 0) { return "0.0 bytes"; }
    int mag = (int)Math.Log(value, 1024);
    decimal adjustedSize = (decimal)value / (1L << (mag * 10));
    return string.Format("{0:n2} {1}", adjustedSize, SizeSuffixes[mag]);
	}

	public static unsafe void PackBytes(float value, byte[] bytes, int offset) {
    uint val = *((uint*)&value);
    bytes[offset + 0] = (byte)(val & 0xFF);
    bytes[offset + 1] = (byte)((val >> 8) & 0xFF);
    bytes[offset + 2] = (byte)((val >> 16) & 0xFF);
    bytes[offset + 3] = (byte)((val >> 24) & 0xFF);
  }

  public static uint BytesToUInt32(byte[] value, int index) {
      return (uint)(
          value[0 + index] << 0 |
          value[1 + index] << 8 |
          value[2 + index] << 16 |
          value[3 + index] << 24);
  }

  public static unsafe float BytesToFloat(byte[] value, int index) {
      uint i = BytesToUInt32(value, index);
      return *(((float*)&i));
  }

  public static int Sizeof<T>() {
    return Marshal.SizeOf(typeof(T));
  }

  public unsafe static T BytesToStruct<T>(byte[] bytes, int offset = 0) where T : struct {
    fixed (byte* ptr = &bytes[offset]) {
      return (T)Marshal.PtrToStructure((IntPtr)ptr, typeof(T));
    }
  }

  public unsafe static void Copy(byte* source, int sourceOffset, byte[] dest, int destOffset, int count) {
    fixed (byte* destPtr = dest) {
      for (int i = 0; i < count; i++) {
        destPtr[destOffset + i] = source[sourceOffset + i];
      }
    }
  }

  public unsafe static byte[] Copy(byte* source, int sourceOffset, int length) {
    var dest = new byte[length];
    fixed (byte* destPtr = dest) {
      for (int i = 0; i < length; i++) {
        destPtr[i] = source[sourceOffset + i];
      }
    }
    return dest;
  }
}

public class Serializer {

  public static byte[] Serialize<T>(T data) where T : struct {
    var formatter = new BinaryFormatter();
    var stream = new MemoryStream();
    formatter.Serialize(stream, data);
    return stream.ToArray();
  }

  public static T Deserialize<T>(byte[] bytes) where T : struct {
    var formatter = new BinaryFormatter();
    var destSize = Marshal.SizeOf(typeof(T));
    var stream = new MemoryStream(bytes);
    return (T)formatter.Deserialize(stream);
  }
}
