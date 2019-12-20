using System;
using System.Linq;

namespace CryptoLab.Extensions
{
    public static class StringExtensions
    {
        public static string[] Split(this string value, int chunkSize)
        {
            return Enumerable.Range(0, (int)Math.Ceiling((double)value.Length / chunkSize))
                .Select(i => value.Substring(
                    i * chunkSize,
                    (i * chunkSize + chunkSize <= value.Length)
                        ? chunkSize
                        : value.Length - i * chunkSize))
                .ToArray();
        }
    }
}
