using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NhaHang.ModelFromDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace NhaHang.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    
    public class TableController : Controller
    {
        private readonly quanlynhahang dbc;

        public TableController(quanlynhahang db)
        {
            dbc = db;
        }

        [HttpGet]
        [Authorize(Policy = "Everyone")]
        [Route("/Table/ByBranch")]
        public IActionResult LayDanhSachBanTheoChiNhanh(int branchID)
        {
            var dsBan = dbc.Tables
                .Where(t => t.BranchId == branchID)
                .Include(t => t.Status)
                .Include(t => t.Branch)
                .Select(t => new
                {
                    t.TableId,
                    t.TableNumber,
                    t.Capacity,
                    StatusName = t.Status.StatusName,
                    t.BranchId,
                }).ToList();

            if (dsBan == null || dsBan.Count == 0)
                return NotFound(new { message = "Không tìm thấy bàn nào thuộc chi nhánh này!" });

            return Ok(new { data = dsBan });
        }

        [HttpPost]
        [Authorize(Policy = "Management")]
        [Route("/Table/Add")]
        public IActionResult ThemBan(int tableNumber, int capacity, int branchID)
        {
            var userIdClaim = User.FindFirst("UserId");
            var roleClaim = User.FindFirst(ClaimTypes.Role);
            var userId = int.Parse(userIdClaim.Value);
            var role = roleClaim?.Value;

            var currentUser = dbc.Users.FirstOrDefault(u => u.UserId == userId);
            if (currentUser == null)
                return Unauthorized(new { message = "Không xác định được người dùng!" });

            if (role != "Quản lý tổng" && currentUser.BranchId != branchID)
                return Unauthorized(new { message = "Bạn không có quyền thêm bàn cho chi nhánh này!" });

            Table dm = new Table()
            {
                TableNumber = tableNumber,
                Capacity = capacity,
                StatusId = 1,
                BranchId = branchID
            };
            dbc.Tables.Add(dm);
            dbc.SaveChanges();
            var ban = dbc.Tables
                .Include(t => t.Status)
                .Include(t => t.Branch)
                .Where(t => t.TableId == dm.TableId)
                .Select(t => new
                {
                    t.TableId,
                    t.TableNumber,
                    t.Capacity,
                    t.BranchId,
                })
                .FirstOrDefault();
            return Ok(new { message = "Thêm bàn thành công!", data = ban });
        }

        [HttpPut]
        [Authorize(Policy = "Management")]
        [Route("/Table/Update")]
        public IActionResult CapNhatThongTinBan(int ID, int tableNumber, int capacity, int branchID)
        {
            var userIdClaim = User.FindFirst("UserId");
            var roleClaim = User.FindFirst(ClaimTypes.Role);
            var userId = int.Parse(userIdClaim.Value);
            var role = roleClaim?.Value;

            var currentUser = dbc.Users.FirstOrDefault(u => u.UserId == userId);
            if (currentUser == null)
                return Unauthorized(new { message = "Không xác định được người dùng!" });

            if (role != "Quản lý tổng" && currentUser.BranchId != branchID)
                return Unauthorized(new { message = "Bạn không có quyền cập nhật bàn của chi nhánh này!" });

            var dm = dbc.Tables.Find(ID);
            if (dm == null)
                return NotFound(new { message = "Bàn không tồn tại!" });
            dm.TableNumber = tableNumber;
            dm.Capacity = capacity;
            dm.BranchId = branchID;

            dbc.Tables.Update(dm);
            dbc.SaveChanges();

            var ban = dbc.Tables
                .Include(t => t.Status)
                .Include(t => t.Branch)
                .Where(t => t.TableId == dm.TableId)
                .Select(t => new
                {
                    t.TableId,
                    t.TableNumber,
                    t.Capacity,
                    t.BranchId,
                })
                .FirstOrDefault();
            return Ok(new { message = "Cập nhật thông tin bàn thành công!", data = ban });
        }

        [HttpDelete]
        [Authorize(Policy = "Management")]
        [Route("/Table/Delete")]
        public IActionResult XoaBan(int ID)
        {
            var userIdClaim = User.FindFirst("UserId");
            var roleClaim = User.FindFirst(ClaimTypes.Role);
            var userId = int.Parse(userIdClaim.Value);
            var role = roleClaim?.Value;

            var currentUser = dbc.Users.FirstOrDefault(u => u.UserId == userId);
            if (currentUser == null)
                return Unauthorized(new { message = "Không xác định được người dùng!" });

            var dm = dbc.Tables.Find(ID);
            if (role != "Quản lý tổng" && currentUser.BranchId != dm.BranchId)
                return Unauthorized(new { message = "Bạn không có quyền xóa bàn của chi nhánh này!" });
            if (dm == null)
                return NotFound(new { message = "Bàn không tồn tại!" });

            var bill = dbc.Bills.FirstOrDefault(b => b.TableId == ID && b.Table.StatusId == 3);
            if (bill != null)
            {
                var billDetails = dbc.BillItems.Where(d => d.BillId == bill.BillId).ToList();
                if (billDetails.Any())
                    dbc.BillItems.RemoveRange(billDetails);

                dbc.Bills.Remove(bill);
            }

            dbc.Tables.Remove(dm);
            dbc.SaveChanges();
            return Ok(new { message = "Xóa bàn thành công!", data = dm });
        }
    }
}
