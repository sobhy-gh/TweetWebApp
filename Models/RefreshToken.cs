namespace TweetWebApp.Models
{
    public class RefreshToken
    {
        public int Id { get; set; }

        public string Token { get; set; } = string.Empty;  //  stored as SHA-256 hash

        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsRevoked { get; set; } = false; 
        public string ReplacedByToken { get; set; }  // for rotation audit trail

        public bool IsExpiresd => DateTime.UtcNow >= ExpiresAt;

        public bool IsActive => !IsExpiresd && !IsRevoked; 

    }
}
