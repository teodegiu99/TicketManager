using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ClientIT.Controls
{
    public sealed partial class UserAdminControl : UserControl, INotifyPropertyChanged
    {
        private HttpClient _apiClient;
        private string _apiBaseUrl = "http://localhost:5210";

        // Dati per il Binding
        private string _currentUsername = "";
        private string _displayName = "";
        private string _email = "";
        private bool _isLocked;
        private bool _isDisabled;
        private string _passwordInfo = "";

        // Visibilità
        private Visibility _isInfoVisible = Visibility.Collapsed;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public Visibility IsInfoVisible { get => _isInfoVisible; set { _isInfoVisible = value; OnPropertyChanged(); } }
        public string CurrentDisplayName { get => _displayName; set { _displayName = value; OnPropertyChanged(); OnPropertyChanged(nameof(UserInitials)); } }
        public string CurrentEmail { get => _email; set { _email = value; OnPropertyChanged(); } }

        public bool IsLocked { get => _isLocked; set { _isLocked = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusColor)); } }
        public string StatusText => IsLocked ? "BLOCCATO" : "Attivo";
        public SolidColorBrush StatusColor => IsLocked ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.Green);

        public bool IsDisabled { get => _isDisabled; set { _isDisabled = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisabledText)); OnPropertyChanged(nameof(EnableButtonText)); } }
        public string DisabledText => IsDisabled ? "SÌ" : "No";
        public string EnableButtonText => IsDisabled ? "Abilita Account" : "Disabilita Account";

        public string PasswordInfo { get => _passwordInfo; set { _passwordInfo = value; OnPropertyChanged(); } }
        public string UserInitials => !string.IsNullOrEmpty(CurrentDisplayName) ? CurrentDisplayName.Substring(0, 1) : "?";


        public UserAdminControl()
        {
            this.InitializeComponent();
            var handler = new HttpClientHandler { UseDefaultCredentials = true, ServerCertificateCustomValidationCallback = (s, c, ch, e) => true };
            _apiClient = new HttpClient(handler);
        }

        private async void BtnSearch_Click(object sender, RoutedEventArgs e) => await SearchUser();
        private async void TxtSearchUser_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter) await SearchUser();
        }

        private async Task SearchUser()
        {
            if (string.IsNullOrWhiteSpace(TxtSearchUser.Text)) return;

            LoadingBar.Visibility = Visibility.Visible;
            IsInfoVisible = Visibility.Collapsed;
            FeedbackInfoBar.IsOpen = false;

            try
            {
                var username = TxtSearchUser.Text.Trim();
                var response = await _apiClient.GetAsync($"{_apiBaseUrl}/api/admin/user/{username}");

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadFromJsonAsync<AdminUserDto>();
                    if (data != null)
                    {
                        _currentUsername = data.Username;
                        CurrentDisplayName = data.DisplayName;
                        CurrentEmail = data.Email;
                        IsLocked = data.IsLocked;
                        IsDisabled = data.IsDisabled;

                        string pwdStatus = data.PasswordNeverExpires ? "Password senza scadenza." : "Scadenza password standard.";
                        if (data.LastPasswordSet.HasValue)
                            pwdStatus += $" Ultimo cambio: {data.LastPasswordSet.Value.ToShortDateString()}.";

                        PasswordInfo = pwdStatus;

                        IsInfoVisible = Visibility.Visible;
                    }
                }
                else
                {
                    ShowMsg("Utente non trovato o errore server.", InfoBarSeverity.Error);
                }
            }
            catch (Exception ex)
            {
                ShowMsg($"Errore: {ex.Message}", InfoBarSeverity.Error);
            }
            finally
            {
                LoadingBar.Visibility = Visibility.Collapsed;
            }
        }

        private async void BtnUnlock_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var resp = await _apiClient.PostAsJsonAsync($"{_apiBaseUrl}/api/admin/unlock", _currentUsername);
                if (resp.IsSuccessStatusCode)
                {
                    ShowMsg("Account sbloccato con successo!", InfoBarSeverity.Success);
                    IsLocked = false; // Aggiorna UI
                }
                else ShowMsg("Errore durante lo sblocco.", InfoBarSeverity.Error);
            }
            catch (Exception ex) { ShowMsg(ex.Message, InfoBarSeverity.Error); }
        }

        private async void BtnToggleEnable_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var resp = await _apiClient.PostAsJsonAsync($"{_apiBaseUrl}/api/admin/toggle-enable", _currentUsername);
                if (resp.IsSuccessStatusCode)
                {
                    IsDisabled = !IsDisabled; // Inverte stato locale
                    ShowMsg(IsDisabled ? "Account disabilitato." : "Account abilitato.", InfoBarSeverity.Success);
                }
                else ShowMsg("Errore operazione.", InfoBarSeverity.Error);
            }
            catch (Exception ex) { ShowMsg(ex.Message, InfoBarSeverity.Error); }
        }

        private async void BtnResetPwd_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string newPwd = TxtNewPwd.Password;
                if (string.IsNullOrWhiteSpace(newPwd)) newPwd = "Ciaociao1!"; // Default come da VB

                var req = new { Username = _currentUsername, NewPassword = newPwd, UserMustChangePassword = ChkChangeAtLogon.IsChecked == true };
                var resp = await _apiClient.PostAsJsonAsync($"{_apiBaseUrl}/api/admin/reset-password", req);

                if (resp.IsSuccessStatusCode)
                {
                    ShowMsg($"Password reimpostata a: {newPwd}", InfoBarSeverity.Success);
                    TxtNewPwd.Password = "";
                }
                else
                {
                    string err = await resp.Content.ReadAsStringAsync();
                    ShowMsg($"Errore: {err}", InfoBarSeverity.Error);
                }
            }
            catch (Exception ex) { ShowMsg(ex.Message, InfoBarSeverity.Error); }
        }

        private void BtnGenPwd_Click(object sender, RoutedEventArgs e)
        {
            TxtNewPwd.Password = System.IO.Path.GetRandomFileName().Replace(".", "") + "1!";
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            TxtSearchUser.Text = "";
            IsInfoVisible = Visibility.Collapsed;
            FeedbackInfoBar.IsOpen = false;
        }

        private void ShowMsg(string msg, InfoBarSeverity severity)
        {
            FeedbackInfoBar.Message = msg;
            FeedbackInfoBar.Severity = severity;
            FeedbackInfoBar.IsOpen = true;
        }

        public class AdminUserDto
        {
            public string Username { get; set; }
            public string DisplayName { get; set; }
            public string Email { get; set; }
            public bool IsLocked { get; set; }
            public bool IsDisabled { get; set; }
            public bool PasswordNeverExpires { get; set; }
            public DateTime? LastPasswordSet { get; set; }
        }
    }
}