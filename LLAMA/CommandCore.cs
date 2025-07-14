using System;
using System.Collections.Generic;
using System.Linq;

namespace cmd
{
    public class Command
    {
        public CommandEnum Type { get; set; }
        public Request Payload { get; set; }

        public Command(CommandEnum type, Request payload)
        {
            Type = type;
            Payload = payload;
        }

        protected Command() { }
    }

    public static class CommandRegistry
    {
        private static readonly Dictionary<ushort, Type> RequestTypes = new();
        private static readonly Dictionary<ushort, Type> ResponseTypes = new();

        public static void RegisterRequest<T>(ushort cmdId) where T : Request =>
            RequestTypes[cmdId] = typeof(T);

        public static void RegisterResponse<T>(ushort cmdId) where T : Response =>
            ResponseTypes[cmdId] = typeof(T);

        public static Type GetRequestType(ushort cmdId) =>
            RequestTypes.TryGetValue(cmdId, out var type) ? type : null;

        public static Type GetResponseType(ushort cmdId) =>
            ResponseTypes.TryGetValue(cmdId, out var type) ? type : null;
    }

    public abstract class Request : Command
    {
        protected Request() { }

        protected Request(byte[] payload)
        {
            Unserialize(payload);
        }

        public abstract void AssignParams(params object[] args);

        public virtual void Unserialize(byte[] payload)
        {
        }

        public static Request Parse(byte[] packet)
        {
            ushort cmdId = BitConverter.ToUInt16(packet, 0);
            byte[] payload = packet.Skip(2).ToArray();

            Type requestType = CommandRegistry.GetRequestType(cmdId);
            if (requestType == null)
                throw new InvalidOperationException($"Unknown request type for CmdId {cmdId}");

            Request instance = (Request)Activator.CreateInstance(requestType, payload);
            instance.CmdId = cmdId;

            return instance;
        }
    }

    public abstract class Response : Command
    {
        public long ServerTime { get; protected set; }
        public object UserDataVersion { get; protected set; }
        public byte AchievedMissionFlag { get; protected set; }

        public virtual int UnserializeCommonResponse(byte[] data, int start)
        {
            start += 2; // skip header or reserved fields

            ServerTime = BitConverter.ToInt64(data, start);
            start += sizeof(long);

            UserDataVersion = DbData.UnserializeDataVersionList(data, start, out int bytesUsed);
            start += bytesUsed;

            AchievedMissionFlag = data[start];
            start += 1;

            return start;
        }

        public static Response Parse(byte[] packet)
        {
            ushort cmdId = BitConverter.ToUInt16(packet, 0);
            byte[] payload = packet.Skip(2).ToArray();

            Type responseType = CommandRegistry.GetResponseType(cmdId);
            if (responseType == null)
                throw new InvalidOperationException($"Unknown response type for CmdId {cmdId}");

            Response resp = (Response)Activator.CreateInstance(responseType);
            resp.CmdId = cmdId;

            try
            {
                resp.Unserialize(payload);
            }
            catch (Exception ex)
            {
                string hex = BitConverter.ToString(packet).Replace("-", " ");
                Console.WriteLine($"[ERROR] Failed to parse {responseType.Name}, packet = {hex}");
                Console.WriteLine(ex);
            }

            return resp;
        }

        public abstract void Unserialize(byte[] payload);
    }

    public enum CommandEnum
    {
        CheckAlive = 5669
    }
}
