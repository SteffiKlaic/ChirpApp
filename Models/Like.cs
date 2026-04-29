namespace ChirpApp.Models
{
    public class Like
    {
        public int Id { get; set; }
        public DateTime CreatedOn { get; set; } = DateTime.Now;

        // Fremdschlüssel
        public int ChirpId { get; set; }
        public Chirp Chirp { get; set; } = null!;
        public int PersonId { get; set; }
        public Person Person { get; set; } = null!;
    }
}
