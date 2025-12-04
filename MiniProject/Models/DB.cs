using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MiniProject.Models;

#nullable disable warnings

public class DB : DbContext
{
    public DB(DbContextOptions options) : base(options){ }
    public DbSet<User> Users { get; set; }
    public DbSet<Admin> Admins { get; set; }
    public DbSet<Member> Members { get; set; }

    public DbSet<RoomType> RoomTypes { get; set; }
    public DbSet<Room> Rooms { get; set; }
    //public DbSet<Hotel> Hotels { get; set; }
    public DbSet<RoomGallery> RoomGalleries { get; set; }
    public DbSet<Reservation> Reservations { get; set; }

    public DbSet<Payment> Payment { get; set; }

    public DbSet<UserToken> UserTokens { get; set; }

    public DbSet<Review> Reviews { get; set; }
}

    public class User
    {

        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [MaxLength(100)]
        public string Email { get; set; }
        [MaxLength(100)]
        public string Hash { get; set; }
        [MaxLength(100)]
        public string Name { get; set; }
        public bool Active { get; set; }
        public int LoginAttemptCount { get; set; }  // For login block
        public DateTime? LastFailedLoginTime { get; set; }

        public string Role => GetType().Name;
    }

    public class Admin : User
    {

    }

    public class Member : User
    {
        [MaxLength(100)]
        public string PhotoURL { get; set; }

        public List<Reservation> Reservations { get; set; } = [];
        public List<Review> Reviews { get; set; } = [];

    }


    public class RoomType
    {
        [Key, MaxLength(3)]
        public string Id { get; set; }

        [MaxLength(100)]
        public string Name { get; set; }

        [Precision(6, 2)]
        public decimal Price { get; set; }

        // Navigation Properties
        public List<Room> Rooms { get; set; } = [];
        public List<RoomGallery> RoomGalleries { get; set; } = [];
        //public Hotel Hotel { get; set; }
    }

    public class Room
    {
        [Key, MaxLength(4)]
        public string Id { get; set; }

        public bool Active { get; set; }

        // Foreign Keys
        public string RoomTypeId { get; set; }

        // Navigation Properties
        public RoomType RoomTypes { get; set; }
        public List<Reservation> Reservations { get; set; } = [];
    }

    public class RoomGallery
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [MaxLength(100)]
        public string PhotoURL { get; set; }

        //Foreign Key
        public string RoomTypeId { get; set; }

        // Navigation Properties
        public RoomType RoomTypes { get; set; }
    }
    public class Reservation
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public DateOnly CheckIn { get; set; }

        public DateOnly CheckOut { get; set; }

        [Precision(6, 2)]
        public decimal Price { get; set; }

        public bool Active { get; set; } = true;


        // Foreign Keys (MemberEmail, RoomId)
        public int MemberId { get; set; }

        public string RoomId { get; set; }

        // Navigation Properties
        public Member Member { get; set; }
        public Room Room { get; set; }
        public Payment Payment { get; set; }
        public Review Review { get; set; }
    }

    public class Payment
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [Precision(6, 2)]
        public decimal Amount { get; set; }
        public string? TransactionId { get; set; }
        public string? PaymentMethod { get; set; }

        public string Status { get; set; }
        public DateTime? RefundDate { get; set; }
        public string? RefundId { get; set; }

        // Foreign Keys
        public int ReservationId { get; set; }

        // Navigation Properties
        public Reservation Reservations { get; set; }

    }


    public class UserToken
    {
        [Key]
        public string Id { get; set; }
        public TimeOnly GenerateTime { get; set; }


        // Foreign Keys
        public int MemberId { get; set; }


        // Navigation Properties
        public Member Member { get; set; }
    }
    public class Review
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int MemberId { get; set; }

        public int? ReservationId { get; set; }

        [Required]
        [StringLength(500)]
        public string Comment { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        [Range(1, 5)]
        public int ServiceRating { get; set; }

        [Range(1, 5)]
        public int CleanlinessRating { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        public Member Member { get; set; }
        public Reservation Reservation { get; set; }
    }
