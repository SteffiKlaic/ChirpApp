using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ChirpApp.Models;

namespace ChirpApp.Data
{
    // EF Core DbContext = "Tor" zur Datenbank.
    // Hält die DbSets (= Tabellen) und die Modellkonfiguration (OnModelCreating).
    public class ChirpAppContext : DbContext
    {
        // Der Konstruktor bekommt die DbContextOptions (ConnectionString, Provider, usw.)
        // -> kommt per Dependency Injection aus Program.cs (AddDbContext<ChirpAppContext>(...))
        public ChirpAppContext(DbContextOptions<ChirpAppContext> options)
            : base(options)
        {
        }

        // DbSet<T> entspricht einer Tabelle in der Datenbank:
        // - Person    -> Tabelle "Person"
        // - Chirp     -> Tabelle "Chirp"
        // - Peep      -> Tabelle "Peep"
        // - Like      -> Tabelle "Like"
        //
        // Über diese Properties führst du LINQ-Abfragen aus (z. B. _context.Chirp.Where(...))
        public DbSet<Person> Person { get; set; } = default!;
        public DbSet<Chirp> Chirp { get; set; } = default!;
        public DbSet<Peep> Peep { get; set; } = default!;
        public DbSet<Like> Like { get; set; } = default!;

        // Hier definierst du Beziehungen/Regeln, die EF nicht automatisch korrekt errät
        // (oder die du explizit steuern willst, z. B. Delete-Verhalten).
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Basis-Klassenkonfiguration von EF nicht verlieren:
            base.OnModelCreating(modelBuilder);

            // ------------------------------------------------------------
            // BEZIEHUNG: Like -> Person (viele Likes gehören zu 1 Person)
            // Person.Likes ist die "many"-Seite auf Person.
            // FK: Like.PersonId
            // OnDelete(DeleteBehavior.NoAction):
            //   Wenn Person gelöscht wird, sollen zugehörige Likes NICHT automatisch
            //   mitgelöscht werden (keine Kaskade). Das verhindert "Multiple cascade paths".
            // ------------------------------------------------------------
            modelBuilder.Entity<Like>()
                .HasOne(l => l.Person)
                .WithMany(p => p.Likes)
                .HasForeignKey(l => l.PersonId)
                .OnDelete(DeleteBehavior.NoAction);

            // ------------------------------------------------------------
            // BEZIEHUNG: Like -> Chirp (viele Likes gehören zu 1 Chirp)
            // Chirp.Likes ist die "many"-Seite auf Chirp.
            // FK: Like.ChirpId
            // Wieder: KEINE Kaskadenlöschung, um Konflikte mit mehreren
            // Delete-Pfaden zu vermeiden.
            // ------------------------------------------------------------
            modelBuilder.Entity<Like>()
                .HasOne(l => l.Chirp)
                .WithMany(c => c.Likes)
                .HasForeignKey(l => l.ChirpId)
                .OnDelete(DeleteBehavior.NoAction);

            // ------------------------------------------------------------
            // BEZIEHUNG: Chirp -> Person (Creator)
            // 1 Person hat viele Chirps (Person.Chirps).
            // FK: Chirp.PersonId
            // KEINE Kaskadenlöschung:
            //   Ein Person-Löschen würde sonst über Chirp -> Like und ggf. andere
            //   Relationen mehrere Kaskadenpfade auslösen (SQL Server mag das nicht).
            // ------------------------------------------------------------
            modelBuilder.Entity<Chirp>()
                .HasOne(c => c.Creator)
                .WithMany(p => p.Chirps)
                .HasForeignKey(c => c.PersonId)
                .OnDelete(DeleteBehavior.NoAction);

            // ------------------------------------------------------------
            // HINWEIS: Chirp <-> Peep (n:m) wird hier NICHT explizit konfiguriert.
            // EF Core erkennt die Many-to-Many-Relation automatisch,
            // weil in den Modellen LISTEN beidseitig vorhanden sind:
            //   - Chirp.Peeps : List<Peep>
            //   - Peep.Chirps : List<Chirp>
            //
            // EF legt dann eine Join-Tabelle (z. B. ChirpPeep) automatisch an.
            // Nur wenn du besondere Namen/Keys/Spalten willst, würdest du es hier konfigurieren.
            // ------------------------------------------------------------



            //    // Person -> Chirp: darf kaskadieren
            //    modelBuilder.Entity<Chirp>()
            //      .HasOne(c => c.Creator)
            //      .WithMany(p => p.Chirps)
            //      .HasForeignKey(c => c.PersonId)
            //      .OnDelete(DeleteBehavior.Cascade);

            //    // Chirp -> Like: darf kaskadieren
            //    modelBuilder.Entity<Like>()
            //      .HasOne(l => l.Chirp)
            //      .WithMany(c => c.Likes)
            //      .HasForeignKey(l => l.ChirpId)
            //      .OnDelete(DeleteBehavior.Cascade);

            //    // Like -> Person: KEINE Kaskade (Diamant brechen)
            //    modelBuilder.Entity<Like>()
            //      .HasOne(l => l.Person)
            //      .WithMany(p => p.Likes)
            //      .HasForeignKey(l => l.PersonId)
            //      .OnDelete(DeleteBehavior.NoAction); // oder Restrict
            //}
        }
    }
}
