using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Nashi.Core;
using Microsoft.Win32;
namespace Nashi.Windows;
public sealed class FloatShelf:Window
{
    private readonly ListBox files=new(){SelectionMode=SelectionMode.Extended,MinHeight=80};private readonly List<string> paths=[];private readonly Func<IReadOnlyList<string>,string,Task> action;private readonly Action<IReadOnlyList<string>> reuse;private readonly TextBlock title=new(){FontSize=15,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center};private readonly WrapPanel footer=new();private bool icons=true,collapsed;private string? edge;private Point mouseOrigin;
    internal IReadOnlyList<string> Paths=>paths;
    public FloatShelf(IEnumerable<string> initial,Func<IReadOnlyList<string>,string,Task> action,Action<IReadOnlyList<string>> reuse)
    {
        this.action=action;this.reuse=reuse;Theme.Attach(this);Title="Float · Nashi";Width=540;Height=300;MinWidth=360;MinHeight=80;Topmost=true;WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ResizeMode=ResizeMode.CanResizeWithGrip;AllowDrop=true;
        var root=new DockPanel{Margin=new(14)};var border=new Border{CornerRadius=new(22),BorderThickness=new(1),Child=root};border.SetResourceReference(Border.BackgroundProperty,"Canvas");border.SetResourceReference(Border.BorderBrushProperty,"Line");Content=border;
        var header=new DockPanel{Margin=new(0,0,0,8)};DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);var controls=new WrapPanel();DockPanel.SetDock(controls,Dock.Right);header.Children.Add(controls);controls.Children.Add(MainWindow.Button("▦",()=>{icons=!icons;Reload();}));controls.Children.Add(MainWindow.Button("Pin",PinMenu));controls.Children.Add(MainWindow.Button("−",()=>{collapsed=!collapsed;files.Visibility=collapsed?Visibility.Collapsed:Visibility.Visible;footer.Visibility=collapsed?Visibility.Collapsed:Visibility.Visible;Height=collapsed?82:300;UpdateLayout();Attach();}));controls.Children.Add(MainWindow.Button("×",Close));header.Children.Add(title);title.MouseLeftButtonDown+=(_,_)=>{DragMove();Snap();};
        DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);footer.Children.Add(MainWindow.Button("Add files",Choose));footer.Children.Add(MainWindow.Button("Use in Nashi",()=>reuse(Selection())));footer.Children.Add(MainWindow.Button("Remove",Remove));footer.Children.Add(MainWindow.Button("Clear",()=>{paths.Clear();Reload();}));root.Children.Add(files);
        files.PreviewMouseLeftButtonDown+=(_,e)=>mouseOrigin=e.GetPosition(files);files.MouseMove+=(_,e)=>{if(e.LeftButton==MouseButtonState.Pressed&&(e.GetPosition(files)-mouseOrigin).Length>8&&files.SelectedItems.Count>0){var data=new DataObject(DataFormats.FileDrop,Selection().ToArray());DragDrop.DoDragDrop(files,data,DragDropEffects.Copy|DragDropEffects.Move);Reload();}};
        files.MouseDoubleClick+=(_,_)=>Preview();PreviewKeyDown+=OnKey;DragOver+=(_,e)=>{e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;};Drop+=(_,e)=>{if(e.Data.GetData(DataFormats.FileDrop) is string[] incoming)Add(incoming);e.Handled=true;};
        var menu=new ContextMenu();foreach(var (label,id) in new[]{("Open","open"),("Quick Preview","preview"),("Show in Explorer","reveal"),("Copy","copy"),("Copy Path","copyPath"),("Share…","share"),("Remove from Float","remove")}){var item=new MenuItem{Header=label};item.Click+=async(_,_)=>{try{if(id=="open")foreach(var path in Selection())MainWindow.OpenUrl(path);else if(id=="preview")Preview();else if(id=="reveal")foreach(var path in Selection())MainWindow.Reveal(path);else if(id=="remove")Remove();else await action(Selection(),id);}catch(Exception error)when(error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or System.Runtime.InteropServices.COMException){MessageBox.Show(this,error.Message,"Float action unavailable");}};menu.Items.Add(item);}files.ContextMenu=menu;Add(initial);
    }
    internal void Add(IEnumerable<string> incoming){foreach(var p in incoming.Where(p=>File.Exists(p)||Directory.Exists(p)).Take(1000)){var full=Path.GetFullPath(p);if(!paths.Contains(full,StringComparer.OrdinalIgnoreCase))paths.Add(full);}Reload();}
    private string[] Selection()=>files.SelectedItems.Count>0?files.SelectedItems.Cast<ListBoxItem>().Select(i=>(string)i.Tag).ToArray():paths.Where(p=>File.Exists(p)||Directory.Exists(p)).ToArray();
    private void Reload()
    {
        var selected=files.SelectedItems.Cast<ListBoxItem>().Select(i=>(string)i.Tag).ToHashSet();files.Items.Clear();files.ItemsPanel=new ItemsPanelTemplate(new FrameworkElementFactory(icons?typeof(WrapPanel):typeof(StackPanel)));
        foreach(var path in paths){var missing=!File.Exists(path)&&!Directory.Exists(path);var text=new TextBlock{Text=(Directory.Exists(path)?"▣  ":"▱  ")+Path.GetFileName(path)+(missing?" · Missing":""),TextWrapping=TextWrapping.Wrap,TextAlignment=icons?TextAlignment.Center:TextAlignment.Left,MaxWidth=icons?115:450};files.Items.Add(new ListBoxItem{Content=text,Tag=path,ToolTip=path,Padding=new(10),Margin=new(3),IsSelected=selected.Contains(path)});}title.Text=$"Float · {paths.Count} {(paths.Count==1?"file":"files")}";
    }
    private void Remove(){foreach(var p in files.SelectedItems.Cast<ListBoxItem>().Select(i=>(string)i.Tag).ToArray())paths.Remove(p);Reload();}
    private void Choose(){var picker=new OpenFileDialog{Multiselect=true};if(picker.ShowDialog(this)==true)Add(picker.FileNames);}
    private void PinMenu(){var menu=new ContextMenu();foreach(var side in new[]{"Top","Right","Bottom","Left","Detach"}){var item=new MenuItem{Header=side};item.Click+=(_,_)=>{edge=side=="Detach"?null:side;Attach();};menu.Items.Add(item);}menu.IsOpen=true;}
    private void Attach(){DesktopGeometry.Snap(this,edge);}
    private void Snap(){edge=DesktopGeometry.Snap(this,null,true);}
    private async void OnKey(object sender,KeyEventArgs e)
    {
        try{if(Keyboard.Modifiers==(ModifierKeys.Control|ModifierKeys.Shift)&&e.Key==Key.C){await action(Selection(),"copyPath");e.Handled=true;return;}
        if(e.Key==Key.Escape){Close();e.Handled=true;}else if(e.Key==Key.Delete){Remove();e.Handled=true;}else if(e.Key==Key.Space){Preview();e.Handled=true;}else if(Keyboard.Modifiers==ModifierKeys.Control)
        {if(e.Key==Key.A){files.SelectAll();e.Handled=true;}else if(e.Key==Key.C){await action(Selection(),"copy");e.Handled=true;}else if(e.Key==Key.V){if(Clipboard.ContainsFileDropList())Add(Clipboard.GetFileDropList().Cast<string>());e.Handled=true;}else if(e.Key==Key.R){foreach(var p in Selection())MainWindow.Reveal(p);e.Handled=true;}else if(e.Key==Key.O){foreach(var p in Selection())MainWindow.OpenUrl(p);e.Handled=true;}}
        }catch(Exception error)when(error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or System.Runtime.InteropServices.COMException){MessageBox.Show(this,error.Message,"Float action unavailable");}
    }
    private void Preview()
    {
        var selected=Selection();if(selected.Length>0)new FilePreviewWindow(this,selected).Show();
    }
}
