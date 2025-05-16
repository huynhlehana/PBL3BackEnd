using System;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NhaHang.ModelFromDB;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace NhaHang.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "Management")]
    public class StatisticsController : ControllerBase
    {
        private readonly quanlynhahang dbc;
        public StatisticsController(quanlynhahang db)
        {
            dbc = db;
        }

        [HttpGet]
        [Route("/Statistics/TableUtilizationRate")]
        public IActionResult ThongKeTiLeSuDungBan(int branchId)
        {
            var userIdClaim = User.FindFirst("UserId");
            var roleClaim = User.FindFirst(ClaimTypes.Role);
            var userId = int.Parse(userIdClaim.Value);
            var role = roleClaim?.Value;

            var currentUser = dbc.Users.FirstOrDefault(u => u.UserId == userId);
            if (currentUser == null)
                return Unauthorized(new { message = "Không xác định được người dùng!" });

            if (role != "Quản lý tổng" && currentUser.BranchId != branchId)
                return Unauthorized(new { message = "Bạn không có quyền xem thống kê của chi nhánh này!" });

            var totalTables = dbc.Tables.Count(t => t.BranchId == branchId);
            if (totalTables == 0)
                return NotFound(new { message = "Chi nhánh không có bàn nào!" });

            var allHours = Enumerable.Range(6, 16).Where(h => h % 2 == 0).ToList();
            var allDays = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().ToList();
            var allTimeSlots = allDays
                .SelectMany(day => allHours, (day, hour) => Tuple.Create(hour, day))
                .ToList();

            var usageData = dbc.Bills
                .Where(b => b.Table.BranchId == branchId && b.Created.HasValue)
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
                        //TimeRange = timeRange,
                        DayOfWeek = day.ToString(),
                        //SoBanSuDung = usedCount,
                        //TongBan = totalTables,
                        TiLeSuDung = Math.Round((double)usedCount * 100 / totalTables, 2),
                        SortKey = (int)day
                    };
                })
                .OrderBy(x => x.SortKey)
                .ThenBy(x => x.Hour)
                .Select(x => new
                {
                    x.Hour,
                    //x.TimeRange,
                    x.DayOfWeek,
                    //x.SoBanSuDung,
                    //x.TongBan,
                    x.TiLeSuDung
                })
                .ToList();

            return Ok(usageStats);
        }

        [HttpGet]
        [Route("/Statistics/TopFoods")]
        public IActionResult ThongKeTop10MonAnBanChay(int branchID)
        {
            var userIdClaim = User.FindFirst("UserId");
            var roleClaim = User.FindFirst(ClaimTypes.Role);
            var userId = int.Parse(userIdClaim.Value);
            var role = roleClaim?.Value;

            var currentUser = dbc.Users.FirstOrDefault(u => u.UserId == userId);
            if (currentUser == null)
                return Unauthorized(new { message = "Không xác định được người dùng!" });

            if (role != "Quản lý tổng" && currentUser.BranchId != branchID)
                return Unauthorized(new { message = "Bạn không có quyền xem thống kê của chi nhánh này!" });

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
                             && bi.Bill.PaidDate.Value <= today
                             && bi.Bill.BranchId == branchID)
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
        [Route("/Statistics/BottomFoods")]
        public IActionResult ThongKeTop10MonAnItBanNhat(int branchID)
        {
            var userIdClaim = User.FindFirst("UserId");
            var roleClaim = User.FindFirst(ClaimTypes.Role);
            var userId = int.Parse(userIdClaim.Value);
            var role = roleClaim?.Value;

            var currentUser = dbc.Users.FirstOrDefault(u => u.UserId == userId);
            if (currentUser == null)
                return Unauthorized(new { message = "Không xác định được người dùng!" });

            if (role != "Quản lý tổng" && currentUser.BranchId != branchID)
                return Unauthorized(new { message = "Bạn không có quyền xem thống kê của chi nhánh này!" });

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
                             && bi.Bill.PaidDate.Value <= today
                             && bi.Bill.BranchId == branchID)
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
        public enum TimeRange
        {
            SevenDays = 1,
            TwelveWeek = 2,
            TwelveMonths = 3,
            FiveYears = 4
        }

        [HttpGet]
        [Route("/Statistics/FoodRevenue")]
        public IActionResult ThongKeDoanhThuTheoMonAn(int branchID, TimeRange range)
        {
            var userIdClaim = User.FindFirst("UserId");
            var roleClaim = User.FindFirst(ClaimTypes.Role);
            var userId = int.Parse(userIdClaim.Value);
            var role = roleClaim?.Value;

            var currentUser = dbc.Users.FirstOrDefault(u => u.UserId == userId);
            if (currentUser == null)
                return Unauthorized(new { message = "Không xác định được người dùng!" });

            if (role != "Quản lý tổng" && currentUser.BranchId != branchID)
                return Unauthorized(new { message = "Bạn không có quyền xem thống kê của chi nhánh này!" });

            DateTime today = DateTime.Now;
            DateTime startDate = range switch
            {
                TimeRange.SevenDays => today.AddDays(-6),
                TimeRange.TwelveWeek => today.AddDays(-83),
                TimeRange.TwelveMonths => today.AddMonths(-11),
                TimeRange.FiveYears => today.AddYears(-4),
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
                             && bi.Bill.PaidDate.Value <= today
                             && bi.Bill.BranchId == branchID)
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
        [Route("/Statistics/FoodCategoryRevenue")]
        public IActionResult ThongKeDoanhThuTheoDanhMuc(int branchID, TimeRange range)
        {
            var userIdClaim = User.FindFirst("UserId");
            var roleClaim = User.FindFirst(ClaimTypes.Role);
            var userId = int.Parse(userIdClaim.Value);
            var role = roleClaim?.Value;

            var currentUser = dbc.Users.FirstOrDefault(u => u.UserId == userId);
            if (currentUser == null)
                return Unauthorized(new { message = "Không xác định được người dùng!" });

            if (role != "Quản lý tổng" && currentUser.BranchId != branchID)
                return Unauthorized(new { message = "Bạn không có quyền xem thống kê của chi nhánh này!" });

            DateTime today = DateTime.Now;
            DateTime startDate = range switch
            {
                TimeRange.SevenDays => today.AddDays(-6),
                TimeRange.TwelveWeek => today.AddDays(-83),
                TimeRange.TwelveMonths => today.AddMonths(-11),
                TimeRange.FiveYears => today.AddYears(-4),
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
                             && bi.Bill.PaidDate.Value <= today
                             && bi.Bill.BranchId == branchID)
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
        [Route("/Statistics/Revenue")]
        public IActionResult ThongKeDoanhThu(int branchID, TimeRange range)
        {
            var userIdClaim = User.FindFirst("UserId");
            var roleClaim = User.FindFirst(ClaimTypes.Role);
            var userId = int.Parse(userIdClaim.Value);
            var role = roleClaim?.Value;

            var currentUser = dbc.Users.FirstOrDefault(u => u.UserId == userId);
            if (currentUser == null)
                return Unauthorized(new { message = "Không xác định được người dùng!" });

            if (role != "Quản lý tổng" && currentUser.BranchId != branchID)
                return Unauthorized(new { message = "Bạn không có quyền xem thống kê của chi nhánh này!" });

            DateTime today = DateTime.Now;
            DateTime startDate = range switch
            {
                TimeRange.SevenDays => today.AddDays(-6),
                TimeRange.TwelveWeek => today.AddDays(-83),
                TimeRange.TwelveMonths => today.AddMonths(-11),
                TimeRange.FiveYears => today.AddYears(-4),
                _ => today
            };

            var doanhThu = dbc.BillItems
                .Include(bi => bi.Bill)
                .Include(bi => bi.Food)
                .Where(bi => bi.Bill.PaidDate != null
                             && bi.Bill.PaidDate.Value >= startDate
                             && bi.Bill.PaidDate.Value <= today
                             && bi.Bill.BranchId == branchID)
                .AsEnumerable();

            List<object> result = range switch
            {
                TimeRange.SevenDays =>
                    Enumerable.Range(0, 7)
                        .Select(i => startDate.AddDays(i))
                        .Select(date => new
                        {
                            Ngay = date.ToString("dddd"), 
                            TongDoanhThu = doanhThu
                                .Where(bi => bi.Bill.PaidDate.Value.Date == date.Date)
                                .Sum(x => x.Quantity * x.Food.Price)
                        }).ToList<object>(),

                TimeRange.TwelveWeek =>
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

                TimeRange.TwelveMonths =>
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

                TimeRange.FiveYears =>
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

        [HttpGet]
        [Route("/Statistics/RevenueByBranch")]
        public IActionResult ThongKeTongDoanhThuTheoChiNhanh(TimeRange range)
        {
            DateTime today = DateTime.Now;
            DateTime startDate = range switch
            {
                TimeRange.SevenDays => today.AddDays(-6),
                TimeRange.TwelveWeek => today.AddDays(-83),
                TimeRange.TwelveMonths => today.AddMonths(-11),
                TimeRange.FiveYears => today.AddYears(-4),
                _ => today
            };

            var allBranches = dbc.Branches.ToList();

            var billItems = dbc.BillItems
                .Include(bi => bi.Bill)
                .Include(bi => bi.Food)
                .Where(bi => bi.Bill.PaidDate != null
                             && bi.Bill.PaidDate.Value >= startDate
                             && bi.Bill.PaidDate.Value <= today)
                .ToList();

            var result = allBranches
                .Select(branch =>
                {
                    var doanhThu = billItems
                        .Where(bi => bi.Bill.BranchId == branch.BranchId)
                        .Sum(bi => bi.Quantity * (bi.Food?.Price ?? 0));

                    return new
                    {
                        BranchId = branch.BranchId,
                        ChiNhanh = branch.BranchName,
                        TongDoanhThu = doanhThu
                    };
                })
                .ToList();

            return Ok(result);
        }
    }
}
