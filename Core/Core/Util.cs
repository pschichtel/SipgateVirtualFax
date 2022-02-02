using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SipgateVirtualFax.Core;

public static class Util
{
    public static string AppPath()
    {
        var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appDataFolder, "SipgateFaxApp");
    }
    
    public static string AppPath(string fileName)
    {
        var appFolder = AppPath();
        Directory.CreateDirectory(appFolder);
        return Path.Combine(appFolder, fileName);
    }
    
    public static string? ReadEncryptedString(string path, Encoding encoding)
    {
        try
        {
            var encrypted = File.ReadAllBytes(path);
            var data = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return encoding.GetString(data);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }
}