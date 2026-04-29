using ChirpApp.Data;
using ChirpApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ChirpApp.Controllers
{
    public class ChirpController : Controller
    {
        // EF-DbContext (Zugang zur Datenbank). Kommt per Dependency Injection.
        private readonly ChirpAppContext _context;

        // Konstruktor: Framework übergibt hier automatisch den DbContext.
        public ChirpController(ChirpAppContext context)
        {
            _context = context;
        }

        // GET: /Chirp
        // Startseite der Chirps: zeigt 10 (eingeloggt) bzw. 5 (anonym) neueste Chirps,
        // inklusive Creator, Peeps und Likes. Außerdem werden Top-5-Peeps (24h) berechnet.
        public async Task<IActionResult> Index()
        {
            // Anzahl je nach Login-Status
            var count = User.Identity?.IsAuthenticated == true ? 10 : 5;

            // Chirps inkl. Navigationseigenschaften (Creator, Peeps, Likes) laden
            // OrderByDescending: neueste zuerst; Take: nur count Stück; ToListAsync: DB wirklich ausführen
            var chirps = await _context.Chirp
                .Include(c => c.Creator)     // Autor des Chirps
                .Include(c => c.Peeps)       // zugehörige Peeps (n:m)
                .Include(c => c.Likes)       // Likes zum Chirp
                .OrderByDescending(c => c.CreatedOn)
                .Take(count)
                .ToListAsync();

            // --- Top 5 Peeps der letzten 24h ---
            var since = DateTime.Now.AddDays(-1); // Zeitpunkt "jetzt minus 1 Tag"

            // Schritt 1 (DB): Chirps der letzten 24h inkl. Peeps laden und in eine flache Peep-Liste umwandeln
            var recentPeeps = await _context.Chirp
                .Where(c => c.CreatedOn >= since) // Filter: nur letzte 24h
                .Include(c => c.Peeps)            // Peeps gleich mitladen
                .SelectMany(c => c.Peeps)         // aus List<Chirp<List<Peep>>> -> List<Peep>
                .ToListAsync();                   // hier wird die DB-Abfrage wirklich ausgeführt

            // Schritt 2 (in Memory): Namen normalisieren, gruppieren, zählen, Top 5 bilden
            var topPeeps = recentPeeps
                 .Select(p => p.Notion.ToLowerInvariant())              // Name in Kleinbuchstaben (webdev == WebDev)
                 .GroupBy(n => n)                                       // gleiche Namen zusammenfassen
                 .Select(g => new KeyValuePair<string, int>(g.Key, g.Count())) // (Name, Anzahl)
                 .OrderByDescending(x => x.Value)                       // meistgenutzte zuerst
                 .Take(5)                                               // nur 5 Stück
                 .ToList();                                             // Liste bilden

            // Übergabe der Top-Peeps an die View
            ViewBag.TopPeeps = topPeeps;
            // --- Ende Top 5 ---

            // View bekommt die Chirp-Liste
            return View(chirps);
        }

        // GET: /Chirp/ByPeep?peep=webdev
        // Filtert Chirps über die in der DB gespeicherten Peeps (n:m-Relation).
        [HttpGet]
        public async Task<IActionResult> ByPeep(string peep)
        {
            // Leere Eingabe -> zurück zur Übersicht
            if (string.IsNullOrWhiteSpace(peep))
                return RedirectToAction(nameof(Index));

            // Benutzer-Eingabe bereinigen und case-insensitive vergleichen
            var key = peep.Trim().ToLowerInvariant();

            // WICHTIG: Filter über Relation (nicht über Textsuche):
            // c.Peeps.Any(...): es existiert mind. ein verknüpfter Peep mit passendem Namen
            // Includes laden wieder alle Infos für die Tabelle mit (Creator, Peeps, Likes)
            var chirps = await _context.Chirp
                .Include(c => c.Creator)
                .Include(c => c.Peeps)
                .Include(c => c.Likes)
                .Where(c => c.Peeps.Any(p => p.Notion.ToLower() == key))
                .OrderByDescending(c => c.CreatedOn)
                .ToListAsync();

            // Für Hinweis in der View ("Gefiltert nach: <xyz>")
            ViewBag.FilterPeep = peep;

            // Gleiche View wie Index verwenden, aber mit gefilterter Liste
            return View("Index", chirps);
        }

        // GET: /Chirp/Create
        // Zeigt das Formular für neuen Chirp (nur für eingeloggte Nutzer).
        [Authorize]
        public IActionResult Create()
        {
            // Beispiel: falls die View eine Person-Auswahl bräuchte (hier versteckt)
            ViewData["PersonId"] = new SelectList(_context.Person, "Id", "Id");
            return View();
        }

        // POST: /Chirp/Create
        // Speichert den neuen Chirp. HIER werden die Peeps aus dem Text extrahiert & mitgespeichert.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken] // Schutz gegen CSRF
        public async Task<IActionResult> Create([Bind("Message")] Chirp chirp)
        {
            // Aktuellen User ermitteln (nur eingeloggte Nutzer dürfen posten)
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Forbid();

            // FK + Zeitstempel setzen
            chirp.PersonId = int.Parse(userId);
            chirp.CreatedOn = DateTime.Now;

            // Falls Validierung fehlschlägt -> zurück ins Formular
            if (!ModelState.IsValid) return View(chirp);

            // Regex für Peeps: nur öffnendes '<' + 3..16 alphanumerische Zeichen (kein schließendes '>' nötig)
            // Beispiel: "<webdev", "<LAP!", "<softwarentwicklung" (das letzte ist zu lang -> passt nicht)
            var rx = new Regex(@"<([A-Za-z0-9]{3,16})(?![A-Za-z])");

            // Alle Peeps aus dem Text holen, in Kleinbuchstaben, doppelte entfernen
            var peepNames = rx.Matches(chirp.Message ?? string.Empty)
                              .Cast<Match>()
                              .Select(m => m.Groups[1].Value.ToLowerInvariant())
                              .Distinct()
                              .ToList();

            // ZUERST den neuen Chirp bei EF registrieren (noch nicht gespeichert)
            _context.Add(chirp);

            // Für JEDE gefundene Peep-Bezeichnung:
            // - prüfen, ob es diesen Peep-Namen schon in der DB gibt
            // - wenn nicht, neuen Peep anlegen
            // - dann Relation Chirp<->Peep setzen (EF pflegt die n:m-Join-Tabelle)
            foreach (var name in peepNames)
            {
                var peep = await _context.Peep
                    .FirstOrDefaultAsync(p => p.Notion.ToLower() == name);

                if (peep == null)
                {
                    peep = new Peep { Notion = name }; // Notion = eigentlicher Peep-Name (z. B. "webdev")
                    _context.Peep.Add(peep);           // neuen Peep zur DB hinzufügen
                }

                chirp.Peeps.Add(peep);                 // n:m-Verknüpfung setzen
            }

            // Alle Änderungen (Chirp, ggf. neue Peeps, Join-Einträge) in die DB schreiben
            await _context.SaveChangesAsync();

            // zurück zur Übersicht
            return RedirectToAction(nameof(Index));
        }

        // POST: /Chirp/ToggleLike
        // Liken/Entliken eines Chirps (nur eingeloggt).
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLike(int chirpId)
        {
            // Aktuelle Benutzer-ID lesen (Claims)
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (uid == null) return Forbid();

            var personId = int.Parse(uid);

            // Prüfen, ob Like bereits existiert (Nutzer hat diesen Chirp schon geliked?)
            var existingLike = await _context.Like
                .FirstOrDefaultAsync(x => x.ChirpId == chirpId && x.PersonId == personId);

            if (existingLike == null)
                _context.Like.Add(new Like { ChirpId = chirpId, PersonId = personId }); // Like hinzufügen
            else
                _context.Like.Remove(existingLike); // Like entfernen (Unlike)

            // Änderungen speichern
            await _context.SaveChangesAsync();

            // zurück zur Liste
            return RedirectToAction(nameof(Index));
        }
    }
}
