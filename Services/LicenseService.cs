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
            "AutomatedClashRunner", "License");
        private static readonly string LeaseFilePath = Path.Combine(LicenseDirectory, ".lease");

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
            if (_sessionAllowed.HasValue && _sessionAllowed.Value)
                return true;

            string hwid = HardwareFingerprint.GetMachineId();
            var offlineResult = TryOfflineLeaseVerification(hwid, DateTime.UtcNow);
            _sessionAllowed = offlineResult.IsAllowed;
            return offlineResult.IsAllowed;
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
                                : "Automated Clash Runner has been remotely deactivated by administrator.";
                            return new LicenseValidationResult
                            {
                                IsAllowed = false,
                                IsRevoked = true,
                                Message = killMsg
                            };
                        }

                        int leaseDays = DefaultLeaseDays;
                        if (rootData.ContainsKey("lease_days") && int.TryParse(rootData["lease_days"]?.ToString(), out int parsedDays))
                        {
                            leaseDays = Math.Max(1, parsedDays);
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

                        // If user is explicitly disabled
                        if (!machineEnabled)
                        {
                            DeleteLease();
                            return new LicenseValidationResult
                            {
                                IsAllowed = false,
                                IsRevoked = true,
                                Message = !string.IsNullOrWhiteSpace(machineMsg) 
                                    ? machineMsg 
                                    : "Your access license for Automated Clash Runner has been revoked by the administrator."
                            };
                        }

                        // Update or register machine record quietly in background
                        SilentRegisterOrPing(client, rootEndpoint, hwid, machineExists, nowUtc);

                        // Issue new encrypted 14-day offline lease
                        WriteEncryptedLease(hwid, nowUtc, leaseDays);

                        return new LicenseValidationResult
                        {
                            IsAllowed = true,
                            IsRevoked = false,
                            DaysRemaining = leaseDays,
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
                if (!File.Exists(LeaseFilePath))
                {
                    return new LicenseValidationResult
                    {
                        IsAllowed = false,
                        IsRevoked = false,
                        Message = "No active license lease found. Please connect to the internet once to activate."
                    };
                }

                byte[] encryptedBytes = File.ReadAllBytes(LeaseFilePath);
                string plainJson = Decrypt(encryptedBytes, hwid);

                if (string.IsNullOrWhiteSpace(plainJson))
                {
                    return new LicenseValidationResult
                    {
                        IsAllowed = false,
                        IsRevoked = false,
                        Message = "License lease is corrupt or invalid. Please connect to the internet to refresh."
                    };
                }

                var serializer = new JavaScriptSerializer();
                var lease = serializer.Deserialize<Dictionary<string, object>>(plainJson);

                if (lease == null || !lease.ContainsKey("hwid") || lease["hwid"]?.ToString() != hwid)
                {
                    return new LicenseValidationResult
                    {
                        IsAllowed = false,
                        IsRevoked = false,
                        Message = "License lease is bound to a different machine. Please connect to the internet to authorize."
                    };
                }

                DateTime expiresUtc = DateTime.Parse(lease["expires"].ToString()).ToUniversalTime();
                DateTime lastVerifiedUtc = DateTime.Parse(lease["last_verified"].ToString()).ToUniversalTime();

                // Anti-tampering: Clock rollback defense (system clock moved backwards > 2 hours)
                if (nowUtc < lastVerifiedUtc.AddHours(-2))
                {
                    return new LicenseValidationResult
                    {
                        IsAllowed = false,
                        IsRevoked = false,
                        Message = "System clock anomaly detected. Please synchronize your system clock and connect to the internet."
                    };
                }

                if (nowUtc > expiresUtc)
                {
                    return new LicenseValidationResult
                    {
                        IsAllowed = false,
                        IsRevoked = false,
                        Message = "Offline grace period has expired. Please connect to the internet to refresh authorization."
                    };
                }

                // Update last verified timestamp in local lease to prevent clock rewind
                int remainingDays = (int)Math.Ceiling((expiresUtc - nowUtc).TotalDays);
                lease["last_verified"] = nowUtc.ToString("o");
                string updatedJson = serializer.Serialize(lease);
                byte[] reEncrypted = Encrypt(updatedJson, hwid);
                
                if (File.Exists(LeaseFilePath))
                {
                    File.SetAttributes(LeaseFilePath, FileAttributes.Normal);
                }
                File.WriteAllBytes(LeaseFilePath, reEncrypted);

                return new LicenseValidationResult
                {
                    IsAllowed = true,
                    IsRevoked = false,
                    DaysRemaining = remainingDays,
                    Message = "Authorized (Offline Lease)"
                };
            }
            catch (Exception ex)
            {
                LoggerService.LogWarningStatic($"Lease validation failure: {ex.Message}");
                return new LicenseValidationResult
                {
                    IsAllowed = false,
                    IsRevoked = false,
                    Message = "License verification failed. Please connect to the internet to authorize."
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
