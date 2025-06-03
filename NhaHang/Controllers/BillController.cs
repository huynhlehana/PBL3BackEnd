using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using NhaHang.ModelFromDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;
using Spire.Doc;
using System.Globalization;
using System.Text;

namespace NhaHang.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "Everyone")]
    public class BillController : Controller
    {
        private readonly quanlynhahang dbc;

        public BillController(quanlynhahang db)
        {
            dbc = db;
        }

        [HttpPost]
        [Route("/Bill/GetByTable")]
        public IActionResult LayHoaDonTheoBan(int tableId)
        {
            var table = dbc.Tables.Find(tableId);

            if (table == null)
                return NotFound(new { message = "Không tìm thấy bàn!" });

            if (table.StatusId != 3)
                return Ok(new { message = "Bàn chưa được sử dụng, không có hóa đơn!" });

            var existingBill = dbc.Bills
                .Include(b => b.BillItems)
                .ThenInclude(i => i.Food)
                .FirstOrDefault(b => b.TableId == tableId && b.PaidDate == null);

            if (existingBill == null)
                return StatusCode(500, new { message = "Bàn đang sử dụng nhưng không có hóa đơn! Kiểm tra dữ liệu." });

            return Ok(new
            {
                message = "Lấy hóa đơn thành công",
                billId = existingBill.BillId,
                danhSachMon = existingBill.BillItems.Select(i => new
                {
                    i.BillItemId,
                    i.FoodId,
                    i.Food.FoodName,
                    i.Food.Picture,
                    i.Food.Price,
                    i.Quantity,
                    i.Description,
                    i.SubTotal,
                }),
                TotalPrice = existingBill.BillItems.Sum(i => i.SubTotal)
            });
        }

        [HttpPut]
        [Route("/Table/CheckIn")]
        public IActionResult KhachDenNhanBan(int tableId)
        {
            var table = dbc.Tables.Find(tableId);
            if (table == null)
                return NotFound(new { message = "Không tìm thấy bàn!" });

            if (table.StatusId != 2)
                return BadRequest(new { message = "Bàn này chưa được đặt trước hoặc đã có khách sử dụng!" });

            table.StatusId = 3;
            dbc.Tables.Update(table);
            dbc.SaveChanges();

            return Ok(new { message = "Khách đã đến. Bàn đã chuyển sang trạng thái đang sử dụng!" });
        }

        [HttpPost]
        [Route("/Bill/UpsertFood")]
        public IActionResult ThemOrCapNhatMon(int tableId, List<FoodOrderRequestcs> foodOrders)
        {
            var table = dbc.Tables.Find(tableId);

            if (table == null || foodOrders == null || !foodOrders.Any())
                return NotFound(new { message = "Không tìm thấy bàn hoặc món ăn!" });

            if (table.StatusId == 2)
                return BadRequest(new { message = "Bàn đã được đặt trước. Chưa thể thêm món khi khách chưa đến.!" });

            var bill = dbc.Bills
                .FirstOrDefault(b => b.TableId == tableId && b.PaidDate == null);

            if (bill == null)
            {
                bill = new Bill
                {
                    TableId = tableId,
                    BranchId = table.BranchId,
                    TotalPrice = 0,
                    Created = DateTime.Now,
                    PaidDate = null
                };

                dbc.Bills.Add(bill);
            }

            table.StatusId = 3;
            dbc.Tables.Update(table);

            dbc.SaveChanges();

            foreach (var i in foodOrders)
            {
                var food = dbc.Foods.Find(i.FoodId);
                if (food == null) continue;

                var existingItem = dbc.BillItems.FirstOrDefault(j => j.BillId == bill.BillId && j.FoodId == i.FoodId);
                BillItem? targetItem;

                if (existingItem == null)
                {
                    targetItem = new BillItem
                    {
                        BillId = bill.BillId,
                        FoodId = i.FoodId,
                        Quantity = i.Quantity,
                        Description = i.Description,
                        SubTotal = food.Price * i.Quantity,
                    };
                    dbc.BillItems.Add(targetItem);
                }
                else
                {
                    existingItem.Quantity = i.Quantity;
                    existingItem.Description = i.Description;
                    existingItem.SubTotal = food.Price * i.Quantity;
                    dbc.BillItems.Update(existingItem);
                    targetItem = existingItem;
                }
            }

            dbc.SaveChanges();

            var updatedItems = dbc.BillItems
                    .Include(i => i.Food)
                    .Where(i => i.BillId == bill.BillId)
                    .Select(i => new
                    {
                        i.BillItemId,
                        i.FoodId,
                        TenMon = i.Food.FoodName,
                        i.Quantity,
                        i.Description,
                        i.SubTotal
                    }).ToList();

            return Ok(new { message = "Cập nhật món ăn thành công!", updatedItems });
        }

        [HttpPost]
        [Route("/Bill/DeleteFoods")]
        public IActionResult XoaNhieuMon([FromBody] List<int> billItemIds)
        {
            var items = dbc.BillItems
                .Include(i => i.Bill)
                .Where(i => billItemIds.Contains(i.BillItemId))
                .ToList();

            if (!items.Any())
                return NotFound(new { message = "Không tìm thấy món nào để xoá!" });

            var bill = items.First().Bill;

            if (bill.PaidDate != null)
                return BadRequest(new { message = "Hóa đơn đã thanh toán, không thể xoá món!" });

            dbc.BillItems.RemoveRange(items);
            dbc.SaveChanges();

            var stillHasItems = dbc.BillItems.Any(i => i.BillId == bill.BillId);
            if (!stillHasItems)
            {
                dbc.Bills.Remove(bill);

                var table = dbc.Tables.Find(bill.TableId);
                if (table != null)
                {
                    table.StatusId = 1;
                    dbc.Tables.Update(table);
                }
                dbc.SaveChanges();

                return Ok(new { message = "Xoá món thành công. Hóa đơn rỗng nên đã xoá và cập nhật trạng thái bàn." });
            }

            return Ok(new { message = "Xoá danh sách món thành công!" });
        }

        [HttpGet]
        [Route("/Bill/SendQR")]
        public IActionResult SendQR(int tableId)
        {
            var bill = dbc.Bills
                .Include(b => b.BillItems)
                .FirstOrDefault(b => b.TableId == tableId && b.PaidDate == null);

            if (bill == null)
                return NotFound(new { message = "Không tìm thấy hóa đơn đang mở cho bàn này!" });

            if (!bill.BillItems.Any())
                return BadRequest(new { message = "Hóa đơn chưa có món ăn!" });

            bill.TotalPrice = bill.BillItems.Sum(i => i.SubTotal);

            string soTaiKhoan = "105880030539";
            string maNganHang = "vietinbank";
            string noiDungCK = $"Thanh toán hóa đơn bàn số #{bill.TableId}";
            decimal soTien = bill.TotalPrice;

            string qrUrl = $"https://img.vietqr.io/image/{maNganHang}-{soTaiKhoan}-print.png?amount={soTien}&addInfo={Uri.EscapeDataString(noiDungCK)}";

            return Ok(new
            {
                message = "Vui lòng quét mã QR để chuyển khoản!",
                data = new
                {
                    bill.BillId,
                    bill.TotalPrice,
                    qrImageUrl = qrUrl,
                    noiDungChuyenKhoan = noiDungCK
                }
            });
        }

        [HttpPut]
        [Route("/Bill/Checkout")]
        public IActionResult ThanhToan(int tableId, int paymentMethodId, decimal? tienKhachGui = null)
        {
            var bill = dbc.Bills
                .Include(b => b.BillItems)
                .ThenInclude(bi => bi.Food)
                .FirstOrDefault(b => b.TableId == tableId && b.PaidDate == null);


            if (bill == null)
                return NotFound(new { message = "Không tìm thấy hóa đơn đang mở cho bàn này!" });

            if (!bill.BillItems.Any())
                return BadRequest(new { message = "Hóa đơn chưa có món ăn!" });

            bill.TotalPrice = bill.BillItems.Sum(i => i.SubTotal);
            bill.PaidDate = DateTime.Now;
            bill.PaymentMethodId = paymentMethodId;

            if (paymentMethodId == 1)
            {
                if (tienKhachGui == null || tienKhachGui < bill.TotalPrice)
                {
                    return BadRequest(new { message = "Số tiền khách gửi không đủ!" });
                }
            }
            else if (paymentMethodId == 2)
            {
            }

            dbc.Bills.Update(bill);

            var table = dbc.Tables.FirstOrDefault(t => t.TableId == bill.TableId);
            if (table != null)
            {
                table.StatusId = 1;
                dbc.Tables.Update(table);
            }

            dbc.SaveChanges();

            var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "templates", "invoice_template.docx");
            if (!System.IO.File.Exists(templatePath))
            {
                return StatusCode(500, new { message = "Template không tồn tại trên server!" });
            }

            Document doc = new Document();
            doc.LoadFromFile(templatePath);

            var branch = dbc.Branches.FirstOrDefault(x => x.BranchId == bill.BranchId);
            string branchName = branch?.BranchName ?? "";
            string branchAddr = branch?.BranchAddr ?? "";
            string branchPhone = branch?.NumberPhone ?? "";

            string tableNumber = table?.TableNumber.ToString() ?? bill.TableId.ToString();
            string billIdStr = bill.BillId.ToString();

            string timeInStr = bill.Created!.Value.ToString("HH:mm", CultureInfo.GetCultureInfo("vi-VN"));
            string timeOutStr = bill.PaidDate.Value.ToString("HH:mm", CultureInfo.GetCultureInfo("vi-VN"));

            string dateStr = bill.PaidDate.Value.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("vi-VN"));

            string totalStr = FormatCurrency(bill.TotalPrice);
            string paymentMethodName = dbc.PaymentMethods
                .Where(p => p.PaymentMethodId == paymentMethodId)
                .Select(p => p.PaymentMethodName)
                .FirstOrDefault() ?? "";

            string moneyReceivedStr = "";
            string changeStr = "";
            if (paymentMethodId == 1)
            {
                var tienKhach = tienKhachGui ?? 0m;
                moneyReceivedStr = FormatCurrency(tienKhach);
                var thoi = tienKhach - bill.TotalPrice;
                changeStr = FormatCurrency(thoi < 0 ? 0 : thoi);
            }

            var sbFoodCombo = new StringBuilder();
            var sbQty = new StringBuilder();
            var sbPrice = new StringBuilder();
            var sbSubTotal = new StringBuilder();

            foreach (var bi in bill.BillItems)
            {
                var line = bi.Food.FoodName
           + (string.IsNullOrWhiteSpace(bi.Description) ? "" : $" - {bi.Description}");
                sbFoodCombo.AppendLine(line);

                sbQty.AppendLine(bi.Quantity.ToString());

                sbPrice.AppendLine(FormatCurrency(bi.Food.Price));

                sbSubTotal.AppendLine(FormatCurrency(bi.SubTotal));
            }

            doc.Replace("${branchName}", branchName, false, true);
            doc.Replace("${branchAddr}", branchAddr, false, true);
            doc.Replace("${numberPhone}", branchPhone, false, true);

            doc.Replace("${tableNumber}", tableNumber, false, true);
            doc.Replace("${date}", dateStr, false, true);
            doc.Replace("${billId}", billIdStr, false, true);

            doc.Replace("${timeIn}", timeInStr, false, true);
            doc.Replace("${timeOut}", timeOutStr, false, true);

            doc.Replace("${total}", totalStr, false, true);
            doc.Replace("${paymentMethod}", paymentMethodName, false, true);
            doc.Replace("${money}", moneyReceivedStr, false, true);
            doc.Replace("${change}", changeStr, false, true);

            doc.Replace("${foodName}", sbFoodCombo.ToString(), false, true);
            doc.Replace("${qty}", sbQty.ToString(), false, true);
            doc.Replace("${price}", sbPrice.ToString(), false, true);
            doc.Replace("${subTotal}", sbSubTotal.ToString(), false, true);

            using (var pdfStream = new MemoryStream())
            {
                doc.SaveToStream(pdfStream, FileFormat.PDF);
                pdfStream.Seek(0, SeekOrigin.Begin);
                return File(pdfStream.ToArray(), "application/pdf", $"{billIdStr}.pdf");
            }
        }

        private string FormatCurrency(decimal value)
        {
            long n = Convert.ToInt64(Math.Round(value, 0));
            var s = n.ToString();
            var rgx = new System.Text.RegularExpressions.Regex(@"\B(?=(\d{3})+(?!\d))");
            var formatted = rgx.Replace(s, ".");
            return $"{formatted}₫";
        }
    }
}