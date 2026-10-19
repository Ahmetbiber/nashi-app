using System.Windows;
using System.Windows.Controls;
using Nashi.Core;
namespace Nashi.Windows;
public sealed class MetadataEditor:Window
{
    private readonly CheckBox removeAll=new(){Content="Remove all metadata",Margin=new(4,14,4,14)};
    private readonly Dictionary<string,TextBox> fields=[];private readonly Dictionary<string,string> original=[];
    public bool RemoveAll=>removeAll.IsChecked==true;public Dictionary<string,string> Updates=>fields.Where(p=>p.Value.Text!=original.GetValueOrDefault(p.Key,"")).ToDictionary(p=>p.Key,p=>p.Value.Text);
    public MetadataEditor(Window owner,IReadOnlyDictionary<string,string> metadata)
    {
        Owner=owner;Theme.Attach(this);Title="Metadata · Nashi";Width=820;Height=700;MinWidth=640;MinHeight=450;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var root=new DockPanel{Margin=new(26)};Content=root;var header=new StackPanel();header.Children.Add(new TextBlock{Text="Metadata",FontSize=28,FontWeight=FontWeights.Bold});header.Children.Add(new TextBlock{Text="Edit fields or remove metadata. Encoded image pixels stay untouched.",Margin=new(0,8,0,0),TextWrapping=TextWrapping.Wrap});header.Children.Add(removeAll);DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
        var footer=new DockPanel();DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);var save=MainWindow.Button("Save new copy ↗",()=>DialogResult=true);save.Background=Theme.Primary;DockPanel.SetDock(save,Dock.Right);footer.Children.Add(save);footer.Children.Add(MainWindow.Button("Cancel",()=>DialogResult=false));var stack=new StackPanel();root.Children.Add(new ScrollViewer{Content=stack,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        var editable=FeatureCatalog.Extension(metadata.GetValueOrDefault("SourceFile","")??"")=="pdf"?MetadataService.EditableTags.Where(k=>k.StartsWith("PDF:",StringComparison.Ordinal)):MetadataService.EditableTags.Where(k=>!k.StartsWith("PDF:",StringComparison.Ordinal)&&!k.StartsWith("QuickTime:",StringComparison.Ordinal));
        foreach(var key in editable){var value=metadata.GetValueOrDefault(key,"");original[key]=value;stack.Children.Add(new TextBlock{Text=key,Margin=new(4,12,4,0)});var field=new TextBox{Text=value};fields[key]=field;stack.Children.Add(field);}
        var details=new StackPanel();foreach(var pair in metadata.Where(p=>!fields.ContainsKey(p.Key)))details.Children.Add(new TextBlock{Text=pair.Key+"  ·  "+pair.Value,TextWrapping=TextWrapping.Wrap,Margin=new(4,5,4,5)});stack.Children.Add(new Expander{Header="All metadata",Content=details,Margin=new(4,18,4,12)});removeAll.Checked+=(_,_)=>{foreach(var field in fields.Values)field.IsEnabled=false;};removeAll.Unchecked+=(_,_)=>{foreach(var field in fields.Values)field.IsEnabled=true;};PreviewKeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Escape){Close();e.Handled=true;}};
    }
}
