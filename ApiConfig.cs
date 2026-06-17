using System;

namespace TicketManager
{
    public static class ApiConfig
    {
        // =========================================================
        // INTERRUTTORE GENERALE
        // true  = Lavori in LOCALE (localhost)
        // false = Lavori in REMOTO (Azure/Server vero)
        // =========================================================
        public const bool UsaLocalhost = false;


        // Qui definisci i due indirizzi
        private const string UrlLocale = "http://localhost:5210";
        private const string UrlRemoto = "http://szblbiis01";

        // La logica che sceglie l'indirizzo giusto automaticamente
        public static string BaseUrl => UsaLocalhost ? UrlLocale : UrlRemoto;
    }
}