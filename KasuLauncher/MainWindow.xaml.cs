using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KasuLauncher
{
    public partial class MainWindow : Window
    {
        private const string DRIVE_FILE_ID = "1BZTTnLVhauRUActPcv6izJqTtG5TIQ0B";

        // 🔴 IP REAL DE TU SERVIDOR
        private const string SERVER_IP = "mc.hypixel.net";

        // 🔴 ENLACE A TU JSON REMOTO PARA NOTICIAS Y VERSIONES
        private const string CONFIG_URL = "https://raw.githubusercontent.com/TuUsuario/TuRepo/main/launcher_config.json";

        // Reproductor de Música
        private MediaPlayer bgMusicPlayer = new MediaPlayer();
        private Dictionary<string, string> listaCanciones = new Dictionary<string, string>();
        private string defaultMusicPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "music", "default.mp3");

        private string remoteVersion = "1.0.0";
        private string driveDownloadUrl = "";

        private static Dictionary<string, string> usuariosDB = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Kasune", "1234" }
        };

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                BackgroundVideo.Play();
            }
            catch { }

            InicializarSistemaMusica();
            ActualizarEstadoBotonJuego();

            await ActualizarEstadoServidorAsync();
            await CargarConfiguracionYNoticiasAsync();
        }

        private void BackgroundVideo_MediaOpened(object sender, RoutedEventArgs e)
        {
            BackgroundVideo.Play();
        }

        private void BackgroundVideo_MediaEnded(object sender, RoutedEventArgs e)
        {
            BackgroundVideo.Position = TimeSpan.Zero;
            BackgroundVideo.Play();
        }

        // ==========================================
        // 🖥️ CONTROL PANTALLA COMPLETA CON F11
        // ==========================================
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F11)
            {
                ToggleFullScreen();
            }
        }

        private void ToggleFullScreen()
        {
            if (this.WindowState == WindowState.Maximized && this.WindowStyle == WindowStyle.None)
            {
                this.WindowState = WindowState.Normal;
                this.WindowStyle = WindowStyle.SingleBorderWindow;

                if (ChkFullScreen != null)
                    ChkFullScreen.IsChecked = false;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                this.WindowStyle = WindowStyle.None;

                if (ChkFullScreen != null)
                    ChkFullScreen.IsChecked = true;
            }
        }

        // ==========================================
        // 🎵 SISTEMA DE MÚSICA DE FONDO
        // ==========================================
        private void InicializarSistemaMusica()
        {
            listaCanciones.Clear();
            CmbMusicList.Items.Clear();

            if (File.Exists(defaultMusicPath))
            {
                listaCanciones.Add("🎵 Música por defecto (Default)", defaultMusicPath);
            }
            else
            {
                listaCanciones.Add("🎵 Música por defecto (Default)", defaultMusicPath);
            }

            string customMusicFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "launcher_music");
            if (Directory.Exists(customMusicFolder))
            {
                string[] files = Directory.GetFiles(customMusicFolder, "*.mp3");
                foreach (var file in files)
                {
                    string fileName = "🎶 " + Path.GetFileNameWithoutExtension(file);
                    if (!listaCanciones.ContainsKey(fileName))
                    {
                        listaCanciones.Add(fileName, file);
                    }
                }
            }

            foreach (var item in listaCanciones.Keys)
            {
                CmbMusicList.Items.Add(item);
            }

            if (CmbMusicList.Items.Count > 0)
                CmbMusicList.SelectedIndex = 0;

            ReproducirMusica(defaultMusicPath);
        }

        private void ReproducirMusica(string rutaPath)
        {
            try
            {
                if (File.Exists(rutaPath))
                {
                    bgMusicPlayer.Stop();
                    bgMusicPlayer.Open(new Uri(rutaPath));
                    bgMusicPlayer.Volume = SliderVolume.Value / 100.0;

                    bgMusicPlayer.MediaEnded += (s, e) => {
                        bgMusicPlayer.Position = TimeSpan.Zero;
                        bgMusicPlayer.Play();
                    };

                    bgMusicPlayer.Play();
                }
            }
            catch { }
        }

        private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (bgMusicPlayer != null)
            {
                bgMusicPlayer.Volume = SliderVolume.Value / 100.0;
            }

            if (LblVolumeValue != null)
            {
                LblVolumeValue.Text = $"{(int)SliderVolume.Value}%";
            }
        }

        private void BtnAddCustomMusic_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Archivos MP3 (*.mp3)|*.mp3",
                Title = "Seleccionar canción de fondo para el launcher"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string sourceFile = openFileDialog.FileName;
                string customMusicFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "launcher_music");

                if (!Directory.Exists(customMusicFolder))
                    Directory.CreateDirectory(customMusicFolder);

                string destFile = Path.Combine(customMusicFolder, Path.GetFileName(sourceFile));
                File.Copy(sourceFile, destFile, true);

                string musicName = "🎶 " + Path.GetFileNameWithoutExtension(destFile);

                if (!listaCanciones.ContainsKey(musicName))
                {
                    listaCanciones.Add(musicName, destFile);
                    CmbMusicList.Items.Add(musicName);
                }

                CmbMusicList.SelectedItem = musicName;
                MessageBox.Show("¡Canción agregada con éxito! Recordá presionar 'Guardar y Aplicar Cambios' para escucharla.", "Música Personalizada", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // ==========================================
        // 🌐 REDES SOCIALES
        // ==========================================
        private void AbrirUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo abrir el enlace: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDiscord_Click(object sender, RoutedEventArgs e) => AbrirUrl("https://discord.gg/y8wxmvqdTG");
        private void BtnInstagram_Click(object sender, RoutedEventArgs e) => AbrirUrl("https://www.instagram.com/kasune27");
        private void BtnYouTube_Click(object sender, RoutedEventArgs e) => AbrirUrl("https://www.youtube.com/@kasune27");
        private void BtnTwitch_Click(object sender, RoutedEventArgs e) => AbrirUrl("https://www.twitch.tv/kasune27");

        // ==========================================
        // 📢 NOTICIAS Y ESTADO DEL SERVIDOR
        // ==========================================
        private async Task CargarConfiguracionYNoticiasAsync()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    client.DefaultRequestHeaders.Add("User-Agent", "KasuLauncher");

                    string jsonResponse = await client.GetStringAsync(CONFIG_URL);

                    using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
                    {
                        var root = doc.RootElement;

                        if (root.TryGetProperty("version_modpack", out JsonElement verElement))
                            remoteVersion = verElement.GetString();

                        if (root.TryGetProperty("url_zip_modpack", out JsonElement urlElement))
                            driveDownloadUrl = urlElement.GetString();

                        if (root.TryGetProperty("noticias", out JsonElement noticiasArray) && noticiasArray.ValueKind == JsonValueKind.Array)
                        {
                            ContainerNoticias.Children.Clear();

                            foreach (var noticia in noticiasArray.EnumerateArray())
                            {
                                string titulo = noticia.GetProperty("titulo").GetString();
                                string fecha = noticia.GetProperty("fecha").GetString();
                                string contenido = noticia.GetProperty("contenido").GetString();

                                var itemBorder = new Border
                                {
                                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E222D")),
                                    CornerRadius = new CornerRadius(8),
                                    Padding = new Thickness(10),
                                    Margin = new Thickness(0, 0, 0, 10),
                                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333846")),
                                    BorderThickness = new Thickness(1)
                                };

                                var sp = new StackPanel();

                                var txtTitulo = new TextBlock
                                {
                                    Text = titulo,
                                    FontWeight = FontWeights.Bold,
                                    Foreground = Brushes.White,
                                    FontSize = 13,
                                    TextWrapping = TextWrapping.Wrap
                                };

                                var txtFecha = new TextBlock
                                {
                                    Text = fecha,
                                    FontSize = 10,
                                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888888")),
                                    Margin = new Thickness(0, 2, 0, 5)
                                };

                                var txtContenido = new TextBlock
                                {
                                    Text = contenido,
                                    FontSize = 11,
                                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC")),
                                    TextWrapping = TextWrapping.Wrap
                                };

                                sp.Children.Add(txtTitulo);
                                sp.Children.Add(txtFecha);
                                sp.Children.Add(txtContenido);

                                itemBorder.Child = sp;
                                ContainerNoticias.Children.Add(itemBorder);
                            }
                        }

                        ActualizarEstadoBotonJuego();
                    }
                }
            }
            catch
            {
                ContainerNoticias.Children.Clear();
                var errorText = new TextBlock
                {
                    Text = "No se pudieron cargar las noticias.",
                    Foreground = Brushes.Gray,
                    FontSize = 12
                };
                ContainerNoticias.Children.Add(errorText);
            }
        }

        private async Task ActualizarEstadoServidorAsync()
        {
            try
            {
                LblServerStatus.Text = "● COMPROBANDO...";
                LblServerStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAA00"));
                LblServerPlayers.Text = "";

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    string apiUrl = $"https://api.mcsrvstat.us/2/{SERVER_IP}";
                    var response = await client.GetStringAsync(apiUrl);

                    using (JsonDocument doc = JsonDocument.Parse(response))
                    {
                        var root = doc.RootElement;
                        bool isOnline = root.GetProperty("online").GetBoolean();

                        if (isOnline)
                        {
                            LblServerStatus.Text = "● ONLINE";
                            LblServerStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5BE37D"));

                            if (root.TryGetProperty("players", out JsonElement players) &&
                                players.TryGetProperty("online", out JsonElement onlineCount) &&
                                players.TryGetProperty("max", out JsonElement maxCount))
                            {
                                int online = onlineCount.GetInt32();
                                int max = maxCount.GetInt32();
                                LblServerPlayers.Text = $"({online}/{max})";
                            }
                        }
                        else
                        {
                            LblServerStatus.Text = "● OFFLINE";
                            LblServerStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5555"));
                            LblServerPlayers.Text = "";
                        }
                    }
                }
            }
            catch
            {
                LblServerStatus.Text = "● OFFLINE";
                LblServerStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5555"));
                LblServerPlayers.Text = "";
            }
        }

        // ==========================================
        // 🎮 INTERFAZ Y LANZAMIENTO
        // ==========================================
        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            PanelSettingsModal.Visibility = Visibility.Visible;
        }

        private void TxtUsername_TextChanged(object sender, TextChangedEventArgs e)
        {
            string username = TxtUsername.Text.Trim();

            if (string.IsNullOrWhiteSpace(username))
            {
                username = "Steve";
            }

            try
            {
                string safeUsername = Uri.EscapeDataString(username);

                var skinImage = new BitmapImage();
                skinImage.BeginInit();
                skinImage.UriSource = new Uri($"https://minotar.net/armor/bust/{safeUsername}/100.png");
                skinImage.CacheOption = BitmapCacheOption.OnLoad;
                skinImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                skinImage.EndInit();

                var headImage = new BitmapImage();
                headImage.BeginInit();
                headImage.UriSource = new Uri($"https://minotar.net/helm/{safeUsername}/100.png");
                headImage.CacheOption = BitmapCacheOption.OnLoad;
                headImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                headImage.EndInit();

                SkinPreview.Source = skinImage;
                HeadPreview.Source = headImage;
            }
            catch { }
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = TxtUsername.Text.Trim();
            string password = TxtPassword.Password;

            if (string.IsNullOrWhiteSpace(username))
            {
                MessageBox.Show("Por favor, ingresá un nombre de usuario.", "Aviso de Seguridad", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Por favor, ingresá tu contraseña.", "Aviso de Seguridad", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (usuariosDB.ContainsKey(username))
            {
                if (usuariosDB[username] == password)
                {
                    IngresarAlJuego(username);
                }
                else
                {
                    MessageBox.Show("Contraseña incorrecta. Intentalo de nuevo.", "Error de Inicio de Sesión", MessageBoxButton.OK, MessageBoxImage.Error);
                    TxtPassword.Clear();
                    TxtPassword.Focus();
                }
            }
            else
            {
                usuariosDB.Add(username, password);
                MessageBox.Show($"¡Cuenta registrada con éxito para {username}!", "Registro Completo", MessageBoxButton.OK, MessageBoxImage.Information);
                IngresarAlJuego(username);
            }
        }

        private void IngresarAlJuego(string username)
        {
            LblPerfilUser.Text = username;
            PanelLogin.Visibility = Visibility.Collapsed;
            PanelJuego.Visibility = Visibility.Visible;
            ActualizarEstadoBotonJuego();
            _ = ActualizarEstadoServidorAsync();
        }

        private void ActualizarEstadoBotonJuego()
        {
            string minecraftPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
            string modsFolder = Path.Combine(minecraftPath, "mods");
            string versionFile = Path.Combine(minecraftPath, "modpack_version.txt");

            string localVersion = File.Exists(versionFile) ? File.ReadAllText(versionFile).Trim() : "";

            if (!Directory.Exists(modsFolder))
            {
                BtnPlay.Content = "INSTALAR";
            }
            else if (!string.IsNullOrEmpty(remoteVersion) && localVersion != remoteVersion)
            {
                BtnPlay.Content = "ACTUALIZAR";
            }
            else
            {
                BtnPlay.Content = "JUGAR";
            }
        }

        private async void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            string minecraftPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
            string mcVersion = "1.21.1";
            string loaderVersion = "0.19.3";
            string versionName = $"fabric-loader-{loaderVersion}-{mcVersion}";
            string versionFile = Path.Combine(minecraftPath, "modpack_version.txt");

            try
            {
                BtnPlay.IsEnabled = false;

                string modsFolder = Path.Combine(minecraftPath, "mods");
                string localVersion = File.Exists(versionFile) ? File.ReadAllText(versionFile).Trim() : "";

                if (!Directory.Exists(modsFolder) || localVersion != remoteVersion)
                {
                    BtnPlay.Content = "DESCARGANDO...";
                    await DescargarYExtraerModpackDesdeDrive(DRIVE_FILE_ID, minecraftPath);
                    await File.WriteAllTextAsync(versionFile, remoteVersion);
                    ActualizarEstadoBotonJuego();
                }

                BtnPlay.Content = "PREPARANDO FABRIC...";
                await AsegurarPerfilFabric(minecraftPath, mcVersion, loaderVersion, versionName);

                BtnPlay.Content = "INICIANDO...";

                var path = new MinecraftPath(minecraftPath);
                var launcher = new MinecraftLauncher(path);

                string username = LblPerfilUser.Text;
                var session = MSession.CreateOfflineSession(username);

                var launchOption = new MLaunchOption
                {
                    Session = session,
                    MaximumRamMb = 4096
                };

                var process = await launcher.CreateProcessAsync(versionName, launchOption);
                process.Start();

                this.WindowState = WindowState.Minimized;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error durante la instalación o inicio:\n{ex.Message}", "Error de Lanzamiento", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnPlay.IsEnabled = true;
                ActualizarEstadoBotonJuego();
            }
        }

        private async Task AsegurarPerfilFabric(string minecraftPath, string mcVersion, string loaderVersion, string versionName)
        {
            string versionFolder = Path.Combine(minecraftPath, "versions", versionName);
            string jsonFile = Path.Combine(versionFolder, $"{versionName}.json");

            if (!File.Exists(jsonFile))
            {
                if (!Directory.Exists(versionFolder))
                {
                    Directory.CreateDirectory(versionFolder);
                }

                string fabricJsonUrl = $"https://meta.fabricmc.net/v2/versions/loader/{mcVersion}/{loaderVersion}/profile/json";

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "KasuLauncher");
                    string jsonContent = await client.GetStringAsync(fabricJsonUrl);
                    await File.WriteAllTextAsync(jsonFile, jsonContent);
                }
            }
        }

        private async Task DescargarYExtraerModpackDesdeDrive(string fileId, string destinationPath)
        {
            string tempZipPath = Path.Combine(destinationPath, "modpack_temp.zip");

            if (!Directory.Exists(destinationPath))
            {
                Directory.CreateDirectory(destinationPath);
            }

            var cookieContainer = new CookieContainer();
            var handler = new HttpClientHandler
            {
                CookieContainer = cookieContainer,
                AllowAutoRedirect = true
            };

            using (var client = new HttpClient(handler))
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                string initialUrl = $"https://drive.google.com/uc?export=download&id={fileId}";
                var response = await client.GetAsync(initialUrl);

                byte[] fileBytes;

                if (response.Content.Headers.ContentType?.MediaType == "text/html")
                {
                    string htmlContent = await response.Content.ReadAsStringAsync();

                    string confirmToken = string.Empty;
                    Match match = Regex.Match(htmlContent, @"confirm=([a-zA-Z0-9_-]+)");
                    if (match.Success)
                    {
                        confirmToken = match.Groups[1].Value;
                    }

                    string downloadUrl = string.IsNullOrEmpty(confirmToken)
                        ? $"https://drive.usercontent.google.com/download?id={fileId}&confirm=t"
                        : $"https://drive.google.com/uc?export=download&id={fileId}&confirm={confirmToken}";

                    var finalResponse = await client.GetAsync(downloadUrl);
                    fileBytes = await finalResponse.Content.ReadAsByteArrayAsync();
                }
                else
                {
                    fileBytes = await response.Content.ReadAsByteArrayAsync();
                }

                await File.WriteAllBytesAsync(tempZipPath, fileBytes);
            }

            ZipFile.ExtractToDirectory(tempZipPath, destinationPath, overwriteFiles: true);

            if (File.Exists(tempZipPath))
            {
                File.Delete(tempZipPath);
            }
        }

        private void ProfileCard_Click(object sender, MouseButtonEventArgs e)
        {
            MenuCuentaPopup.Visibility = MenuCuentaPopup.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void BtnChangeSkin_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Seleccioná tu archivo .png de skin.", "Cambiar Skin", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            TxtPassword.Clear();
            MenuCuentaPopup.Visibility = Visibility.Collapsed;
            PanelJuego.Visibility = Visibility.Collapsed;
            PanelLogin.Visibility = Visibility.Visible;
        }

        private void BtnToggleResetPassword_Click(object sender, MouseButtonEventArgs e)
        {
            PanelResetPassword.Visibility = PanelResetPassword.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void BtnConfirmPasswordReset_Click(object sender, RoutedEventArgs e)
        {
            string username = TxtUsername.Text.Trim();
            string newPassword = TxtNewPasswordLogin.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(newPassword))
            {
                MessageBox.Show("Completá tu usuario y la nueva contraseña para actualizarla.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (usuariosDB.ContainsKey(username))
            {
                usuariosDB[username] = newPassword;
                MessageBox.Show($"La contraseña de {username} fue actualizada con éxito.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                TxtNewPasswordLogin.Clear();
                PanelResetPassword.Visibility = Visibility.Collapsed;
            }
            else
            {
                MessageBox.Show("El usuario no existe. Primero iniciá sesión/registrate para crear la cuenta.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnCloseSettings_Click(object sender, RoutedEventArgs e)
        {
            PanelSettingsModal.Visibility = Visibility.Collapsed;
        }

        private void BtnBrowseJava_Click(object sender, RoutedEventArgs e)
        {
        }

        private void BtnOpenGameFolder_Click(object sender, RoutedEventArgs e)
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string minecraftPath = Path.Combine(appData, ".minecraft");

            if (!Directory.Exists(minecraftPath))
                Directory.CreateDirectory(minecraftPath);

            System.Diagnostics.Process.Start("explorer.exe", minecraftPath);
        }

        private void BtnClearCache_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Caché limpiada correctamente.", "Sistema", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ==========================================
        // ⚙️ APLICAR CAMBIOS DE AJUSTES EN VIVO
        // ==========================================
        private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            if (CmbMusicList.SelectedItem != null)
            {
                string selectedName = CmbMusicList.SelectedItem.ToString();
                if (listaCanciones.ContainsKey(selectedName))
                {
                    ReproducirMusica(listaCanciones[selectedName]);
                }
            }

            if (ChkDisableVideo.IsChecked == true)
            {
                BackgroundVideo.Pause();
                BackgroundVideo.Visibility = Visibility.Collapsed;
            }
            else
            {
                BackgroundVideo.Visibility = Visibility.Visible;
                BackgroundVideo.Play();
            }

            if (ChkFullScreen.IsChecked == true)
            {
                this.WindowState = WindowState.Maximized;
                this.WindowStyle = WindowStyle.None;
            }
            else
            {
                this.WindowState = WindowState.Normal;
                this.WindowStyle = WindowStyle.SingleBorderWindow;
            }

            PanelSettingsModal.Visibility = Visibility.Collapsed;
        }
    }
}