using static System.Net.Mime.MediaTypeNames;

namespace TweetWebApp.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Relationships 
        public List<Tweet> Tweets { get; set; } = [];
        public List<Like> Likes { get; set; } = [];
        public List<Follow> Followers { get; set; } = []; 
        public List<Follow> Following { get; set; } = []; 
    }
}
