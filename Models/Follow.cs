namespace TweetWebApp.Models
{
    public class Follow
    {
        public int FollowerId { get; set; } // اللي بيتابع
        public int FollowingId { get; set; } // اللي اتتابع

        public User Follower { get; set; } = null!;
        public User Following { get; set; } = null!;
    }
}
