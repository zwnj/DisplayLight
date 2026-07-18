using Velopack;

namespace DisplayLight.App;

internal static class Program
{
    [STAThread]
    public static void Main()
    {
        // 更新適用時は通常のWPF起動より先にVelopackのライフサイクルを処理する必要がある。
        VelopackApp.Build().Run();

        App application = new();
        application.InitializeComponent();
        application.Run();
    }
}
