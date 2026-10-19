using System.IO.Compression;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Nashi.Core;

public static class DocumentEngine
{
    public static IReadOnlyList<string> Pdf(string tool,IReadOnlyList<string> inputs,string destination,int[]? pages=null,CancellationToken token=default)
    {
        using var tx=new ExportTransaction(destination);var outputs=new List<string>();
        if(tool=="mergePDFs")
        {
            if(inputs.Count<2)throw new ArgumentException("Choose at least two PDFs.");
            using var merged=new PdfDocument();
            foreach(var file in inputs){token.ThrowIfCancellationRequested();using var source=PdfReader.Open(file,PdfDocumentOpenMode.Import);foreach(var page in source.Pages)merged.AddPage(page);}
            var output=SafePaths.Unique(tx.StagingDirectory,"Merged.pdf");merged.Save(output);outputs.Add(output);
        }
        else foreach(var input in inputs)
        {
            token.ThrowIfCancellationRequested();using var source=PdfReader.Open(input,PdfDocumentOpenMode.Import);
            if(tool=="splitPDF")
            {
                for(var i=0;i<source.PageCount;i++)
                {
                    token.ThrowIfCancellationRequested();using var part=new PdfDocument();part.AddPage(source.Pages[i]);
                    var output=SafePaths.Unique(tx.StagingDirectory,Path.GetFileNameWithoutExtension(input)+$" Page {i+1}.pdf");part.Save(output);outputs.Add(output);
                }
            }
            else if(tool=="organizePDF")
            {
                if(pages is null||pages.Length==0||pages.Any(p=>p<1||p>source.PageCount))throw new ArgumentException("Enter page numbers within this PDF, in the desired order.");
                using var result=new PdfDocument();foreach(var page in pages)result.AddPage(source.Pages[page-1]);
                var output=SafePaths.Unique(tx.StagingDirectory,Path.GetFileNameWithoutExtension(input)+" Organized.pdf");result.Save(output);outputs.Add(output);
            }
            else throw new ArgumentException("Unknown PDF tool.");
        }
        return tx.Publish(outputs,token);
    }
    public static string Zip(IReadOnlyList<string> inputs,string destination,IProgress<OperationProgress>? progress=null,CancellationToken token=default)
    {
        if(inputs.Count==0)throw new ArgumentException("Choose files to zip.");
        using var tx=new ExportTransaction(destination);var output=Path.Combine(tx.StagingDirectory,"Archive.zip");
        var usedNames=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using(var zip=ZipFile.Open(output,ZipArchiveMode.Create))
        for(var i=0;i<inputs.Count;i++)
        {
            token.ThrowIfCancellationRequested();var input=inputs[i];if(!File.Exists(input)||(File.GetAttributes(input)&FileAttributes.ReparsePoint)!=0)throw new ArgumentException("Choose regular files; links and folders are not included.");
            var name=Path.GetFileName(input);var n=2;while(usedNames.Contains(name))name=Path.GetFileNameWithoutExtension(input)+$" {n++}"+Path.GetExtension(input);
            usedNames.Add(name);zip.CreateEntryFromFile(input,name,CompressionLevel.Optimal);progress?.Report(new((i+1d)/inputs.Count*.98,"Packing files…"));
        }
        return tx.Publish([output],token)[0];
    }
    public static IReadOnlyList<string> Unzip(string input,string destination,IProgress<OperationProgress>? progress=null,CancellationToken token=default)
    {
        const long maxBytes=4L*1024*1024*1024;const int maxEntries=10000;
        using var zip=ZipFile.OpenRead(input);
        if(zip.Entries.Count>maxEntries)throw new InvalidDataException("Archive has too many entries (10,000 limit).");
        long total=0;var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var entry in zip.Entries)
        {
            total=checked(total+entry.Length);if(total>maxBytes)throw new InvalidDataException("Archive expands beyond 4 GB.");
            if(((entry.ExternalAttributes>>16)&0xF000)==0xA000||(entry.ExternalAttributes&0x400)!=0)throw new InvalidDataException("Archive contains a link.");
            if(!names.Add(entry.FullName.TrimEnd('/','\\')))throw new InvalidDataException("Archive contains duplicate names.");
        }
        using var tx=new ExportTransaction(destination);var folder=Path.Combine(tx.StagingDirectory,Path.GetFileNameWithoutExtension(input)+" Extracted");Directory.CreateDirectory(folder);
        var paths=zip.Entries.Select(e=>(Entry:e,Path:SafePaths.ArchiveEntry(folder,e.FullName.TrimEnd('/','\\')))).ToList();long copied=0;
        foreach(var pair in paths)
        {
            token.ThrowIfCancellationRequested();var entry=pair.Entry;
            if(entry.FullName.EndsWith('/')||entry.FullName.EndsWith('\\')){Directory.CreateDirectory(pair.Path);continue;}
            Directory.CreateDirectory(Path.GetDirectoryName(pair.Path)!);using var source=entry.Open();using var target=new FileStream(pair.Path,FileMode.CreateNew);
            var buffer=new byte[81920];long entryBytes=0;int count;
            while((count=source.Read(buffer))>0){token.ThrowIfCancellationRequested();entryBytes+=count;copied+=count;if(entryBytes>entry.Length||copied>maxBytes)throw new InvalidDataException("Archive size exceeds its declared limit.");target.Write(buffer,0,count);progress?.Report(new(total==0?null:(double)copied/total*.98,"Extracting files…"));}
        }
        return tx.Publish([folder],token);
    }
}
