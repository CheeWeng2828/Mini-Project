using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniProject.Models;

namespace MiniProject.Controllers;

public class HomeController : Controller
{

    private readonly DB db;
    private readonly IWebHostEnvironment en;
    private readonly IConfiguration cf;

    public HomeController(DB db, IWebHostEnvironment en, IConfiguration cf)
    {
        this.db = db;
        this.en = en;
        this.cf = cf;
    }
    public IActionResult Index()
    {
        var roomType = db.RoomTypes.Include(t => t.RoomGalleries).ToList();

        var salesByRoomType = new List<ReportVM>();

        if(User.IsInRole("Admin"))
        {
            salesByRoomType = db.Reservations
            .Include(r => r.Room.RoomTypes)
            .Include(r => r.Payment)
            .Where(r => r.Payment != null && r.Payment.Status == "Completed")
            .GroupBy(r => r.Room.RoomTypes.Name)
            .Select(g => new ReportVM
            {
                RoomType = g.Key,
                TotalSales = g.Sum(r => r.Payment.Amount)
            })
            .OrderBy(s => s.RoomType)
            .ToList();
        }

        var vm = new ReportPageVM
        {
            RoomTypes = roomType,
            SalesByRoomType = salesByRoomType
        };
        return View(vm);
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Report()
    {
        return View("Home/Index");
    }

    public IActionResult Detail(string id)
    {
        var type = db.RoomTypes
            .Where(t => t.Id == id)
            .Include(t => t.RoomGalleries)
            .FirstOrDefault();

        if (type == null)
        {
            RedirectToAction("Home/Index");
        }
        return View(type);
    }
}

