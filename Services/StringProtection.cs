using System;
using System.Text;

namespace AutomatedClashRunner.Services
{
    internal static class StringProtection
    {
        // 32-byte compile-time masking key
        private static readonly byte[] MaskKey = new byte[]
        {
            0x4B, 0x8F, 0x12, 0x99, 0x5C, 0x3E, 0x77, 0xAA,
            0x01, 0xFD, 0x88, 0x34, 0x55, 0x19, 0xEB, 0x72,
            0x39, 0x1A, 0x8F, 0x22, 0x4D, 0x70, 0x88, 0xAC,
            0x29, 0x77, 0x12, 0xEF, 0x50, 0xBB, 0x38, 0x1D
        };

        // Default Firebase Realtime Database Root endpoint (Masked)
        // Default target: https://autoclash-control-default-rtdb.firebaseio.com/
        // We will provide a clean way to update this when the user configures their Firebase project.
        private static readonly byte[] EncryptedEndpoint = Transform(
            "https://autoclash-control-default-rtdb.firebaseio.com/autoclash"
        );

        private static readonly byte[] EncryptedSalt = Transform(
            "ACR_Secret_Salt_Key_9882194812_NavisworksManage_2026"
        );

        internal static string GetLicenseEndpoint() => Unmask(EncryptedEndpoint);
        internal static string GetMasterSalt() => Unmask(EncryptedSalt);

        private static byte[] Transform(string input)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)(bytes[i] ^ MaskKey[i % MaskKey.Length]);
            }
            return bytes;
        }

        private static string Unmask(byte[] masked)
        {
            byte[] copy = new byte[masked.Length];
            for (int i = 0; i < masked.Length; i++)
            {
                copy[i] = (byte)(masked[i] ^ MaskKey[i % MaskKey.Length]);
            }
            return Encoding.UTF8.GetString(copy);
        }
    }
}
