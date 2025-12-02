using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using X.PagedList.Extensions;

namespace MiniProject.Controllers;

public class RoomTypeController : Controller
{
    private readonly DB db;
    private readonly Helper hp;

    public RoomTypeController(DB db, Helper hp)
    {
        this.db = db;
        this.hp = hp;
    }

    public IActionResult Index(string? name, string? sort, string? dir, int page = 1)
    {
        // Searching
        ViewBag.Name = name = name?.Trim() ?? "";
        var searched = db.RoomTypes
                          .Include(t => t.Rooms)
                          .Where(t => t.Name.Contains(name));

        // Sorting
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Func<RoomType, object> fn = sort switch
        {
            "Id" => r => r.Id,
            "Name" => r => r.Name,
            "Price" => r => r.Price,
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
            return PartialView("TypeRecord", m);
        }

        return View(m);
    }

    [Authorize(Roles = "Admin")]
    public IActionResult NewType()
    {
        return View();
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public IActionResult NewType(AddTypeVM vm)
    {
        if (ModelState.IsValid("Id") && db.RoomTypes.Any(p => p.Id == vm.Id))
        {
            ModelState.AddModelError("Id", "Duplicated Id.");
        }
        if(ModelState.IsValid("Name")  && db.RoomTypes.Any(p => p.Name.ToLower().Trim() == vm.Name.ToLower().Trim()))
        {
            ModelState.AddModelError("Name", "Duplicated Name in Same Branch.");
        }
        if (ModelState.IsValid("Photo"))
        {
            foreach (var photo in vm.Photo)
            {
                var err = hp.ValidatePhoto(photo);
                if (err != "") ModelState.AddModelError("Photo", err);
            }

        }

        if (ModelState.IsValid)
        {

            db.RoomTypes.Add(new()
            {
                Id = vm.Id,
                Name = vm.Name,
                Price = vm.Price,
            });

            foreach (var photo in vm.Photo)
            {
                db.RoomGalleries.Add(new()
                {
                    RoomTypeId = vm.Id,
                    PhotoURL = hp.SavePhoto(photo, "room"),
                });
            }
            db.SaveChanges();

            TempData["Info"] = "Record Inserted.";
            return RedirectToAction("Index");
        }

        return View();
    }

    [Authorize(Roles = "Admin")]
    public IActionResult UpdateRoomType(string? id)
    {
        var t = db.RoomTypes.Find(id);
        var g = db.RoomGalleries.Where(r => r.RoomTypeId == id)
           .ToList();

        if (t == null)
        {
            return RedirectToAction("Index");
        }

        var vm = new UpdateRoomTypeVm
        {
            Id = t.Id,
            Name = t.Name,
            Price = t.Price,
            PhotoURL = g.Select(x => x.PhotoURL).ToList(),
        };
        return View(vm);

    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public IActionResult UpdateRoomType(UpdateRoomTypeVm vm)
    {
        var t = db.RoomTypes.Find(vm.Id);

        if (t == null)
        {
            return RedirectToAction("Index");
        }

        if (vm.Photo != null)
        {
            foreach (var photo in vm.Photo)
            {
                var err = hp.ValidatePhoto(photo);
                if (err != "") ModelState.AddModelError("Photo", err);
            }

        }


        if (ModelState.IsValid)
        {
            t.Id = vm.Id;
            t.Name = vm.Name;
            t.Price = vm.Price;

            if (vm.Photo != null)
            {
                foreach (var photo in vm.Photo)
                {
                    db.RoomGalleries.Add(new()
                    {
                        RoomTypeId = t.Id,
                        PhotoURL = hp.SavePhoto(photo, "room"),
                    });
                }

            }

            db.SaveChanges();

            TempData["Info"] = "Record Updated.";
            return RedirectToAction("Index");
        }
        return View(vm);
    }

    public bool CheckId(string id)
    {
        return !db.RoomTypes.Any(p => p.Id == id);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public IActionResult DeletePhoto(string file, string id)
    {
        hp.DeletePhoto(file, "room");

        var deleted = db.RoomGalleries.Where(x => x.PhotoURL == file && x.RoomTypeId == id);
        db.RoomGalleries.RemoveRange(deleted);
        db.SaveChanges();

        TempData["Info"] = "Delete Successful";
        return RedirectToAction("Index");
    }
}
