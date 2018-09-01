using System;

namespace LightMessager.Common
{
    internal static class MessageIdHelper
    {
        public static string GenerateMessageIdFrom(byte[] body)
        {
            if (body == null) return "MESSAGE-BODY-IS-NULL";

            var base64String = Convert.ToBase64String(body);

            return $"knuth-{Knuth.CalculateHash(base64String)}";
        }
    }

    internal static class Knuth
    {
        public static ulong CalculateHash(string read)
        {
            var hashedValue = 3074457345618258791ul;
            for (var i = 0; i < read.Length; i++)
            {
                hashedValue += read[i];
                hashedValue *= 3074457345618258799ul;
            }
            return hashedValue;
        }
    }
}
