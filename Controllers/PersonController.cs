using ChirpApp.Data;
using ChirpApp.Models;
using ChirpApp.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ChirpApp.Controllers
{
    public class PersonController : Controller
    {
        // EF Core Datenbankzugriff
        private readonly ChirpAppContext _context;

        // Service-Klasse für Registrierung / Login / Passwort-Hashing
        private readonly AuthService _authService;

        // Konstruktor mit Dependency Injection
        public PersonController(ChirpAppContext context, AuthService authservice)
        {
            _context = context;
            _authService = authservice;
        }

        // GET: /Person/Register
        // Zeigt die Registrierungsseite
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // POST: /Person/Register
        // Führt Registrierung eines neuen Benutzers durch
        [HttpPost]
        [ValidateAntiForgeryToken] // Schutz gegen Cross-Site Request Forgery
        public async Task<IActionResult> Register(Person person, string passwort)
        {
            // Registrierungslogik an AuthService auslagern (Passwort-Hashing, Salt etc.)
            await _authService.RegisterNewUserAsync(person, passwort);

            // Nachricht an nächste View übergeben (wird in Login angezeigt)
            TempData["RegisterSuccess"] = "Registrierung erfolgreich! Du kannst dich jetzt anmelden.";

            // Redirect (neue GET-Request) -> vermeidet doppeltes Abschicken bei „F5“
            return RedirectToAction("Login", "Person");
        }

        // GET: /Person/Login
        // Zeigt Login-Formular
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // POST: /Person/Login
        // Prüft Benutzername/E-Mail und Passwort -> erstellt Login-Cookie
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            // ModelState prüft, ob alle Pflichtfelder ausgefüllt sind
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Benutzer anhand von E-Mail ODER Benutzername suchen
            var user = _authService.GetBenutzerByLogin(model.Login);

            // Wenn Benutzer existiert UND Passwort korrekt ist ...
            if (user != null && _authService.VerifyPassword(model.Passwort, user.PasswordHash, user.PasswordSalt))
            {
                // --- Authentifizierung vorbereiten ---

                // Claims = Infos über den Benutzer (werden im Cookie gespeichert)
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), // Primärschlüssel
                    new Claim(ClaimTypes.Name, user.Name),                    // Anzeigename
                    new Claim(ClaimTypes.Email, user.Email)                   // E-Mail
                };

                // Identity + Principal (Standard-Authentifizierung in ASP.NET Core)
                var claimsIdentity = new ClaimsIdentity(claims, "MyCookieAuth");
                var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

                // Login-Cookie setzen
                await HttpContext.SignInAsync("MyCookieAuth", claimsPrincipal);

                // Nach erfolgreichem Login -> Home-Seite
                return RedirectToAction("Index", "Home");
            }

            // Wenn falsche Logindaten -> Fehlermeldung
            ViewBag.LoginError = "Login fehlgeschlagen";
            return View();
        }

        // POST: /Person/Logout
        // Beendet Sitzung -> Cookie wird gelöscht
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            // Löscht Auth-Cookie
            await HttpContext.SignOutAsync("MyCookieAuth");

            // Zurück zur Startseite
            return RedirectToAction("Index", "Home");
        }

        // GET: /Person/Profile/{id}
        // Zeigt das Profil eines Benutzers (eigene Seite)
        public async Task<IActionResult> Profile(int id)
        {
            // Benutzer mit passender ID aus DB laden
            var person = await _context.Person.FirstOrDefaultAsync(p => p.Id == id);

            // Falls Benutzer nicht existiert -> 404-Fehler
            if (person == null) return NotFound();

            // Letzte 5 Chirps dieses Benutzers
            var recentChirps = await _context.Chirp
                .Where(c => c.PersonId == id)             // nur eigene Chirps
                .OrderByDescending(c => c.CreatedOn)      // neueste zuerst
                .Include(c => c.Likes)                    // Likes mitladen (für Anzeige)
                .Take(5)
                .ToListAsync();

            // ViewModel für Profilseite: fasst alles zusammen
            var vm = new ProfileVm
            {
                Person = person, // Daten zum Benutzer selbst
                RecentChirps = recentChirps, // Liste der letzten 5 Chirps
                ChirpCount = await _context.Chirp.CountAsync(c => c.PersonId == id),         // Anzahl aller eigenen Chirps
                LikesGiven = await _context.Like.CountAsync(l => l.PersonId == id),          // Likes, die dieser Nutzer vergeben hat
                LikesReceived = await _context.Like.CountAsync(l => l.Chirp.PersonId == id)  // Likes, die er auf eigene Chirps bekam
            };

            // View "Profile.cshtml" mit Daten anzeigen
            return View(vm);
        }

    }
}
