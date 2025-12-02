using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Data;
using X.PagedList.Extensions;

namespace Assignment.Controllers;

public class ReservationController : Controller
{
    private readonly DB db;
    private readonly Helper hp;

    public ReservationController(DB db, Helper hp)
    {
        this.db = db;
        this.hp = hp;
    }

    // GET: Home/Index
    public IActionResult Index(string? member, DateTime? startDate, DateTime? endDate,string? sort, string? dir, int page = 1)
    {

        var reservations = db.Reservations
            .Include(r => r.Member)
            .Include(r => r.Room)
                .ThenInclude(rm => rm.RoomTypes)
            .Include(r => r.Payment)
            .AsQueryable();

        // Sorting
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;
        ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
        ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

        Func<Reservation, object> fn = sort switch
        {
            "Id" => r => r.Id,
            "Member" => r => r.Member,
            "Room" => r => r.Room,
            "Check In" => r => r.CheckIn.ToString("yyyy-MM-dd"),
            "Check Out" => r => r.CheckOut.ToString("yyyy-MM-dd"),
            "Total" => r => r.Payment.Amount,
            "Payment Status" => r => r.Payment.Status,
            "Order Status" => r => r.Active ? "Active" : "Inactive",
            _ => r => r.Id
        };

        if (!string.IsNullOrEmpty(member))
        {
            member = member?.Trim() ?? "";
            reservations = reservations.Where(r => r.Member.Name.Contains(member));
        }

        if (startDate.HasValue)
        {
            var start = DateOnly.FromDateTime(startDate.Value);
            reservations = reservations.Where(r => r.CheckIn >= start);
        }

        if (endDate.HasValue)
        {
            var end = DateOnly.FromDateTime(endDate.Value);
            reservations = reservations.Where(r => r.CheckOut <= end);

        }

        var result = reservations.ToList();

        var sorted = dir == "des" ?
             reservations.OrderByDescending(fn) :
             reservations.OrderBy(fn);

        var m = sorted.ToPagedList(page, 10);

        // Paging
        if (page < 1)
        {
            return RedirectToAction(null, new { member, sort, dir, page = 1 });
        }


        if (page > m.PageCount && m.PageCount > 0)
        {
            return RedirectToAction(null, new { member, sort, dir, page = m.PageCount });
        }


        if (Request.IsAjax())
        {
            return PartialView("ReservationRecord", m);

        }
         return View(m);
    }



    // ------------------------------------------------------------------------
    // Reservation
    // ------------------------------------------------------------------------

    // GET: Home/Reserve
    public IActionResult Reserve(string id)
    {
        var r = db.RoomTypes.First(t => t.Id == id);
        var vm = new ReserveVM
        {
             TypeName = r.Name,
             TypeId = r.Id,
             CheckIn = DateTime.Today.ToDateOnly(),
             CheckOut = DateTime.Today.ToDateOnly().AddDays(1),
        };
        return View(vm);

    }

    // POST: Home/Reserve
    [HttpPost]
    public IActionResult Reserve(ReserveVM vm)
    {
        var roomType = db.RoomTypes.Find(vm.TypeId);
        vm.TypeName = roomType.Name;

        if(roomType == null)
        {
            ModelState.AddModelError("", "Invalid Room Type");
            return View(vm);
        }

        // Validation (1): CheckIn within 30 days range
        if (ModelState.IsValid("CheckIn"))
        {
            var a = DateTime.Today.ToDateOnly();
            var b = DateTime.Today.ToDateOnly().AddDays(30);

            if (vm.CheckIn < a || vm.CheckIn > b)
            {
                ModelState.AddModelError("CheckIn", "Date out of range.");
            }
        }

        // Validation (2): CheckOut within 10 days range (after CheckIn)
        if (ModelState.IsValid("CheckIn") && ModelState.IsValid("CheckOut"))
        {
            var a = vm.CheckIn.AddDays(1);
            var b = vm.CheckIn.AddDays(10);



            if (vm.CheckOut < a || vm.CheckOut > b)
            {
                ModelState.AddModelError("CheckOut", "Date out of range.");
            }
        }

        if (ModelState.IsValid)
        {
            // 1. Get occupied rooms 
            var occupied = db.Reservations
                             .Where(rs => vm.CheckIn < rs.CheckOut &&
                                          rs.CheckIn < vm.CheckOut
                                          && rs.Active == true)
                             .Select(rs => rs.Room.Id);

            // 2. Get first available room (filtered by room type)
            Room? room = db.Rooms
                           .Include(rm => rm.RoomTypes)
                           .Where(rm => rm.RoomTypeId == vm.TypeId && !occupied.Contains(rm.Id))
                           .FirstOrDefault();
            var user = db.Users.Where(u => u.Email == User.Identity!.Name!).FirstOrDefault();
            // 3. Is room available?
            if (room == null)
            {
                ModelState.AddModelError("CheckOut", "No room availble.");
            }
            else
            {
                // 4. Insert Reservation record
                var rs = new Reservation
                {
                    MemberId = user.Id,
                    RoomId = room.Id,
                    CheckIn = vm.CheckIn,
                    CheckOut = vm.CheckOut,
                    Price = roomType.Price,
                    Active = true,
                };
                db.Reservations.Add(rs);
                db.SaveChanges();

                var py = new Payment
                {
                    Amount = room.RoomTypes.Price * (vm.CheckOut.DayNumber - vm.CheckIn.DayNumber),
                    ReservationId = rs.Id,
                };
                db.Payment.Add(py);
                db.SaveChanges();

                db.Reservations.Where(r => r.Id == rs.Id)
                          .ExecuteUpdate(s => s
                          .SetProperty(r => r.Payment.Id, py.Id));

                db.SaveChanges();

                TempData["Info"] = "Room reserved.";
                return RedirectToAction("Detail", new { rs.Id });
            }
        }

        ViewBag.TypeList = new SelectList(db.RoomTypes.OrderBy(t => t.Price), "Id", "Name");
        return View(vm);
    }

