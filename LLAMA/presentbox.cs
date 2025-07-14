using System;
using System.Collections.Generic;
using System.IO;
using CommandCore.cs;
using cmd;
using gmsir;

namespace PresentBox
{
    // GetPresentBoxRequest inherits from GetMissionSetInfoRequest
    public class GetPresentBoxRequest : GetMissionSetInfoRequest
    {
    }

    // ReceivePresentBoxRequest
    public class ReceivePresentBoxRequest : Request
    {
        public List<long> PresentBoxIds { get; private set; }

        public void AssignParams(List<long> presentBoxIds)
        {
            PresentBoxIds = presentBoxIds;
        }

        public override byte[] SerializeContents()
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                dbdata.SerializeList(writer, PresentBoxIds, BinaryFormat.Long);
                return ms.ToArray();
            }
        }

        public override void UnserializeContents(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                PresentBoxIds = dbdata.UnserializeList<long>(reader, BinaryFormat.Long);
            }
        }
    }

    // GetPresentBoxResponse
    public class GetPresentBoxResponse : Response
    {
        public List<PresentBoxItem> PresentBoxList { get; private set; }
        public int PageSize { get; private set; }
        public int Page { get; private set; }
        public bool IsNextPage { get; private set; }

        public override void UnserializeContents(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                int start = UnserializeCommonResponse(reader);
                PresentBoxList = dbdata.UnserializeList(reader, dbdata.UnserializePresentBox);
                PageSize = reader.ReadInt32();
                Page = reader.ReadInt32();
                IsNextPage = reader.ReadBoolean();
            }
        }
    }

    // ReceivePresentBoxResponse
    public class ReceivePresentBoxResponse : Response
    {
        public List<PbRewardResult> GivenItemList { get; private set; }
        public long Zeny { get; private set; }

        public override void UnserializeContents(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                int start = UnserializeCommonResponse(reader);
                GivenItemList = dbdata.UnserializeList(reader, dbdata.UnserializePbRewardResult);
                Zeny = reader.ReadInt64();
            }
        }
    }

    // SetNextLoginBonusItemResponse
    public class SetNextLoginBonusItemResponse : Response
    {
        public override void UnserializeContents(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                UnserializeCommonResponse(reader);
            }
        }
    }
}
