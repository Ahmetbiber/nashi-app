using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Nashi.Core;
namespace Nashi.Windows;
public sealed partial class MainWindow
{
    private Guid? selectedLibraryId;private Point libraryDragStart;
    private readonly TextBlock homeTitle=Text("Your files. Their next form.",30);
    private readonly StackPanel shortcutHints=new();
    private readonly Dictionary<Guid,Border> libraryCards=[];
    private void ConfigureLibraryRow(Border card,LibraryEntry entry)
    {
        libraryCards[entry.Id]=card;card.Tag=entry;card.Focusable=true;card.ToolTip="Space: preview · Enter: use in Nashi";
        card.BorderBrush=selectedLibraryId==entry.Id?Theme.Brush("Gold"):Theme.Brush("Line");
        card.GotKeyboardFocus+=(_,_)=>SelectCard(entry.Id);
        card.PreviewMouseLeftButtonDown+=(_,e)=>{libraryDragStart=e.GetPosition(card);SelectCard(entry.Id);if(!IsButton(e.OriginalSource as DependencyObject)){card.Focus();if(e.ClickCount==2){PreviewEntry(entry);e.Handled=true;}}};
        card.MouseMove+=(_,e)=>{if(e.LeftButton==MouseButtonState.Pressed&&(e.GetPosition(card)-libraryDragStart).Length>8&&entry.AvailablePaths.Length>0&&!IsButton(e.OriginalSource as DependencyObject)){DragDrop.DoDragDrop(card,new DataObject(DataFormats.FileDrop,entry.AvailablePaths),DragDropEffects.Copy);}};
    }
    private static bool IsButton(DependencyObject? value){while(value is not null){if(value is Button)return true;value=value is Visual?VisualTreeHelper.GetParent(value):LogicalTreeHelper.GetParent(value);}return false;}
    private void SelectCard(Guid id){selectedLibraryId=id;foreach(var pair in libraryCards)pair.Value.BorderBrush=Theme.Brush(pair.Key==id?"Gold":"Line");}
    private void SelectLibrary(int direction)
    {
        var entries=library.Search(search.Text).ToArray();if(entries.Length==0)return;var old=Array.FindIndex(entries,e=>e.Id==selectedLibraryId);var next=old<0?0:Math.Clamp(old+direction,0,entries.Length-1);SelectCard(entries[next].Id);if(libraryCards.TryGetValue(entries[next].Id,out var card)){card.Focus();card.BringIntoView();}
    }
    private LibraryEntry? SelectedLibrary()=>library.Search(search.Text).FirstOrDefault(e=>e.Id==selectedLibraryId)??library.Search(search.Text).FirstOrDefault();
    private void PreviewSelectedLibrary(){if(SelectedLibrary() is {} entry)PreviewEntry(entry);}
    private void PreviewEntry(LibraryEntry entry){if(entry.AvailablePaths.Length>0)new FilePreviewWindow(this,entry.AvailablePaths).Show();else status.Text="These files have moved or were deleted.";}
    private void ReuseSelectedLibrary(){if(busy||SelectedLibrary() is not {} entry)return;files.Clear();Add(entry.AvailablePaths);ShowLibrary(false);}
    private void BuildSelectionActions()
    {
        homeTitle.Text=files.Count==0?"Your files. Their next form.":"Let’s make something of it.";
        shortcutHints.Children.Clear();foreach(var hint in new[]{preferences.Value.ConvertModifier+"  Convert",preferences.Value.ToolsModifier+"  Tools",preferences.Value.ActionsModifier+"  File actions"}){var pill=Card(Text(hint,11,"Muted"));pill.Padding=new(9,5,9,5);pill.Margin=new(0,0,7,0);pill.CornerRadius=new(9);shortcutHints.Children.Add(pill);}
        if(files.Count==0)return;
        var stack=new StackPanel();actions.Children.Add(stack);var tiles=new Grid();for(var n=0;n<3;n++)tiles.ColumnDefinitions.Add(new());stack.Children.Add(tiles);
        var formats=FeatureCatalog.Formats(files);var tools=Choices();
        void Tile(int column,string title,string subtitle,string symbol,IEnumerable<(string,Action)> commands)
        {
            var label=new DockPanel();var icon=Text(symbol,22,"Gold");icon.Margin=new(0,0,12,0);DockPanel.SetDock(icon,Dock.Left);label.Children.Add(icon);var caption=new StackPanel();caption.Children.Add(Text(title,14));caption.Children.Add(Text(subtitle,10,"Muted"));label.Children.Add(caption);
            var button=Button("",()=>{});button.Content=label;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Padding=new(15);button.Margin=new(column==0?0:6,0,column==2?0:6,0);var menu=new ContextMenu();foreach(var (name,action) in commands){var item=new MenuItem{Header=name};item.Click+=(_,_)=>action();menu.Items.Add(item);}button.IsEnabled=menu.Items.Count>0;button.Click+=(_,_)=>{menu.PlacementTarget=button;menu.IsOpen=true;};Grid.SetColumn(button,column);tiles.Children.Add(button);
        }
        Tile(0,"Convert",$"{formats.Length} ways to change it","⇄",formats.Select(f=>(f.ToUpperInvariant(),(Action)(async()=>await EditAsync(new("convert:"+f,"Convert","↗"))))));
        Tile(1,"Fine-tune","Edit, compress, and more","☷",tools.Select(t=>(t.Title,(Action)(async()=>await EditAsync(t)))));
        Tile(2,"Move & share","Send it on its way","↗",new[]{("Share","share"),("Float","float"),("Copy","copy"),("Move","move"),("Copy Path","copyPath"),("Terminal","terminal")}.Select(t=>(t.Item1,(Action)(async()=>await PerformFileAction(t.Item2,files.ToArray())))));
        var quick=new WrapPanel{Margin=new(0,8,0,0)};quick.Children.Add(Text("QUICK PICKS",10,"Muted"));foreach(var format in formats.Take(4))quick.Children.Add(Button(format.ToUpperInvariant()+" ↗",async()=>await EditAsync(new("convert:"+format,"Convert","↗"))));stack.Children.Add(quick);
    }
}
