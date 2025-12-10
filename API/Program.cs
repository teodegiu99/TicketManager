// Program.cs
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.EntityFrameworkCore;
using TicketAPI.Data;

var builder = WebApplication.CreateBuilder(args);

// --- 1. Configura la connessione al DB Postgres ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApiDbContext>(options =>
    options.UseNpgsql(connectionString));

// --- 2. Aggiungi i Controller e l'Autenticazione ---
builder.Services.AddControllers();
builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
   .AddNegotiate();

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = options.DefaultPolicy;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Non usare HTTPS per ora, semplifica il debug
    // app.UseHttpsRedirection(); 
}
app.UseStaticFiles(); // Permette di accedere a /Uploads/immagine.png
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();