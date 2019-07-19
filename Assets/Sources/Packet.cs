using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.InteropServices;
using System.Collections;
using UnityEngine;

public class Packet {

  public unsafe static MessageHeader GetHeader(byte[] bytes) {
    return MemUtils.BytesToStruct<MessageHeader>(bytes);
  }

  public unsafe static T Deserialize<T>(byte[] source) where T: struct {
    // create new buffer for body bytes
    // the header size is substracted directly
    // because it was serialized directly
    var headerSize = MemUtils.Sizeof<MessageHeader>();
    var dest = new byte[source.Length - headerSize];
    Buffer.BlockCopy(source, headerSize, dest, 0, dest.Length);

    return Serializer.Deserialize<T>(dest);
  }

  // helper to serialize a message with a default header
  public unsafe static byte[] Serialize<T>(T body) where T: struct {
    return Serialize(body, new MessageHeader(MessageHeader.HEADER_PROTOCOL_APP));
  }

  public unsafe static byte[] Serialize<T>(T body, MessageHeader header) where T: struct {

    // serialize header and body
    var headerSize = MemUtils.Sizeof<MessageHeader>();
    var headerBytes = MemUtils.Copy((byte*)&header, 0, headerSize);
    var bodyBytes = Serializer.Serialize<T>(body);

    // merge serialized parts into buffer
    var dest = new byte[bodyBytes.Length + headerBytes.Length];
    Buffer.BlockCopy(headerBytes, 0, dest, 0, headerBytes.Length);
    Buffer.BlockCopy(bodyBytes, 0, dest, headerBytes.Length, bodyBytes.Length);

    return dest;
  }

}