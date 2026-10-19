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
public sealed class OfficeDocuments(string office,string deno,string assets)
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
        var bundle=Path.Combine(assets,"PagesInterop.js");var script=Path.Combine(assets,"pages-windows.js");return ProcessRunner.RunAsync(deno,["run","--no-prompt","--no-config","--cached-only","--allow-read="+bundle+","+Path.GetFullPath(input),"--allow-write="+Path.GetFullPath(output),script,bundle,mode,Path.GetFullPath(input),Path.GetFullPath(output)],token);
    }
    private static XDocument Xml(ZipArchive zip,string entry)
    {
        var resource=zip.GetEntry(entry)??throw new InvalidDataException("The document is missing "+entry);if(resource.Length>20_000_000)throw new InvalidDataException("Document XML is too large.");using var stream=resource.Open();using var reader=XmlReader.Create(stream,new(){DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=20_000_000});return XDocument.Load(reader);
    }
    public static object ReadDocx(string path)
    {
        using var zip=ZipFile.OpenRead(path);var xml=Xml(zip,"word/document.xml");XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";XNamespace a="http://schemas.openxmlformats.org/drawingml/2006/main";XNamespace r="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var relationships=zip.GetEntry("word/_rels/document.xml.rels") is null?new Dictionary<string,string>():Xml(zip,"word/_rels/document.xml.rels").Root!.Elements().Where(e=>e.Attribute("TargetMode")?.Value!="External").ToDictionary(e=>e.Attribute("Id")!.Value,e=>e.Attribute("Target")!.Value);
        var blocks=new List<object>();
        object Paragraph(XElement p)
        {
            var runs=new List<object>();foreach(var run in p.Descendants(w+"r"))
            {
                var text=string.Concat(run.Descendants().Select(n=>n.Name==w+"t"?n.Value:n.Name==w+"br"?"\n":n.Name==w+"tab"?"\t":""));var properties=run.Element(w+"rPr");
                if(text.Length>0)runs.Add(new{text,character=new{bold=properties?.Element(w+"b") is not null,italic=properties?.Element(w+"i") is not null,fontSize=double.TryParse(properties?.Element(w+"sz")?.Attribute(w+"val")?.Value,out var size)?size/2:12,fontName=properties?.Element(w+"rFonts")?.Attribute(w+"ascii")?.Value??"Arial"}});
                foreach(var blip in run.Descendants(a+"blip"))if(relationships.TryGetValue(blip.Attribute(r+"embed")?.Value??"",out var target))
                {var normalized="word/"+target.Replace('\\','/');if(target.Contains("..",StringComparison.Ordinal)||target.StartsWith('/'))continue;var entry=zip.GetEntry(normalized);if(entry is not null&&entry.Length<20_000_000){using var input=entry.Open();using var output=new MemoryStream();input.CopyTo(output);runs.Add(new{type="image",data=Convert.ToBase64String(output.ToArray()),fileName=Path.GetFileName(target)});}}
            }
            var alignment=p.Element(w+"pPr")?.Element(w+"jc")?.Attribute(w+"val")?.Value;return new{type="paragraph",runs,paragraph=new{alignment=alignment switch{"right"=>1,"center"=>2,"both"=>3,_=>0},pageBreakBefore=p.Element(w+"pPr")?.Element(w+"pageBreakBefore") is not null}};
        }
        foreach(var element in xml.Root!.Element(w+"body")!.Elements())
        {
            if(element.Name==w+"p")blocks.Add(Paragraph(element));
            else if(element.Name==w+"tbl")blocks.Add(new{type="table",rows=element.Elements(w+"tr").Select(row=>row.Elements(w+"tc").Select(cell=>new{text=string.Join("\n",cell.Elements(w+"p").Select(p=>string.Concat(p.Descendants(w+"t").Select(t=>t.Value))))}).ToArray()).ToArray()});
        }
        return new{blocks};
    }
    public static string ModelHtml(JsonElement model)
    {
        var html=new StringBuilder("<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><style>body{font:12pt Arial}table{border-collapse:collapse}td{border:1px solid #888;padding:5pt}p{white-space:pre-wrap}img{max-width:100%;height:auto}</style></head><body>");
        foreach(var block in model.GetProperty("blocks").EnumerateArray())
        {
            if(block.TryGetProperty("type",out var type)&&type.GetString()=="table")
            {html.Append("<table>");foreach(var row in block.GetProperty("rows").EnumerateArray()){html.Append("<tr>");foreach(var cell in row.EnumerateArray())html.Append("<td>").Append(Escape(cell.GetProperty("text").GetString()??"")).Append("</td>");html.Append("</tr>");}html.Append("</table>");continue;}
            html.Append("<p>");if(block.TryGetProperty("runs",out var runs))foreach(var run in runs.EnumerateArray())
            {
                if(run.TryGetProperty("type",out var kind)&&kind.GetString()=="image"){var name=run.GetProperty("fileName").GetString()??"image.png";var mime=FeatureCatalog.Extension(name) is "jpg" or "jpeg"?"image/jpeg":"image/png";var bytes=Convert.FromBase64String(run.GetProperty("data").GetString()??"");if(bytes.Length>20_000_000)throw new InvalidDataException("Embedded image exceeds 20 MB.");html.Append("<img src=\"data:").Append(mime).Append(";base64,").Append(Convert.ToBase64String(bytes)).Append("\"/>");}
                else if(run.TryGetProperty("text",out var text)){var value=Escape(text.GetString()??"").Replace("\n","<br/>");if(run.TryGetProperty("character",out var character)){if(character.TryGetProperty("bold",out var bold)&&bold.ValueKind==JsonValueKind.True)value="<b>"+value+"</b>";if(character.TryGetProperty("italic",out var italic)&&italic.ValueKind==JsonValueKind.True)value="<i>"+value+"</i>";}html.Append(value);}
            }html.Append("</p>");
        }
        return html.Append("</body></html>").ToString();
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
