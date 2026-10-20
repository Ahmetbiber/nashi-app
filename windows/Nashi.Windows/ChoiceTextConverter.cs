using System.Globalization;
using System.Windows.Data;
using Nashi.Core;
namespace Nashi.Windows;
public sealed class ChoiceTextConverter:IValueConverter
{
    private static readonly Dictionary<string,string> Labels=new(StringComparer.Ordinal){["matchSource"]="Match source",["pcm16"]="16-bit PCM",["pcm24"]="24-bit PCM",["pcm32"]="32-bit PCM",["float32"]="32-bit float",["createNew"]="Create a new Float",["addToExisting"]="Add to an existing Float",["beside"]="Beside original files",["ask"]="Ask each time",["folder"]="Selected folder",["editable"]="Editable paragraphs",["preserve"]="Preserve original pages",["reflowable"]="Reflowable",["fixed"]="Fixed pages",["video"]="Video",["audio"]="Audio · MP3",["thumbnail"]="Thumbnail",["photos"]="Photos",["image"]="Image",["glass"]="Glass",["solid"]="Solid",["gradient"]="Gradient",["grid"]="Grid",["row"]="Row",["column"]="Column",["featured"]="Featured",["blur"]="Blur",["pixelate"]="Pixelate",["bleep"]="Bleep",["mute"]="Mute",["waveform"]="Waveform",["spectrum"]="Spectrum",["bars"]="Bars",["system"]="System",["light"]="Light",["dark"]="Dark"};
    private static readonly HashSet<string> Formats=FeatureCatalog.Conversions.Values.SelectMany(v=>v).ToHashSet();
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>value is string text?Labels.GetValueOrDefault(text,Formats.Contains(text)?text.ToUpperInvariant():text):value;
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>throw new NotSupportedException();
}
