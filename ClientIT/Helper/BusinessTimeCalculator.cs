using System;

namespace ClientIT.Helpers
{
    public static class BusinessTimeCalculator
    {
        // Configurazione Orari
        private static readonly TimeSpan StartHour = new TimeSpan(8, 30, 0); // 08:30
        private static readonly TimeSpan EndHour = new TimeSpan(17, 30, 0);  // 17:30

        public static double GetBusinessHoursElapsed(DateTime start, DateTime end)
        {
            if (start >= end) return 0;

            double totalHours = 0;
            DateTime current = start;

            while (current < end)
            {
                // Se è Sabato o Domenica, saltiamo al prossimo Lunedì ore 8:30
                if (current.DayOfWeek == DayOfWeek.Saturday || current.DayOfWeek == DayOfWeek.Sunday)
                {
                    current = current.Date.AddDays(1); // Vai a domani
                    // Se domani è lunedì, resetta l'orario di inizio
                    if (current.DayOfWeek == DayOfWeek.Monday)
                        current = current.Add(StartHour);
                    continue;
                }

                // Definizione inizio e fine giornata lavorativa corrente
                DateTime workStart = current.Date.Add(StartHour);
                DateTime workEnd = current.Date.Add(EndHour);

                // Se siamo prima dell'inizio lavoro, saltiamo all'inizio
                if (current < workStart)
                {
                    current = workStart;
                }

                // Se siamo dopo la fine lavoro, saltiamo a domani mattina
                if (current >= workEnd)
                {
                    current = current.Date.AddDays(1).Add(StartHour);
                    continue;
                }

                // Calcoliamo quanto manca alla fine di QUESTA giornata lavorativa
                // O quanto manca alla fine del ticket (se finisce oggi)
                DateTime effectiveEnd = (end < workEnd) ? end : workEnd;

                if (effectiveEnd > current)
                {
                    totalHours += (effectiveEnd - current).TotalHours;
                    current = effectiveEnd;
                }

                // Se abbiamo raggiunto la fine giornata ma non la fine del ticket, avanziamo
                if (current >= workEnd)
                {
                    current = current.Date.AddDays(1).Add(StartHour);
                }
            }

            return totalHours;
        }
    }
}