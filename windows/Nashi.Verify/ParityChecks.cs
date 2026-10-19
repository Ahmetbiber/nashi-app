using Nashi.Core;
using System.IO.Compression;
using System.Text;
using ImageMagick;
internal static class ParityChecks
{
    public static void Run(Action<bool,string> check,string root)
    {
        check(FeatureCatalog.Conversions.Count==45&&FeatureCatalog.ToolNames.Count==27,"Mac/Windows catalog count diverged");
        check(FeatureCatalog.Formats(["a.jpeg","b.png"]).Contains("webp"),"Mixed image conversion intersection failed");
        check(!FeatureCatalog.Formats(["a.png","b.mp4"]).Any(),"Unrelated inputs offered an invalid format");
        check(FeatureCatalog.Tools(["a.pdf","b.pdf"]).SequenceEqual(new[]{"compress","removeMetadata","splitPDF","mergePDF","readQRCode"}),"Multiple PDF wheel diverged from Mac");
        check(FeatureCatalog.Tools(["a.jpg","b.png"]).Contains("createCollage"),"Mixed image collage missing");
        var preferences=new PreferencesStore(Path.Combine(root,"Preferences"));preferences.Save(preferences.Value with{Feedback=false,WheelScale=1.2,FloatBehavior="addToExisting"});preferences.SetProfile("jpg",new(Quality:.7));var persisted=new PreferencesStore(Path.Combine(root,"Preferences"));check(!persisted.Value.Feedback&&persisted.Value.WheelScale==1.2&&persisted.Profile("jpg").Quality==.7,"Windows settings/export defaults did not persist");try{persisted.Save(persisted.Value with{ToolsModifier="Shift"});throw new Exception("Conflicting drag shortcuts accepted");}catch(ArgumentException){check(true,"Drag shortcut collision validation");}
        check(WebMediaPayload.Parse("https://i.ytimg.com/vi/mXS5W7_kdzk/hqdefault.jpg") is {Kind:"youtube",Url:"https://www.youtube.com/watch?v=mXS5W7_kdzk"},"YouTube home thumbnail did not recover its video");
        check(WebMediaPayload.Parse("https://cdn.example/image.jpg","<a href=\"https://www.instagram.com/p/FixtureCode/\"><img src=\"https://cdn.example/image.jpg\"/></a>") is {Kind:"instagramPhotos"},"Instagram grid image chose video/thumbnail");check(WebMediaPayload.Parse("https://www.instagram.com/reel/FixtureCode/") is {Kind:"instagramReel"},"Reel classification failed");check(WebMediaPayload.Parse("https://cdn.example/image.jpg") is {Kind:"image"},"Direct image classification failed");
        var photoFixture="{\"entries\":[{\"thumbnails\":[{\"url\":\"https://scontent.cdninstagram.com/photo.jpg?stp=c0.0.720.720a_s640x640\",\"width\":640,\"height\":640},{\"url\":\"https://scontent.cdninstagram.com/original.jpg?sig=keep\",\"width\":1440,\"height\":1920}]},{\"duration\":5,\"formats\":[{\"url\":\"https://scontent.cdninstagram.com/video.mp4\"}]}]}";var photos=WebPhotos.Instagram(photoFixture);check(photos.Length==1&&photos[0].Url.EndsWith("original.jpg?sig=keep",StringComparison.Ordinal),"Instagram carousel did not choose full-frame photo or skipped video incorrectly");
        var srt="1\r\n00:00:01,005 --> 00:00:02,230\r\n<b>Türkçe 🍐</b>\r\n\r\n2\r\n00:00:03,000 --> 00:00:04,000\r\nSecond line\r\n";
        var vtt=SubtitleCodec.Convert(srt,"vtt");check(vtt.StartsWith("WEBVTT",StringComparison.Ordinal)&&vtt.Contains("00:00:01.005"),"SRT/VTT timestamps changed");check(SubtitleCodec.Parse(SubtitleCodec.Convert(vtt,"srt")).Length==2,"Subtitle round trip lost cues");check(SubtitleCodec.Convert(vtt,"txt").Contains("Türkçe 🍐")&&!SubtitleCodec.Convert(vtt,"txt").Contains("<b>"),"Subtitle plain text failed");
        try{SubtitleCodec.Parse("00:00:02.000 --> 00:00:01.000\ninvalid");throw new Exception("Reversed subtitle accepted");}catch(InvalidDataException){check(true,"Subtitle validation");}
        var input=Path.Combine(root,"Archive source.txt");File.WriteAllText(input,"Türkçe 🍐 retained");var zip=DocumentEngine.Zip([input],root);
        foreach(var format in new[]{"tar","gz","zip"})
        {
            Console.WriteLine("Verifying archive conversion: "+format);
            var converted=ArchiveEngine.Convert(zip,format,root)[0];var restored=ArchiveEngine.Extract(converted,root)[0];check(File.ReadAllText(Path.Combine(restored,"Archive source.txt"))==File.ReadAllText(input),"Archive round trip changed contents: "+format);
        }
        var attack=Path.Combine(root,"Unsafe second.zip");using(var archive=ZipFile.Open(attack,ZipArchiveMode.Create)){using var stream=archive.CreateEntry("../outside.txt").Open();stream.WriteByte(1);}
        try{ArchiveEngine.Extract(attack,root);throw new Exception("Traversal archive accepted");}catch(InvalidDataException){check(true,"New archive traversal protection");}
        var svg=Path.Combine(root,"Static.svg");File.WriteAllText(svg,"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"96\" height=\"64\"><rect width=\"96\" height=\"64\" fill=\"#E5BC59\"/></svg>");
        var png=PhotoCodec.Png(svg);using(var image=new MagickImage(png))check(image.Width==96&&image.Height==64,"SVG native rasterization failed");
        var external=Path.Combine(root,"External.svg");File.WriteAllText(external,"<svg xmlns=\"http://www.w3.org/2000/svg\"><image href=\"file:///private/example.png\"/></svg>");try{PhotoCodec.Png(external);throw new Exception("External SVG resource accepted");}catch(InvalidDataException){check(true,"SVG resource isolation");}
        foreach(var format in new[]{"png","jpg","webp","heic","avif","tiff","bmp"})
        {if(format=="heic"&&Environment.GetEnvironmentVariable("NASHI_VERIFY_FFMPEG") is null)continue;var output=Path.Combine(root,"Image codec."+format);PhotoCodec.EncodePng(png,output,format,encoder:Environment.GetEnvironmentVariable("NASHI_VERIFY_FFMPEG"));using var decoded=PhotoCodec.Read(output);check(decoded.Width==96&&decoded.Height==64,"Image codec round trip failed: "+format);}
        var neutral=PhotoCodec.Adjust(png,new());using(var decoded=new MagickImage(neutral))check(decoded.Width==96&&decoded.Height==64,"Neutral photo adjustment changed dimensions");
        var changed=PhotoCodec.Adjust(png,new(){Brightness=.2,Saturation=.3,Temperature=.2,Shadows=.1});using(var a=new MagickImage(png))using(var b=new MagickImage(changed))check(a.Compare(b,ErrorMetric.Absolute)>0,"Photo adjustments did not change pixels");
        foreach(var effect in new[]{new PhotoAdjustments{Blur=2},new(){Sharpen=1},new(){NoiseReduction=.5},new(){Grain=.3},new(){Vignette=.4},new(){Gamma=1.2},new(){Exposure=.5},new(){Hue=50},new(){Dehaze=.2}})check(PhotoCodec.Adjust(png,effect).Length>0,"Photo effect failed");
        try{PhotoCodec.Adjust(png,new(){Gamma=double.NaN});throw new Exception("Invalid adjustment accepted");}catch(ArgumentException){check(true,"Photo finite validation");}
    }
}
