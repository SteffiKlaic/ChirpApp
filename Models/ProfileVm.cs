namespace ChirpApp.Models
{
    public class ProfileVm
    {
        public Person Person { get; set; } = null!;
        public List<Chirp> RecentChirps { get; set; } = new();
        public int ChirpCount { get; set; }
        public int LikesGiven { get; set; }
        public int LikesReceived { get; set; }
    }
}
