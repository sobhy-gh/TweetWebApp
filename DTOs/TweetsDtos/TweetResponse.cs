namespace TweetWebApp.DTOs.TweetsDtos
{
    public class TweetResponse
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public int LikesCount { get; set; }
        public bool IsLikedByMe { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