    // GET: Home/Detail
    public IActionResult Detail(int id)
    {
        var m = db.Reservations
                  .Include(rs => rs.Member)
                  .Include(rs => rs.Payment)
                  .Include(rs => rs.Room)
                  .ThenInclude(rm => rm.RoomTypes)
                  .FirstOrDefault(rs => rs.Id == id);

        if (m == null)
        {
            return RedirectToAction("List");
        }

        return View(m);
    }

    // GET: Home/Status
    public IActionResult Status(DateOnly? month)
    {
        var m = month.GetValueOrDefault(DateTime.Today.ToDateOnly());

        // Min = First day of the month
        // Max = First day of next month

        var min = new DateOnly(m.Year, m.Month, 1);
        var max = min.AddMonths(1);

        ViewBag.Min = min;
        ViewBag.Max = max;

        // 1. Initialize dictionary
        // ------------------------
        // Dictionary<Room, List<DateOnly>>
        // Key   = Room object
        // Value = List of DateOnly objects
        //
        // dict[R001] = [2022-12-01, 2022-12-02, ...]
        // dict[R002] = [2022-12-03, 2022-12-04, ...]

        var dict = db.Rooms
                     .OrderBy(rm => rm.Id)
                     .ToDictionary(rm => rm, rm => new List<DateOnly>());

        // 2. Retrieve reservation records
        // -------------------------------
        // Example: 2024-12-01 (min) ... 2025-01-01 (max)

        var reservation = db.Reservations
                            .Where(rs => min < rs.CheckOut &&
                                         rs.CheckIn < max);

        // 3. Fill the dictionary
        // ----------------------
        // Example: CheckIn = 2024-12-10, CheckOut = 2024-12-15
        // Entries --> 10, 11, 12, 13, 14 *** 15 not included ***

        foreach (var rs in reservation)
        {
            for (var d = rs.CheckIn; d < rs.CheckOut; d = d.AddDays(1))
            {
                dict[rs.Room].Add(d);
            }
        }

        return View(dict);
    }

