using System;
using System.Collections.Generic;
using System.Text;

public abstract class Request
{
    /// <summary>
    /// Assigns parameters to the request object
    /// </summary>
    /// <param name="args">Parameters to assign</param>
    public abstract void AssignParams(params object[] args);

    /// <summary>
    /// Serializes the request contents to a byte array
    /// </summary>
    /// <returns>Serialized byte array</returns>
    public abstract byte[] SerializeContents();

    /// <summary>
    /// Deserializes the request contents from a byte array
    /// </summary>
    /// <param name="data">Byte array to deserialize from</param>
    public abstract void UnserializeContents(byte[] data);
}

public static class SerializationHelper
{
    public static byte[] SerializeVLong(long num)
    {
        var bytestream = new byte[10];
      
        byte signflag;
        if (num < 0)
        {
            num ^= -1;
            signflag = 64;
        }
        else
        {
            signflag = 0;
        }
        
        bytestream[0] = (byte)((num & 63) | signflag);
        bytestream[1] = (byte)((num >> 6) & 0x7f);
        bytestream[2] = (byte)((num >> 13) & 0x7f);
        bytestream[3] = (byte)((num >> 20) & 0x7f);
        bytestream[4] = (byte)((num >> 27) & 0x7f);
        bytestream[5] = (byte)((num >> 34) & 0x7f);
        bytestream[6] = (byte)((num >> 41) & 0x7f);
        bytestream[7] = (byte)((num >> 48) & 0x7f);
        bytestream[8] = (byte)((num >> 55) & 0x7f);
        bytestream[9] = (byte)((num >> 62) & 0x01);
        int tail = 9;
        while (bytestream[tail] == 0 && tail > 0)
        {
            tail--;
        }
        bytestream[tail] += 128;
        var result = new byte[tail + 1];
        Array.Copy(bytestream, 0, result, 0, tail + 1);
        return result;
    }
    public static (long num, int bytesConsumed) UnserializeVLong(byte[] data, int start = 0)
    {
        var bytestream = new byte[10];
        int i = 0;
        
        while (true)
        {
            byte b = data[start + i];
            bytestream[i] = b;
            i++;
            if (!((b & 128) == 0 && i < 10))
                break;
        }
        
        int tail = i;

        long num = 0;
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
            num = num << 6;
        }
        num += bytestream[0] & 63;

        bool signFlag = (bytestream[0] & 64) == 64;
        if (signFlag)
        {
            num = num ^ -1;
        }
        
