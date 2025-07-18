using System;
using System.Collections.Generic;
using System.Text;

public enum CommandEnum
{
    RequestLogin = 0,
    Hello = 1,
    CheckAlive = 144,
    GetVersion = 4097,
    GetUserItemAndPoint = 4108,
    GetValue = 4109,
    CheckNewDay = 4113,
    SetNextLoginBonusItem = 4114,
    LoginUser = 4119,
    GetLimitedLoginBonus = 4120,
    GetDataVersion = 4138,
    GetHomeInfo = 4353,
    ReceivePresentBox = 4355,
    GetPresentBox = 4354,
    GetPersonalMessage = 4370,
    GetStoryModeStatusVersion = 4448,
    GetStoryClearCountDay = 4449,
    GetAvailableVipIdList = 5385,
    GetPremiumPassStatus = 5393,
    GetMissionSetInfo = 5457,
    GetMissionInfo = 5458,
    GetMissionGainInfo = 5463
}

public abstract class Command
{
    public ushort? CmdId { get; set; }
    public long? SeqNumber { get; set; }

    public byte[] SerializeHeader()
    {
        var buffer = new byte[10];
        BitConverter.GetBytes(CmdId.Value).CopyTo(buffer, 0);
        BitConverter.GetBytes(SeqNumber.Value).CopyTo(buffer, 2);
        return buffer;
    }

    public virtual byte[] SerializeContents()
    {
        return new byte[0];
    }

    public byte[] Serialize()
    {
        var header = SerializeHeader();
        var contents = SerializeContents();
        var result = new byte[header.Length + contents.Length];
        header.CopyTo(result, 0);
        contents.CopyTo(result, header.Length);
        return result;
    }

    public int UnserializeSeq(byte[] serialized)
    {
        SeqNumber = BitConverter.ToInt64(serialized, 0);
        return 8;
    }

    public virtual void UnserializeContents(byte[] serialized)
    {
    }

    public void Unserialize(byte[] serialized)
    {
        int start = UnserializeSeq(serialized);
        var contentsData = new byte[serialized.Length - start];
        Array.Copy(serialized, start, contentsData, 0, contentsData.Length);
        UnserializeContents(contentsData);
    }
}

public abstract class Request : Command
{
    public Request()
    {
        if (CmdId == null)
        {
            string cmdName = GetType().Name.Replace("Request", "");
            if (Enum.TryParse<CommandEnum>(cmdName, out CommandEnum cmdEnum))
            {
                CmdId = (ushort)cmdEnum;
            }
        }
    }

    public virtual void AssignParams(params object[] args)
    {
    }

    public static Request Parse(byte[] packet)
    {
        ushort cmdId = BitConverter.ToUInt16(packet, 0);
        int i = 2;
        var payload = new byte[packet.Length - i];
        Array.Copy(packet, i, payload, 0, payload.Length);
        var req = new UnknownRequest();
        req.CmdId = cmdId;
        req.Unserialize(payload);
        return req;
    }
}

public class UnknownRequest : Request
{
}

public class CheckAliveRequest : Request
{
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
        RegionId = "";
        LanguageId = "";
    }

    public override byte[] SerializeContents()
    {
        var result = new List<byte>();
        result.AddRange(Guid);
        result.AddRange(Key);
        result.AddRange(BitConverter.GetBytes(ApiVersion));
        return result.ToArray();
    }
}

public class HelloRequest : Request
{
    public byte[] Token { get; set; }

    public override void AssignParams(params object[] args)
    {
        if (args.Length >= 1)
        {
            Token = (byte[])args[0];
        }
    }

    public override void UnserializeContents(byte[] data)
    {
        Token = new byte[16];
        Array.Copy(data, 0, Token, 0, 16);
    }

    public override byte[] SerializeContents()
    {
        return Token;
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
    }

    public override byte[] SerializeContents()
    {
        var result = new List<byte>();
        result.Add(RomType);
        result.Add(PlatformId);
        return result.ToArray();
    }
}

