using Microsoft.EntityFrameworkCore;
using TweetWebApp.Models;

namespace TweetWebApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Tables
        public DbSet<User> Users => Set<User>();
        public DbSet<Tweet> Tweets => Set<Tweet>();
        public DbSet<Like> Likes => Set<Like>();
        public DbSet<Follow> Follows => Set<Follow>();
        public DbSet<RefreshToken> RefreshTokens { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ── User ──────────────────────────────────────────────────────────────
            modelBuilder.Entity<User>(e =>
            {
                e.HasKey(u => u.Id);

                e.HasIndex(u => u.Email).IsUnique();
                e.HasIndex(u => u.Username).IsUnique();

                e.Property(u => u.Username).HasMaxLength(30).IsRequired();
                e.Property(u => u.Email).HasMaxLength(256).IsRequired();
                e.Property(u => u.Bio).HasMaxLength(160);
            });

            // ── Tweet ─────────────────────────────────────────────────────────────
            modelBuilder.Entity<Tweet>(e =>
            {
                e.HasKey(t => t.Id);
                e.Property(t => t.Content).HasMaxLength(280).IsRequired();

                e.HasOne(t => t.User)
                 .WithMany(u => u.Tweets)
                 .HasForeignKey(t => t.UserId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // ── Like ──────────────────────────────────────────────────────────────
            modelBuilder.Entity<Like>(e =>
            {
                
                e.HasKey(l => new { l.UserId, l.TweetId });

                e.HasOne(l => l.User)
                 .WithMany(u => u.Likes)
                 .HasForeignKey(l => l.UserId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(l => l.Tweet)
                 .WithMany(t => t.Likes)
                 .HasForeignKey(l => l.TweetId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // ── Follow ────────────────────────────────────────────────────────────
            modelBuilder.Entity<Follow>(e =>
            {
                
                e.HasKey(f => new { f.FollowerId, f.FollowingId });

                e.HasOne(f => f.Follower)
                 .WithMany(u => u.Following)
                 .HasForeignKey(f => f.FollowerId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(f => f.Following)
                 .WithMany(u => u.Followers)
                 .HasForeignKey(f => f.FollowingId)
                 .OnDelete(DeleteBehavior.Restrict);
            });
        }


    }
}
