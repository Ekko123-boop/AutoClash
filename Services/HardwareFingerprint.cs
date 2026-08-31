using System;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace AutomatedClashRunner.Services
{
    public static class HardwareFingerprint
    {
        private static string _cachedHwid;
        private static readonly object _lock = new object();

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GetVolumeInformation(
            string rootPathName,
            StringBuilder volumeNameBuffer,
            int volumeNameSize,
            out uint volumeSerialNumber,
            out uint maximumComponentLength,
            out uint fileSystemFlags,
            StringBuilder fileSystemNameBuffer,
            int fileSystemNameSize);

        public static string GetMachineId()
        {
            if (!string.IsNullOrEmpty(_cachedHwid))
                return _cachedHwid;

            lock (_lock)
            {
                if (!string.IsNullOrEmpty(_cachedHwid))
                    return _cachedHwid;

                try
                {
                    string cpuId = GetCpuId();
                    string mbSerial = GetMotherboardSerial();
                    string volSerial = GetSystemDriveVolumeSerial();
                    string machineGuid = GetMachineGuid();

                    string combined = $"{cpuId}#{mbSerial}#{volSerial}#{machineGuid}";
                    
                    using (var sha = SHA256.Create())
                    {
                        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(combined));
                        StringBuilder sb = new StringBuilder("ACR-");
                        for (int i = 0; i < 8; i++)
                        {
                            sb.Append(hash[i].ToString("X2"));
                            if (i == 3) sb.Append("-");
                        }
                        _cachedHwid = sb.ToString();
                    }
                }
                catch
                {
                    // Fallback to basic environment identity if WMI is completely locked down
                    string fallback = $"{Environment.MachineName}#{Environment.UserName}#{GetMachineGuid()}";
                    using (var sha = SHA256.Create())
                    {
                        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(fallback));
                        _cachedHwid = "ACR-" + BitConverter.ToString(hash, 0, 8).Replace("-", "");
                    }
                }

                return _cachedHwid;
            }
        }

        public static string GetMachineDetails()
        {
            return $"{Environment.MachineName} ({Environment.UserName} on {Environment.OSVersion.VersionString})";
        }

        private static string GetCpuId()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        var id = mo["ProcessorId"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(id))
                            return id.Trim();
                    }
                }
            }
            catch { }
            return Environment.ProcessorCount.ToString();
        }

        private static string GetMotherboardSerial()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        var serial = mo["SerialNumber"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(serial) && !serial.Equals("None", StringComparison.OrdinalIgnoreCase))
                            return serial.Trim();
                    }
                }
            }
            catch { }
            return "MB-DEFAULT";
        }

        private static string GetSystemDriveVolumeSerial()
        {
            try
            {
                string systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
                if (!systemDrive.EndsWith("\\")) systemDrive += "\\";

                if (GetVolumeInformation(systemDrive, null, 0, out uint serialNum, out _, out _, null, 0))
                {
                    return serialNum.ToString("X8");
                }
            }
            catch { }
            return "VOL-DEFAULT";
        }

        private static string GetMachineGuid()
        {
            try
            {
                using (var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                    .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                {
                    if (key != null)
                    {
                        var guid = key.GetValue("MachineGuid")?.ToString();
                        if (!string.IsNullOrWhiteSpace(guid))
                            return guid;
                    }
                }
            }
            catch { }
            return "GUID-FALLBACK";
        }
    }
}
