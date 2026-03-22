namespace Forex.Wpf.Common.Services;
using System.IO;
using System.Management;
using System.Security.Cryptography;

public static partial class DevKeyService
{
    private const string SecureKeyFileName = "forex.key";

    // ? AES shifrlash uchun kalit va IV (16 byte)
    private static readonly byte[] EncryptionKey =
    {
        0x2B, 0x7E, 0x15, 0x16, 0x28, 0xAE, 0xD2, 0xA6,
        0xAB, 0xF7, 0x15, 0x88, 0x09, 0xCF, 0x4F, 0x3C,
        0x2B, 0x7E, 0x15, 0x16, 0x28, 0xAE, 0xD2, 0xA6,
        0xAB, 0xF7, 0x15, 0x88, 0x09, 0xCF, 0x4F, 0x3C
    };

    private static readonly byte[] InitializationVector =
    {
        0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07,
        0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F
    };

    public static (string login, string password)? TryGetSecureCredentials()
    {
        try
        {
            var removableDrives = DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Removable && d.IsReady);
            foreach (var drive in removableDrives)
            {
                var keyFilePath = Path.Combine(drive.RootDirectory.FullName, SecureKeyFileName);
                if (File.Exists(keyFilePath))
                {
                    var serialNumber = GetDriveSerialNumber(drive.Name);
                    var credentials = DecryptAndValidateKey(keyFilePath, serialNumber);
                    if (credentials != null) return credentials;
                }
            }
        }
        catch { }
        return null;
    }

    public static void GenerateHardwareKey(string drivePath, string login, string password)
    {
        if (!Directory.Exists(drivePath))
            throw new Exception($"Xato: '{drivePath}' diski topilmadi. Fleshka suqilganligini va harfi to'g'riligini tekshiring.");

        var serialNumber = GetDriveSerialNumber(drivePath);
        var rawContent = $"{login}|{password}|{serialNumber}";

        using var aes = Aes.Create();
        aes.Key = EncryptionKey;
        aes.IV = InitializationVector;

        using var encryptor = aes.CreateEncryptor();
        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs)) { sw.Write(rawContent); }

        var secureKeyPath = Path.Combine(drivePath, SecureKeyFileName);
        File.WriteAllBytes(secureKeyPath, ms.ToArray());
    }

    private static (string, string)? DecryptAndValidateKey(string filePath, string hardwareSerial)
    {
        try
        {
            var encryptedBytes = File.ReadAllBytes(filePath);

            using var aes = Aes.Create();
            aes.Key = EncryptionKey;
            aes.IV = InitializationVector;

            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream(encryptedBytes);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);

            var decryptedContent = sr.ReadToEnd();
            var parts = decryptedContent.Split('|');

            if (parts.Length == 3 && parts[2] == hardwareSerial)
                return (parts[0], parts[1]);
        }
        catch { }
        return null;
    }

    private static string GetDriveSerialNumber(string drivePath)
    {
        try
        {
            var driveId = drivePath.TrimEnd('\\').TrimEnd('/');
            using var searcher = new ManagementObjectSearcher($"SELECT VolumeSerialNumber FROM Win32_LogicalDisk WHERE DeviceID = '{driveId}'");
            using var collection = searcher.Get();

            foreach (var item in collection)
                return item["VolumeSerialNumber"]?.ToString() ?? "00000000";
        }
        catch { }
        return "UNKNOWN_HARDWARE";
    }
}