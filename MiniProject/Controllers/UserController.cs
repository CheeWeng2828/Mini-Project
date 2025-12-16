using Microsoft.AspNetCore.Mvc;
using X.PagedList.Extensions;
using Microsoft.AspNetCore.Authorization;
using System.Net;

namespace MiniProject.Controllers
{
    public class UserController : Controller
    {
        private readonly DB db;
        private readonly Helper hp;

        public UserController(Helper hp, DB db)
        {
            this.hp = hp;
            this.db = db;
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Index(string? name, string? sort, string? dir, int page = 1)
        {
            // Searching
            ViewBag.Name = name = name?.Trim() ?? "";
            var searched = db.Users.Where(u => u.Name.Contains(name));

            // Sorting
            ViewBag.Sort = sort;
            ViewBag.Dir = dir;

            Func<User, object> fn = sort switch
            {
                "Email" => u => u.Email,
                "Name" => u => u.Name,
                "Role" => u => u.Role,
                "Active" => u => u.Active,
                _ => u => u.Email
            };

            var sorted = dir == "des" ? searched.OrderByDescending(fn)
                                      : searched.OrderBy(fn);

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
                return PartialView("User", m);
            }

            return View(m);
        }


        [Authorize(Roles = "Admin")]
        public IActionResult Detail(int id)
        {
            var u = db.Users.FirstOrDefault(u => u.Id == id);

            if (u == null)
                return RedirectToAction("Index");

            return View(u);
        }

        // GET: User/AddAdmin
        public IActionResult AddAdmin()
        {
            return View();
        }
        
        // POST: User/AddAdmin
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public IActionResult AddAdmin(AddAdminVM vm)
        {
            if (ModelState.IsValid("Email") &&
                db.Users.Any(u => u.Email == vm.Email))
            {
                ModelState.AddModelError("Email", "Duplicated Email.");
            }

            if (ModelState.IsValid)
            {
                // Insert Admin
                db.Admins.Add(new()
                {
                    Email = vm.Email,
                    Hash = hp.HashPassword(vm.Password),
                    Name = vm.Name,
                    Active = true,
                    LoginAttemptCount = 0,
                    LastFailedLoginTime = null,
                    //PhotoURL = null,
                });

                db.SaveChanges();

                TempData["Info"] = "Add Admin Successful.";
                return RedirectToAction("Index");
            }

            return View(vm);
        }


        [Authorize(Roles = "Admin")]
        public IActionResult ActiveUser(int id)
        {
            var u = db.Users.FirstOrDefault(u => u.Id == id && u.Email != User!.Identity!.Name);

            if (u != null)
            {
                u.Active = !u.Active;
                db.SaveChanges();
                TempData["Info"] = u.Active ? "User Active." : "User Inactive.";
            }
            else
            {
                TempData["Info"] = "Cannot Deactivate Self-Account";
            }
                return Redirect(Request.Headers.Referer.ToString());
        }
    }
}
