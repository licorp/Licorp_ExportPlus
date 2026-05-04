using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LicorpExportPlus.Views;

namespace LicorpExportPlus.Services
{
    public interface IMainWindowLauncher
    {
        void ShowOrActivate(Document document, UIApplication uiApplication);
    }

    public class MainWindowLauncher : IMainWindowLauncher
    {
        private readonly IMainWindowFactory _mainWindowFactory;
        private ExportPlusMainWindow _window;

        public MainWindowLauncher(IMainWindowFactory mainWindowFactory)
        {
            _mainWindowFactory = mainWindowFactory;
        }

        public void ShowOrActivate(Document document, UIApplication uiApplication)
        {
            if (_window == null || !_window.IsLoaded)
            {
                _window = _mainWindowFactory.Create(document, uiApplication);
                _window.Closed += (_, __) => _window = null;
                _window.Show();
                return;
            }

            _window.Activate();
        }
    }
}
