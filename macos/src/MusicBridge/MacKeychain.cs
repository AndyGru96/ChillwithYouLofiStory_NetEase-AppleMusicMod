using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MusicBridge;

// Only accesses this mod's own generic-password item. No security CLI/argv secrets.
internal static class MacKeychain
{
    const string Security = "/System/Library/Frameworks/Security.framework/Security";
    const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    const int NotFound = -25300;
    static readonly byte[] Account = Encoding.UTF8.GetBytes("netease-session-v1");
    internal static string Service = "com.chillwithyou.musicbridge.macos";
    [DllImport(Security)] static extern int SecKeychainFindGenericPassword(IntPtr keychain, uint serviceLength, byte[] service, uint accountLength, byte[] account, out uint length, out IntPtr data, out IntPtr item);
    [DllImport(Security)] static extern int SecKeychainAddGenericPassword(IntPtr keychain, uint serviceLength, byte[] service, uint accountLength, byte[] account, uint length, byte[] data, out IntPtr item);
    [DllImport(Security)] static extern int SecKeychainItemModifyAttributesAndData(IntPtr item, IntPtr attributes, uint length, byte[] data);
    [DllImport(Security)] static extern int SecKeychainItemDelete(IntPtr item);
    [DllImport(Security)] static extern int SecKeychainItemFreeContent(IntPtr attributes, IntPtr data);
    [DllImport(CoreFoundation)] static extern void CFRelease(IntPtr item);

    static int Find(out uint length, out IntPtr data, out IntPtr item)
    {
        var service = Encoding.UTF8.GetBytes(Service);
        return SecKeychainFindGenericPassword(IntPtr.Zero, (uint)service.Length, service, (uint)Account.Length, Account, out length, out data, out item);
    }
    static void Check(int status) { if (status != 0) throw new InvalidOperationException("钥匙串访问失败 (OSStatus " + status + ")；未保存明文，请检查钥匙串授权。"); }
    static void Free(IntPtr data, IntPtr item) { if (data != IntPtr.Zero) SecKeychainItemFreeContent(IntPtr.Zero, data); if (item != IntPtr.Zero) CFRelease(item); }
    public static string Load()
    {
        uint length; IntPtr data, item;
        int status = Find(out length, out data, out item);
        try
        {
            if (status == NotFound) return null;
            Check(status);
            if (length > 4 * 1024 * 1024) throw new InvalidOperationException("钥匙串会话超过大小上限。");
            var bytes = new byte[length];
            try { Marshal.Copy(data, bytes, 0, bytes.Length); return Encoding.UTF8.GetString(bytes); }
            finally { Array.Clear(bytes, 0, bytes.Length); }
        }
        finally { Free(data, item); }
    }
    public static void Save(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        uint length; IntPtr data, item;
        int status = Find(out length, out data, out item);
        try
        {
            if (status == NotFound)
            {
                var service = Encoding.UTF8.GetBytes(Service);
                Check(SecKeychainAddGenericPassword(IntPtr.Zero, (uint)service.Length, service, (uint)Account.Length, Account, (uint)bytes.Length, bytes, out item));
            }
            else { Check(status); Check(SecKeychainItemModifyAttributesAndData(item, IntPtr.Zero, (uint)bytes.Length, bytes)); }
        }
        finally { Array.Clear(bytes, 0, bytes.Length); Free(data, item); }
    }
    public static void Delete()
    {
        uint length; IntPtr data, item;
        int status = Find(out length, out data, out item);
        try { if (status != NotFound) { Check(status); Check(SecKeychainItemDelete(item)); } }
        finally { Free(data, item); }
    }
}
