using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;

namespace ClientIT.Models
{
    public class ProjectViewModel
    {
        public int Id { get; set; }

        // FIX 1: Protezione per Titolo null
        private string _titolo;
        public string Titolo
        {
            get => _titolo ?? string.Empty;
            set => _titolo = value;
        }

        // FIX 2: Protezione per Descrizione null (che causava crash nella lista)
        private string _descrizione;
        public string Descrizione
        {
            get => _descrizione ?? string.Empty;
            set => _descrizione = value;
        }

        // --- FIX CRITICO: Cambia int in int? per evitare NullReferenceException nel Binding TwoWay ---
        public int? StatoId { get; set; }

        public int? AssegnatoAId { get; set; }

        // Protezione anche per StatoNome
        private string _statoNome;
        public string StatoNome
        {
            get => _statoNome ?? "-";
            set => _statoNome = value;
        }

        public DateTime? DataInizio { get; set; }
        public DateTime? DataPrevFine { get; set; }

        public Stato Stato { get; set; }
        public ItUtente AssegnatoA { get; set; }

        public List<PhaseViewModel> Fasi { get; set; } = new();

        public string AssegnatoANome => AssegnatoA?.Nome ?? "Non assegnato";
        public string StatoColor => StatoId switch
        {
            1 => "#3498db", // Nuovo (Blu)
            2 => "#f39c12", // In Corso (Arancio)
            3 => "#27ae60", // Terminato (Verde)
            _ => "#7f8c8d"
        };
    }

    public class CommentoViewModel
    {
        public int Id { get; set; }
        public string Testo { get; set; }
        public DateTime DataCreazione { get; set; }
        public string Username { get; set; }
        public int UtenteId { get; set; }

        // --- PROPRIETÀ VISIVE ---
        public HorizontalAlignment Allineamento { get; set; }

        // Sfondo del fumetto
        public SolidColorBrush Sfondo { get; set; }

        // NUOVO: Colore del testo (per contrasto)
        public SolidColorBrush ColoreTesto { get; set; }

        // NUOVO: Le iniziali da mostrare nel cerchietto
        public string Iniziali { get; set; }
    }
}