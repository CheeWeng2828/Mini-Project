global using MiniProject.Models;
global using MiniProject;

var builder = WebApplication.CreateBuilder(args);

// Recaptcha Configuration
builder.Services.Configure<RecaptchaOptions>(builder.Configuration.GetSection("Recaptcha"));

// Add MVC with TempData provider - CRITICAL for TempData to work
builder.Services.AddMvc()
    .AddCookieTempDataProvider(); // This line is absolutely essential

// HttpClient Used for Google site verify
builder.Services.AddHttpClient();

builder.Services.AddSqlServer<DB>($@"
    Data Source=(LocalDB)\MSSQLLocalDB;
    AttachDbFilename={builder.Environment.ContentRootPath}\DB.mdf;
");

builder.Services.AddScoped<Helper>();
builder.Services.AddAuthentication().AddCookie();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRequestLocalization("en-MY");
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.MapDefaultControllerRoute();
app.Run();

public class RecaptchaOptions
{
    public string SiteKey { get; set; }
    public string SecretKey { get; set; }
}