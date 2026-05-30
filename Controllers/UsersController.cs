using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TweetWebApp.Data;
using TweetWebApp.DTOs;

namespace TweetWebApp.Controllers; 

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db)
    {
        _db = db;
    }

    // ── GET /api/users/{id} ───────────────────────────────────────────────────
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetProfile(int id)
    {
        
        var user = await _db.Users
            .Include(u => u.Tweets)
            .Include(u => u.Followers)
            .Include(u => u.Following)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
            return NotFound(" The user is not found !");

        return Ok(new UserProfileResponse
        {
            Id = user.Id,
            Username = user.Username,
            Bio = user.Bio,
            TweetsCount = user.Tweets.Count,
            FollowersCount = user.Followers.Count,
            FollowingCount = user.Following.Count,
            CreatedAt = user.CreatedAt
        });
    }

    // ── GET /api/users/{username} ─────────────────────────────────────────────
    [HttpGet("{username}")]
    public async Task<IActionResult> GetProfileByUsername(string username)
    {
        var user = await _db.Users
            .Include(u => u.Tweets)
            .Include(u => u.Followers)
            .Include(u => u.Following)
            .FirstOrDefaultAsync(u => u.Username == username);

        if (user == null)
            return NotFound(" The user is not exist !");

        return Ok(new UserProfileResponse
        {
            Id = user.Id,
            Username = user.Username,
            Bio = user.Bio,
            TweetsCount = user.Tweets.Count,
            FollowersCount = user.Followers.Count,
            FollowingCount = user.Following.Count,
            CreatedAt = user.CreatedAt
        });
    }
}
