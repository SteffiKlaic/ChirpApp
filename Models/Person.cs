using System.ComponentModel.DataAnnotations;

namespace ChirpApp.Models
{
    public class Person
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public byte[] PasswordHash { get; set; }
        public byte[] PasswordSalt { get; set; }
        [MaxLength(300)]
        public string? ShortDescription { get; set; }
        public DateTime RegistrationDate { get; set; } = DateTime.Now;

        public List<Chirp> Chirps { get; set; } = new();
        public List<Like> Likes { get; set; } = new();

    }
}
