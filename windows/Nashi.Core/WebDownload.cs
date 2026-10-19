using System.Text.Json;
namespace Nashi.Core;
public sealed class WebDownload(Toolchain tools)
{
    public static Uri Validate(string input)
    {
        if(!Uri.TryCreate(input,UriKind.Absolute,out var url)||url.Scheme is not ("http" or "https")||!string.IsNullOrEmpty(url.UserInfo)||url.IsLoopback||url.Host.EndsWith(".local",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Paste a public http(s) media or page link.");return url;
    }
    public async Task<IReadOnlyList<string>> DownloadAsync(string input,string kind,string destination,IProgress<OperationProgress>? progress=null,CancellationToken token=default)
    {
        var uri=Validate(input);if(kind is not ("video" or "audio" or "thumbnail"))throw new ArgumentException("Choose video, audio or thumbnail.");
        using var tx=new ExportTransaction(destination);
        var args=new List<string>{"--ignore-config","--no-playlist","--no-part","--no-overwrites","--no-cache-dir","--restrict-filenames","--no-mtime","--progress","--newline","--progress-template","download:NASHI:%(progress.downloaded_bytes)s:%(progress.total_bytes,progress.total_bytes_estimate)s","--print","after_move:filepath","--ffmpeg-location",tools.DirectoryPath,"--js-runtimes","deno:"+tools.Executable("deno"),"-P",tx.StagingDirectory,"-o","%(title).160B [%(id)s].%(ext)s"};
        if(kind=="audio")args.AddRange(["-x","--audio-format","mp3"]);else if(kind=="thumbnail")args.AddRange(["--skip-download","--write-thumbnail","--convert-thumbnails","jpg"]);else args.AddRange(["-f","bv*+ba/b","--merge-output-format","mp4"]);
        args.AddRange(["--",uri.AbsoluteUri]);
        progress?.Report(new(null,"Resolving media…"));
        await ProcessRunner.RunAsync(tools.Executable("yt-dlp"),args,token,line=>
        {
            if(!line.StartsWith("NASHI:",StringComparison.Ordinal))return;
            var parts=line.Split(':');if(parts.Length==3&&double.TryParse(parts[1],out var current)&&double.TryParse(parts[2],out var total)&&total>0)progress?.Report(new(Math.Clamp(current/total,0,.98),"Downloading media…"));
        });
        var files=Directory.GetFiles(tx.StagingDirectory).Where(p=>new FileInfo(p).Length>0).ToList();
        if(files.Count==0)throw new IOException("No public downloadable media found. Some sites require sign-in or do not expose downloadable media.");
        progress?.Report(new(.99,"Saving files…"));return tx.Publish(files,token);
    }
}
