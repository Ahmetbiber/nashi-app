using System.Windows.Controls;
using Nashi.Core;
namespace Nashi.Windows;
public sealed partial class ToolEditor
{
    private readonly CheckBox preserveMetadata=new(){Content="Preserve metadata",IsChecked=true,Margin=new(4,12,4,12)};
    private OutputProfile exportProfile=new();
    internal void ConfigureProfiles(PreferencesStore preferences,string? requestedFormat=null)
    {
        if(tool!="convert")return;
        if(!fields.ContainsKey("Quality (%)"))Field("Quality (%)",85);
        if(media is not null){Field("Audio bitrate (kbps)",256);Field("Maximum video height (0 = original)",0);Field("GIF frames per second",15);Field("GIF width",960);options.Children.Add(new TextBlock{Text="Audio sample depth"});options.Children.Add(sampleDepth);}
        options.Children.Add(preserveMetadata);
        void Apply(){if(format.SelectedItem is not string value)return;exportProfile=preferences.Profile(value);fields["Quality (%)"].Text=Number(exportProfile.Quality*100);preserveMetadata.IsChecked=exportProfile.PreserveMetadata;
            if(fields.TryGetValue("Audio bitrate (kbps)",out var bitrate))bitrate.Text=Number(exportProfile.AudioBitrate);if(fields.TryGetValue("Maximum video height (0 = original)",out var height))height.Text=Number(exportProfile.MaxVideoHeight);if(fields.TryGetValue("GIF frames per second",out var fps))fps.Text=Number(exportProfile.GifFrameRate);if(fields.TryGetValue("GIF width",out var width))width.Text=Number(exportProfile.GifWidth);sampleDepth.SelectedItem=exportProfile.SampleDepth;
            if(fields.TryGetValue("DPI",out var dpi))dpi.Text=Number(exportProfile.Dpi);documentLayout.SelectedItem=exportProfile.Layout;epubLayout.SelectedItem=exportProfile.EpubLayout;
        }
        format.SelectionChanged+=(_,_)=>Apply();if(requestedFormat is not null&&format.Items.Contains(requestedFormat))format.SelectedItem=requestedFormat;Apply();
    }
    internal bool PreserveImageMetadata=>preserveMetadata.IsChecked==true;
}
