using CommandCore;
using cmd;
using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MissionSet
{
    public class GetMissionSetInfoRequest : Request
    {
        public int Page { get; private set; }

        public void AssignParams(int page)
        {
            Page = page;
        }

        public override byte[] SerializeContents()
        {
            byte[] buffer = new byte[4];
            BitConverter.GetBytes(Page).CopyTo(buffer, 0);
            return buffer;
        }

        public override void UnserializeContents(byte[] data)
        {
            if (data.Length < 4)
                throw new ArgumentException("Data too short");

            Page = BitConverter.ToInt32(data, 0);
        }
    }

    public class GetMissionSetInfoResponse : Response
    {
        public int Page { get; private set; }
        public int PageSize { get; private set; }
        public int LastPage { get; private set; }
        public List<MissionSetInfo> MissionSetInfoList { get; private set; }

        public override void UnserializeContents(byte[] data)
        {
            int start = UnserializeCommonResponse(data, 0);

            if (data.Length < start + 12)
                throw new ArgumentException("Data too short for page info");

            Page = BitConverter.ToInt32(data, start);
            PageSize = BitConverter.ToInt32(data, start + 4);
            LastPage = BitConverter.ToInt32(data, start + 8);
            start += 12;

            MissionSetInfoList = DBData.UnserializeList(DBData.UnserializeMissionSetInfo, data, ref start);
        }
    }
}
