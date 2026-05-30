using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TweetWebApp.Data;
using TweetWebApp.DTOs.TweetsDtos;
using TweetWebApp.Models;

namespace TweetWebApp.Controllers
{
    [ApiController]
    [Route("api/tweets")]
    public class TweetsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public TweetsController(AppDbContext db)
        {
            _db = db;
        }

        // ── POST /api/tweets ──────────────────────────────────────────────────────
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateTweet([FromBody] CreateTweetRequest request)
        {
            // 
            int userId = int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!
            );

            var tweet = new Tweet
            {
                Content = request.Content,
                UserId = userId
            };

            _db.Tweets.Add(tweet);
            await _db.SaveChangesAsync();

            //
            var user = await _db.Users.FindAsync(userId);

            return StatusCode(201, new TweetResponse
            {
                Id = tweet.Id,
                Content = tweet.Content,
                AuthorName = user!.Username,
                LikesCount = 0,
                IsLikedByMe = false,
                CreatedAt = tweet.CreatedAt
            });
        }

        // ── GET /api/tweets/{id} ──────────────────────────────────────────────────
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetTweet(int id)
        {
            var tweet = await _db.Tweets
                .Include(t => t.User)
                .Include(t => t.Likes)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tweet == null)
                return NotFound(" Tweet does not exist ! .");

          
            bool isLiked = false;
            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = int.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!); 
                isLiked = tweet.Likes.Any(l => l.UserId == userId);
            }

            return Ok(new TweetResponse
            {
                Id = tweet.Id,
                Content = tweet.Content,
                AuthorName = tweet.User.Username,
                LikesCount = tweet.Likes.Count,
                IsLikedByMe = isLiked,
                CreatedAt = tweet.CreatedAt
            });
        }

        // ── GET /api/tweets/user/{userId} ─────────────────────────────────────────
        [HttpGet("user/{userId:int}")]
        public async Task<IActionResult> GetUserTweets(int userId, int page = 1, int pageSize = 20)
        {
            // نتأكد إن الـ User موجود
            bool userExists = await _db.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
                return NotFound(" User is not exist ! .");

            var tweets = await _db.Tweets
                .Include(t => t.User)
                .Include(t => t.Likes)
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 
            int? currentUserId = null;

            if (User.Identity?.IsAuthenticated == true)
            {
                currentUserId = int.Parse(
                    User.FindFirstValue(ClaimTypes.NameIdentifier)!
                );
            }

            var response = tweets.Select(t => new TweetResponse
            {
                Id = t.Id,
                Content = t.Content,
                AuthorName = t.User.Username,
                LikesCount = t.Likes.Count,
                IsLikedByMe = currentUserId.HasValue && t.Likes.Any(l => l.UserId == currentUserId),
                CreatedAt = t.CreatedAt
            });

            return Ok(response);
        }

        // ── DELETE /api/tweets/{id} ───────────────────────────────────────────────
        [HttpDelete("{id:int}")]
        [Authorize]
        public async Task<IActionResult> DeleteTweet(int id)
        {
            var userId = int.Parse(
           User.FindFirstValue(ClaimTypes.NameIdentifier)!
         );

            var tweet = await _db.Tweets.FindAsync(id);
            if (tweet == null)
                return NotFound(" Tweet does not exist ! .");

            // 
            if (tweet.UserId != userId)
                return Forbid();

            _db.Tweets.Remove(tweet);
            await _db.SaveChangesAsync();

            return Ok(" This tweet has been deleted !.");
        }
    }

}
