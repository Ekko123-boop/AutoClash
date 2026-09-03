using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace AutomatedClashRunner.Services
{
    public class LicenseValidationResult
    {
        public bool IsAllowed { get; set; }
        public bool IsRevoked { get; set; }
        public string Message { get; set; }
        public int DaysRemaining { get; set; }
    }

    public static class LicenseService
    {
        private const int DefaultLeaseDays = 14;
        private static readonly object _lock = new object();
        private static bool? _sessionAllowed;
        private static readonly string LicenseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CypherNavisTools", "License");
        private static readonly string LegacyLicenseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AutomatedClashRunner", "License");
        private static readonly string LeaseFilePath = Path.Combine(LicenseDirectory, ".lease");
        private static readonly string RevocationFilePath = Path.Combine(LicenseDirectory, ".revoked");

        private static void SaveRevocation(string message)
        {
            try
            {
                if (!Directory.Exists(LicenseDirectory))
                {
                    Directory.CreateDirectory(LicenseDirectory);
                }
                File.WriteAllText(RevocationFilePath, message ?? string.Empty);
            }
            catch { }
        }

        private static void ClearRevocation()
        {
            try
            {
                if (File.Exists(RevocationFilePath))
                {
                    File.Delete(RevocationFilePath);
                }
            }
            catch { }
        }

        static LicenseService()
        {
            MigrateLegacyLease();
        }

        private static void MigrateLegacyLease()
        {
            try
            {
                string legacyLease = Path.Combine(LegacyLicenseDirectory, ".lease");
                if (File.Exists(legacyLease) && !File.Exists(LeaseFilePath))
                {
                    if (!Directory.Exists(LicenseDirectory))
                    {
                        Directory.CreateDirectory(LicenseDirectory);
                    }
                    File.Copy(legacyLease, LeaseFilePath, true);
                }
            }
            catch { }
        }

        public static LicenseValidationResult Validate()
        {
            lock (_lock)
            {
                string hwid = HardwareFingerprint.GetMachineId();
                DateTime nowUtc = DateTime.UtcNow;

                // 1. Attempt online verification against Firebase Realtime Database
                var onlineResult = TryOnlineVerification(hwid, nowUtc);
                if (onlineResult != null)
                {
                    _sessionAllowed = onlineResult.IsAllowed;
                    return onlineResult;
                }

                // 2. Fallback to offline lease validation
                var offlineResult = TryOfflineLeaseVerification(hwid, nowUtc);
                _sessionAllowed = offlineResult.IsAllowed;
                return offlineResult;
            }
        }

        public static bool QuickValidate()
        {
            lock (_lock)
            {
                if (_sessionAllowed.HasValue && _sessionAllowed.Value)
                    return true;

                string hwid = HardwareFingerprint.GetMachineId();
                var offlineResult = TryOfflineLeaseVerification(hwid, DateTime.UtcNow);
                _sessionAllowed = offlineResult.IsAllowed;
                return offlineResult.IsAllowed;
            }
        }

        private static LicenseValidationResult TryOnlineVerification(string hwid, DateTime nowUtc)
        {
            try
            {
                string rootEndpoint = StringProtection.GetLicenseEndpoint();
                if (string.IsNullOrWhiteSpace(rootEndpoint))
                    return null;

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(4);

                    // Fetch global config & machine record in one call
                    string getUrl = $"{rootEndpoint}.json";
                    var response = client.GetAsync(getUrl).GetAwaiter().GetResult();
                    
                    if (response.IsSuccessStatusCode)
                    {
                        string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        var serializer = new JavaScriptSerializer();
                        var rootData = serializer.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();

                        // Check global kill switch
                        if (rootData.ContainsKey("global_kill") && Convert.ToBoolean(rootData["global_kill"]))
                        {
                            DeleteLease();
                            string killMsg = rootData.ContainsKey("kill_message") 
                                ? rootData["kill_message"]?.ToString() 
                                : "Cypher Tools is temporarily unavailable. Please contact administrator.";
                            SaveRevocation(killMsg);
                            return new LicenseValidationResult
                            {
                                IsAllowed = false,
                                IsRevoked = true,
                                Message = killMsg
                            };
                        }

                        // Inspect machines registry
                        Dictionary<string, object> machines = null;
                        if (rootData.ContainsKey("machines") && rootData["machines"] is Dictionary<string, object> machDict)
                        {
                            machines = machDict;
                        }

                        bool machineEnabled = true;
                        string machineMsg = null;
                        bool machineExists = false;

                        if (machines != null && machines.ContainsKey(hwid))
                        {
                            machineExists = true;
                            if (machines[hwid] is Dictionary<string, object> mData)
                            {
                                if (mData.ContainsKey("enabled"))
                                {
                                    machineEnabled = Convert.ToBoolean(mData["enabled"]);
                                }
                                if (mData.ContainsKey("message"))
                                {
                                    machineMsg = mData["message"]?.ToString();
                                }
                            }
                        }

                        // If user is explicitly disabled by administrator
                        if (!machineEnabled)
                        {
                            DeleteLease();
                            string finalMsg = !string.IsNullOrWhiteSpace(machineMsg) 
                                ? machineMsg 
                                : "Cypher Tools is temporarily unavailable. Please contact administrator.";
                            SaveRevocation(finalMsg);
                            return new LicenseValidationResult
                            {
                                IsAllowed = false,
                                IsRevoked = true,
                                Message = finalMsg
                            };
                        }

                        // Clear any local revocation marker since access is approved
                        ClearRevocation();

                        // Update or register machine record quietly in background
                        SilentRegisterOrPing(client, rootEndpoint, hwid, machineExists, nowUtc);

                        return new LicenseValidationResult
                        {
                            IsAllowed = true,
                            IsRevoked = false,
                            Message = "Authorized"
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogWarningStatic($"Online license check unreachable: {ex.Message}");
            }

            return null; // Fallback to offline lease
        }

        private static void SilentRegisterOrPing(HttpClient client, string rootEndpoint, string hwid, bool exists, DateTime nowUtc)
        {
            try
            {
                var payload = new Dictionary<string, object>
                {
                    { "product", "Cypher Tools" },
                    { "last_seen", nowUtc.ToString("o") },
                    { "user", Environment.UserName },
                    { "machine", Environment.MachineName },
                    { "os", Environment.OSVersion.VersionString }
                };

                if (!exists)
                {
                    payload["enabled"] = true;
                    payload["first_seen"] = nowUtc.ToString("o");
                    payload["hwid"] = hwid;
                }

                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Send background update (PATCH so existing fields like custom notes or enabled flag are preserved)
                var patchMethod = new HttpMethod("PATCH");
                var request = new HttpRequestMessage(patchMethod, $"{rootEndpoint}/machines/{hwid}.json")
                {
                    Content = content
                };
                client.SendAsync(request);
            }
            catch { }
        }

        private static LicenseValidationResult TryOfflineLeaseVerification(string hwid, DateTime nowUtc)
        {
            try
            {
                // Check if this machine was previously deactivated by administrator
                if (File.Exists(RevocationFilePath))
                {
                    string msg = null;
                    try { msg = File.ReadAllText(RevocationFilePath); } catch { }
                    return new LicenseValidationResult
                    {
                        IsAllowed = false,
                        IsRevoked = true,
                        Message = !string.IsNullOrWhiteSpace(msg) 
                            ? msg 
                            : "Cypher Tools is temporarily unavailable. Please contact administrator."
                    };
                }

                // Normal offline operation: seamlessly allowed with zero trial or license warnings
                return new LicenseValidationResult
                {
                    IsAllowed = true,
                    IsRevoked = false,
                    Message = "Authorized"
                };
            }
            catch
            {
                return new LicenseValidationResult
                {
                    IsAllowed = true,
                    IsRevoked = false,
                    Message = "Authorized"
                };
            }
        }

        private static void WriteEncryptedLease(string hwid, DateTime nowUtc, int leaseDays)
        {
            try
            {
                if (!Directory.Exists(LicenseDirectory))
                {
                    Directory.CreateDirectory(LicenseDirectory);
                }

                var leaseData = new Dictionary<string, object>
                {
                    { "hwid", hwid },
                    { "issued", nowUtc.ToString("o") },
                    { "last_verified", nowUtc.ToString("o") },
                    { "expires", nowUtc.AddDays(leaseDays).ToString("o") },
                    { "lease_days", leaseDays }
                };

                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(leaseData);
                byte[] encrypted = Encrypt(json, hwid);

                if (File.Exists(LeaseFilePath))
                {
                    File.SetAttributes(LeaseFilePath, FileAttributes.Normal);
                }
                File.WriteAllBytes(LeaseFilePath, encrypted);
            }
            catch (Exception ex)
            {
                LoggerService.LogWarningStatic($"Could not write lease file: {ex.Message}");
            }
        }

        private static void DeleteLease()
        {
            try
            {
                if (File.Exists(LeaseFilePath))
                {
                    File.SetAttributes(LeaseFilePath, FileAttributes.Normal);
                    File.Delete(LeaseFilePath);
                }
            }
            catch { }
        }

        private static byte[] Encrypt(string plainText, string hwid)
        {
            byte[] salt = Encoding.UTF8.GetBytes(StringProtection.GetMasterSalt());
            using (var keyDerivation = new Rfc2898DeriveBytes(hwid, salt, 10000))
            {
                byte[] key = keyDerivation.GetBytes(32);
                byte[] iv = keyDerivation.GetBytes(16);

                using (var aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = iv;
                    using (var ms = new MemoryStream())
                    {
                        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                        {
                            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                            cs.Write(plainBytes, 0, plainBytes.Length);
                            cs.FlushFinalBlock();
                        }
                        return ms.ToArray();
                    }
                }
            }
        }

        private static string Decrypt(byte[] cipherText, string hwid)
        {
            try
            {
                byte[] salt = Encoding.UTF8.GetBytes(StringProtection.GetMasterSalt());
                using (var keyDerivation = new Rfc2898DeriveBytes(hwid, salt, 10000))
                {
                    byte[] key = keyDerivation.GetBytes(32);
                    byte[] iv = keyDerivation.GetBytes(16);

                    using (var aes = Aes.Create())
                    {
                        aes.Key = key;
                        aes.IV = iv;
                        using (var ms = new MemoryStream())
                        {
                            using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                            {
                                cs.Write(cipherText, 0, cipherText.Length);
                                cs.FlushFinalBlock();
                            }
                            return Encoding.UTF8.GetString(ms.ToArray());
                        }
                    }
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
