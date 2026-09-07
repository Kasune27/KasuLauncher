using System;
using System.Threading.Tasks;
using System.Windows;

namespace KasuLauncher
{
    public partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            InitializeComponent();
            IniciarCarga();
        }

        private async void IniciarCarga()
        {
            // Tiempo de carga (3 segundos)
            await Task.Delay(3000);

            // Muestra la ventana principal
            MainWindow main = new MainWindow();
            main.Show();

            // Cierra el Splash Screen
            this.Close();
        }
    }
}