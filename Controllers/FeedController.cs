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
    [Route("api/feed")]
    [Authorize]
    public class FeedController : ControllerBase
    {
        private readonly AppDbContext _db;

        public FeedController(AppDbContext db)
        {
            _db = db;
        }

        // ── GET /api/feed?page=1&pageSize=20 ─────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetFeed(int page = 1, int pageSize = 20)
        {
            var myId = int.Parse(
          User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // 
           var followingIds = await _db.Follows
                .Where(f => f.FollowerId == myId)
                .Select(f => f.FollowingId)
                .ToListAsync();


            followingIds.Add(myId);


            var totalCount = await _db.Tweets
                .Where(t => followingIds.Contains(t.UserId))
                .CountAsync();

            var tweets = await _db.Tweets
                .Include(t => t.User)
                .Include(t => t.Likes)
                .Where(t => followingIds.Contains(t.UserId))
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var response = tweets.Select(t => new TweetResponse
            {
                Id = t.Id,
                Content = t.Content,
                AuthorName = t.User.Username,
                LikesCount = t.Likes.Count,
                IsLikedByMe = t.Likes.Any(l => l.UserId == myId),
                CreatedAt = t.CreatedAt
            });

            return Ok(new
            {
                Items = response,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }
    }

}
