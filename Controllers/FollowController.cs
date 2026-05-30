using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TweetWebApp.Data;
using TweetWebApp.Models;

namespace TweetWebApp.Controllers
{

    [ApiController]
    [Route("api/users/{targetUserId:int}")]
    [Authorize]
    public class FollowController : ControllerBase
    {
        private readonly AppDbContext _db;

        public FollowController(AppDbContext db)
        {
            _db = db;
        }

        // ── POST /api/users/{targetUserId}/follow ─────────────────────────────────
        [HttpPost("follow")]
        public async Task<IActionResult> Follow(int targetUserId)
        {
            var myId = int.Parse(
          User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // 
            if (myId == targetUserId)
                return BadRequest("This action is not allowed .");

            // 
            bool targetExists = await _db.Users.AnyAsync(u => u.Id == targetUserId);
            if (!targetExists)
                return NotFound(" The user is not exist ! .");

            // 
            bool alreadyFollowing = await _db.Follows.AnyAsync(f =>
                f.FollowerId == myId && f.FollowingId == targetUserId);

            if (alreadyFollowing)
                return BadRequest("You already follow this person !");

            var follow = new Follow
            {
                FollowerId = myId,
                FollowingId = targetUserId
            };

            _db.Follows.Add(follow);
            await _db.SaveChangesAsync();

            return Ok("User followed successfully.");
        }

        // ── DELETE /api/users/{targetUserId}/follow ───────────────────────────────
        [HttpDelete("follow")]
        public async Task<IActionResult> Unfollow(int targetUserId)
        {
            var myId = int.Parse(
          User.FindFirstValue(ClaimTypes.NameIdentifier)!);

           
            var follow = await _db.Follows.FirstOrDefaultAsync(f =>
                f.FollowerId == myId && f.FollowingId == targetUserId);

            if (follow == null)
                return BadRequest("You are not following this user.");

            _db.Follows.Remove(follow);
            await _db.SaveChangesAsync();

            return Ok("Unfollowed successfully.");
        }
    }
}
