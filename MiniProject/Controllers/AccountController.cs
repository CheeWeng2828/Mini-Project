using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mail;

// For reCaptcha Use
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace MiniProject.Controllers;

public class AccountController : Controller
{
    private readonly DB db;
    private readonly Helper hp;
    private readonly IWebHostEnvironment en;

    // For reCaptcha Use
    private readonly IHttpClientFactory cf;
    private readonly RecaptchaOptions _recaptchaOptions;

    public AccountController(DB db, Helper hp, IWebHostEnvironment en, IOptions<RecaptchaOptions> recaptchaOptions, IHttpClientFactory cf)
    {
        this.db = db;
        this.hp = hp;
        this.en = en;
        this.cf = cf;
        _recaptchaOptions = recaptchaOptions.Value;
    }

    // GET: Account/Login
    public IActionResult Login()
    {
        return View();
    }

    // POST: Account/Login
    [HttpPost]
    [ValidateAntiForgeryToken]

    public async Task<IActionResult> Login(LoginVM vm, string? returnURL)
    {
        // (1) Get user (admin or member) record based on email (PK)
        var u = db.Users.Where(u => u.Email == vm.Email).FirstOrDefault();

        // (2) Custom validation -> verify password
        if (u == null)
        {
            ModelState.AddModelError("", "Account Not Found.");
        }

        if (u.Active == false)
        {
            ModelState.AddModelError("", "Login credentials not matched or account disabled.");
        }

        // Get the reCaptcha response from our view
        var recaptachaResponse = Request.Form["g-recaptcha-response"].ToString();

        if (string.IsNullOrEmpty(recaptachaResponse))
        {
            ModelState.AddModelError(string.Empty, "Please Complete Verify...");
        }

        // Call Google To verify the API
        var client = cf.CreateClient();
        var values = new Dictionary<string, string>
        {
            {"secret",_recaptchaOptions.SecretKey },
            {"response",recaptachaResponse },
            {"remoteip",HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""},
        };

        var content = new FormUrlEncodedContent(values);
        var res = await client.PostAsync("https://www.google.com/recaptcha/api/siteverify", content);
        var resString = await res.Content.ReadAsStringAsync();

        // Wait for verify response by returing JSON
        var json = JsonSerializer.Deserialize<RecaptchaVerifyResponse>(resString,new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (json == null && json.Success != true)
        {
            ModelState.AddModelError(string.Empty, "Verify Failed Please Try Again...");
        }

        if (!hp.VerifyPassword(u.Hash, vm.Password))
        {
            // Check the last failed login time if greater than 30 minutes, clear up the failed login count
            if (u.LastFailedLoginTime.HasValue && (DateTime.UtcNow - u.LastFailedLoginTime.Value).TotalMinutes > 30)
            {
                u.LoginAttemptCount = 0;
            }

            u.LoginAttemptCount += 1;
            u.LastFailedLoginTime = DateTime.UtcNow;
            var token = hp.TokenId();

            if (u.LoginAttemptCount >= 5)
            {
                u.Active = false;
                db.UserTokens.Add(new UserToken
                {
                    Id = token,
                    MemberId = u.Id,
                    GenerateTime = TimeOnly.FromDateTime(DateTime.Now),
                });

                db.SaveChanges();

                SendReactiveAccountEmail(u, token);

                ModelState.AddModelError("", "Account disabled after too many failed attempt.Please Check Your Email To reset and reactive your account");
            }
            ModelState.AddModelError("", "Login credentials not matched or account disabled.");

        }

        if (ModelState.IsValid)
        {
            u.LoginAttemptCount = 0;
            u.LastFailedLoginTime = null;
            db.SaveChanges();

            // (3) Sign in
            hp.SignIn(u!.Email, u.Role, vm.RememberMe);

            // (4) Handle return URL
            if (string.IsNullOrEmpty(returnURL))
            {
                TempData["Info"] = "Login successfully.";
                return RedirectToAction("Index", "Home");
            }
        }

        db.SaveChanges();
        return View(vm);
    }

    // Reactive Account via Email token link
    private void SendReactiveAccountEmail(User u, string token)
    {
        var mail = new MailMessage();
        mail.To.Add(new MailAddress(u.Email, u.Name));
        mail.Subject = "Account Reactive";
        mail.IsBodyHtml = true;

        var url = Url.Action("Token", "Account", new { tokenId = token }, "https");

        var path = u switch
        {
            Admin => Path.Combine(en.WebRootPath, "photos", "admin.jpg"),
            Member m => Path.Combine(en.WebRootPath, "photos", m.PhotoURL),
            _ => "",
        };

        var att = new Attachment(path);
        mail.Attachments.Add(att);
        att.ContentId = "photo";

        mail.Body = $@"
            <img src='cid:photo' style='width: 200px; height: 200px;
                                        border: 1px solid #333'>
            <p>Dear {u.Name},<p>
            <h1 style='color:red'>Account Reactive</h1>
            <p>
                Please Click <a href='{url}'>Me</a>
                to reactive and update your password.
            </p>
            <p>From, 🐱 Super Admin</p>
        ";

        hp.SendEmail(mail);
    }

    // GET: Account/Logout
    public IActionResult Logout(string? returnURL)
    {
        TempData["Info"] = "Logout successfully.";

        // Sign out
        hp.SignOut();

        return RedirectToAction("Index", "Home");
    }

    // GET: Account/AccessDenied
    public IActionResult AccessDenied(string? returnURL)
    {
        return View();
    }



    // ------------------------------------------------------------------------
    // Others
    // ------------------------------------------------------------------------

    // GET: Account/CheckEmail
    public bool CheckEmail(string email)
    {
        return !db.Users.Any(u => u.Email == email);
    }

    // GET: Account/Register
    public IActionResult Register()
    {
        var vm = new RegisterVM();
        return View(vm);
    }

    // POST: Account/Register
    [ValidateAntiForgeryToken]

    [HttpPost]
    public async Task<IActionResult> Register(RegisterVM vm)
    {
        if (ModelState.IsValid("Email") &&
            db.Users.Any(u => u.Email == vm.Email))
        {
            ModelState.AddModelError("Email", "Duplicated Email.");
        }

        // Get the reCaptcha response from our view
        var recaptachaResponse = Request.Form["g-recaptcha-response"].ToString();

        if (string.IsNullOrEmpty(recaptachaResponse))
        {
            ModelState.AddModelError(string.Empty, "Please Complete Verify...");
        }

        // Call Google To verify the API
        var client = cf.CreateClient();
        var values = new Dictionary<string, string>
        {
            {"secret",_recaptchaOptions.SecretKey },
            {"response",recaptachaResponse },
            {"remoteip",HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""},
        };

        var content = new FormUrlEncodedContent(values);
        var res = await client.PostAsync("https://www.google.com/recaptcha/api/siteverify", content);
        var resString = await res.Content.ReadAsStringAsync();

        // Wait for verify response by returing JSON
        var json = JsonSerializer.Deserialize<RecaptchaVerifyResponse>(resString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (json == null && json.Success != true)
        {
            ModelState.AddModelError(string.Empty, "Verify Failed Please Try Again...");
        }

        if (ModelState.IsValid)
        {
            // Insert member
            db.Members.Add(new()
            {
                Email = vm.Email,
                Hash = hp.HashPassword(vm.Password),
                Name = vm.Name,
                Active = true,
                PhotoURL = "guest.jpg",
            });
            db.SaveChanges();

            TempData.Clear();
            TempData["Info"] = "Register successfully. Please login.";
            return RedirectToAction("Login");
        }

        return View(vm);
    }

    // GET: Account/UpdatePassword
    [Authorize]
    public IActionResult UpdatePassword()
    {
        return View();
    }

    // POST: Account/UpdatePassword
    [Authorize]
    [HttpPost]
    public IActionResult UpdatePassword(UpdatePasswordVM vm)
    {
        // Get user (admin or member) record based on email (PK)
        var u = db.Users.Where(u => u.Email == User.Identity!.Name).FirstOrDefault();
        if (u == null) return RedirectToAction("Index", "Home");

        // If current password not matched
        if (!hp.VerifyPassword(u.Hash, vm.Current))
        {
            ModelState.AddModelError("Current", "Current Password not matched.");
        }

        if (ModelState.IsValid)
        {
            // Update user password (hash)
            u.Hash = hp.HashPassword(vm.New);
            db.SaveChanges();

            TempData["Info"] = "Password updated.";
            return RedirectToAction("Index", "Home");
        }

        return View();
    }

    // GET: Account/UpdateProfile
    [Authorize(Roles = "Member")]
    public IActionResult UpdateProfile()
    {
        // Get member record based on email (PK)
        var m = db.Members.Where(m => m.Email == User.Identity!.Name).FirstOrDefault();
        if (m == null) return RedirectToAction("Index", "Home");

        var capturedPhoto = HttpContext.Session.GetString("CapturedPhoto");

        var vm = new UpdateProfileVM
        {
            Email = m.Email,
            Name = m.Name,
            PhotoURL = !string.IsNullOrEmpty(capturedPhoto) ? capturedPhoto : m.PhotoURL
        };
        return View(vm);
    }

    // POST: Account/UpdateProfile
    [Authorize(Roles = "Member")]
    [HttpPost]
    public IActionResult UpdateProfile(UpdateProfileVM vm)
    {
        var capturePhoto = HttpContext.Session.GetString("CapturedPhoto");
        // Get member record based on email (PK)
        var m = db.Members.Where(u => u.Email == User.Identity!.Name).FirstOrDefault();
        if (m == null) return RedirectToAction("Index", "Home");

        if (vm.Photo != null && string.IsNullOrEmpty(capturePhoto))
        {
            var err = hp.ValidatePhoto(vm.Photo);
            if (err != "") ModelState.AddModelError("Photo", err);
        }

        if (ModelState.IsValid)
        {
            m.Name = vm.Name;

            if (!string.IsNullOrEmpty(capturePhoto))
            {
                if (m.PhotoURL != null && m.PhotoURL != "guest.jpg")
                    hp.DeletePhoto(m.PhotoURL, "photos");

                m.PhotoURL = capturePhoto;
                HttpContext.Session.Remove("CapturedPhoto");

            }
            else if (vm.Photo != null)
            {
                if (m.PhotoURL != null && m.PhotoURL != "guest.jpg")
                {
                    hp.DeletePhoto(m.PhotoURL, "photos");
                }
                m.PhotoURL = hp.SavePhoto(vm.Photo, "photos");
            }

            db.SaveChanges();

            TempData["Info"] = "Profile updated.";
            return RedirectToAction();
        }

        vm.Email = m.Email;

        if(string.IsNullOrEmpty(capturePhoto))
        {
            vm.PhotoURL = m.PhotoURL;
        }
        return View(vm);
    }


    // GET: Account/ResetPassword
    public IActionResult ResetPassword()
    {
        return View();
    }

    // POST: Account/ResetPassword
    [HttpPost]
    public IActionResult ResetPassword(ResetPasswordVM vm)
    {
        var u = db.Users.Where(u => u.Email == vm.Email).FirstOrDefault();

        if (u == null)
        {
            ModelState.AddModelError("Email", "Email not found.");
        }

        if (ModelState.IsValid)
        {
            // Generate random password
            string token = hp.TokenId();
            var current_time = TimeOnly.FromDateTime(DateTime.Now);
            var expire_token = db.UserTokens.Where(t => t.GenerateTime < current_time)
                .ToList();

            db.UserTokens.RemoveRange(expire_token);

            // Update user (admin or member) record
            db.UserTokens.Add(new()
            {
                Id = token,
                MemberId = u.Id,
                GenerateTime = current_time,
            });
            db.SaveChanges();

            //Send reset password email
            SendResetPasswordEmail(u, token);

            TempData["Info"] = "Please Check Your Email";
            return RedirectToAction("Index", "Home");
        }

        return View();
    }

    private void SendResetPasswordEmail(User u, string token)
    {
        var mail = new MailMessage();
        mail.To.Add(new MailAddress(u.Email, u.Name));
        mail.Subject = "Account Verification";
        mail.IsBodyHtml = true;

        var url = Url.Action("Token", "Account", new { tokenId = token }, "https");

        var path = u switch
        {
            Admin => Path.Combine(en.WebRootPath, "photos", "admin.jpg"),
            Member m => Path.Combine(en.WebRootPath, "photos", m.PhotoURL),
            _ => "",
        };

        var att = new Attachment(path);
        mail.Attachments.Add(att);
        att.ContentId = "photo";

        mail.Body = $@"
            <img src='cid:photo' style='width: 200px; height: 200px;
                                        border: 1px solid #333'>
            <p>Dear {u.Name},<p>
            <h1>Your Email has been validate</h1>
            <p>
                Please Click <a href='{url}'>Me</a>
                to setup your new password.
            </p>
            <p>From, 🐱 Super Admin</p>
        ";

        hp.SendEmail(mail);
    }

    public IActionResult Token(string tokenId)
    {
        var id = db.UserTokens.Find(tokenId);

        if (id == null)
        {
            TempData["Info"] = "Invalid Access !!!";
            return RedirectToAction("Index", "Home");
        }

        var user = db.Users.Where(u => u.Id == id.MemberId).FirstOrDefault();

        var vm = new NewPasswordVM
        {
            Email = user.Email,
        };

        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Token(NewPasswordVM vm)
    {
        var u = db.Users.Where(u => u.Email == vm.Email).FirstOrDefault();
        if (u == null)
        {
            TempData["Info"] = "Invalid Email !!!";
            return RedirectToAction("Index", "Home");
        }

        // Get the reCaptcha response from our view
        var recaptachaResponse = Request.Form["g-recaptcha-response"].ToString();

        if (string.IsNullOrEmpty(recaptachaResponse))
        {
            ModelState.AddModelError(string.Empty, "Please Complete Verify...");
        }

        // Call Google To verify the API
        var client = cf.CreateClient();
        var values = new Dictionary<string, string>
        {
            {"secret",_recaptchaOptions.SecretKey },
            {"response",recaptachaResponse },
            {"remoteip",HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""},
        };

        var content = new FormUrlEncodedContent(values);
        var res = await client.PostAsync("https://www.google.com/recaptcha/api/siteverify", content);
        var resString = await res.Content.ReadAsStringAsync();

        // Wait for verify response by returing JSON
        var json = JsonSerializer.Deserialize<RecaptchaVerifyResponse>(resString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (json == null && json.Success != true)
        {
            ModelState.AddModelError(string.Empty, "Verify Failed Please Try Again...");
        }


        if (ModelState.IsValid)
        {
            u.Active = true;
            u.Hash = hp.HashPassword(vm.New);
            var current_time = TimeOnly.FromDateTime(DateTime.Now);
            var expire_token = db.UserTokens.Where(t => t.GenerateTime < current_time)
                .ToList();
            db.UserTokens.RemoveRange(expire_token);
            db.SaveChanges();
            TempData["Info"] = "Password Update Successful";
            return RedirectToAction("Login", "Account");
        }
        return View();

    }

    [HttpPost]
    public IActionResult Upload(IFormFile photo)
    {
        if (photo == null || photo.Length == 0)
            return BadRequest("No file uploaded.");

        // Ensure the photos folder exists under wwwroot so SavePhoto can write into it
        var folderName = "photos";
        var folderPath = Path.Combine(en.WebRootPath, folderName);
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        // Use your helper (unchanged) which returns the saved filename (e.g. "guid.jpg")
        var fileName = hp.SavePhoto(photo, folderName);

        // Build a public URL that the browser can fetch
        var url = Url.Content($"{fileName}");

        // Put it in TempData so Done -> Register can read it
        HttpContext.Session.SetString("CapturedPhoto", url);

        // Return JSON { url: "..."} so your fetch() code receives data.url
        return Json(new { url });
    }

    // Recaptcha Verify Field
    private class RecaptchaVerifyResponse
    {
        public bool Success { get; set; }
        public DateTime Challenge_ts { get; set; }
        public string Hostname { get; set; }
        public string[] ErrorCodes { get; set; }
    }
}
