﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NhaHang.ModelFromDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace NhaHang.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    
    public class MonAnController : ControllerBase
    {
        private readonly quanlynhahang dbc;

        public MonAnController(quanlynhahang db)
        {
            dbc = db;
        }

        [HttpGet]
        [Authorize(Policy = "Everyone")]
        [Route("/Food/List")]
        public IActionResult GetList()
        {
            var foods = dbc.Foods
                .Include(f => f.Category)
                .Select(f => new
                {
                    f.FoodId,
                    f.FoodName,
                    f.CategoryId,
                    CategoryName = f.Category.CategoryName,
                    f.Price,
                    f.Picture,
                })
                .ToList();
            return Ok(new { data = foods });
        }

        [HttpGet]
        [Route("/Food/Search")]
        public IActionResult TimKiemMonAn(string TenMonAn)
        {
            var ketQua = dbc.Foods
                .Include(f => f.Category)
                .Where(f => f.FoodName.Contains(TenMonAn))
                .Select(f => new
                {
                    f.FoodId,
                    f.FoodName,
                    f.CategoryId,
                    CategoryName = f.Category.CategoryName,
                    f.Price,
                    f.Picture,
                })
                .ToList();

            if (ketQua.Count == 0)
            {
                return Ok(new { message = "Trong menu không có món ăn này", data = ketQua });
            }

            return Ok(new { message = "Kết quả tìm kiếm", data = ketQua });
        }

        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        [Route("/Food/Add")]
        public IActionResult ThemMonAn(string TenMonAn, int MaDanhMuc, decimal Gia, IFormFile? AnhUpload)
        {
            string? urlAnh = null;

            if (AnhUpload != null && AnhUpload.Length > 0)
            {
                var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "foods");
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(AnhUpload.FileName);
                var fullPath = Path.Combine(folderPath, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    AnhUpload.CopyTo(stream);
                }

                urlAnh = Path.Combine("images", "foods", fileName).Replace("\\", "/");
            }

            Food dm = new Food()
            {
                FoodName = TenMonAn,
                CategoryId = MaDanhMuc,
                Price = Gia,
                Picture = urlAnh
            };

            dbc.Foods.Add(dm);
            dbc.SaveChanges();

            var ma = dbc.Foods
                .Include(f => f.Category)
                .Where(f => f.FoodId == dm.FoodId)
                .Select(f => new
                {
                    f.FoodId,
                    f.FoodName,
                    f.CategoryId,
                    CategoryName = f.Category.CategoryName,
                    f.Price,
                    Picture = string.IsNullOrEmpty(f.Picture)
                        ? null
                        : $"{Request.Scheme}://{Request.Host}/{f.Picture}"
                })
                .FirstOrDefault();

            return Ok(new { message = "Thêm món ăn thành công!", data = ma });
        }

        [HttpPut]
        [Authorize(Policy = "AdminOnly")]
        [Route("/Food/Update")]
        public IActionResult CapNhatMonAn(int ID, string? TenMonAn, int? MaDanhMuc, decimal? Gia, IFormFile? AnhUpload)
        {
            var ma = dbc.Foods.Find(ID);
            if (ma == null)
                return NotFound(new { message = "Món ăn không tồn tại!" });

            ma.FoodName = TenMonAn ?? ma.FoodName;
            ma.CategoryId = MaDanhMuc ?? ma.CategoryId;
            ma.Price = Gia ?? ma.Price;

            if (AnhUpload != null && AnhUpload.Length > 0)
            {
                var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "foods");
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(AnhUpload.FileName);
                var fullPath = Path.Combine(folderPath, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    AnhUpload.CopyTo(stream);
                }

                ma.Picture = Path.Combine("images", "foods", fileName).Replace("\\", "/");
            }

            dbc.Foods.Update(ma);
            dbc.SaveChanges();

            var mon = dbc.Foods
                .Include(f => f.Category)
                .Where(f => f.FoodId == ma.FoodId)
                .Select(f => new
                {
                    f.FoodId,
                    f.FoodName,
                    f.CategoryId,
                    CategoryName = f.Category.CategoryName,
                    f.Price,
                    Picture = string.IsNullOrEmpty(f.Picture)
                        ? null
                        : $"{Request.Scheme}://{Request.Host}/{f.Picture}"
                })
                .FirstOrDefault();

            return Ok(new { message = "Cập nhật món ăn thành công!", data = mon });
        }

        [HttpDelete]
        [Authorize(Policy = "AdminOnly")]
        [Route("/Food/Delete")]
        public IActionResult XoaMonAn(int ID)
        {
            var ma = dbc.Foods.Find(ID);
            if (ma == null)
                return NotFound(new { message = "Món ăn không tồn tại!" });

            dbc.Foods.Remove(ma);
            dbc.SaveChanges();
            return Ok(new { message = "Xóa món ăn thành công!", data = ma });
        }
    }
}
