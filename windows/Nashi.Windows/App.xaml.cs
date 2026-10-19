using System.Windows;
namespace Nashi.Windows;
public partial class App:Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if(e.Args.Length==2&&e.Args[0]=="--apply-update"){try{Shutdown(UpdateInstaller.Apply(e.Args[1]));}catch(Exception error){MessageBox.Show(error.Message,"Nashi update could not finish");Shutdown(1);}return;}
        if(e.Args.Contains("--smoke-test")){try{SmokeTests.Run();Shutdown(0);}catch(Exception error){Console.Error.WriteLine(error);Shutdown(1);}return;}
        new MainWindow(e.Args.Where(File.Exists).ToArray()).Show();
    }
}
