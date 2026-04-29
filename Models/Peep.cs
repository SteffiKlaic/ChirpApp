namespace ChirpApp.Models
{
    public class Peep
    {
        public int Id { get; set; }
        public DateTime CreatedOn { get; set; } = DateTime.Now;
        public string Notion { get; set; }

        // Navigation zur n:m Beziehung zur Chirps
        public List<Chirp> Chirps { get; set; } = new();
    }
}
