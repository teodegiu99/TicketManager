// Data/ApiDbContext.cs
using Microsoft.EntityFrameworkCore;
using TicketAPI.Models;

namespace TicketAPI.Data
{
    public class ApiDbContext : DbContext
    {
        public ApiDbContext(DbContextOptions<ApiDbContext> options) : base(options)
        {
        }

        // Mappa le nostre classi alle tabelle del DB
        // I nomi qui DEVONO corrispondere ai nomi delle tue tabelle in Postgres
        public DbSet<Ticket> Ticket { get; set; }
        public DbSet<Tipologia> Tipologie { get; set; }
        public DbSet<Urgenza> Urgenza { get; set; }
        public DbSet<Sede> Sedi { get; set; }
        public DbSet<Stato> Stati { get; set; }
        public DbSet<ItUtente> ItUtenti { get; set; }
        public DbSet<Progetto> Progetti { get; set; }
        public DbSet<FaseProgetto> FasiProgetto { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Serve per dire a EF Core che i nomi delle tabelle
            // sono minuscoli (come è standard in Postgres)
            modelBuilder.Entity<Ticket>().ToTable("ticket");
            modelBuilder.Entity<Tipologia>().ToTable("tipologie");
            modelBuilder.Entity<Urgenza>().ToTable("urgenza");
            modelBuilder.Entity<Sede>().ToTable("sedi");
            modelBuilder.Entity<ItUtente>().ToTable("it_utenti");
            modelBuilder.Entity<Stato>().ToTable("stato");
            modelBuilder.Entity<Progetto>().ToTable("progetti");
            modelBuilder.Entity<FaseProgetto>().ToTable("fasiprogetto");
        }
    }
}