using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Net.Mail;
using System.Text.Json;

namespace Assignment.Controllers;

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

    public IActionResult QR(string Id,decimal amount)
    {
        int reservation_id = int.Parse(Id);

        ViewBag.rsId = reservation_id;
        ViewBag.Amount = amount;
        return View();
    }

    // Generate QR Code For User Scan To Pay
    [HttpPost]
    public IActionResult GenerateQRCode(int reservationId,decimal amount)
    {
        // Create Payment Information to Encode in QR
        var paymentData = new
        {
            ReservationId = reservationId,
            Amount = amount,
            WalletType = "DuitNow",
            PaymentId = Guid.NewGuid().ToString(),
            Timestamp = DateTime.Now,
            MerchantName = "Hotel Booking System"
        };

        string qrData = JsonSerializer.Serialize(paymentData);

        using(QRCodeGenerator qrGenerator = new QRCodeGenerator())
        {
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(qrData,QRCodeGenerator.ECCLevel.Q);
            using (var qrCode = new PngByteQRCode(qrCodeData))
            {
                using(MemoryStream ms = new MemoryStream())
                {
                    var qrBytes = qrCode.GetGraphic(10);
                    var base64 = Convert.ToBase64String(qrBytes);

                    TempData["Info"] = "Pending Payment";

                        return Json(new
                        {
                            success = true,
                            qrCode = $"data:image/png;base64,{base64}",
                            paymentId = paymentData.PaymentId,
                        });
                }
                
            }
        }
    }

    public IActionResult VerifyPayment(string paymentId, int reservationId)
    {
        var reservation = db.Reservations
            .Include(r => r.Room)
                .ThenInclude(rm => rm.Type)
            .FirstOrDefault(r => r.Id == reservationId);

        if (reservation == null)
        {
            return Json(new { success = false, message = "Reservation Not Found" });
        }

        var days = reservation.CheckOut.DayNumber - reservation.CheckIn.DayNumber;
        var total = reservation.Price * days;

        var payment = new Payment
        {
            ReservationId = reservationId,
            Amount = total,
            MemberEmail = User?.Identity?.Name ?? reservation.MemberEmail,
            PaymentMethod = "E-Wallet",
            TransactionId = paymentId,
        };
        db.Payment.Add(payment);
        db.SaveChanges();

        reservation.PaymentId = payment.Id;
        reservation.Paid = true;
        reservation.Active = true;

        db.SaveChanges();

        ReceiptEmail(reservation.MemberEmail, reservation);

        return Json(new
        {
            success = true,
            message = "Payment successful!",
            redirectUrl = Url.Action("PaymentSuccess","Payment", new { Id = reservation.Id })
        });
    }

    public IActionResult PaymentSuccess(int Id)
    {
        var reservation = db.Reservations
            .Include(r => r.Payment)
            .Include(r => r.Room)
            .ThenInclude(rm => rm.Type)
            .ThenInclude(t => t.Hotel)
            .FirstOrDefault(r => r.Id == Id);

        if(reservation == null || !reservation.Paid)
        {
            TempData["Info"] = "Payment Not Found Or Incomplete";
            return RedirectToAction("Index", "Home");
        }

        return View(reservation);
    }

    public IActionResult QrRefund(int paymentId)
    {
        var payment = db.Payment.FirstOrDefault(p => p.Id == paymentId);


        if (payment != null && payment.IsRefund == true)
        {
            TempData["Info"] = "Not Refund Record Exist";
            return RedirectToAction("History","Checkout");
        }

        return View(payment);
    }

    [HttpPost]
    public IActionResult QrRefund(int paymentId,int reservationId)
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
                .ThenInclude(rm => rm.Type)
            .First(r => r.Id == reservationId);

        payment.IsRefund = true;
        payment.RefundDate = DateTime.UtcNow;
        payment.RefundId = Guid.NewGuid().ToString();
        db.SaveChanges();

        rs.Paid = false;
        rs.Active = false;
        db.SaveChanges();

        RefundEmail(reservation.MemberEmail, reservation);
        TempData["Info"] = "Refund Successfully";
        return RedirectToAction("Refund","Checkout" ,new { paymentId = payment.Id });


    }

    private void ReceiptEmail(string email, Reservation reservation)
    {
        User u = db.Users.Find(email)!;

        var roomName = reservation.Room.Type.Name;
        var totalAmount = reservation.Payment.Amount;
        var paymentId = reservation.PaymentId;
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
"; ;

        hp.SendEmail(mail);
    }

    private void RefundEmail(string email, Reservation reservation)
    {
        User u = db.Users.Find(email)!;

        var roomName = reservation.Room.Type.Name;
        var totalAmount = reservation.Payment.Amount;
        var paymentId = reservation.PaymentId;
        var checkIn = reservation.CheckIn;
        var checkOut = reservation.CheckOut;
        var paymentMethod = reservation.Payment.PaymentMethod;
        var refundStatus = reservation.Payment.IsRefund ? "Complete" : "Pending";
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
            <h2 style='margin:0; font-size:20px; color:'red';'>Refund</h2>
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
"; ;

        hp.SendEmail(mail);
    }
}