        return (num, tail);
    }
    public static byte[] SerializeString(string s)
    {
        var utf8Bytes = Encoding.UTF8.GetBytes(s);
        var lengthBytes = SerializeVLong(utf8Bytes.Length);
        
        var result = new byte[lengthBytes.Length + utf8Bytes.Length];
        lengthBytes.CopyTo(result, 0);
        utf8Bytes.CopyTo(result, lengthBytes.Length);
        
        return result;
    }
    public static (string str, int bytesConsumed) UnserializeString(byte[] data, int start = 0)
    {
        var (length, lengthBytes) = UnserializeVLong(data, start);
        var stringBytes = new byte[length];
        Array.Copy(data, start + lengthBytes, stringBytes, 0, (int)length);
        var str = Encoding.UTF8.GetString(stringBytes);
        
        return (str, lengthBytes + (int)length);
    }
    public static byte[] SerializeIntList(List<int> list)
    {
        var result = new List<byte>();
        result.AddRange(SerializeVLong(list.Count));
        
        foreach (var item in list)
        {
            result.AddRange(BitConverter.GetBytes(item));
        }
        
        return result.ToArray();
    }
    public static (List<int> list, int bytesConsumed) UnserializeIntList(byte[] data, int start = 0)
    {
        var (count, lengthBytes) = UnserializeVLong(data, start);
        var list = new List<int>();
        int currentPos = start + lengthBytes;
        
        for (int i = 0; i < count; i++)
        {
            list.Add(BitConverter.ToInt32(data, currentPos));
            currentPos += 4;
        }
        
        return (list, currentPos - start);
    }
    public static byte[] SerializeLongList(List<long> list)
    {
        var result = new List<byte>();
        result.AddRange(SerializeVLong(list.Count));
        
        foreach (var item in list)
        {
            result.AddRange(BitConverter.GetBytes(item));
        }
        
        return result.ToArray();
    }
    public static (List<long> list, int bytesConsumed) UnserializeLongList(byte[] data, int start = 0)
    {
        var (count, lengthBytes) = UnserializeVLong(data, start);
        var list = new List<long>();
        int currentPos = start + lengthBytes;
        
        for (int i = 0; i < count; i++)
        {
            list.Add(BitConverter.ToInt64(data, currentPos));
            currentPos += 8;
        }
        
        return (list, currentPos - start);
    }
    
    public static byte[] SerializeStringList(List<string> list)
    {
        var result = new List<byte>();
        result.AddRange(SerializeVLong(list.Count));
        
        foreach (var item in list)
        {
            result.AddRange(SerializeString(item));
        }
        
        return result.ToArray();
    }
    
    public static (List<string> list, int bytesConsumed) UnserializeStringList(byte[] data, int start = 0)
    {
        var (count, lengthBytes) = UnserializeVLong(data, start);
        var list = new List<string>();
        int currentPos = start + lengthBytes;
        
        for (int i = 0; i < count; i++)
        {
            var (str, strBytes) = UnserializeString(data, currentPos);
            list.Add(str);
            currentPos += strBytes;
        }
        
        return (list, currentPos - start);
    }
}
public class RequestLoginRequest : Request
{
    public ushort ApiVersion { get; set; }
    public byte[] Guid { get; set; }
    public byte[] Key { get; set; }
    public string RegionId { get; set; }
    public string LanguageId { get; set; }

    public override void AssignParams(params object[] args)
    {
        if (args.Length >= 5)
        {
            ApiVersion = (ushort)args[0];
            Guid = (byte[])args[1];
            Key = (byte[])args[2];
            RegionId = (string)args[3];
            LanguageId = (string)args[4];
        }
    }

    public override void UnserializeContents(byte[] data)
    {
        Guid = new byte[16];
        Array.Copy(data, 0, Guid, 0, 16);
        Key = new byte[16];
        Array.Copy(data, 16, Key, 0, 16);
        ApiVersion = BitConverter.ToUInt16(data, 32);
        
        int start = 34;
        var (regionId, regionBytes) = SerializationHelper.UnserializeString(data, start);
        RegionId = regionId;
        start += regionBytes;
        
        var (languageId, languageBytes) = SerializationHelper.UnserializeString(data, start);
        LanguageId = languageId;
    }

    public override byte[] SerializeContents()
    {
        var result = new List<byte>();
        result.AddRange(Guid);
        result.AddRange(Key);
        result.AddRange(BitConverter.GetBytes(ApiVersion));
        result.AddRange(SerializationHelper.SerializeString(RegionId));
        result.AddRange(SerializationHelper.SerializeString(LanguageId));
        return result.ToArray();
    }
}

public class LoginUserRequest : Request
{
    public byte RomType { get; set; } = 2;
    public byte PlatformId { get; set; }
    public string PlatformUserId { get; set; }
    public string CountryCode { get; set; }
    public string CurrencyCode { get; set; }
    public string AdId { get; set; }

    public override void AssignParams(params object[] args)
    {
        if (args.Length >= 5)
        {
            PlatformUserId = (string)args[0];
            CountryCode = (string)args[1];
            CurrencyCode = (string)args[2];
            AdId = (string)args[3];
            PlatformId = (byte)args[4];
        }
        if (args.Length >= 6)
        {
            RomType = (byte)args[5];
        }
    }

