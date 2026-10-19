using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Nashi.Core;
namespace Nashi.Windows;

/// Extracts the official MSI as data. Does not install a product, run its custom
/// actions, register COM servers or change Windows Installer/system settings.
public sealed class OfficeRuntime(string root)
{
    public string? Executable=>new[]{Path.Combine(root,"office-26.8.1","program","soffice.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"LibreOffice","program","soffice.exe")}.FirstOrDefault(File.Exists);
    public async Task InstallAsync(IProgress<OperationProgress>? progress,CancellationToken token)
    {
        if(Executable is not null)return;Directory.CreateDirectory(root);var stage=Path.Combine(root,".office-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
        try
        {
            var msi=Path.Combine(stage,"office.msi");await VerifiedDownload.FetchAsync(new("https://download.documentfoundation.org/libreoffice/stable/26.8.1/win/x86_64/LibreOffice_26.8.1_Win_x86-64.msi"),msi,600L*1024*1024,p=>progress?.Report(new(p*.75,"Downloading LibreOffice 26.8.1…")),token);
            await using(var source=File.OpenRead(msi))if(Convert.ToHexString(await SHA256.HashDataAsync(source,token)).ToLowerInvariant()!="c6298b10cfa1748bcbedad94c62ce771e240e5021bfd6d7a967cd571b04b5312")throw new InvalidDataException("Office checksum mismatch. Nothing was installed.");
            var content=Path.Combine(stage,"content");Directory.CreateDirectory(content);await Task.Run(()=>MsiImage.Extract(msi,content,progress,token),token);
            var executable=Directory.GetFiles(content,"soffice.exe",SearchOption.AllDirectories).Single(p=>Path.GetFileName(Path.GetDirectoryName(p))=="program");var application=Directory.GetParent(Path.GetDirectoryName(executable)!)!.FullName;
            token.ThrowIfCancellationRequested();Directory.Move(application,Path.Combine(root,"office-26.8.1"));progress?.Report(new(1,"Office tools ready"));
        }
        finally{if(Directory.Exists(stage))Directory.Delete(stage,true);}
    }
    private static class MsiImage
    {
        [DllImport("msi.dll",CharSet=CharSet.Unicode)]private static extern uint MsiOpenDatabase(string path,IntPtr persistence,out uint database);
        [DllImport("msi.dll",CharSet=CharSet.Unicode)]private static extern uint MsiDatabaseOpenView(uint database,string query,out uint view);
        [DllImport("msi.dll")]private static extern uint MsiViewExecute(uint view,uint record);
        [DllImport("msi.dll")]private static extern uint MsiViewFetch(uint view,out uint record);
        [DllImport("msi.dll",CharSet=CharSet.Unicode)]private static extern uint MsiRecordGetString(uint record,uint field,StringBuilder buffer,ref uint length);
        [DllImport("msi.dll")]private static extern uint MsiRecordReadStream(uint record,uint field,byte[] buffer,ref uint size);
        [DllImport("msi.dll")]private static extern uint MsiCloseHandle(uint handle);
        private static void Check(uint result){if(result!=0)throw new IOException("Office package could not be extracted (Windows Installer data code "+result+").");}
        private static List<string[]> Rows(uint database,string sql,int fields)
        {
            Check(MsiDatabaseOpenView(database,sql,out var view));try{Check(MsiViewExecute(view,0));var result=new List<string[]>();uint code;
                while((code=MsiViewFetch(view,out var record))==0){try{var row=new string[fields];for(uint n=1;n<=fields;n++){var text=new StringBuilder(32768);uint length=32768;Check(MsiRecordGetString(record,n,text,ref length));row[n-1]=text.ToString();}result.Add(row);}finally{MsiCloseHandle(record);}}
                if(code!=259)Check(code);return result;
            }finally{MsiCloseHandle(view);}
        }
        private static string LongName(string name)=>name.Split(':')[0].Split('|')[^1];
        public static void Extract(string path,string destination,IProgress<OperationProgress>? progress,CancellationToken token)
        {
            Check(MsiOpenDatabase(path,IntPtr.Zero,out var db));try
            {
                var cabinets=Rows(db,"SELECT `Cabinet` FROM `Media`",1);var unpack=Path.Combine(Path.GetDirectoryName(destination)!,"cabinets");Directory.CreateDirectory(unpack);
                foreach(var cabinet in cabinets.Where(c=>c[0].StartsWith('#')))
                {
                    token.ThrowIfCancellationRequested();var name=cabinet[0][1..];if(name.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not ('.' or '_' or '-')))throw new InvalidDataException("Unsafe cabinet name.");var cab=Path.Combine(unpack,name);
                    Check(MsiDatabaseOpenView(db,"SELECT `Data` FROM `_Streams` WHERE `Name`='"+name+"'",out var view));try{Check(MsiViewExecute(view,0));Check(MsiViewFetch(view,out var record));try{using var file=new FileStream(cab,FileMode.CreateNew);var buffer=new byte[65536];while(true){uint size=(uint)buffer.Length;Check(MsiRecordReadStream(record,1,buffer,ref size));if(size==0)break;token.ThrowIfCancellationRequested();file.Write(buffer,0,(int)size);}}finally{MsiCloseHandle(record);}}finally{MsiCloseHandle(view);}
                    ProcessRunner.RunAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"expand.exe"),[cab,"-F:*",unpack],token).GetAwaiter().GetResult();File.Delete(cab);
                }
                var dirs=Rows(db,"SELECT `Directory`, `Directory_Parent`, `DefaultDir` FROM `Directory`",3).ToDictionary(r=>r[0]);var components=Rows(db,"SELECT `Component`, `Directory_` FROM `Component`",2).ToDictionary(r=>r[0],r=>r[1]);
                string Relative(string id,int depth=0){if(depth>64)throw new InvalidDataException("Office directory cycle.");if(id=="TARGETDIR"||!dirs.TryGetValue(id,out var row))return "";var parent=Relative(row[1],depth+1);var name=LongName(row[2]);return name is "." or ""?parent:Path.Combine(parent,name);}
                var files=Rows(db,"SELECT `File`, `Component_`, `FileName` FROM `File`",3);var index=0;
                foreach(var row in files){token.ThrowIfCancellationRequested();var source=SafePaths.ArchiveEntry(unpack,row[0]);if(!File.Exists(source))throw new InvalidDataException("Office cabinet is missing a file.");var target=SafePaths.ArchiveEntry(destination,Path.Combine(Relative(components[row[1]]),LongName(row[2])));Directory.CreateDirectory(Path.GetDirectoryName(target)!);if(File.Exists(target)){using var existing=File.OpenRead(target);using var incoming=File.OpenRead(source);if(!SHA256.HashData(existing).SequenceEqual(SHA256.HashData(incoming)))throw new InvalidDataException("Office package contains conflicting files: "+Path.GetRelativePath(destination,target));}else File.Move(source,target);progress?.Report(new(.75+.24*++index/files.Count,"Preparing office tools…"));}
            }finally{MsiCloseHandle(db);}
        }
    }
}
