using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Nashi.Core;
namespace Nashi.Windows;
internal static class WheelPresentation
{
    internal static Preferences Preferences{get;set;}=new();
    internal static Brush Backdrop{get{var brush=Theme.Brush("Surface").Clone();brush.Opacity=Preferences.WheelFinish=="glass"?.95:1;return brush;}}
    internal static double LabelRadius(int count,double half)=>count<=2?180:4*Math.Sin(half)*(Math.Pow(250,3)-Math.Pow(108,3))/(6*half*(250*250-108*108));
    internal static void Animate(Window window){if(Preferences.Animations&&SystemParameters.ClientAreaAnimation)window.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(140)));}
}
