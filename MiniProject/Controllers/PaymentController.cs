using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Net.Mail;
using System.Text.Json;

namespace MiniProject.Controllers;

public class PaymentController : Controller
{
    private readonly DB db;
    private readonly IWebHostEnvironment en;
    private readonly Helper hp;


    public PaymentController(DB db, Helper hp, IWebHostEnvironment en)
    {
        this.db = db;
        this.hp = hp;
        this.en = en;
    }

    [Authorize]
    public IActionResult Index(string Id)
    {
        int reservation_id = int.Parse(Id);
        ViewBag.ReservationId = reservation_id;

        return View();
    }

    // Display the Payment Detail Before Generate QR Code
    public IActionResult PaymentDetail(string Id)
    {
        int reservation_id = int.Parse(Id);
        var rs = db.Reservations.Where(r => r.Id == reservation_id).First();
        var days = rs.CheckOut.DayNumber - rs.CheckIn.DayNumber;
        var total = rs.Price * days;

        ViewBag.rsId = reservation_id;
        ViewBag.Amount = total;
        return View();
    }

    // Payment Controller - Clean Production Version

    public IActionResult QR(string Id, decimal amount)
    {
        try
        {
            int reservation_id = int.Parse(Id);
            ViewBag.rsId = reservation_id;
            ViewBag.Amount = amount;
            return View();
        }
        catch (Exception ex)
        {
            TempData["Info"] = $"Error: {ex.Message}";
            return RedirectToAction("Index", "Home");
        }
    }

    // Generate QR Code For User Scan To Pay
    [HttpPost]
    public IActionResult GenerateQRCode([FromBody] QRRequestModel model)
    {
        try
        {
            // Validate reservation exists
            var reservation = db.Reservations.FirstOrDefault(r => r.Id == model.ReservationId);
            if (reservation == null)
            {
                return Json(new { success = false, message = "Reservation not found" });
            }

            // Create Payment Information to Encode in QR
            var paymentData = new
            {
                ReservationId = model.ReservationId,
                Amount = model.Amount,
                WalletType = "DuitNow",
                PaymentId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.Now,
                MerchantName = "Hotel Booking System"
            };

            string qrData = JsonSerializer.Serialize(paymentData);

            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            {
                QRCodeData qrCodeData = qrGenerator.CreateQrCode(qrData, QRCodeGenerator.ECCLevel.Q);
                using (var qrCode = new PngByteQRCode(qrCodeData))
                {
                    var qrBytes = qrCode.GetGraphic(10);
                    var base64 = Convert.ToBase64String(qrBytes);

                    TempData["Info"] = "Payment Successfully";

                    return Json(new
                    {
                        success = true,
                        qrCode = $"data:image/png;base64,{base64}",
                        paymentId = paymentData.PaymentId,
                    });
                }
            }
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Error: {ex.Message}" });
        }
    }

