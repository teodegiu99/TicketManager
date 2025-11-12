using Microsoft.UI.Xaml;

// Per assicurarti che il namespace corrisponda al tuo progetto
namespace ClientUser
{
    /// <summary>
    /// File code-behind per App.xaml
    /// </summary>
    public partial class App : Application
    {
        private Window m_window;

        public App()
        {
            this.InitializeComponent(); // Questo legge App.xaml
        }

        /// <summary>
        /// Chiamato quando l'applicazione viene avviata.
        /// </summary>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow(); // Crea la tua finestra principale
            m_window.Activate();
        }
    }
}