using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
namespace Nashi.Windows;

// The same composition and exported pear artwork as macOS HomeView.FileGarden.
internal sealed class FileGarden:Grid
{
    private readonly Image mark;
    internal FileGarden()
    {
        Width=166;Height=150;IsHitTestVisible=false;
        var circle=new Ellipse{Width=144,Height=144};circle.SetResourceReference(Shape.FillProperty,"Sand");Children.Add(circle);
        AddCard("PDF","▤",-16,-34,12,"Muted");AddCard("PNG","▧",7,31,-2,"Gold");
        mark=new Image{Width=65,Height=65,RenderTransform=new TranslateTransform(0,42)};Children.Add(mark);SetSelected(false);
    }
    internal void SetSelected(bool selected)=>mark.Source=Artwork(selected?"NashiMarkHappy":"NashiMark");
    internal static BitmapImage Artwork(string name)=>new(new Uri("pack://application:,,,/Assets/"+name+".png"));
    private void AddCard(string label,string symbol,double angle,double x,double y,string color)
    {
        var content=new StackPanel();foreach(var (text,size) in new[]{(symbol,23d),(label,10d)}){var block=new TextBlock{Text=text,FontSize=size,FontWeight=FontWeights.Bold,Margin=new(0,0,0,8)};block.SetResourceReference(TextBlock.ForegroundProperty,color);content.Children.Add(block);}
        var line=new Border{Width=37,Height=4,CornerRadius=new(2),HorizontalAlignment=HorizontalAlignment.Left,Opacity=.18};line.SetResourceReference(Border.BackgroundProperty,color);content.Children.Add(line);
        var transforms=new TransformGroup();transforms.Children.Add(new RotateTransform(angle));transforms.Children.Add(new TranslateTransform(x,y));
        var card=new Border{Width=77,Height=103,CornerRadius=new(15),Padding=new(13),Child=content,BorderThickness=new(1),RenderTransformOrigin=new(.5,.5),RenderTransform=transforms};card.SetResourceReference(Border.BackgroundProperty,"Surface");card.SetResourceReference(Border.BorderBrushProperty,"Line");Children.Add(card);
    }
}
