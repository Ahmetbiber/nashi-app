using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
namespace Nashi.Core;
public sealed record DocumentOptions(int Dpi=144,string Layout="editable",string EpubLayout="reflowable",string Password="");
public sealed partial class OfficeDocuments(string office,string deno,string assets)
{
    private static string Escape(string value)=>WebUtility.HtmlEncode(value);
    public async Task<string> OfficeAsync(string input,string format,string outputDirectory,CancellationToken token)
    {
        Directory.CreateDirectory(outputDirectory);var profile=Path.Combine(outputDirectory,"profile-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(profile,"user"));
        try
        {
            await File.WriteAllTextAsync(Path.Combine(profile,"user","registrymodifications.xcu"),"<?xml version=\"1.0\"?><oor:items xmlns:oor=\"http://openoffice.org/2001/registry\"><item oor:path=\"/org.openoffice.Office.Common/Security/Scripting\"><prop oor:name=\"MacroSecurityLevel\" oor:op=\"fuse\"><value>3</value></prop></item><item oor:path=\"/org.openoffice.Office.Common/Load\"><prop oor:name=\"UpdateLinkMode\" oor:op=\"fuse\"><value>0</value></prop></item></oor:items>",token);
            var ext=FeatureCatalog.Extension(input);var spreadsheet=new[]{"csv","tsv","xls","xlsx","ods"}.Contains(ext);var slides=new[]{"ppt","pptx"}.Contains(ext);
            var filter=format switch{"docx"=>"docx:Office Open XML Text","pptx"=>"pptx:Impress MS PowerPoint 2007 XML","xlsx"=>"xlsx:Calc MS Excel 2007 XML","pdf"=>"pdf:"+(spreadsheet?"calc_pdf_Export":slides?"impress_pdf_Export":"writer_pdf_Export"),"txt"=>"txt:Text (encoded):UTF8,LF","csv"=>"csv:Text - txt - csv (StarCalc):44,34,76,1,,0,false,true,true,false,false,0","tsv"=>"csv:Text - txt - csv (StarCalc):9,34,76,1,,0,false,true,true,false,false,0","html"=>"html:HTML (StarWriter)","md"=>"md:Markdown","epub"=>"epub:EPUB",_=>format};
            var args=new List<string>{"-env:UserInstallation="+new Uri(profile+Path.DirectorySeparatorChar).AbsoluteUri,"--headless","--nologo","--nodefault","--nolockcheck","--nofirststartwizard","--norestore"};
            if(ext is "csv" or "tsv")args.Add("--infilter=Text - txt - csv (StarCalc):"+(ext=="tsv"?9:44)+",34,76,1,,1033,true,true");else if(ext=="txt")args.Add("--infilter=Text (encoded):UTF8,LF");else if(ext=="md")args.Add("--infilter=Markdown");
            if(ext=="html"&&format is "xlsx" or "ods" or "csv" or "tsv")args.Add("--infilter=HTML (StarCalc)");
            if(ext=="pdf"&&format=="pptx")args.Add("--infilter=impress_pdf_import");
            args.AddRange(["--convert-to",filter,"--outdir",outputDirectory,Path.GetFullPath(input)]);await ProcessRunner.RunAsync(office,args,token);
            var output=Path.Combine(outputDirectory,Path.GetFileNameWithoutExtension(input)+"."+(format=="tsv"?"csv":format));if(!File.Exists(output)||new FileInfo(output).Length==0)throw new IOException("Office could not convert this document to "+format.ToUpperInvariant()+".");return output;
        }
        finally{if(Directory.Exists(profile))Directory.Delete(profile,true);}
    }
    public async Task<string> PagesReadAsync(string input,string work,CancellationToken token)
    {
        if(new FileInfo(input).Length>200_000_000)throw new InvalidDataException("Pages input exceeds 200 MB.");var json=Path.Combine(work,"pages.json");await PagesCall("read",input,json,token);using var model=JsonDocument.Parse(await File.ReadAllTextAsync(json,token));var html=Path.Combine(work,"Pages document.html");await File.WriteAllTextAsync(html,ModelHtml(model.RootElement),token);return html;
    }
    public async Task<string> PagesWriteAsync(string docx,string output,CancellationToken token)
    {
        var json=output+".json";try{await File.WriteAllTextAsync(json,JsonSerializer.Serialize(ReadDocx(docx)),token);await PagesCall("write",json,output,token);return output;}finally{if(File.Exists(json))File.Delete(json);}
    }
    private Task<ProcessResult> PagesCall(string mode,string input,string output,CancellationToken token)
    {
        var bundle=Path.Combine(assets,"PagesInterop.js");var script=Path.Combine(assets,"pages-windows.js");return ProcessRunner.RunAsync(deno,["run","--no-prompt","--no-config","--cached-only","--allow-read="+bundle.Replace(",",",,")+","+Path.GetFullPath(input).Replace(",",",,"),"--allow-write="+Path.GetFullPath(output).Replace(",",",,"),script,bundle,mode,Path.GetFullPath(input),Path.GetFullPath(output)],token);
    }
    private static XDocument Xml(ZipArchive zip,string entry)
    {
        var resource=zip.GetEntry(entry)??throw new InvalidDataException("The document is missing "+entry);if(resource.Length>20_000_000)throw new InvalidDataException("Document XML is too large.");using var stream=resource.Open();using var reader=XmlReader.Create(stream,new(){DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=20_000_000});return XDocument.Load(reader);
    }
    public static string PdfText(string input,string password="")
    {
        using var pdf=UglyToad.PdfPig.PdfDocument.Open(input,new(){Password=password});return string.Join("\n\n",pdf.GetPages().Select(p=>ContentOrderTextExtractor.GetText(p)));
    }
    public static string PdfHtml(string input,string password="")
    {
        using var pdf=UglyToad.PdfPig.PdfDocument.Open(input,new(){Password=password});return "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/></head><body>"+string.Join("",pdf.GetPages().Select(p=>"<section style=\"page-break-after:always\"><p style=\"white-space:pre-wrap\">"+Escape(ContentOrderTextExtractor.GetText(p))+"</p></section>"))+"</body></html>";
    }
    public static string SanitizedHtml(string html)
    {
        html=Regex.Replace(html,@"<(script|iframe|object|embed)\b[^>]*>[\s\S]*?</\1\s*>","",RegexOptions.IgnoreCase,TimeSpan.FromSeconds(2));
        html=Regex.Replace(html,@"<link\b[^>]*>|<base\b[^>]*>","",RegexOptions.IgnoreCase,TimeSpan.FromSeconds(2));
        html=Regex.Replace(html,"\\s(?:src|background)\\s*=\\s*([\"'])(?!data:)[^\"']*\\1","",RegexOptions.IgnoreCase,TimeSpan.FromSeconds(2));
        return Regex.Replace(html,@"url\s*\([^)]*\)","none",RegexOptions.IgnoreCase,TimeSpan.FromSeconds(2));
    }
}
