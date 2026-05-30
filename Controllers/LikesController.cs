using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TweetWebApp.Data;
using TweetWebApp.Models;

namespace TweetWebApp.Controllers
{
    [ApiController]
    [Route("api/tweets/{tweetId:int}")]
    [Authorize]
    public class LikesController : ControllerBase
    {
        private readonly AppDbContext _db;

        public LikesController(AppDbContext db)
        {
            _db = db;
        }

        // ── POST /api/tweets/{tweetId}/like ──────────────────────────────────────
        [HttpPost("like")]
        public async Task<IActionResult> Like(int tweetId)
        {
            var userId = int.Parse(
           User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // نتأكد إن التغريدة موجودة
            bool tweetExists = await _db.Tweets.AnyAsync(t => t.Id == tweetId);
            if (!tweetExists)
                return NotFound(" Tweet does not exist ! .");

            // 
            bool alreadyLiked = await _db.Likes.AnyAsync(l =>
                l.UserId == userId && l.TweetId == tweetId);

            if (alreadyLiked)
                return BadRequest("You liked this before!.");

            var like = new Like
            {
                UserId = userId,
                TweetId = tweetId
            };

            _db.Likes.Add(like);
            await _db.SaveChangesAsync();

            return Ok(" Done.");
        }

        // ── DELETE /api/tweets/{tweetId}/like ────────────────────────────────────
        [HttpDelete("like")]
        public async Task<IActionResult> Unlike(int tweetId)
        {
            var userId = int.Parse(
           User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var like = await _db.Likes.FirstOrDefaultAsync(l =>
                l.UserId == userId && l.TweetId == tweetId);

            if (like == null)
                return BadRequest("You have not liked this tweet.");

            _db.Likes.Remove(like);
            await _db.SaveChangesAsync();

            return Ok("Like removed .");
        }
    }

}
