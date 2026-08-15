using System.Windows;
using Velopack;

namespace Timer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    // WPF's SDK-generated Main is disabled (Timer.csproj: ApplicationDefinition
    // removed from App.xaml, StartupObject points here) so Velopack can run
    // before any WPF/window overhead, per Velopack's guidance.
    [STAThread]
    private static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
