using System;
using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.DynamicDependency;

namespace MetaEnricher;

public static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // Initialize Windows App Runtime bootstrapper (required for unpackaged apps).
        // 0x00010006 = Windows App SDK 1.6 (major=1, minor=6).
        Bootstrap.Initialize(0x00010006);

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(p =>
        {
            var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            System.Threading.SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });

        Bootstrap.Shutdown();
    }
}
