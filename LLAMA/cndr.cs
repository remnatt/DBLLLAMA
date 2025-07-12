using System;
using System.IO;
using System.Collections.Generic;

public class CheckNewDayResponse : Response
{
    public byte NewDay { get; set; }
    public int TotalLoginCount { get; set; }
    public int ContinuousLoginCount { get; set; }

    public object FirstLoginBonusItem { get; set; }
    public List<object> ComebackLoginBonusItems { get; set; }
    public List<object> LimitedLoginBonusResult { get; set; }
    public List<object> LoginBonusItems { get; set; }

    public object NextLoginBonusItem { get; set; }
    public object SelectLoginBonusItem { get; set; }

    public short ExpiredVipCount { get; set; }

    public object StaminaInfo { get; set; }

    public byte MissionPlanStatus { get; set; }
    public byte ReleaseEquipment { get; set; }
    public byte MoveEquipment { get; set; }
    public byte ComebackUser { get; set; }
    public int ComebackDaysLeft { get; set; }

    public List<byte> UnreceivedRewardIdList { get; set; }

    public int RouletteGashaCount { get; set; }
    public int PickUpCount { get; set; }

    public override void UnserializeContents(byte[] data)
    {
        int offset = 0;
        offset = UnserializeCommonResponse(data, offset);

        (NewDay, TotalLoginCount, ContinuousLoginCount) = DBData.Unpack<byte, int, int>(data, ref offset);

        FirstLoginBonusItem = DBData.UnserializeFirstLoginBonusItem(data, ref offset);
        ComebackLoginBonusItems = DBData.UnserializeList(DBData.UnserializeComebackLoginBonusItem, data, ref offset);
        LimitedLoginBonusResult = DBData.UnserializeList(DBData.UnserializeLimitedLoginBonusResult, data, ref offset);
        LoginBonusItems = DBData.UnserializeList(DBData.UnserializeRewardResult, data, ref offset);

        NextLoginBonusItem = DBData.UnserializeNextLoginBonusItem(data, ref offset);
        SelectLoginBonusItem = DBData.UnserializeNextLoginBonusItem(data, ref offset);

        (ExpiredVipCount) = DBData.Unpack<short>(data, ref offset);

        StaminaInfo = DBData.UnserializeStaminaInfo(data, ref offset);

        (MissionPlanStatus, ReleaseEquipment, MoveEquipment, ComebackUser, ComebackDaysLeft) =
            DBData.Unpack<byte, byte, byte, byte, int>(data, ref offset);

        UnreceivedRewardIdList = DBData.UnserializeList<byte>(data, ref offset);

        (RouletteGashaCount, PickUpCount) = DBData.Unpack<int, int>(data, ref offset);
    }
}