    public IActionResult RoomStatus(string id,DateOnly? month)
    {
        var m = month.GetValueOrDefault(DateTime.Today.ToDateOnly());

        // Min = First day of the month
        // Max = First day of next month

        var min = new DateOnly(m.Year, m.Month, 1);
        var max = min.AddMonths(1);

        ViewBag.Min = min;
        ViewBag.Max = max;

        // 1. Initialize dictionary
        // ------------------------
        // Dictionary<Room, List<DateOnly>>
        // Key   = Room object
        // Value = List of DateOnly objects
        //
        // dict[R001] = [2022-12-01, 2022-12-02, ...]
        // dict[R002] = [2022-12-03, 2022-12-04, ...]

        var dict = db.Rooms
                     .Where(rm => rm.RoomTypeId == id)
                     .ToDictionary(rm => rm, rm => new List<DateOnly>());

        // 2. Retrieve reservation records
        // -------------------------------
        // Example: 2024-12-01 (min) ... 2025-01-01 (max)

        var reservation = db.Reservations
                            .Include(rs => rs.Room)
                                .ThenInclude(rs => rs.RoomTypeId)
                            .Where(rs => rs.Room.RoomTypeId == id && min < rs.CheckOut &&
                                         rs.CheckIn < max && rs.Active == true);

        // 3. Fill the dictionary
        // ----------------------
        // Example: CheckIn = 2024-12-10, CheckOut = 2024-12-15
        // Entries --> 10, 11, 12, 13, 14 *** 15 not included ***

        foreach (var rs in reservation)
        {
            for (var d = rs.CheckIn; d < rs.CheckOut; d = d.AddDays(1))
            {
                dict[rs.Room].Add(d);
            }
        }

        return View(dict);
    }
    //delete in orderhistory
    [Authorize(Roles = "Admin")]
    public IActionResult Active(int id)
    {
        var rs = db.Reservations.Find(id);
        if (rs == null) return NotFound();

        rs.Active = !rs.Active;
        db.SaveChanges();

        TempData["Info"] = "Update Status Sucessful";
        return RedirectToAction("Index");
    }
    //update in history
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public IActionResult Update(int id)
    {
        var rs = db.Reservations
                   .Include(r => r.Member)
                   .Include(r => r.Payment)
                   .Include(r => r.Room)
                   .ThenInclude(rm => rm.RoomTypes)
                   .FirstOrDefault(r => r.Id == id);

        if (rs == null) return NotFound();
        return View("HistoryUpdate", rs);
    }
    // POST: Reservation/Update
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public IActionResult Update(Reservation model)
    {
        var rs = db.Reservations.Find(model.Id);
        if (rs == null) return NotFound();

        db.SaveChanges();

        TempData["Info"] = "Update Sucessful";
        return RedirectToAction("Index");
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Report()
    {
        var salesByRoomType = db.Reservations
         .Include(r => r.Room.RoomTypes)
         .Include(r => r.Payment)
         .Where(r => r.Payment != null && r.Payment.Status == "Paid")
         .GroupBy(r => r.Room.RoomTypes.Name)
         .Select(g => new ReportVM
         {
             RoomType = g.Key,
             TotalSales = g.Sum(r => r.Payment.Amount)
         })
         .OrderBy(s => s.RoomType)
         .ToList();
        return View(salesByRoomType);
    }

    //filration
    /*public IActionResult Filter(string paid)
    {
        var reservations = db.Reservations
                        .Include(r => r.Member)
                        .Include(r => r.Room)
                        .ThenInclude(rm => rm.Type)
                        .AsQueryable();
         if (paid == "true")
        reservations = reservations.Where(r => r.Paid == true);
    else if (paid == "false")
        reservations = reservations.Where(r => r.Paid == false);

    return View("List", reservations.ToList()); // 直接用 List.cshtml 显示结果
    }*/
    //filter by date
    //public IActionResult Filter(DateTime? startDate, DateTime? endDate)
    //{
    //    var reservations = db.Reservations
    //        .Include(r => r.Member)
    //        .Include(r => r.Room)
    //        .ThenInclude(rm => rm.Type)
    //        .AsQueryable();

    //    if (startDate.HasValue)
    //    {
    //        var start = DateOnly.FromDateTime(startDate.Value);
    //        reservations = reservations.Where(r => r.CheckIn >= start);
    //    }

    //    if (endDate.HasValue)
    //    {
    //        var end = DateOnly.FromDateTime(endDate.Value);
    //        reservations = reservations.Where(r => r.CheckOut <= end);

    //    }
    //    return View("Index", reservations.ToList());
    //}

    ////search function
    //[Authorize(Roles = "Admin")]
    //public IActionResult Search(string? keyword)
    //{
    //    ViewBag.Name = keyword = keyword?.Trim() ?? "";

    //    var results = db.Reservations
    //        .Include(r => r.Member)
    //        .Include(r => r.Room)
    //        .ThenInclude(rm => rm.Type)
    //        .AsQueryable();

    //    if (!string.IsNullOrEmpty(keyword))
    //    {
    //        results = results.Where(r =>
    //               r.Member.Email.Contains(keyword) ||
    //               r.Member.Name.Contains(keyword) ||
    //               r.RoomId.Contains(keyword) ||
    //               r.Id.ToString().Contains(keyword));
    //    }

    //    if (Request.IsAjax())
    //    {
    //        return PartialView("ReservationRecord", results.ToList());
    //    }

    //    return View("Index", results.ToList());
    //}

}