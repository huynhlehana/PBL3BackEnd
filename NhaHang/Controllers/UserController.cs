using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using NhaHang.ModelFromDB;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authorization;

namespace NhaHang.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly quanlynhahang dbc;

        public UserController(IConfiguration configuration, quanlynhahang db)
        {
            _configuration = configuration;
            dbc = db;
        }

        [HttpGet]
        [Authorize(Policy = "Management")]
        [Route("/User/ByBranch")]
        public IActionResult LayDanhSachUserTheoChiNhanh(int branchID)
        {
            var userIdClaim = User.FindFirst("UserId");
            var roleClaim = User.FindFirst(ClaimTypes.Role);
            var userId = int.Parse(userIdClaim.Value);
            var role = roleClaim?.Value;

            var currentUser = dbc.Users.FirstOrDefault(u => u.UserId == userId);
            if (currentUser == null)
                return Unauthorized(new { message = "Không xác định được người dùng!" });

            if (role != "Quản lý tổng" && currentUser.BranchId != branchID)
                return Unauthorized(new { message = "Bạn không có quyền truy cập danh sách user của chi nhánh này!" });

            var dsUser = dbc.Users
                .Where(t => t.BranchId == branchID && t.UserId != userId)
                .Include(t => t.Role)
                .Include(t => t.Gender)
                .Include(t => t.Branch)
                .Select(t => new
                {
                    t.UserId,
                    fullName = t.FirstName + " " + t.LastName,
                    t.PhoneNumber,
                    birthday = t.BirthDay.ToString("yyyy-MM-dd"),
                    gender = t.Gender.GenderName,
                    role = t.Role.RoleName,
                    t.Picture,
                    CreateAt = t.CreateAt.Value.ToString("yyyy-MM-dd hh:mm:ss tt"),
                    t.BranchId,
                }).ToList();

            if (dsUser == null || dsUser.Count == 0)
                return NotFound(new { message = "Không tìm thấy user nào thuộc chi nhánh này!" });

            return Ok(new { data = dsUser });
        }

        [HttpPost("/User/Login")]
        public IActionResult Login(string username, string password)
        {
            var user = dbc.Users
                .Include(u => u.Gender)
                .Include(u => u.Role)
                .Include(u => u.Branch)
                .FirstOrDefault(u => u.UserName == username);

            if (user == null)
                return Unauthorized(new { message = "Tài khoản không tồn tại!" });
            if (user.Password != password)
                return Unauthorized(new { message = "Sai mật khẩu!" });

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim("UserId", user.UserId.ToString()),
                new Claim(ClaimTypes.Role, user.Role.RoleName)
            };

            var secretKey = _configuration["JwtSettings:SecretKey"];
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: "yourApp",
                audience: "yourAppUsers",
                claims: claims,
                expires: DateTime.Now.AddHours(1),
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new
            {
                message = "Đăng nhập thành công!",
                token = tokenString,
                user = new
                {
                    fullName = user.FirstName + " " + user.LastName,
                    user.PhoneNumber,
                    birthday = user.BirthDay.ToString("yyyy-MM-dd"),
                    gender = user.Gender.GenderName,
                    role = user.Role.RoleName,
                    user.Picture,
                    CreateAt = user.CreateAt.Value.ToString("yyyy-MM-dd hh:mm:ss tt"),
                    user.BranchId,
                }
            });
        }

        [HttpPut("/User/Edit")]
        [Authorize(Policy = "Everyone")]
        public IActionResult EditUser(string firstName, string lastName, string phoneNumber)
        {
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim == null)
                return Unauthorized(new { message = "Không tìm thấy thông tin người dùng trong token!" });

            int userId = int.Parse(userIdClaim.Value);

            var user = dbc.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null)
                return NotFound(new { message = "Người dùng không tồn tại!" });

            user.FirstName = firstName;
            user.LastName = lastName;
            user.PhoneNumber = phoneNumber;

            dbc.SaveChanges();
            return Ok(new { message = "Cập nhật thông tin thành công!" });
        }

        [HttpPost("/User/ChangePassword")]
        [Authorize(Policy = "Everyone")]
        public IActionResult ChangePassword(string oldPassword, string newPassword)
        {
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim == null)
                return Unauthorized(new { message = "Không tìm thấy thông tin người dùng trong token!" });

            int userId = int.Parse(userIdClaim.Value);

            var user = dbc.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null)
                return NotFound(new { message = "Người dùng không tồn tại!" });

            if (user.Password != oldPassword)
                return Unauthorized(new { message = "Mật khẩu cũ không chính xác!" });

            user.Password = newPassword;
            dbc.SaveChanges();
            return Ok(new { message = "Đổi mật khẩu thành công!" });
        }
    }
}
