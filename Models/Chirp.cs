using System.ComponentModel.DataAnnotations;

namespace ChirpApp.Models
{
    public class Chirp
    {
        public int Id { get; set; }
        public DateTime CreatedOn { get; set; } = DateTime.Now;

        [MaxLength(123)]
        public string Message { get; set; }

        // FK zur Person
        public int PersonId { get; set; }
        public Person? Creator { get; set; } = null!;

        //Navigation zur n:m Beziehung mit Peeps
        public List<Peep> Peeps { get; set; } = new();
        public List<Like> Likes { get; set; } = new();

    }
}
