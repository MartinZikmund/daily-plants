using System.Runtime.InteropServices;

namespace DailyPlants.Helpers;

/// <summary>
/// On macOS the title bar and the Mica backdrop follow the system appearance, not the app's theme, so a dark app
/// on a light Mac gets a light title bar and a grey wash behind the content. This hands the app's theme to AppKit.
/// </summary>
public static class MacAppearance
{
    private const string ObjC = "/usr/lib/libobjc.A.dylib";

    public static void Apply(ElementTheme theme)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var name = NSString(theme == ElementTheme.Dark ? "NSAppearanceNameDarkAqua" : "NSAppearanceNameAqua");
        var appearance = Send(Class("NSAppearance"), Selector("appearanceNamed:"), name);
        var app = Send(Class("NSApplication"), Selector("sharedApplication"));
        Send(app, Selector("setAppearance:"), appearance);
    }

    private static nint NSString(string value)
    {
        var utf8 = Marshal.StringToCoTaskMemUTF8(value);
        try
        {
            return Send(Class("NSString"), Selector("stringWithUTF8String:"), utf8);
        }
        finally
        {
            Marshal.FreeCoTaskMem(utf8);
        }
    }

    private static nint Class(string name) => objc_getClass(name);

    private static nint Selector(string name) => sel_registerName(name);

    [DllImport(ObjC)]
    private static extern nint objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjC)]
    private static extern nint sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nint Send(nint receiver, nint selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nint Send(nint receiver, nint selector, nint argument);
}
