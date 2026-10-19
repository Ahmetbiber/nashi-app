using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Nashi.Windows;
public sealed class PdfOrganizer:Window
{
    private sealed record Page(int Number,byte[] Image){public int Rotation{get;set;}}
    private readonly List<Page> pages=[];private readonly ListBox gallery=new(){SelectionMode=SelectionMode.Extended};
    public int[] Pages=>pages.Select(p=>p.Number).ToArray();public Dictionary<int,int> Rotations=>pages.ToDictionary(p=>p.Number,p=>p.Rotation);
    private PdfOrganizer(Window owner){Owner=owner;Theme.Attach(this);Title="Organize PDF · Nashi";Width=1000;Height=760;MinWidth=700;MinHeight=500;WindowStartupLocation=WindowStartupLocation.CenterOwner;var root=new DockPanel{Margin=new(26)};Content=root;var heading=new TextBlock{Text="Organize PDF",FontSize=28,FontWeight=FontWeights.Bold,Margin=new(0,0,0,18)};DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);var footer=new DockPanel{Margin=new(0,16,0,0)};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);var save=MainWindow.Button("Save organized copy ↗",()=>{if(pages.Count==0){MessageBox.Show(this,"Keep at least one page.");return;}DialogResult=true;});save.Background=Theme.Primary;DockPanel.SetDock(save,Dock.Right);footer.Children.Add(save);var controls=new WrapPanel();controls.Children.Add(MainWindow.Button("Move left",()=>Move(-1)));controls.Children.Add(MainWindow.Button("Move right",()=>Move(1)));controls.Children.Add(MainWindow.Button("Rotate",Rotate));controls.Children.Add(MainWindow.Button("Remove",Remove));controls.Children.Add(MainWindow.Button("Cancel",()=>DialogResult=false));footer.Children.Add(controls);gallery.ItemsPanel=new ItemsPanelTemplate(new FrameworkElementFactory(typeof(WrapPanel)));root.Children.Add(gallery);PreviewKeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Escape){Close();e.Handled=true;}else if(e.Key==System.Windows.Input.Key.Delete){Remove();e.Handled=true;}};}
    public static async Task<PdfOrganizer> CreateAsync(Window owner,string file){var result=new PdfOrganizer(owner);var images=await NativePdf.RenderAsync(file,72);for(var i=0;i<images.Length;i++)result.pages.Add(new(i+1,images[i]));result.Reload();return result;}
    private void Reload(){var selected=gallery.SelectedItems.Cast<ListBoxItem>().Select(i=>(Page)i.Tag).ToArray();gallery.Items.Clear();foreach(var page in pages){var content=new StackPanel();content.Children.Add(new Image{Source=ImageEngine.FromPng(page.Image),Width=130,Height=170,Stretch=Stretch.Uniform,LayoutTransform=new RotateTransform(page.Rotation)});content.Children.Add(new TextBlock{Text="Page "+page.Number,TextAlignment=TextAlignment.Center,Margin=new(0,8,0,0)});var item=new ListBoxItem{Content=content,Tag=page,Margin=new(6),Padding=new(10),IsSelected=selected.Contains(page)};gallery.Items.Add(item);}}
    private Page[] Selected()=>gallery.SelectedItems.Cast<ListBoxItem>().Select(i=>(Page)i.Tag).ToArray();
    private void Move(int delta){var selected=Selected();foreach(var item in delta<0?selected:selected.Reverse()){var index=pages.IndexOf(item);var next=index+delta;if(next<0||next>=pages.Count)continue;pages.RemoveAt(index);pages.Insert(next,item);}Reload();}
    private void Rotate(){foreach(var page in Selected())page.Rotation=(page.Rotation+90)%360;Reload();}
    private void Remove(){foreach(var page in Selected())pages.Remove(page);Reload();}
}