    public override void UnserializeContents(byte[] data)
    {
        RomType = data[0];
        PlatformId = data[1];
        int start = 2;
        
        var (platformUserId, platformUserIdBytes) = SerializationHelper.UnserializeString(data, start);
        PlatformUserId = platformUserId;
        start += platformUserIdBytes;
        
        var (countryCode, countryCodeBytes) = SerializationHelper.UnserializeString(data, start);
        CountryCode = countryCode;
        start += countryCodeBytes;
        
        var (currencyCode, currencyCodeBytes) = SerializationHelper.UnserializeString(data, start);
        CurrencyCode = currencyCode;
        start += currencyCodeBytes;
        
        var (adId, adIdBytes) = SerializationHelper.UnserializeString(data, start);
        AdId = adId;
    }

    public override byte[] SerializeContents()
    {
        var result = new List<byte>();
        result.Add(RomType);
        result.Add(PlatformId);
        result.AddRange(SerializationHelper.SerializeString(PlatformUserId));
        result.AddRange(SerializationHelper.SerializeString(CountryCode));
        result.AddRange(SerializationHelper.SerializeString(CurrencyCode));
        result.AddRange(SerializationHelper.SerializeString(AdId));
        return result.ToArray();
    }
}

public class GetValueRequest : Request
{
    public List<string> Keys { get; set; }

    public override void AssignParams(params object[] args)
    {
        if (args.Length >= 1)
        {
            Keys = (List<string>)args[0];
        }
    }

    public override byte[] SerializeContents()
    {
        return SerializationHelper.SerializeStringList(Keys);
    }

    public override void UnserializeContents(byte[] data)
    {
        var (keys, _) = SerializationHelper.UnserializeStringList(data);
        Keys = keys;
    }
}

public class GetMissionInfoRequest : Request
{
    public List<int> MissionSetIdList { get; set; }
    public int Page { get; set; }

    public override void AssignParams(params object[] args)
    {
        if (args.Length >= 2)
        {
            MissionSetIdList = (List<int>)args[0];
            Page = (int)args[1];
        }
    }

    public override byte[] SerializeContents()
    {
        var result = new List<byte>();
        result.AddRange(SerializationHelper.SerializeIntList(MissionSetIdList));
        result.AddRange(BitConverter.GetBytes(Page));
        return result.ToArray();
    }

    public override void UnserializeContents(byte[] data)
    {
        var (missionSetIdList, listBytes) = SerializationHelper.UnserializeIntList(data);
        MissionSetIdList = missionSetIdList;
        Page = BitConverter.ToInt32(data, listBytes);
    }
}

public class ReceivePresentBoxRequest : Request
{
    public List<long> PresentBoxIds { get; set; }

    public override void AssignParams(params object[] args)
    {
        if (args.Length >= 1)
        {
            PresentBoxIds = (List<long>)args[0];
        }
    }

    public override byte[] SerializeContents()
    {
        return SerializationHelper.SerializeLongList(PresentBoxIds);
    }

    public override void UnserializeContents(byte[] data)
    {
        var (presentBoxIds, _) = SerializationHelper.UnserializeLongList(data);
        PresentBoxIds = presentBoxIds;
    }
}

public class SetNextLoginBonusItemRequest : Request
{
    public List<object> NextLoginBonusItemList { get; set; }
    public long Nonce { get; set; }

    public override void AssignParams(params object[] args)
    {
        if (args.Length >= 2)
        {
            NextLoginBonusItemList = (List<object>)args[0];
            Nonce = (long)args[1];
        }
    }

    public override byte[] SerializeContents()
    {
        var result = new List<byte>();
        result.AddRange(SerializationHelper.SerializeVLong(0));
        result.AddRange(BitConverter.GetBytes(Nonce));
        return result.ToArray();
    }

    public override void UnserializeContents(byte[] data)
    {
        var (count, listBytes) = SerializationHelper.UnserializeVLong(data);
        NextLoginBonusItemList = new List<object>();
        int start = listBytes;
        if (data.Length >= start + 8)
        {
            Nonce = BitConverter.ToInt64(data, data.Length - 8);
        }
    }
}
