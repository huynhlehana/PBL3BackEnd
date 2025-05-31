using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NhaHang.ModelFromDB;
using Microsoft.EntityFrameworkCore;
using static NhaHang.Controllers.StatisticsController;

namespace NhaHang.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly quanlynhahang dbc;
        public AdminController(quanlynhahang db)
        {
            dbc = db;
        }

        [HttpPost]
        [Authorize(Policy = "Management")]
        [Route("/Admin/Users/Create")]
        public IActionResult TaoTaiKhoan(string username, string password, string fistname, string lastname, string phone, DateOnly birth, int genderId, int roleId, IFormFile? AnhUpload, int branchId)
        {
            var currentUserId = int.Parse(User.FindFirst("UserId").Value);
            var currentUser = dbc.Users.FirstOrDefault(u => u.UserId == currentUserId);
            if (currentUser == null)
                return Unauthorized(new { message = "Không xác định được người dùng!" });

            if (dbc.Users.Any(u => u.UserName == username || u.PhoneNumber == phone))
                return Conflict(new { message = "Tên đăng nhập hoặc số điện thoại đã tồn tại!" });

            string? imagePath = null;

            if (AnhUpload != null && AnhUpload.Length > 0)
            {
                var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "users");
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(AnhUpload.FileName);
                var fullPath = Path.Combine(folderPath, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    AnhUpload.CopyTo(stream);
                }

                imagePath = Path.Combine("images", "users", fileName).Replace("\\", "/");
            }

            var newUser = new User
            {
                UserName = username,
                Password = password,
                FirstName = fistname,
                LastName = lastname,
                PhoneNumber = phone,
                BirthDay = birth,
                GenderId = genderId,
                RoleId = roleId,
                Picture = imagePath,
                CreateAt = DateTime.Now,
                BranchId = currentUser.RoleId == 2 ? currentUser.BranchId : branchId
            };

            if (currentUser.RoleId == 1)
            {
                if (roleId != 2 && roleId != 3)
                    return BadRequest(new { message = "Chỉ được tạo quản lý chi nhánh hoặc nhân viên!" });
            }
            else if (currentUser.RoleId == 2)
            {
                if (roleId != 3)
                    return Unauthorized(new { message = "Bạn chỉ được tạo tài khoản nhân viên!" });
            }
            else
            {
                return Unauthorized(new { message = "Bạn không có quyền tạo tài khoản!" });
            }

            dbc.Users.Add(newUser);
            dbc.SaveChanges();

            return Ok(new { message = "Tạo tài khoản thành công!", data = newUser });
        }

        [HttpPut]
        [Authorize(Policy = "Management")]
        [Route("/Admin/Users/Update")]
        public IActionResult CapNhatTaiKhoan(int id, string? username, string? password, string? firstname, string? lastname, string? phone, DateOnly? birth, int? genderId, int? roleId, IFormFile? AnhUpload, int? branchId)
        {
            var user = dbc.Users.Find(id);
            if (user == null)
                return NotFound(new { message = "Tài khoản không tồn tại!" });

            var currentUserId = int.Parse(User.FindFirst("UserId").Value);
            var currentUser = dbc.Users.FirstOrDefault(u => u.UserId == currentUserId);
            if (currentUser == null)
                return Unauthorized(new { message = "Không xác định được người dùng!" });

            if (!string.IsNullOrEmpty(username) && dbc.Users.Any(u => u.UserName == username && u.UserId != id))
                return Conflict(new { message = "Tên đăng nhập đã tồn tại!" });

            if (!string.IsNullOrEmpty(phone) && dbc.Users.Any(u => u.PhoneNumber == phone && u.UserId != id))
                return Conflict(new { message = "Số điện thoại đã tồn tại!" });

            if (AnhUpload != null && AnhUpload.Length > 0)
            {
                var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "users");
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(AnhUpload.FileName);
                var fullPath = Path.Combine(folderPath, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    AnhUpload.CopyTo(stream);
                }

                user.Picture = Path.Combine("images", "users", fileName).Replace("\\", "/");
            }

            user.UserName = username ?? user.UserName;
            user.Password = password ?? user.Password;
            user.FirstName = firstname ?? user.FirstName;
            user.LastName = lastname ?? user.LastName;
            user.PhoneNumber = phone ?? user.PhoneNumber;
            user.BirthDay = birth ?? user.BirthDay;
            user.GenderId = genderId ?? user.GenderId;
            
            if (currentUser.RoleId == 1)
            {
                if (roleId != null && (roleId != 2 && roleId != 3))
                    return BadRequest(new { message = "Chỉ được phân quyền quản lý hoặc nhân viên!" });

                if (branchId != null)
                    user.BranchId = branchId.Value;

                if (roleId != null)
                    user.RoleId = roleId.Value;
            }
            else if (currentUser.RoleId == 2)
            {
                if (roleId != null && roleId != 3)
                    return Unauthorized(new { message = "Bạn chỉ được cập nhật vai trò nhân viên!" });

                user.BranchId = currentUser.BranchId;
                user.RoleId = 3;
            }
            else
            {
                return Unauthorized(new { message = "Bạn không có quyền cập nhật tài khoản!" });
            }

            dbc.Users.Update(user);
            dbc.SaveChanges();

            return Ok(new { message = "Cập nhật tài khoản thành công!", data = user });
        }

        [HttpDelete]
        [Authorize(Policy = "Management")]
        [Route("/Admin/Users/Delete")]
        public IActionResult XoaTaiKhoan(int id)
        {
            var user = dbc.Users.Find(id);
            if (user == null)
                return NotFound(new { message = "Tài khoản không tồn tại!" });

            var currentUserId = int.Parse(User.FindFirst("UserId").Value);
            var currentUser = dbc.Users.FirstOrDefault(u => u.UserId == currentUserId);
            if (currentUser == null)
                return Unauthorized(new { message = "Không xác định được người dùng!" });

            if (currentUser.RoleId == 2)
            {
                if (user.RoleId != 3 || user.BranchId != currentUser.BranchId)
                    return Unauthorized(new { message = "Bạn chỉ được xoá nhân viên thuộc chi nhánh của bạn!" });
            }
            else if (currentUser.RoleId != 1)
            {
                return Unauthorized(new { message = "Bạn không có quyền xoá tài khoản!" });
            }

            dbc.Users.Remove(user);
            dbc.SaveChanges();

            return Ok(new { message = "Xoá tài khoản thành công!" });
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        [Route("/Admin/Staff")]
        public IActionResult LayDanhSachNhanVien()
        {
            var userIdClaim = User.FindFirst("UserId");
            var roleClaim = User.FindFirst(ClaimTypes.Role);
            var userId = int.Parse(userIdClaim.Value);
            var role = roleClaim?.Value;

            var currentUser = dbc.Users.FirstOrDefault(u => u.UserId == userId);
            if (currentUser == null)
                return Unauthorized(new { message = "Không xác định được người dùng!" });

            var dsUser = dbc.Users
                .Where(t => t.UserId != userId && t.Role.RoleId == 2 || t.Role.RoleId == 3)
                .Include(t => t.Role)
                .Include(t => t.Gender)
                .Include(t => t.Branch)
                .Select(t => new
                {
                    t.UserId,
                    fullName = t.FirstName + " " + t.LastName,
                    t.PhoneNumber,
                    birthDay = t.BirthDay.ToString("yyyy-MM-dd"),
                    gender = t.Gender.GenderName,
                    role = t.Role.RoleName,
                    t.Picture,
                    CreateAt = t.CreateAt.Value.ToString("yyyy-MM-dd hh:mm:ss tt"),
                    t.BranchId,
                }).ToList();

            if (dsUser == null || dsUser.Count == 0)
                return NotFound(new { message = "Không tìm thấy nhân viên nào!" });

            return Ok(new { data = dsUser });
        }

        [HttpGet]
        [Authorize(Policy = "Management")]
        [Route("/Admin/StaffByBranch")]
        public IActionResult LayDanhSachNhanVienTheoChiNhanh(int branchID)
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
                .Where(t => t.BranchId == branchID && t.UserId != userId && t.Role.RoleId == 3)
                .Include(t => t.Role)
                .Include(t => t.Gender)
                .Include(t => t.Branch)
                .Select(t => new
                {
                    t.UserId,
                    fullName = t.FirstName + " " + t.LastName,
                    t.PhoneNumber,
                    birthDay = t.BirthDay.ToString("yyyy-MM-dd"),
                    gender = t.Gender.GenderName,
                    role = t.Role.RoleName,
                    t.Picture,
                    CreateAt = t.CreateAt.Value.ToString("yyyy-MM-dd hh:mm:ss tt"),
                    t.BranchId,
                }).ToList();

            if (dsUser == null || dsUser.Count == 0)
                return NotFound(new { message = "Không tìm thấy nhân viên nào thuộc chi nhánh này!" });

            return Ok(new { data = dsUser });
        }

        [HttpGet]
        [Authorize(Policy = "Management")]
        [Route("/Admin/StaffByID")]
        public IActionResult LayChiTietNhanVien(int id)
        {
            var userIdClaim = User.FindFirst("UserId");
            var roleClaim = User.FindFirst(ClaimTypes.Role);

            if (userIdClaim == null || roleClaim == null)
                return Unauthorized(new { message = "Thông tin xác thực không hợp lệ!" });

            int currentUserId = int.Parse(userIdClaim.Value);
            string currentUserRole = roleClaim.Value;

            var currentUser = dbc.Users.FirstOrDefault(u => u.UserId == currentUserId);
            if (currentUser == null)
                return Unauthorized(new { message = "Không xác định được người dùng!" });

            var user = dbc.Users
                .Include(u => u.Role)
                .Include(u => u.Gender)
                .Include(u => u.Branch)
                .FirstOrDefault(u => u.UserId == id);

            if (user == null)
                return NotFound(new { message = "Không tìm thấy nhân viên!" });

            if (currentUserRole != "Quản lý tổng" && currentUser.BranchId != user.BranchId)
            {
                return Unauthorized(new { message = "Bạn không có quyền truy cập thông tin nhân viên này!" });
            }

            var data = new
            {
                user.UserId,
                user.UserName,
                user.Password,
                user.FirstName,
                user.LastName,
                fullName = user.FirstName + " " + user.LastName,
                user.PhoneNumber,
                birthDay = user.BirthDay.ToString("yyyy-MM-dd"),
                gender = user.Gender?.GenderName,
                genderId = user.GenderId,
                role = user.Role?.RoleName,
                roleId = user.RoleId,
                user.Picture,
                CreateAt = user.CreateAt?.ToString("yyyy-MM-dd hh:mm:ss tt"),
                user.BranchId,
                branch = user.Branch?.BranchName
            };

            return Ok(new { data });
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        [Route("/Admin/Table")]
        public IActionResult LayDanhSachBan()
        {
            var dsBan = dbc.Tables
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
                return NotFound(new { message = "Không tìm thấy bàn nào thuộc nhà hàng này!" });

            return Ok(new { data = dsBan });
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        [Route("/Admin/Statistics/TableUtilizationRate")]
        public IActionResult ThongKeTiLeSuDungBan()
        {
            var totalTables = dbc.Tables.Count();

            var allHours = Enumerable.Range(6, 16).Where(h => h % 2 == 0).ToList();
            var allDays = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().ToList();
            var allTimeSlots = allDays
                .SelectMany(day => allHours, (day, hour) => Tuple.Create(hour, day))
                .ToList();

            var usageData = dbc.Bills
                .Where(b => b.Created.HasValue)
                .AsEnumerable()
                .GroupBy(b =>
                {
                    var time = b.Created.Value;
                    int hourSlot = (time.Hour / 2) * 2;
                    Console.WriteLine($"Hóa đơn {b.BillId}: Thời gian tạo {time}, Xếp vào khung giờ {hourSlot}:00 - {hourSlot + 1}:59");
                    return Tuple.Create(hourSlot, time.DayOfWeek);
                })
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(b => b.TableId).Distinct().Count()
                );

            var usageStats = allTimeSlots
                .Select(slot =>
                {
                    usageData.TryGetValue(slot, out int usedCount);

                    int hour = slot.Item1;
                    DayOfWeek day = slot.Item2;
                    string timeRange = $"{hour:00}:00 - {hour + 1:00}:59";

                    return new
                    {
                        Hour = hour,
                        DayOfWeek = day.ToString(),
                        TiLeSuDung = Math.Round((double)usedCount * 100 / totalTables, 2),
                        SortKey = (int)day
                    };
                })
                .OrderBy(x => x.SortKey)
                .ThenBy(x => x.Hour)
                .Select(x => new
                {
                    x.Hour,
                    x.DayOfWeek,
                    x.TiLeSuDung
                })
                .ToList();

            return Ok(usageStats);
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        [Route("/Admin/Statistics/TopFoods")]
        public IActionResult ThongKeTop10MonAnBanChay()
        {
            DateTime today = DateTime.Now;
            DateTime thirtyDaysAgo = today.AddDays(-30);

            var allFoods = dbc.Foods
                .Select(f => new
                {
                    f.FoodId,
                    f.FoodName,
                    f.Picture
                })
                .ToList();

            var foodSales = dbc.BillItems
                .Include(bi => bi.Bill)
                .Where(bi => bi.Bill.PaidDate != null
                             && bi.Bill.PaidDate.Value >= thirtyDaysAgo
                             && bi.Bill.PaidDate.Value <= today)
                .GroupBy(bi => bi.FoodId)
                .Select(g => new
                {
                    FoodId = g.Key,
                    SoLuong = g.Sum(x => x.Quantity)
                })
                .ToList(); ;

            var result = allFoods
                .GroupJoin(
                    foodSales,
                    f => f.FoodId,
                    s => s.FoodId,
                    (f, sales) => new
                    {
                        f.FoodId,
                        f.FoodName,
                        f.Picture,
                        SoLuong = sales.FirstOrDefault()?.SoLuong ?? 0
                    })
                .OrderByDescending(x => x.SoLuong)
                .Take(10)
                .ToList();

            return Ok(result);
        }

        [HttpGet]
        [Route("/Admin/Statistics/BottomFoods")]
        public IActionResult ThongKeTop10MonAnItBanNhat()
        {
            DateTime today = DateTime.Now;
            DateTime thirtyDaysAgo = today.AddDays(-30);

            var allFoods = dbc.Foods
                .Select(f => new
                {
                    f.FoodId,
                    f.FoodName,
                    f.Picture
                })
                .ToList();

            var foodSales = dbc.BillItems
                .Include(bi => bi.Bill)
                .Where(bi => bi.Bill.PaidDate != null
                             && bi.Bill.PaidDate.Value >= thirtyDaysAgo
                             && bi.Bill.PaidDate.Value <= today)
                .GroupBy(bi => bi.FoodId)
                .Select(g => new
                {
                    FoodId = g.Key,
                    SoLuong = g.Sum(x => x.Quantity)
                })
                .ToList();

            var result = allFoods
                .GroupJoin(
                    foodSales,
                    f => f.FoodId,
                    s => s.FoodId,
                    (f, sales) => new
                    {
                        f.FoodId,
                        f.FoodName,
                        f.Picture,
                        SoLuong = sales.FirstOrDefault()?.SoLuong ?? 0
                    })
                .OrderBy(x => x.SoLuong)
                .Take(10)
                .ToList();

            return Ok(result);
        }
        public enum TimeRange1
        {
            SevenDays = 1,
            TwelveWeek = 2,
            TwelveMonths = 3,
            FiveYears = 4
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        [Route("/Admin/Statistics/FoodRevenue")]
        public IActionResult ThongKeDoanhThuTheoMonAn(TimeRange1 range)
        {
            DateTime today = DateTime.Now;
            DateTime startDate = range switch
            {
                TimeRange1.SevenDays => today.AddDays(-6),
                TimeRange1.TwelveWeek => today.AddDays(-83),
                TimeRange1.TwelveMonths => today.AddMonths(-11),
                TimeRange1.FiveYears => today.AddYears(-4),
                _ => today
            };

            var allFoods = dbc.Foods
                .Select(f => new
                {
                    f.FoodId,
                    f.FoodName,
                })
                .ToList();

            var foodRevenues = dbc.BillItems
                .Include(bi => bi.Bill)
                .Where(bi => bi.Bill.PaidDate != null
                             && bi.Bill.PaidDate.Value >= startDate
                             && bi.Bill.PaidDate.Value <= today)
                .GroupBy(bi => bi.FoodId)
                .Select(g => new
                {
                    FoodId = g.Key,
                    TongSoLuong = g.Sum(x => x.Quantity),
                    TongDoanhThu = g.Sum(x => x.Quantity * x.Food.Price)
                })
                .ToList();

            var result = allFoods
                .GroupJoin(
                    foodRevenues,
                    f => f.FoodId,
                    r => r.FoodId,
                    (f, revenues) => new
                    {
                        f.FoodId,
                        f.FoodName,
                        SoLuong = revenues.FirstOrDefault()?.TongSoLuong ?? 0,
                        DoanhThu = revenues.FirstOrDefault()?.TongDoanhThu ?? 0
                    })
                .OrderByDescending(x => x.DoanhThu)
                .ToList();

            return Ok(result);
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        [Route("/Admin/Statistics/FoodCategoryRevenue")]
        public IActionResult ThongKeDoanhThuTheoDanhMuc(TimeRange1 range)
        {
            DateTime today = DateTime.Now;
            DateTime startDate = range switch
            {
                TimeRange1.SevenDays => today.AddDays(-6),
                TimeRange1.TwelveWeek => today.AddDays(-83),
                TimeRange1.TwelveMonths => today.AddMonths(-11),
                TimeRange1.FiveYears => today.AddYears(-4),
                _ => today
            };

            var categories = dbc.Categories
                .Select(c => new
                {
                    c.CategoryId,
                    c.CategoryName
                })
                .ToList();

            var foodSales = dbc.BillItems
                .Include(bi => bi.Bill)
                .Where(bi => bi.Bill.PaidDate != null
                             && bi.Bill.PaidDate.Value >= startDate
                             && bi.Bill.PaidDate.Value <= today)
                .GroupBy(bi => bi.Food.CategoryId)
                .Select(g => new
                {
                    CategoryId = g.Key,
                    TongSoLuong = g.Sum(x => x.Quantity),
                    TongDoanhThu = g.Sum(x => x.Quantity * x.Food.Price)
                })
                .ToList();

            var result = categories
                .GroupJoin(
                    foodSales,
                    c => c.CategoryId,
                    s => s.CategoryId,
                    (c, sales) => new
                    {
                        CategoryId = c.CategoryId,
                        CategoryName = c.CategoryName,
                        TongSoLuong = sales.FirstOrDefault()?.TongSoLuong ?? 0,
                        TongDoanhThu = sales.FirstOrDefault()?.TongDoanhThu ?? 0
                    })
                .OrderByDescending(x => x.TongDoanhThu)
                .ToList();

            return Ok(result);
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        [Route("/Admin/Statistics/Revenue")]
        public IActionResult ThongKeDoanhThu(TimeRange1 range)
        {
            DateTime today = DateTime.Now;
            DateTime startDate = range switch
            {
                TimeRange1.SevenDays => today.AddDays(-6),
                TimeRange1.TwelveWeek => today.AddDays(-83),
                TimeRange1.TwelveMonths => today.AddMonths(-11),
                TimeRange1.FiveYears => today.AddYears(-4),
                _ => today
            };

            var doanhThu = dbc.BillItems
                .Include(bi => bi.Bill)
                .Include(bi => bi.Food)
                .Where(bi => bi.Bill.PaidDate != null
                             && bi.Bill.PaidDate.Value >= startDate
                             && bi.Bill.PaidDate.Value <= today)
                .AsEnumerable();

            List<object> result = range switch
            {
                TimeRange1.SevenDays =>
                    Enumerable.Range(0, 7)
                        .Select(i => startDate.AddDays(i))
                        .Select(date => new
                        {
                            Ngay = date.ToString("dddd"),
                            TongDoanhThu = doanhThu
                                .Where(bi => bi.Bill.PaidDate.Value.Date == date.Date)
                                .Sum(x => x.Quantity * x.Food.Price)
                        }).ToList<object>(),

                TimeRange1.TwelveWeek =>
                    Enumerable.Range(0, 12)
                        .Select(weekOffset =>
                        {
                            var weekStart = startDate.AddDays(weekOffset * 7);
                            if (weekStart.DayOfWeek != DayOfWeek.Monday)
                            {
                                int offset = ((int)weekStart.DayOfWeek + 6) % 7;
                                weekStart = weekStart.AddDays(-offset);
                            }
                            var weekEnd = weekStart.AddDays(6);

                            return new
                            {
                                Ngay = weekEnd.ToString("dd/MM"),
                                TongDoanhThu = doanhThu
                                    .Where(bi => bi.Bill.PaidDate.Value.Date >= weekStart.Date
                                              && bi.Bill.PaidDate.Value.Date <= weekEnd.Date)
                                    .Sum(x => x.Quantity * x.Food.Price)
                            };
                        }).ToList<object>(),

                TimeRange1.TwelveMonths =>
                    Enumerable.Range(0, 12)
                        .Select(i => startDate.AddMonths(i))
                        .Distinct()
                        .OrderBy(m => m)
                        .Select(date => new
                        {
                            Ngay = $"{date.Month}/{date.Year}",
                            TongDoanhThu = doanhThu
                                .Where(bi => bi.Bill.PaidDate.Value.Month == date.Month
                                             && bi.Bill.PaidDate.Value.Year == date.Year)
                                .Sum(x => x.Quantity * x.Food.Price)
                        }).ToList<object>(),

                TimeRange1.FiveYears =>
                    Enumerable.Range(0, 5)
                        .Select(i => today.AddYears(-i).Year)
                        .OrderBy(y => y)
                        .Select(year => new
                        {
                            Ngay = year.ToString(),
                            TongDoanhThu = doanhThu
                                .Where(bi => bi.Bill.PaidDate.Value.Year == year)
                                .Sum(x => x.Quantity * x.Food.Price)
                        }).ToList<object>(),

                _ => new List<object>()
            };

            return Ok(result);
        }
    }
}
