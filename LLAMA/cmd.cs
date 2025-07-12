using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

public enum CommandEnum
{
    RequestLogin = 0,
    Hello = 1,
    RequestLoginId = 3,
    SendCapyVerification = 5,
    CheckAlive = 144,
    GetVersion = 4097,
    CreateUser = 4096,
    GetUserCharacter = 4101,
    GetUserItemAndPoint = 4108,
    GetValue = 4109,
    SetValue = 4110,
    GetParty = 4112,
    CheckNewDay = 4113,
    SetNextLoginBonusItem = 4114,
    LoginUser = 4119,
    GetLimitedLoginBonus = 4120,
    GetDataVersion = 4138,
    GetHomeInfo = 4353,
    GetPresentBox = 4354,
    ReceivePresentBox = 4355,
    UpdateUserName = 4358,
    GetPersonalMessage = 4370,
    GetResultStoryBattle = 4432,
    GetStoryInfo = 4433,
    PlayStoryBattle = 4434,
    RecoverStamina = 4438,
    GetStoryModeStatus = 4440,
    PlayTotalBattle = 4441,
    GetResultTotalBattle = 4442,
    GetTotalBattleLevelList = 4444,
    GetTotalBattleLevelInfo = 4445,
    GetTotalBattleLayerInfo = 4446,
    GetStoryModeStatusVersion = 4448,
    GetStoryClearCountDay = 4449,
    UnlockBoostPanel = 4867,
    UnlockBoostBoard = 4868,
    UnlockBoostBoardBulk = 4883,
    ExecuteGasha = 4688,
    GetGashaInfo = 4689,
    UpdatePartyInfo = 4865,
    GetAvailableVipIdList = 5385,
    GetPremiumPassStatus = 5393,
    GetMissionSetInfo = 5457,
    GetCompletedMission = 5460,
    GetMissionInfo = 5458,
    GetMissionReward = 5459,
    GetMissionGainInfo = 5463
}

public class Command
{
    public CommandEnum? CmdId { get; set; }
    public long? SeqNumber { get; set; }

    public static Type GetClassByCmdId(int cmdId)
    {
        string baseName;
        if (Enum.IsDefined(typeof(CommandEnum), cmdId))
        {
            baseName = ((CommandEnum)cmdId).ToString();
        }
        else
        {
            baseName = "Unknown";
        }

        string className = baseName + nameof(Command);
        Type type = Assembly.GetExecutingAssembly()
                            .GetTypes()
                            .FirstOrDefault(t => t.Name == className && typeof(Command).IsAssignableFrom(t));
        return type;
    }

    public virtual byte[] SerializeHeader()
    {
        var buffer = new List<byte>();
        if (CmdId.HasValue)
            buffer.AddRange(BitConverter.GetBytes((ushort)CmdId.Value));
        else
            buffer.AddRange(new byte[2]); // default ushort = 0

        if (SeqNumber.HasValue)
            buffer.AddRange(BitConverter.GetBytes(SeqNumber.Value));
        else
            buffer.AddRange(new byte[8]); // default long = 0

        return buffer.ToArray();
    }

    public virtual byte[] SerializeContents()
    {
        return Array.Empty<byte>();
    }

    public byte[] Serialize()
    {
        var header = SerializeHeader();
        var contents = SerializeContents();
        var result = new byte[header.Length + contents.Length];
        Buffer.BlockCopy(header, 0, result, 0, header.Length);
        Buffer.BlockCopy(contents, 0, result, header.Length, contents.Length);
        return result;
    }

    public int UnserializeSeq(byte[] serialized)
    {
        SeqNumber = BitConverter.ToInt64(serialized, 0);
        return sizeof(long);
    }

    public virtual void UnserializeContents(byte[] serialized)
    {
       // passable
    }

    public void Unserialize(byte[] serialized)
    {
        int offset = UnserializeSeq(serialized);
        byte[] contents = new byte[serialized.Length - offset];
        Array.Copy(serialized, offset, contents, 0, contents.Length);
        UnserializeContents(contents);
    }

    public override string ToString()
    {
        string name = GetType().Name;
        string seq = SeqNumber.HasValue ? SeqNumber.ToString() : "";
        string id = CmdId.HasValue ? ((int)CmdId.Value).ToString() : "null";
        return $"[{seq}] {name}({id})";
    }
}
