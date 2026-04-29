using ChirpApp.Data;
using ChirpApp.Models;
using System.Security.Cryptography;

namespace ChirpApp.Services
{
    // Service für Authentifizierung: Registrierung, Passwort-Hashing/Prüfung, Benutzer-Suche
    public class AuthService
    {
        // EF Core DbContext (Zugriff auf DB)
        private readonly ChirpAppContext _context;

        // DI-Konstruktor: DbContext kommt aus Program.cs (AddDbContext)
        public AuthService(ChirpAppContext context)
        {
            _context = context;
        }

        // -----------------------------
        // Registrierung eines neuen Users
        // -----------------------------
        public async Task<bool> RegisterNewUserAsync(Person userData, string password)
        {
            // 1) Zufälliges Salt erzeugen (schutz gegen Rainbow-Tables/identische Hashes)
            var saltBytes = GenerateSalt();

            // 2) Passwort mit PBKDF2 + Salt hashen
            //    HashPassword liefert (Hash, SaltBase64) zurück – Base64 wird hier nicht benötigt,
            //    wir speichern das Salt als Byte[] (saltBytes) in der DB.
            var (hashBytes, saltString) = HashPassword(password, saltBytes);

            // 3) Person-Objekt mit gehashtem Passwort + Salt aufbauen
            var person = new Person
            {
                Email = userData.Email,
                Name = userData.Name,
                PasswordHash = hashBytes,     // gespeicherter Hash (nicht das Klartext-Passwort!)
                PasswordSalt = saltBytes,     // gespeichertes Salt (pro Benutzer individuell)
                ShortDescription = userData.ShortDescription
            };

            // 4) In DB schreiben
            _context.Person.Add(person);
            await _context.SaveChangesAsync();
            return true;
        }

        // -----------------------------
        // Login-Prüfung
        // -----------------------------
        public bool VerifyPassword(string password, byte[] storedHash, byte[] storedSalt)
        {
            // 1) Beim Login dasselbe Verfahren wie bei der Registrierung anwenden:
            //    PBKDF2(password, gespeichertes Salt) -> Vergleich mit gespeichertem Hash
            var saltBytes = storedSalt;
            var (computedHashBytes, _) = HashPassword(password, saltBytes);

            // 2) Zeitkonstanter Vergleich (SequenceEqual) zwischen DB-Hash und neu berechnetem Hash
            var storedHashBytes = storedHash;
            return computedHashBytes.SequenceEqual(storedHashBytes);
        }

        // ------------------------------------------------
        // PBKDF2-Hashing mit Salt (Ableitung-Schlüsselverfahren)
        // - 100.000 Iterationen (Work-Faktor)
        // - SHA512 als Hash-Funktion
        // - 64 Bytes (512 Bit) Hashlänge
        // ------------------------------------------------
        private (byte[] Hash, string Salt) HashPassword(string password, byte[] salt)
        {
            // Rfc2898DeriveBytes = PBKDF2-Implementierung
            var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA512);

            // 64 Bytes Hash erzeugen (512 Bit)
            var hash = pbkdf2.GetBytes(64);

            // Zusätzlich: Salt als Base64-String, falls man es irgendwo textuell braucht (z. B. Logs/Export)
            return (hash, Convert.ToBase64String(salt));
        }

        // -----------------------------
        // Kryptographisch starkes Salt erzeugen (16 Byte)
        // -----------------------------
        private byte[] GenerateSalt()
        {
            byte[] salt = new byte[16]; // 128 Bit Salt
            using var rng = RandomNumberGenerator.Create(); // Kryptographisch sicherer Zufall
            rng.GetBytes(salt);
            return salt;
        }

        // --------------------------------------------
        // Benutzer anhand Login (E-Mail ODER Benutzername)
        // --------------------------------------------
        public Person? GetBenutzerByLogin(string login)
        {
            // Findet den ersten Benutzer, bei dem Login als E-Mail ODER Name passt
            // (Gross-/Kleinschreibung je nach Datenbank-Kollation)
            return _context.Person
                .FirstOrDefault(b => b.Email == login || b.Name == login);
        }
    }
}
