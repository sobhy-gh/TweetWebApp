namespace TweetWebApp.Models
{
    public class Tweet
    {
        public int Id { get; set; } 
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Foreign Key
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public List<Like> Likes { get; set; } = [];
    }
}
