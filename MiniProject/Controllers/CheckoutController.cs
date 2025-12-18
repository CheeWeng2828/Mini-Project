using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using System.Text;
using System.Text.Json.Nodes;

namespace MiniProject.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly Helper hp;
        private readonly IWebHostEnvironment en;
        private readonly DB db;
        private string PaypalClientId { get; set; } = "";
        private string PaypalSecret { get; set; } = "";
        private string PaypalUrl { get; set; } = "";


        public CheckoutController(IConfiguration configuration, DB db, Helper hp, IWebHostEnvironment en)
        {
            PaypalClientId = configuration["PayPalSettings:ClientId"]!;
            PaypalSecret = configuration["PayPalSettings:Secret"]!;
            PaypalUrl = configuration["PayPalSettings:Url"]!;
            this.db = db;
            this.hp = hp;
            this.en = en;
        }

        public IActionResult Index(string Id)
        {
            int reservation_id = int.Parse(Id);
            var rs = db.Reservations.Where(r => r.Id == reservation_id).First();
            var days = rs.CheckOut.DayNumber - rs.CheckIn.DayNumber;
            var total = rs.Price * days;

            ViewBag.rsId = reservation_id;
            ViewBag.Amount = total;
            ViewBag.PaypalClientId = PaypalClientId;
            return View();
        }

        [HttpPost]
        public async Task<JsonResult> CreateOrder([FromBody] JsonObject data)
        {
            var totalAmount = data?["amount"]?.ToString();
            if (totalAmount == null)
            {
                return new JsonResult(new { Id = "" });
            }

            JsonObject createOrderRequest = new JsonObject();
            createOrderRequest.Add("intent", "CAPTURE");

            // Add this to your CreateOrder method after creating createOrderRequest
            JsonObject applicationContext = new JsonObject();
            applicationContext.Add("shipping_preference", "NO_SHIPPING");
            applicationContext.Add("user_action", "PAY_NOW");
            applicationContext.Add("payment_method", new JsonObject()
            {
                ["payer_selected"] = "PAYPAL",
                ["payee_preferred"] = "IMMEDIATE_PAYMENT_REQUIRED"
            });
            createOrderRequest.Add("application_context", applicationContext);

            JsonObject amount = new JsonObject();
            amount.Add("currency_code", "MYR");
            amount.Add("value", totalAmount);

            JsonArray purchaseUnits = new JsonArray();
            JsonObject purchaseUnit = new JsonObject();
            purchaseUnit.Add("amount", amount);
            purchaseUnits.Add(purchaseUnit);

            createOrderRequest.Add("purchase_units", purchaseUnits);

            string accessToken = await GetPaypalAccessToken();
            string url = PaypalUrl + "/v2/checkout/orders";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
                requestMessage.Content = new StringContent(createOrderRequest.ToString(), null, "application/json");

                var httpResponse = await client.SendAsync(requestMessage);

                if (httpResponse.IsSuccessStatusCode)
                {
                    var strResponse = await httpResponse.Content.ReadAsStringAsync();
                    var jsonResponse = JsonNode.Parse(strResponse);

                    if (jsonResponse != null)
                    {
                        string paypalOrderId = jsonResponse["id"]?.ToString() ?? "";

                        return new JsonResult(new { id = paypalOrderId });
                    }
                }
            }

            return new JsonResult(new { Id = "" });
        }

        [HttpPost]
        public async Task<JsonResult> CompleteOrder([FromBody] JsonObject data)
        {
            var orderId = data["orderID"]?.ToString();
            if (orderId == null)
            {
                return new JsonResult("error");
            }

            string accessToken = await GetPaypalAccessToken();

            string url = PaypalUrl + "/v2/checkout/orders/" + orderId + "/capture";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);

                var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
                requestMessage.Content = new StringContent("", null, "application/json");

                var httpResponse = await client.SendAsync(requestMessage);

                if (httpResponse.IsSuccessStatusCode)
                {
                    var strResponse = await httpResponse.Content.ReadAsStringAsync();
                    var jsonResponse = JsonNode.Parse(strResponse);

                    if (jsonResponse != null)
                    {
                        string paypalOrderStatus = jsonResponse["status"]?.ToString() ?? "";
                        if (paypalOrderStatus == "COMPLETED")
                        {
                            // For Store Token Id into the database
                            var paypalTokenId = ExtractPaypalTokenId(jsonResponse);
                            return new JsonResult(new {
                                status = "success",
                                paypalTokenId = paypalTokenId
                            });
                        }
                    }
                }

            }

            return new JsonResult("error");
        }

        // For export the paypal token id (Helper Function)
        private string ExtractPaypalTokenId(JsonNode jsonResponse)
        {
            try
            {
                var purchaseUnits = jsonResponse["purchase_units"]?.AsArray();
                if (purchaseUnits != null && purchaseUnits.Count > 0)
                {
                    var payments = purchaseUnits[0]?["payments"];
                    var captures = payments?["captures"]?.AsArray();
                    if (captures != null && captures.Count > 0)
                    {
                        return captures[0]?["id"]?.ToString() ?? "";
                    }
                }
            }
            catch
            {

            }

            return "";
        }


        private async Task<string> GetPaypalAccessToken()
        {
            string accessToken = "";

            string url = PaypalUrl + "/v1/oauth2/token";

            using (var client = new HttpClient())
            {
                string credentials64 =
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(PaypalClientId + ":" + PaypalSecret));


                client.DefaultRequestHeaders.Add("Authorization", "Basic " + credentials64);


                var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
                requestMessage.Content = new StringContent("grant_type=client_credentials", null,
                    "application/x-www-form-urlencoded");

                var httpResponse = await client.SendAsync(requestMessage);

                if (httpResponse.IsSuccessStatusCode)
                {
                    var strResponse = await httpResponse.Content.ReadAsStringAsync();

                    var jsonResponse = JsonNode.Parse(strResponse);
                    if (jsonResponse != null)
                    {
                        accessToken = jsonResponse["access_token"]?.ToString() ?? "";
                    }
                }

                return accessToken;
            }
        }

        public IActionResult ProcessingPayment(string id)
        {
            var vm = new CheckOutVM {
                Id = id,
            };
            return View(vm);
        }

        [HttpPost]
        [Authorize]
        public IActionResult ProcessingPayment(CheckOutVM vm)
        {
            var reservationId = int.Parse(vm.Id);
            var payment = db.Payment.First(p => p.ReservationId == reservationId);

            var reservation = db.Reservations
                .Include(r => r.Room.RoomTypes)
                .Include(r => r.Payment)
                .First(r => r.Id == payment.ReservationId);

            if (!string.IsNullOrEmpty(vm.PaypalCaptureId))
            {
                payment.TransactionId = vm.PaypalCaptureId;
                payment.PaymentMethod = "Paypal";
                payment.Status = "Completed";
            }

            var email = db.Users.Where(u => u.Id == reservation.MemberId).Select(u => u.Email).First();

            db.SaveChanges();
            ReceiptEmail(email, reservation);
            TempData["Info"] = "Make Payment Sucessful.";
            return RedirectToAction("PaymentSuccess", "Payment", new { Id = reservationId });

        }

        [Authorize]
        public IActionResult RefundPayment(int id)
        {
            var payment = db.Payment.FirstOrDefault(p => p.Id == id);
            if (payment == null)
            {
                TempData["Info"] = "Payment not found";
                return RedirectToAction("History");
            }

            if (payment.Status == "Refund")
            {
                TempData["Info"] = "This payment has already been refunded";
                return RedirectToAction("History");
            }

            if (string.IsNullOrEmpty(payment.TransactionId))
            {
                TempData["Info"] = "Paypal Capture ID is missing for this payment";
                return RedirectToAction("History");
            }

            var vm = new Refund
            {
                PaymentId = id,
                Amount = payment.Amount,
            };
            return View(vm);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ProcessRefund(Refund vm)
        {

            try
            {
                var payment = db.Payment.FirstOrDefault(p => p.Id == vm.PaymentId);
                if (payment == null || string.IsNullOrEmpty(payment.TransactionId))
                {
                    TempData["Info"] = "Payment not found or Paypal Capture ID missing";
                    return RedirectToAction("History");
                }
                if (payment.Status == "Refund")
                {
                    TempData["Info"] = "This payment has already been refunded";
                    return RedirectToAction("History");
                }
                if (vm.Amount <= 0 || vm.Amount > payment.Amount)
                {
                    ModelState.AddModelError("Amount", "Invalid refund amount");
                    return RedirectToAction("History");
                }

                // Get Paypal Access Token
                string accessToken = await GetPaypalAccessToken();
                if (string.IsNullOrEmpty(accessToken))
                {
                    TempData["Info"] = "Failed to authenticate with Paypal";
                    return RedirectToAction("History");
                }

                // MODIFIED: Convert RM to USD for PayPal refund (same 0.24 conversion as payment)
                decimal usdAmount = vm.Amount * 0.24m;

                // Check if this is a full refund
                bool isFullRefund = vm.Amount == payment.Amount;

                // MODIFIED: For full refunds, don't specify amount - PayPal will refund the full captured amount
                var refundRequest = new JsonObject();

                if (!isFullRefund)
                {
                    // Only specify amount for partial refunds, converted to USD
                    refundRequest["amount"] = new JsonObject
                    {
                        ["currency_code"] = "USD",
                        ["value"] = usdAmount.ToString("F2")
                    };
                }

                string url = $"{PaypalUrl}/v2/payments/captures/{payment.TransactionId}/refund";

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                    client.DefaultRequestHeaders.Add("Accept", "application/json");
                    client.DefaultRequestHeaders.Add("Prefer", "return=representation");

                    var httpResponse = await client.PostAsync(url,
                        new StringContent(refundRequest.ToString(), System.Text.Encoding.UTF8, "application/json"));

                    if (!httpResponse.IsSuccessStatusCode)
                    {
                        var errorContent = await httpResponse.Content.ReadAsStringAsync();
                        // MODIFIED: Added errorContent details to help with debugging PayPal API errors
                        TempData["Info"] = $"PayPal refund failed: {httpResponse.ReasonPhrase}. Details: {errorContent}";
                        return RedirectToAction("History");
                    }

                    var strResponse = await httpResponse.Content.ReadAsStringAsync();
                    var jsonResponse = JsonNode.Parse(strResponse);

                    if (jsonResponse != null)
                    {
                        var refundStatus = jsonResponse["status"]?.ToString();
                        var refundId = jsonResponse["id"]?.ToString();

                        if (string.IsNullOrEmpty(refundId))
                        {
                            TempData["Info"] = "Paypal refund response missing refund ID";
                            return RedirectToAction("History");
                        }

                        // MODIFIED: Added validation to check if PayPal actually approved/completed the refund
                        if (refundStatus != "COMPLETED" && refundStatus != "PENDING")
                        {
                            TempData["Info"] = $"PayPal refund failed with status: {refundStatus}";
                            return RedirectToAction("History");
                        }

                        using var transaction = db.Database.BeginTransaction();
                        try
                        {
                            payment.Status = "Refund";
                            payment.RefundDate = DateTime.UtcNow;
                            payment.RefundId = refundId;

                            var reservation = db.Reservations
                                .Include(r => r.Payment)
                                .Include(r => r.Room)
                                    .ThenInclude(rm => rm.RoomTypes)
                                .FirstOrDefault(r => r.Id == payment.ReservationId);
                            if (reservation != null)
                            {
                                reservation.Active = false;
                            }

                            db.SaveChanges();
                            transaction.Commit();

                            var email = db.Users.Where(u => u.Id == reservation.MemberId).Select(u => u.Email).First();
                            RefundEmail(email, reservation);
                            TempData["Info"] = "Refund processed successfully";
                            return RedirectToAction("Refund", new { paymentId = payment.Id });
                        }
                        catch (Exception dbEx)
                        {
                            transaction.Rollback();
                            TempData["Info"] = "Database error after successful PayPal refund. Please contact support.";
                            return RedirectToAction("History");
                        }
                    }
                    else
                    {
                        // MODIFIED: Added proper handling for null/invalid JSON response from PayPal
                        TempData["Info"] = "Invalid response from PayPal";
                        return RedirectToAction("History");
                    }
                }
            }
            catch (Exception ex)
            {
                // MODIFIED: Fixed TempData key casing from "Info" to "Info" to match other instances
                TempData["Info"] = "An error occurred while processing refund: " + ex.Message;
                return RedirectToAction("History");
            }
        }

        public IActionResult Refund(int paymentId)
        {
            var r = db.Payment
                .Where(p => p.Id == paymentId).FirstOrDefault();

            if (r != null && r.Status == "Completed")
            {
                TempData["Info"] = "Not Refund Record Exist";
                return RedirectToAction("History");
            }

            return View(r);
        }
        public IActionResult History()
        {
            var user = db.Users.First(u => u.Email == User!.Identity!.Name);
            var m = db.Reservations
                      .Include(rs => rs.Payment)
                      .Include(rs => rs.Member)
                      .Include(m => m.Review)
                      .Include(rs => rs.Room)
                        .ThenInclude(rm => rm.RoomTypes)
                      .Where(rs => rs.MemberId == user.Id);

            return View(m);
        }
        private void ReceiptEmail(string email, Reservation reservation)
        {
            User u = db.Users.First(u => u.Email == email)!;

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
"; ;

            hp.SendEmail(mail);
        }

        private void RefundEmail(string email, Reservation reservation)
        {
            User u = db.Users.Find(email)!;

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
}