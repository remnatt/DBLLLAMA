using cmd;

namespace CheckAliveModule
{
    // checkAlive Enum
    public enum CheckAliveCommandEnum
    {
        CheckAlive = 5669
    }

    // CheckAlive Request
    public class CheckAliveRequest : Request
    {
        public override void AssignParams(params object[] args)
        {
            // passable
        }
    }

    // CheckAlive Response
    public class CheckAliveResponse : Response
    {
        public override void Unserialize(byte[] payload)
        {
            // passable
        }
    }
}
