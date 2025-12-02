using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using X.PagedList.Extensions;
using Microsoft.AspNetCore.Authorization;


namespace MiniProject.Controllers;

public class RoomController : Controller
{
    private readonly DB db;
    private readonly Helper hp;

    public RoomController(DB db, Helper hp)
    {
        this.db = db;
        this.hp = hp;
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Index(string typeId, string? name, string? sort, string? dir, int page = 1)
    {
        // Searching
        ViewBag.TypeId = typeId;
        ViewBag.Name = name = name?.Trim() ?? "";
        var searched = db.Rooms
                          .Include(r => r.RoomTypes)
                          .Where(r => r.RoomTypes.Name.Contains(name) && r.RoomTypeId == typeId);
        ViewBag.RoomTypes = new SelectList(db.RoomTypes, "Id", "Name");
        // Sorting
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Func<Room, object> fn = sort switch
        {
            "Id" => r => r.Id,
            "TypeId" => r => r.RoomTypeId,
            "Name" => r => r.RoomTypes.Name,
            "Price" => r => r.RoomTypes.Price,
            "Active" => r => r.Active,
            _ => r => r.Id
        };

        var sorted = dir == "des" ?
                     searched.OrderByDescending(fn) :
                     searched.OrderBy(fn);

        // Paging
        if (page < 1)
        {
            return RedirectToAction(null, new { name, sort, dir, page = 1 });
        }

        var m = sorted.ToPagedList(page, 10);

        if (page > m.PageCount && m.PageCount > 0)
        {
            return RedirectToAction(null, new { name, sort, dir, page = m.PageCount });
        }

        if (Request.IsAjax())
        {
            return PartialView("RoomRecord", m);
        }

        return View(m);
    }
    [Authorize(Roles = "Admin")]
    public IActionResult Detail(string? id)
    {
        var r = db.Rooms
                  .Include(p => p.RoomTypes)
                    .ThenInclude(t => t.RoomGalleries)
                  .FirstOrDefault(p => p.Id == id);

        if (r == null)
        {
            RedirectToAction("RoomType/Index");
        }
        return View(r);
    }

    //[HttpPost]
    //[Authorize(Roles = "Admin")]
    //public IActionResult AddRoom(string typeId)
    //{
    //    var id = NextId();

    //    if (typeId.Length > 1)
    //    {
    //        typeId = typeId.Substring(0, 1);
    //    }

    //    db.Rooms.Add(new()
    //    {
    //        Id = id,
    //        TypeId = typeId,
    //        Active = true,
    //    });

    //    db.SaveChanges();

    //    TempData["Info"] = "Room Add Successfully.";

    //    return Redirect(Request.Headers.Referer.ToString());
    //}

    [Authorize(Roles = "Admin")]
    public IActionResult UpdateRoom(string? id)
    {
        var r = db.Rooms.Find(id);


        if (r == null)
        {
            return RedirectToAction("Index");
        }

        var vm = new UpdateRoomVm
        {
            Id = r.Id,
            TypeId = r.RoomTypeId,
        };
        ViewBag.CategoryList = new SelectList(db.RoomTypes, "Id", "Name");
        return View(vm);

    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public IActionResult UpdateRoom(UpdateRoomVm vm)
    {
        var r = db.Rooms.Find(vm.Id);

        if (r == null)
        {
            return RedirectToAction("Index");
        }

        if (ModelState.IsValid)
        {
            r.RoomTypeId = vm.TypeId;

            db.SaveChanges();

            TempData["Info"] = "Record Updated.";
            return RedirectToAction("Index", new { typeId = r.RoomTypeId });
        }

        ViewBag.CategoryList = new SelectList(db.RoomTypes, "Id", "Name");
        return View(vm);
    }


    [Authorize(Roles = "Admin")]
    public IActionResult InactiveRoom(string? id)
    {
        if (string.IsNullOrEmpty(id)) return BadRequest();

        var room = db.Rooms.Find(id);
        if (room == null) return NotFound();

        var today = DateOnly.FromDateTime(DateTime.Now);

        // Block if any reservation that either is ongoing or in the future:
        bool hasOverlap = db.Reservations
            .Any(rs => rs.RoomId == id && rs.CheckOut > today);

        if (hasOverlap)
        {
            TempData["Info"] = "Have future or ongoing reservation!";
        }
        else
        {
            room.Active = !room.Active;
            db.SaveChanges();
            TempData["Info"] = room.Active ? "Room Active." : "Room Inactive.";
        }

        var referer = Request.Headers["Referer"].ToString();
        return Redirect(string.IsNullOrEmpty(referer) ? Url.Action("Index", "Room") : referer);
    }


    public IActionResult BatchAdd(string typeId)
    {
        var r = db.RoomTypes.Find(typeId);
        if (r == null)
        {
            TempData["Info"] = "Invalid Room Type";
            return RedirectToAction("Index", new { typeId = typeId });
        }
        var vm = new BatchAddVM
        {
            TypeId = typeId,
        };

        ViewBag.TypeId = r.Id;
        ViewBag.TypeName = r.Name;
        return View(vm);
    }

    [HttpPost]
    public IActionResult BatchAdd(BatchAddVM vm)
    {
        if (ModelState.IsValid)
        {
            var id = NextIds(vm.Count);
            var rooms = new List<Room>();

            for (int i = 0; i < vm.Count; i++)
            {
                rooms.Add(new Room
                {
                    Id = id[i],
                    RoomTypeId = vm.TypeId,
                    Active = true,
                });
            }

            db.Rooms.AddRange(rooms);
            db.SaveChanges();

            TempData["Info"] = $"{vm.Count} rooms added successfully";
            return RedirectToAction("Index", new { typeId = vm.TypeId });
        }

        ViewBag.CategoryList = new SelectList(db.RoomTypes, "Id", "Name");
        return View(vm);
    }


    // 1. BATCH UPDATE - Select Multiple Rooms
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public IActionResult BatchUpdate(string[] selectedIds, string newTypeId)
    {
        if (selectedIds?.Length > 0 && !string.IsNullOrEmpty(newTypeId))
        {
            var rooms = db.Rooms.Where(r => selectedIds.Contains(r.Id)).ToList();

            foreach (var room in rooms)
            {
                room.RoomTypeId = newTypeId;
            }

            db.SaveChanges();
            TempData["Info"] = $"{rooms.Count} rooms updated successfully.";
        }
        else if (selectedIds?.Length == 0)
        {
            TempData["Info"] = "No rooms selected for update.";
        }
        else if (string.IsNullOrEmpty(newTypeId))
        {
            TempData["Info"] = "Please select a new room type.";
        }

        return Redirect(Request.Headers.Referer.ToString());
    }

    // 2. BATCH ACTIVATE/DEACTIVATE - Select Multiple Rooms
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public IActionResult BatchInactive(string[] selectedIds, bool makeActive = false)
    {
        if (selectedIds == null || selectedIds.Length == 0)
        {
            TempData["Info"] = "No rooms selected for the operation.";
            var refUrlEmpty = Request.Headers["Referer"].ToString();
            return Redirect(string.IsNullOrEmpty(refUrlEmpty) ? Url.Action("Index", "Room") : refUrlEmpty);
        }

        var today = DateOnly.FromDateTime(DateTime.Now);

        // Find selected room IDs that have ongoing or future reservations.
        var blockedRoomIds = db.Reservations
            .Where(res => selectedIds.Contains(res.RoomId) && res.CheckOut > today)
            .Select(res => res.RoomId)
            .Distinct()
            .ToHashSet();

        // Load the rooms that the user selected
        var rooms = db.Rooms
            .Where(r => selectedIds.Contains(r.Id))
            .ToList();

        int updated = 0;
        int skipped = 0;
        var skippedIds = new List<string>();

        foreach (var room in rooms)
        {
            if (!makeActive && blockedRoomIds.Contains(room.Id))
            {
                // Skip deactivation when there is an ongoing/future reservation
                skipped++;
                skippedIds.Add(room.Id);
                continue;
            }

            if (room.Active != makeActive)
            {
                room.Active = makeActive;
                updated++;
            }
        }

        if (updated > 0) db.SaveChanges();

        string action = makeActive ? "activated" : "deactivated";
        string message = $"{updated} rooms {action} successfully.";
        if (skipped > 0)
        {
            message += $" {skipped} skipped because they have ongoing/future reservations (IDs: {string.Join(", ", skippedIds.Take(10))}{(skippedIds.Count > 10 ? ", ..." : "")}).";
        }

        TempData["Info"] = message;

        var referer = Request.Headers["Referer"].ToString();
        return Redirect(string.IsNullOrEmpty(referer) ? Url.Action("Index", "Room") : referer);
    }



    //To check existing of room id
    public bool CheckId(string id)
    {
        return !db.Rooms.Any(p => p.Id == id);
    }

    //To check existing of category id
    public bool CheckTypeId(string typeId)
    {
        return db.RoomTypes.Any(t => t.Id == typeId);
    }

    // Used for single room record insert
    private string NextId()
    {
        string max = db.Rooms.Max(p => p.Id) ?? "R001";
        int n = int.Parse(max[1..]);
        return (n + 1).ToString("'R'000");
    }

    // Used for batch insert auto generate multiple Room ID
    private List<string> NextIds(int? count)
    {
        var ids = new List<string>();
        if (count == null)
        {
            count = 1;
        }
        string max = db.Rooms.Max(p => p.Id) ?? "R001";
        int n = int.Parse(max[1..]);

        for (int i = 1; i <= count; i++)
        {
            int newNumber = n + i;
            ids.Add(newNumber.ToString("'R'000"));
        }
        return ids;
    }
}
