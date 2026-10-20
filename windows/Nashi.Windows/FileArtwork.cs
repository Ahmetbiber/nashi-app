using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
namespace Nashi.Windows;
internal static class FileArtwork
{
    internal static ImageSource? Icon(string path)
    {
        var flags=0x100u|0x1u; // SHGFI_ICON | SHGFI_SMALLICON
        if(!File.Exists(path)&&!Directory.Exists(path))flags|=0x10; // name/extension only for missing entries
        _ = SHGetFileInfo(path,0x80,out var info,(uint)Marshal.SizeOf<Info>(),flags);
        if(info.Icon==IntPtr.Zero)return null;
        try{var image=Imaging.CreateBitmapSourceFromHIcon(info.Icon,System.Windows.Int32Rect.Empty,System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());image.Freeze();return image;}finally{DestroyIcon(info.Icon);}
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct Info{public IntPtr Icon;public int Index;public uint Attributes;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string Name;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)]public string Type;}
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr SHGetFileInfo(string path,uint attributes,out Info info,uint size,uint flags);
    [DllImport("user32.dll")]private static extern bool DestroyIcon(IntPtr icon);
}
