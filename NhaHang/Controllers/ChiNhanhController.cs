using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NhaHang.ModelFromDB;

namespace NhaHang.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    
    public class ChiNhanhController : ControllerBase
    {
        private readonly quanlynhahang dbc;

        public ChiNhanhController(quanlynhahang db)
        {
            dbc = db;
        }

        [HttpGet]
        [Route("/Branch/List")]
        [Authorize(Policy = "Everyone")]
        public IActionResult GetList()
        {
            return Ok(new { data = dbc.Branches.ToList() });
        }

        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        [Route("/Branch/Add")]
        public IActionResult ThemChiNhanh(string TenChiNhanh, string DiaChi, string phone, IFormFile? AnhUpload)
        {
            string? imagePath = null;

            if (AnhUpload != null && AnhUpload.Length > 0)
            {
                var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "branches");
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(AnhUpload.FileName);
                var fullPath = Path.Combine(folderPath, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    AnhUpload.CopyTo(stream);
                }

                imagePath = Path.Combine("images", "branches", fileName).Replace("\\", "/");
            }

            Branch dm = new Branch()
            {
                BranchName = TenChiNhanh,
                BranchAddr = DiaChi,
                NumberPhone = phone,
                Image = imagePath,
            };
            
            dbc.Branches.Add(dm);
            dbc.SaveChanges();
            return Ok(new { message = "Thêm chi nhánh thành công!", data = dm });
        }

        [HttpPut]
        [Authorize(Policy = "AdminOnly")]
        [Route("/Branch/Update")]
        public IActionResult CapNhatThongTinChiNhanh(int ID, string? TenChiNhanh, string? DiaChi, string? phone, IFormFile? AnhUpload)
        {
            var dm = dbc.Branches.Find(ID);
            if (dm == null)
                return NotFound(new { message = "Chi nhánh không tồn tại!" });

            if (!string.IsNullOrWhiteSpace(TenChiNhanh))
                dm.BranchName = TenChiNhanh;

            if (!string.IsNullOrWhiteSpace(DiaChi))
                dm.BranchAddr = DiaChi;

            if (!string.IsNullOrWhiteSpace(phone))
                dm.NumberPhone = phone;

            if (AnhUpload != null && AnhUpload.Length > 0)
            {
                var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "branches");
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(AnhUpload.FileName);
                var fullPath = Path.Combine(folderPath, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    AnhUpload.CopyTo(stream);
                }

                dm.Image = Path.Combine("images", "branches", fileName).Replace("\\", "/");
            }

            dbc.Branches.Update(dm);
            dbc.SaveChanges();
            return Ok(new { message = "Cập nhật chi nhánh thành công!", data = dm });
        }

        [HttpDelete]
        [Authorize(Policy = "AdminOnly")]
        [Route("/Branch/Delete")]
        public IActionResult XoaChiNhanh(int ID)
        {
            var cn = dbc.Branches.Find(ID);
            if (cn == null)
                return NotFound(new { message = "Chi nhánh không tồn tại!" });
            var banTrongChiNhanh = dbc.Tables.Where(t => t.BranchId == ID).ToList();
            dbc.Tables.RemoveRange(banTrongChiNhanh);
            var nhanVienTrongChiNhanh = dbc.Users.Where(u => u.BranchId == ID).ToList();
            dbc.Users.RemoveRange(nhanVienTrongChiNhanh);
            dbc.Branches.Remove(cn);
            dbc.SaveChanges();
            return Ok(new { message = "Xóa chi nhánh thành công!", data = cn });
        }
    }
}
