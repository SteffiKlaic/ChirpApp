using ChirpApp.Data;           // Zugriff auf den DbContext (für Datenbank)
using ChirpApp.Services;       // Zugriff auf den AuthService (Registrierung/Login)
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
// Erstellt den "Builder" – hier konfigurierst du alle Services, bevor die App gebaut wird

// --- Datenbank-Konfiguration (Entity Framework Core) ---
// AddDbContext registriert den ChirpAppContext (EF Core) als Service (Dependency Injection)
// ConnectionString wird aus appsettings.json gelesen (Name: "ChirpAppContext")
builder.Services.AddDbContext<ChirpAppContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("ChirpAppContext")
        ?? throw new InvalidOperationException("Connection string 'ChirpAppContext' not found.")
    ));

// --- MVC hinzufügen ---
// Stellt Controller + Views bereit (z. B. ChirpController, PersonController + Razor Views)
builder.Services.AddControllersWithViews();

// --- Cookie-basierte Authentifizierung aktivieren ---
// "MyCookieAuth" ist der interne Name der Authentifizierung
builder.Services.AddAuthentication("MyCookieAuth")
    .AddCookie("MyCookieAuth", options =>
    {
        // Name des Cookies im Browser
        options.Cookie.Name = "UserAuthCookie";

        // HttpOnly = true → Cookie kann NICHT per JavaScript ausgelesen werden (sicherer)
        options.Cookie.HttpOnly = true;

        // SecurePolicy.None → Cookie wird auch über HTTP gesendet (für lokale Entwicklung)
        // In Produktion: CookieSecurePolicy.Always (nur HTTPS!)
        options.Cookie.SecurePolicy = CookieSecurePolicy.None;

        // SlidingExpiration = true → Ablaufzeit wird bei jeder Aktivität verlängert
        options.SlidingExpiration = true;

        // Wenn ein nicht eingeloggter Nutzer auf eine geschützte Seite zugreift,
        // wird er automatisch auf diese Login-URL weitergeleitet:
        options.LoginPath = "/Person/Login";
    });

// --- Authentifizierungs-Service registrieren ---
// AddScoped = 1 Instanz pro HTTP-Request (Standard für Services, die DB-Zugriff haben)
builder.Services.AddScoped<AuthService>();

// --- Jetzt wird die App gebaut (alle Services stehen ab hier bereit) ---
var app = builder.Build();







// --- Fehlerbehandlung konfigurieren ---
// In Produktionsumgebung: Zeige benutzerdefinierte Fehlerseite
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

// --- Statische Dateien aktivieren ---
// z. B. wwwroot/css, wwwroot/js, wwwroot/images
app.UseStaticFiles();

// --- Routing aktivieren ---
// Legt fest, wie URLs zu Controllern/Aktionen aufgelöst werden
app.UseRouting();

// --- Authentifizierung + Autorisierung aktivieren ---
// Achtung: Reihenfolge wichtig! Erst UseAuthentication, dann UseAuthorization
app.UseAuthentication(); // 🔹 MUSS VOR UseAuthorization stehen!
app.UseAuthorization();

// --- Standard-Route (Fallback), falls keine spezifischere Route passt ---
// z. B. / → HomeController.Index()
// z. B. /Chirp/Index → ChirpController.Index()
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// --- App starten (startet Webserver) ---
app.Run();
