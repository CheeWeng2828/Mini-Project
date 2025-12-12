using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace MiniProject.Models;

// View Models ----------------------------------------------------------------

#nullable disable warnings

public class LoginVM
{
    [StringLength(100)]
    [EmailAddress]
    public string Email { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    public bool RememberMe { get; set; }
}

public class RegisterVM
{
    [StringLength(100)]
    [EmailAddress]
    [Remote("CheckEmail", "Account", ErrorMessage = "Duplicated {0}.")]
    public string Email { get; set; }
    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [Compare("Password")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    public string Confirm { get; set; }

    [StringLength(100)]
    public string Name { get; set; }
}
public class UpdatePasswordVM
{
    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    [Display(Name = "Current Password")]
    public string Current { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    [Display(Name = "New Password")]
    public string New { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [Compare("New")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    public string Confirm { get; set; }
}

public class UpdateProfileVM
{
    public string? Email { get; set; }

    [StringLength(100)]
    public string Name { get; set; }

    public string? PhotoURL { get; set; }

    public IFormFile? Photo { get; set; }
}

public class ResetPasswordVM
{
    [StringLength(100)]
    [EmailAddress]
    public string Email { get; set; }
}
public class UpdateRoomVm
{
    public string? Id { get; set; }

    [Display(Name = "Type")]
    [StringLength(4)]
    [Remote("CheckTypeId", "Room", ErrorMessage = "Invalid {0}.")]
    public string TypeId { get; set; }

}

public class CheckOutVM
{
    public string Id { get; set; }
    public string? PaypalCaptureId { get; set; }
}
public class Refund
{
    public int PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; }
}

// Reservation

public class ReserveVM
{
    public string? TypeName { get; set; }
    public string TypeId { get; set; }

    [Display(Name = "Check In Date")]
    [DataType(DataType.Date)]
    public DateOnly CheckIn { get; set; }

    [Display(Name = "Check Out Date")]
    [DataType(DataType.Date)]
    public DateOnly CheckOut { get; set; }
}

public class AddAdminVM
{
    [StringLength(100)]
    [EmailAddress]
    [Remote("CheckEmail", "Account", ErrorMessage = "Duplicated {0}.")]
    public string Email { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [Compare("Password")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    public string Confirm { get; set; }

    [StringLength(100)]
    public string Name { get; set; }

}

public class AddTypeVM
{
    [StringLength(3)]
    [RegularExpression(@"^[A-Z]{3}", ErrorMessage = "Only Three Upper Case Alphabet")]

    [Remote("CheckId", "RoomType", ErrorMessage = "Duplicated {0}.")]
    public string Id { get; set; }

    [StringLength(100)]
    public string Name { get; set; }

    [Range(1, 500)]
    public decimal Price { get; set; }

    public List<IFormFile> Photo { get; set; }
}

public class UpdateRoomTypeVm
{
    public string? Id { get; set; }

    [StringLength(100)]
    public string Name { get; set; }

    [Range(1, 500)]
    public decimal Price { get; set; }
    public List<string>? PhotoURL { get; set; } = new List<string>();
    [Display(Name = "New Photo")]
    public List<IFormFile>? Photo { get; set; }

}

public class NewPasswordVM
{
    public string Email { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [DataType(DataType.Password)]
    [Display(Name = "New Password")]
    public string New { get; set; }

    [StringLength(100, MinimumLength = 5)]
    [Compare("New")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    public string Confirm { get; set; }
}

//Review
public class AddReviewVM
{
    [Required]
    public int BookingId { get; set; }
    public string HotelId { get; set; }
    public string HotelName { get; set; }
    [Required]
    [StringLength(500)]
    public string Comment { get; set; }

    [Required]
    [Range(1, 5)]
    public int ServiceRating { get; set; }

    [Required]
    [Range(1, 5)]
    public int CleanlinessRating { get; set; }
    [Required]
    [Range(1, 5)]
    public int Rating { get; set; }

}
//view review(own)
public class ListByBookingVM
{
    public int BookingId { get; set; }
    public string HotelId { get; set; }
    public string HotelName { get; set; }

    public List<Review> Reviews { get; set; }
}

//show all review at mainpage 
public class ReviewVM
{
    public int Id { get; set; }
    public string HotelName { get; set; }
    public string MemberEmail { get; set; }
    public string Comment { get; set; }
    public int ServiceRating { get; set; }
    public int CleanlinessRating { get; set; }
    public int OverallRating { get; set; }
}

public class AllReviewVM
{
    public List<ReviewVM> Reviews { get; set; }
}
//update
public class UpdateReviewVM
{
    [Required]
    public int Id { get; set; }
    public string HotelId { get; set; }
    public string HotelName { get; set; }
    public int BookingId { get; set; }

    [Required]
    [StringLength(500)]
    public string Comment { get; set; }

    [Required]
    [Range(1, 5)]
    public int ServiceRating { get; set; }

    [Required]
    [Range(1, 5)]
    public int CleanlinessRating { get; set; }

    [Required]
    [Range(1, 5)]
    public int Rating { get; set; }
    // not yet add photo function
    //public string PhotoURL { get; set; } 

    //public IFormFile Photo { get; set; }  
}

public class BatchAddVM
{
    public string TypeId { get; set; }
    public int Count { get; set; } = 1;
}

public class ReportVM
{
    public string RoomType { get; set; }
    public decimal TotalSales { get; set; }
}

public class ReportPageVM
{
    public IEnumerable<RoomType> RoomTypes { get; set; }
    public IEnumerable<ReportVM> SalesByRoomType { get; set; }
}
