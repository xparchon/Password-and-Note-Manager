using System;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace Password_and_Note_Manager.Views
{
    public partial class LoginPopupWnd : Window
    {
        public bool IsLoginSuccessful { get; private set; } = false;
        private readonly string _authUrl;
        private readonly string _redirectUri;
        public string RedirectedUrl { get; private set; }

        public LoginPopupWnd(string authUrl, string redirectUri)
        {
            InitializeComponent();
            _authUrl = authUrl;
            _redirectUri = redirectUri;
            InitializeAsync();
        }

        // Event handler for navigation starting in the WebView2 control
        private void CoreWebView2_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (e.Uri.StartsWith(_redirectUri, StringComparison.OrdinalIgnoreCase))
            {
                e.Cancel = true;
                RedirectedUrl = e.Uri; // Keep the complete URL with the "code" parameter
                IsLoginSuccessful = true;
                this.DialogResult = true;
                this.Close();
            }
        }

        // Initializes the WebView2 control and navigates to the authentication URL
        private async void InitializeAsync()
        {
            await webView.EnsureCoreWebView2Async();
            webView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
            webView.CoreWebView2.Navigate(_authUrl);
        }

    }
}