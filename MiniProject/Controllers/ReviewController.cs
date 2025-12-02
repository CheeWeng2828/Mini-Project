using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
 

namespace Assignment.Controllers
{
    public class ReviewController : Controller
    {
        private readonly DB db;

        public ReviewController(DB db)
        {
            this.db = db;
        }

        // GET: Add review from a booking
        [Authorize]
        public IActionResult AddReview(int bookingId)
        {
            var booking = db.Reservations
                            .Include(r => r.Room)
                                .ThenInclude(rt => rt.Type)
                                    .ThenInclude(t => t.Hotel)
                            .FirstOrDefault(r => r.Id == bookingId);

            if (booking == null) return NotFound();

            var vm = new AddReviewVM
            {
                BookingId = booking.Id,
                HotelId = booking.Room.Type.Hotel.Id,
                HotelName = booking.Room.Type.Hotel.Name
            };

            return View(vm);
        }

        // POST 
        [HttpPost]
        [Authorize]
        public IActionResult AddReview(AddReviewVM vm)
        {

            var review = new Review
            {
                MemberEmail = User?.Identity?.Name ?? "",
                HotelId = vm.HotelId,
                Comment = vm.Comment,
                Rating = vm.Rating,
                CleanlinessRating = vm.CleanlinessRating,
                ServiceRating = vm.ServiceRating,
                CreatedAt = DateTime.Now,
                ReservationId = vm.BookingId,
            };

            db.Reviews.Add(review);  
            db.SaveChanges();      
            
            TempData["Info"] = "Review submitted!";
            return RedirectToAction("ListByBooking", new { bookingId = vm.BookingId });
        }

        public IActionResult ListByBooking(int bookingId)
        {
            var reservation = db.Reservations
                .Include(r => r.Room)
                    .ThenInclude(rm => rm.Type)
                        .ThenInclude(t => t.Hotel)
                .FirstOrDefault(r => r.Id == bookingId);

            if (reservation == null)
                return RedirectToAction("List", "Reservation");

            var reviews = db.Reviews
                .Where(rv => rv.ReservationId == reservation.Id)
                .ToList();

            return View(reviews);
        }

        // update review
        [Authorize]
        public IActionResult UpdateReview(int id)
        {
            var review = db.Reviews
                .Include(r => r.Hotel)
                .FirstOrDefault(r => r.Id == id);


            if (review == null)
            {
                TempData["Info"] = "You haven't made the review yet.";
                return RedirectToAction("ListByBooking"); 
            }

            //check email
            if (review.MemberEmail != User.Identity.Name)
                return Forbid();

            var vm = new UpdateReviewVM
            {
                Id = review.Id,
                HotelId = review.HotelId,
                HotelName = review.Hotel.Name,
                Comment = review.Comment,
                ServiceRating = review.ServiceRating,
                CleanlinessRating = review.CleanlinessRating,
                Rating = review.Rating,
                BookingId = review.ReservationId ?? 0,
            };

            return View(vm);
        }
        [HttpPost]
        [Authorize]
        public IActionResult UpdateReview(UpdateReviewVM vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var review = db.Reviews.FirstOrDefault(r => r.Id == vm.Id);
            if (review == null)
            {
                TempData["Info"] = "You haven't made the review yet.";
                return RedirectToAction("History","Checkout");
            }

            review.Comment = vm.Comment;
            review.ServiceRating = vm.ServiceRating;
            review.CleanlinessRating = vm.CleanlinessRating;
            review.Rating = vm.Rating;

            //review.UpdatedAt = DateTime.Now;
            db.SaveChanges();

            TempData["Info"] = "Review updated!";
            return RedirectToAction("History", "Checkout");
        }

        public IActionResult AllReview()
        {
            var reviews = db.Reviews
                .Select(r => new ReviewVM
                {
                    Id = r.Id,
                    HotelName = r.Hotel.Name,
                    MemberEmail = r.MemberEmail,
                    Comment = r.Comment,
                    ServiceRating = r.ServiceRating,
                    CleanlinessRating = r.CleanlinessRating,
                    OverallRating = r.Rating
                })
                .ToList();

            var vm = new AllReviewVM { Reviews = reviews };

            return View(vm);
        }
        //show all review
        public IActionResult HotelReview(string hotelId)
        {
            var reviews = db.Reviews
                .Where(rw => rw.HotelId == hotelId)
                .Select(r => new ReviewVM
                {
                    Id = r.Id,
                    HotelName = r.Hotel.Name,
                    MemberEmail = r.MemberEmail,
                    Comment = r.Comment,
                    ServiceRating = r.ServiceRating,
                    CleanlinessRating = r.CleanlinessRating,
                    OverallRating = r.Rating  
                })
                .ToList();

            var vm = new AllReviewVM { Reviews = reviews };

            return View(vm);
        }

        public IActionResult DeleteReview(int id)
        {
            var review = db.Reviews
                .FirstOrDefault(r => r.Id == id);


            if (review == null)
            {
                TempData["Info"] = "You haven't made the review yet.";
                return RedirectToAction("ListByBooking");
            }

            db.Reviews.Remove(review);
            db.SaveChanges();

            TempData["Info"] = "Delete Successfully";
            return RedirectToAction("History", "Checkout");
        }

    }
}

