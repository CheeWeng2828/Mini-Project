using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        var roomType = db.RoomTypes.Include(t => t.RoomGalleries);
        return View(roomType);
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

