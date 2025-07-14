using System;

public static class Error
{
    public class PacketDecodingError : Exception
    {
        public PacketDecodingError() { }
        public PacketDecodingError(string message) : base(message) { }
    }

    public class PacketTooBig : PacketDecodingError
    {
        public PacketTooBig() { }
        public PacketTooBig(string message) : base(message) { }
    }

    public class InvalidCRC : PacketDecodingError
    {
        public InvalidCRC() { }
        public InvalidCRC(string message) : base(message) { }
    }

    public class LoginError : Exception
    {
        public LoginError() { }
        public LoginError(string message) : base(message) { }
    }
}