    // CHANGE 1: In VerifyPayment method - Fix the payment already exists check
    [HttpPost]
    public IActionResult VerifyPayment(string paymentId, int reservationId)
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrEmpty(paymentId))
            {
                return Json(new { success = false, message = "Payment ID is required" });
            }

            if (reservationId <= 0)
            {
                return Json(new { success = false, message = "Invalid Reservation ID" });
            }

            var reservation = db.Reservations
                .Include(p => p.Payment)
                .Include(r => r.Room)
                    .ThenInclude(rm => rm.RoomTypes)
                .FirstOrDefault(r => r.Id == reservationId);

            if (reservation == null)
            {
                return Json(new { success = false, message = "Reservation Not Found" });
            }

            // FIX: Check if payment already exists AND is completed
            if (reservation.Payment != null && reservation.Payment.Status == "Completed")
            {
                return Json(new
                {
                    success = true,
                    message = "Payment already processed",
                    redirectUrl = Url.Action("PaymentSuccess", "Payment", new { Id = reservation.Id })
                });
            }

            var days = reservation.CheckOut.DayNumber - reservation.CheckIn.DayNumber;
            var total = reservation.Price * days;

            var payment = new Payment
            {
                ReservationId = reservationId,
                Amount = total,
                PaymentMethod = "E-Wallet",
                TransactionId = paymentId,
                Status = "Completed"
            };

            db.Payment.Add(payment);
            db.SaveChanges();

            var user = db.Users.FirstOrDefault(u => u.Id == reservation.MemberId);
            if (user != null)
            {
                ReceiptEmail(user.Email, reservation);
            }

            return Json(new
            {
                success = true,
                message = "Payment successful!",
                redirectUrl = Url.Action("PaymentSuccess", "Payment", new { Id = reservation.Id })
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Error: {ex.Message}" });
        }
    }

    // CHANGE 2: Fix PaymentSuccess method
    public IActionResult PaymentSuccess(int Id)
    {
        try
        {
            var reservation = db.Reservations
                .Include(r => r.Payment)
                .Include(r => r.Room)
                .ThenInclude(rm => rm.RoomTypes)
                .FirstOrDefault(r => r.Id == Id);

            if (reservation == null)
            {
                TempData["Info"] = "Reservation Not Found";
                return RedirectToAction("Index", "Home");
            }

            // FIX: Check if payment exists and is completed
            if (reservation.Payment == null)
            {
                TempData["Info"] = "Payment Not Found";
                return RedirectToAction("Index", "Home");
            }

            if (reservation.Payment.Status != "Completed")
            {
                TempData["Info"] = "Payment Incomplete";
                return RedirectToAction("Index", "Home");
            }

            return View(reservation);
        }
        catch (Exception ex)
        {
            TempData["Info"] = $"Error: {ex.Message}";
            return RedirectToAction("Index", "Home");
        }
    }


    // Helper Model for QR Request
    public class QRRequestModel
    {
        public int ReservationId { get; set; }
        public decimal Amount { get; set; }
    }

    public IActionResult QrRefund(int paymentId)
    {
        var payment = db.Payment.FirstOrDefault(p => p.Id == paymentId);


        if (payment != null && payment.Status == "Refund")
        {
            TempData["Info"] = "Not Refund Record Exist";
            return RedirectToAction("History", "Checkout");
        }

        return View(payment);
    }

    [HttpPost]
    public IActionResult QrRefund(int paymentId, int reservationId)
    {
        var payment = db.Payment.Find(paymentId);
        var rs = db.Reservations.Find(reservationId);

        if (payment == null || rs == null)
        {
            TempData["Info"] = "Not Refund Record Exist";
            return RedirectToAction("History", "Checkout");
        }

        var reservation = db.Reservations
            .Include(r => r.Payment)
            .Include(r => r.Room)
                .ThenInclude(rm => rm.RoomTypes)
            .First(r => r.Id == reservationId);

        payment.Status = "Refund";
        payment.RefundDate = DateTime.UtcNow;
        payment.RefundId = Guid.NewGuid().ToString();
        db.SaveChanges();

        rs.Active = false;
        db.SaveChanges();

        var user = db.Users.First(u => u.Id == reservation.MemberId);

        RefundEmail(user.Email, reservation);
        TempData["Info"] = "Refund Successfully";
        return RedirectToAction("Refund", "Checkout", new { paymentId = payment.Id });


    }

    // CHANGE 3: Fix ReceiptEmail method - Use FirstOrDefault with Email instead of Find
    private void ReceiptEmail(string email, Reservation reservation)
    {
        // FIX: Use FirstOrDefault with email instead of Find
        User u = db.Users.FirstOrDefault(user => user.Email == email);

        if (u == null) return; // Exit if user not found

        var roomName = reservation.Room.RoomTypes.Name;
        var totalAmount = reservation.Payment.Amount;
        var paymentId = reservation.Payment.Id;
        var checkIn = reservation.CheckIn;
        var checkOut = reservation.CheckOut;
        var paymentMethod = reservation.Payment.PaymentMethod;

        var mail = new MailMessage();
        mail.To.Add(new MailAddress(u.Email, u.Name));
        mail.Subject = "Receipt";
        mail.IsBodyHtml = true;

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
<table width='100%' cellpadding='0' cellspacing='0' border='0' bgcolor='#f4f6f9'>
  <tr>
    <td align='center' style='padding:20px;'>
      <table width='600' cellpadding='0' cellspacing='0' border='0' bgcolor='#ffffff' style='border-radius:8px; padding:20px; font-family:Arial, sans-serif;'>
        
        <!-- Header -->
        <tr>
          <td align='center' style='border-bottom:2px solid #eee; padding-bottom:15px;'>
            <h2 style='margin:0; font-size:20px; color:#333;'>Booking Receipt</h2>
          </td>
        </tr>

        <!-- Details -->
        <tr>
          <td style='padding:20px 0; font-size:14px; color:#444;'>
            <p style='margin:6px 0;'><strong>Room Name:</strong> {roomName}</p>
            <p style='margin:6px 0;'><strong>Payment Method:</strong> {paymentMethod}</p>
            <p style='margin:6px 0;'><strong>Total Amount:</strong> {totalAmount}</p>
            <p style='margin:6px 0;'><strong>Payment ID:</strong> {paymentId}</p>
            <p style='margin:6px 0;'><strong>Check-in Date:</strong> {checkIn:MMM dd, yyyy}</p>
            <p style='margin:6px 0;'><strong>Check-out Date:</strong> {checkOut:MMM dd, yyyy}</p>
          </td>
        </tr>

        <!-- Footer -->
        <tr>
          <td align='center' style='padding-top:15px; font-size:12px; color:#888;'>
            <p style='margin:5px 0;'>Thank you for booking with us!</p>
          </td>
        </tr>

      </table>
    </td>
  </tr>
</table>
";

        hp.SendEmail(mail);
    }

    // CHANGE 4: Fix RefundEmail method - Use FirstOrDefault with Email instead of Find
    private void RefundEmail(string email, Reservation reservation)
    {
        // FIX: Use FirstOrDefault with email instead of Find
        User u = db.Users.FirstOrDefault(user => user.Email == email);

        if (u == null) return; // Exit if user not found

        var roomName = reservation.Room.RoomTypes.Name;
        var totalAmount = reservation.Payment.Amount;
        var paymentId = reservation.Payment.Id;
        var checkIn = reservation.CheckIn;
        var checkOut = reservation.CheckOut;
        var paymentMethod = reservation.Payment.PaymentMethod;
        var refundStatus = reservation.Payment.Status;
        var refundTime = reservation.Payment.RefundDate;
        var refundId = reservation.Payment.RefundId;

        var mail = new MailMessage();
        mail.To.Add(new MailAddress(u.Email, u.Name));
        mail.Subject = "Refund";
        mail.IsBodyHtml = true;

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
<table width='100%' cellpadding='0' cellspacing='0' border='0' bgcolor='#f4f6f9'>
  <tr>
    <td align='center' style='padding:20px;'>
      <table width='600' cellpadding='0' cellspacing='0' border='0' bgcolor='#ffffff' style='border-radius:8px; padding:20px; font-family:Arial, sans-serif;'>
        
        <!-- Header -->
        <tr>
          <td align='center' style='border-bottom:2px solid #eee; padding-bottom:15px;'>
            <h2 style='margin:0; font-size:20px; color:red;'>Refund</h2>
          </td>
        </tr>

        <!-- Details -->
        <tr>
          <td style='padding:20px 0; font-size:14px; color:#444;'>
            <p style='margin:6px 0;'><strong>Room Name:</strong> {roomName}</p>
            <p style='margin:6px 0;'><strong>Payment Method:</strong> {paymentMethod}</p>
            <p style='margin:6px 0;'><strong>Total Amount:</strong> {totalAmount}</p>
            <p style='margin:6px 0;'><strong>Check-in Date:</strong> {checkIn:MMM dd, yyyy}</p>
            <p style='margin:6px 0;'><strong>Check-out Date:</strong> {checkOut:MMM dd, yyyy}</p>
            <p style='margin:6px 0;'><strong>Date:</strong> {refundTime}</p>
            <p style='margin:6px 0;'><strong>Status:</strong> {refundStatus}</p>
            <p style='margin:6px 0;'><strong>Refund Id:</strong> {refundId}</p>
          </td>
        </tr>

        <!-- Footer -->
        <tr>
          <td align='center' style='padding-top:15px; font-size:12px; color:#888;'>
            <p style='margin:5px 0;'>Thank you for booking with us!</p>
          </td>
        </tr>

      </table>
    </td>
  </tr>
</table>
";

        hp.SendEmail(mail);
    }
}