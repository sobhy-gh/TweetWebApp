using System.ComponentModel.DataAnnotations;

namespace TweetWebApp.DTOs.TweetsDtos
{
    public class CreateTweetRequest
    {
        [Required]
        [MinLength(1)]
        [MaxLength(280)]
        public string Content { get; set; } = string.Empty;
    }
}
