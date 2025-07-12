using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public static class DBData
{
    // VLong encoding (signed)
    public static byte[] SerializeVLong(long num)
    {
        var bytestream = new byte[10];
        int signFlag = 0;

        if (num < 0)
        {
            num ^= -1;
            signFlag = 64;
        }

        bytestream[0] = (byte)((num & 63) | signFlag);
        bytestream[1] = (byte)((num >> 6) & 0x7F);
        bytestream[2] = (byte)((num >> 13) & 0x7F);
        bytestream[3] = (byte)((num >> 20) & 0x7F);
        bytestream[4] = (byte)((num >> 27) & 0x7F);
        bytestream[5] = (byte)((num >> 34) & 0x7F);
        bytestream[6] = (byte)((num >> 41) & 0x7F);
        bytestream[7] = (byte)((num >> 48) & 0x7F);
        bytestream[8] = (byte)((num >> 55) & 0x7F);
        bytestream[9] = (byte)((num >> 62) & 0x01);

        int tail = 9;
        while (tail > 0 && bytestream[tail] == 0)
            tail--;

        bytestream[tail] += 128;

        var result = new byte[tail + 1];
        Array.Copy(bytestream, result, tail + 1);
        return result;
    }

    public static (long value, int length) UnserializeVLong(byte[] data, int start = 0)
    {
        long num = 0;
        byte[] bytestream = new byte[10];
        int i = 0;
        while (true)
        {
            byte b = data[start + i];
            bytestream[i] = b;
            i++;
            if ((b & 128) != 0 || i >= 10) break;
        }

        int tail = i;
        if (tail > 1)
        {
            i = tail - 1;
            num = bytestream[i] & 127;
            i--;
            while (i > 0)
            {
                num = (num << 7) | bytestream[i];
                i--;
            }
            num = (num << 6);
        }

        num += bytestream[0] & 63;
        if ((bytestream[0] & 64) == 64)
            num ^= -1;

        return (num, tail);
    }

    // String encoding
    public static byte[] SerializeString(string s)
    {
        byte[] strBytes = Encoding.UTF8.GetBytes(s);
        byte[] length = SerializeVLong(strBytes.Length);
        return Combine(length, strBytes);
    }

    public static (string value, int length) UnserializeString(byte[] data, int start = 0)
    {
        var (len, offset) = UnserializeVLong(data, start);
        string str = Encoding.UTF8.GetString(data, start + offset, (int)len);
        return (str, offset + (int)len);
    }

    // UUID encoding
    public static byte[] SerializeUUID(string uuid)
    {
        var bytes = Guid.Parse(uuid).ToByteArray();
        return new byte[]
        {
            bytes[3], bytes[2], bytes[1], bytes[0], // reverse first 4
            bytes[5], bytes[4], // reverse next 2
            bytes[7], bytes[6], // reverse next 2
            bytes[8], bytes[9], bytes[10], bytes[11], bytes[12], bytes[13], bytes[14], bytes[15]
        };
    }

    public static string UnserializeUUID(byte[] data, int start = 0)
    {
        var reversed = new byte[16];
        reversed[0] = data[start + 3];
        reversed[1] = data[start + 2];
        reversed[2] = data[start + 1];
        reversed[3] = data[start + 0];
        reversed[4] = data[start + 5];
        reversed[5] = data[start + 4];
        reversed[6] = data[start + 7];
        reversed[7] = data[start + 6];

        for (int i = 8; i < 16; i++)
            reversed[i] = data[start + i];

        return new Guid(reversed).ToString();
    }

    // Generic list serialization
    public static byte[] SerializeList<T>(List<T> list, Func<T, byte[]> serializer)
    {
        var result = new List<byte>();
        result.AddRange(SerializeVLong(list.Count));
        foreach (var item in list)
            result.AddRange(serializer(item));
        return result.ToArray();
    }

    public static (List<T> list, int length) UnserializeList<T>(byte[] data, int start, Func<byte[], int, (T item, int next)> parser)
    {
        var (count, offset) = UnserializeVLong(data, start);
        var list = new List<T>();
        int pos = start + offset;
        for (int i = 0; i < count; i++)
        {
            var (item, next) = parser(data, pos);
            list.Add(item);
            pos = next;
        }
        return (list, pos);
    }

    // Basic struct deserialization
    public static (T value, int next) Unpack<T>(byte[] data, int start, Func<BinaryReader, T> readerFunc, int size)
    {
        using var ms = new MemoryStream(data, start, size);
        using var br = new BinaryReader(ms);
        return (readerFunc(br), start + size);
    }

    // Utility
    public static byte[] Combine(params byte[][] arrays)
    {
        int length = arrays.Sum(a => a.Length);
        byte[] result = new byte[length];
        int offset = 0;
        foreach (var arr in arrays)
        {
            Buffer.BlockCopy(arr, 0, result, offset, arr.Length);
            offset += arr.Length;
        }
        return result;
    }

    // Struct-like field parsing using tuples
    public static (Dictionary<string, object> fields, int next) UnpackStruct(byte[] data, int start, string[] names, string format)
    {
        int size = GetStructSize(format);
        var values = ParseStructValues(data, start, format);
        var dict = new Dictionary<string, object>();
        for (int i = 0; i < names.Length; i++)
            dict[names[i]] = values[i];
        return (dict, start + size);
    }

    public static object[] ParseStructValues(byte[] data, int start, string format)
    {
        using var ms = new MemoryStream(data, start, data.Length - start);
        using var br = new BinaryReader(ms);
        var values = new List<object>();

        foreach (char c in format)
        {
            values.Add(c switch
            {
                'b' => br.ReadByte(),
                'h' => br.ReadInt16(),
                'i' => br.ReadInt32(),
                'q' => br.ReadInt64(),
                _ => throw new InvalidDataException($"Unsupported format character: {c}")
            });
        }
        return values.ToArray();
    }

    public static int GetStructSize(string format)
    {
        return format.Sum(c => c switch
        {
            'b' => 1,
            'h' => 2,
            'i' => 4,
            'q' => 8,
            _ => throw new InvalidDataException($"Unsupported format character: {c}")
        });
    }
}

// this is a NEW file
// i'm going to implant the usage of this file into _api_.cs after i convert the rest to c#
