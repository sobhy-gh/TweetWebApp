namespace TweetWebApp.Models
{
    public class Like
    {
        public int UserId { get; set; }
        public int TweetId { get; set; }

        public User User { get; set; } = null!;
        public Tweet Tweet { get; set; } = null!;
    }
}
