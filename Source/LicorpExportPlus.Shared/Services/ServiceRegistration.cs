using ricaun.DI;

namespace LicorpExportPlus.Services
{
    public static class ServiceRegistration
    {
        public static void AddExportPlusServices(this Container container)
        {
            container.AddSingleton<IMainWindowFactory>(new MainWindowFactory());
            container.AddSingleton<IMainWindowLauncher>(new MainWindowLauncher(container.Resolve<IMainWindowFactory>()));
        }
    }
}