public class GetVersionRequest : Request
{
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
        var result = new List<byte>();
        return result.ToArray();
    }

    public override void UnserializeContents(byte[] data)
    {
        Keys = new List<string>();
    }
}

public class GetDataVersionRequest : Request
{
}

public class GetStoryModeStatusVersionRequest : Request
{
}

public class GetPremiumPassStatusRequest : Request
{
}

public class GetStoryClearCountDayRequest : Request
{
    public int Page { get; set; }

    public override void AssignParams(params object[] args)
    {
        if (args.Length >= 1)
        {
            Page = (int)args[0];
        }
    }

    public override byte[] SerializeContents()
    {
        return BitConverter.GetBytes(Page);
    }

    public override void UnserializeContents(byte[] data)
    {
        Page = BitConverter.ToInt32(data, 0);
    }
}

public class GetAvailableVipIdListRequest : Request
{
}

public class GetUserItemAndPointRequest : Request
{
    public int Page { get; set; }

    public override void AssignParams(params object[] args)
    {
        if (args.Length >= 1)
        {
            Page = (int)args[0];
        }
    }

    public override byte[] SerializeContents()
    {
        return BitConverter.GetBytes(Page);
    }

    public override void UnserializeContents(byte[] data)
    {
        Page = BitConverter.ToInt32(data, 0);
    }
}

public class CheckNewDayRequest : Request
{
    public long Nonce { get; set; }

    public override void AssignParams(params object[] args)
    {
        if (args.Length >= 1)
        {
            Nonce = (long)args[0];
        }
    }

    public override byte[] SerializeContents()
    {
        return BitConverter.GetBytes(Nonce);
    }

    public override void UnserializeContents(byte[] data)
    {
        Nonce = BitConverter.ToInt64(data, 0);
    }
}

public class GetLimitedLoginBonusRequest : Request
{
    public int LoginBonusEventId { get; set; }
    public int Page { get; set; }

    public override void AssignParams(params object[] args)
    {
        if (args.Length >= 2)
        {
            LoginBonusEventId = (int)args[0];
            Page = (int)args[1];
        }
    }

    public override byte[] SerializeContents()
    {
        var result = new List<byte>();
        result.AddRange(BitConverter.GetBytes(LoginBonusEventId));
        result.AddRange(BitConverter.GetBytes(Page));
        return result.ToArray();
    }

    public override void UnserializeContents(byte[] data)
    {
        LoginBonusEventId = BitConverter.ToInt32(data, 0);
        Page = BitConverter.ToInt32(data, 4);
    }
}

public class GetMissionSetInfoRequest : Request
{
    public int Page { get; set; }

    public override void AssignParams(params object[] args)
    {
        if (args.Length >= 1)
        {
            Page = (int)args[0];
        }
    }

    public override byte[] SerializeContents()
    {
        return BitConverter.GetBytes(Page);
    }

    public override void UnserializeContents(byte[] data)
    {
        Page = BitConverter.ToInt32(data, 0);
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
        result.AddRange(BitConverter.GetBytes(Page));
        return result.ToArray();
    }

    public override void UnserializeContents(byte[] data)
    {
        MissionSetIdList = new List<int>();
        Page = 0;
    }
}

public class GetMissionGainInfoRequest : GetMissionInfoRequest
{
}

public class GetHomeInfoRequest : Request
{
}

public class GetPersonalMessageRequest : Request
{
    public long CheckMessageId { get; set; }
    public byte DoNotShowAgain { get; set; }

    public override void AssignParams(params object[] args)
    {
        if (args.Length >= 2)
        {
            CheckMessageId = (long)args[0];
            DoNotShowAgain = (byte)args[1];
        }
    }

    public override byte[] SerializeContents()
    {
        var result = new List<byte>();
        result.AddRange(BitConverter.GetBytes(CheckMessageId));
        result.Add(DoNotShowAgain);
        return result.ToArray();
    }

    public override void UnserializeContents(byte[] data)
    {
        CheckMessageId = BitConverter.ToInt64(data, 0);
        DoNotShowAgain = data[8];
    }
}

public class GetPresentBoxRequest : GetMissionSetInfoRequest
{
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
