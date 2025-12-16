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
            var reservation = db.Reservations
                            .Include(r => r.Room)
                            .Include(r => r.Member)
                            .FirstOrDefault(r => r.Id == bookingId);

            if (reservation == null) 
                return NotFound();

            var vm = new AddReviewVM
            {
                BookingId = reservation.Id,
            };

            return View(vm);
        }

        // POST : add review
        [HttpPost]
        [Authorize]
        public IActionResult AddReview(AddReviewVM vm)
        {

            var email = User.Identity.Name;
            var member = db.Members.FirstOrDefault(m => m.Email == email);

            var reservation = db.Reservations.FirstOrDefault(r => r.Id == vm.BookingId && r.MemberId == member.Id);
            if (reservation == null)

            {
                TempData["Error"] = "Reservation not found or does not belong to you.";
                return RedirectToAction("Index", "Home");
            }

            var review = new Review
            {
                MemberId = member.Id,
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
                .FirstOrDefault(r => r.Id == bookingId);

            if (reservation == null)
                return RedirectToAction("List", "Reservation");

            var reviews = db.Reviews
                .Where(rv => rv.ReservationId == bookingId)
                .ToList();

            return View(reviews);
        }

        // update review
        [Authorize]
        
        public IActionResult UpdateReview(int id)
        {
            //var memberId = int.Parse(User.FindFirst("MemberId").Value);
            var email = User.Identity.Name;
            var member = db.Members.FirstOrDefault(m => m.Email == email);

            if (member == null)
                return Unauthorized();

            var review = db.Reviews
                .FirstOrDefault(r => r.Id == id);

            if (review == null)
            {
                TempData["Info"] = "Review not found.";
                return RedirectToAction("History", "Checkout");
            }
            //var review = db.Reviews
            //    .Include(r => r.Member)
            //    .FirstOrDefault(r => r.Id == id);

            //var review = db.Reviews.FirstOrDefault(r => r.Id == id);

            //if (review == null)
            //{
            //    TempData["Info"] = "You haven't made the review yet.";
            //    return RedirectToAction("ListByBooking");
            //}

            //check member
            if (review.MemberId != member.Id)
                return Forbid();

            var vm = new UpdateReviewVM
            {
                Id = review.Id,
                BookingId = review.ReservationId ?? 0,
                Comment = review.Comment,
                ServiceRating = review.ServiceRating,
                CleanlinessRating = review.CleanlinessRating,
                Rating = review.Rating
            };

            return View(vm);
        }
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateReview(UpdateReviewVM vm)
        {

            if (!ModelState.IsValid)
                return View(vm);

            var email = User.Identity.Name;
            var member = db.Members.FirstOrDefault(m => m.Email == email);

            if (member == null)
                return Unauthorized();

            var review = db.Reviews.FirstOrDefault(r => r.Id == vm.Id);
            
            if (review == null)
            {
                TempData["Info"] = "You haven't made the review yet.";
                return RedirectToAction("History", "Checkout");
            }

            if (review.MemberId != member.Id)
                return Forbid();

            review.Comment = vm.Comment;
            review.ServiceRating = vm.ServiceRating;
            review.CleanlinessRating = vm.CleanlinessRating;
            review.Rating = vm.Rating;

            //review.UpdatedAt = DateTime.Now;
            db.SaveChanges();

            TempData["Info"] = "Review updated!";
            return RedirectToAction("History", "Checkout");
        }

        //show all review
        public IActionResult AllReview()
        {
            var reviews = db.Reviews
                .Include(r=>r.Member)
                .Select(r => new ReviewVM
                {
                    Id = r.Id,
                    MemberName = r.Member.Name,
                    Comment = r.Comment,
                    ServiceRating = r.ServiceRating,
                    CleanlinessRating = r.CleanlinessRating,
                    OverallRating = r.Rating
                })
                .ToList();

            var vm = new AllReviewVM { Reviews = reviews };

            return View(vm);
        }
        ////show all review
        //public IActionResult HotelReview(string hotelId)
        //{
        //    var reviews = db.Reviews
        //        .Where(rw => rw.HotelId == hotelId)
        //        .Select(r => new ReviewVM
        //        {
        //            Id = r.Id,
        //            HotelName = r.Hotel.Name,
        //            MemberEmail = r.MemberEmail,
        //            Comment = r.Comment,
        //            ServiceRating = r.ServiceRating,
        //            CleanlinessRating = r.CleanlinessRating,
        //            OverallRating = r.Rating
        //        })
        //        .ToList();

        //    var vm = new AllReviewVM { Reviews = reviews };

        //    return View(vm);
        //}
        
        //delete review
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

