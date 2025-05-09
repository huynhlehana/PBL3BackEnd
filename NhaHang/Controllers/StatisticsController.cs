using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NhaHang.ModelFromDB;

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
            var totalTables = dbc.Tables.Count(t => t.BranchId == branchId);
            if (totalTables == 0)
                return NotFound(new { message = "Chi nhánh không có bàn nào!" });

            var allHours = Enumerable.Range(0, 24).Where(h => h % 2 == 0).ToList();
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
                        TimeRange = timeRange,
                        DayOfWeek = day.ToString(),
                        SoBanSuDung = usedCount,
                        TongBan = totalTables,
                        TiLeSuDung = Math.Round((double)usedCount * 100 / totalTables, 2),
                        SortKey = (int)day
                    };
                })
                .OrderBy(x => x.SortKey)
                .ThenBy(x => x.Hour)
                .Select(x => new
                {
                    x.Hour,
                    x.TimeRange,
                    x.DayOfWeek,
                    x.SoBanSuDung,
                    x.TongBan,
                    x.TiLeSuDung
                })
                .ToList();

            return Ok(usageStats);
        }

        [HttpGet]
        [Route("/Statistics/TopFoods")]
        public IActionResult ThongKeTop10MonAnBanChay(int branchID, int month, int year)
        {
            var allFoods = dbc.Foods
                .Select(f => new
                {
                    f.FoodId,
                    f.FoodName
                })
                .ToList();

            var foodSales = dbc.BillItems
                .Include(bi => bi.Bill)
                .Where(bi => bi.Bill.PaidDate != null
                             && bi.Bill.PaidDate.Value.Month == month
                             && bi.Bill.PaidDate.Value.Year == year
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
                        SoLuong = sales.FirstOrDefault()?.SoLuong ?? 0
                    })
                .OrderByDescending(x => x.SoLuong) 
                .Take(10)
                .ToList();

            return Ok(result);
        }

        [HttpGet]
        [Route("/Statistics/BottomFoods")]
        public IActionResult ThongKeTop10MonAnItBanNhat(int branchID, int month, int year)
        {
            var allFoods = dbc.Foods
                .Select(f => new
                {
                    f.FoodId,
                    f.FoodName
                })
                .ToList();

            var foodSales = dbc.BillItems
                .Include(bi => bi.Bill)
                .Where(bi => bi.Bill.PaidDate != null
                             && bi.Bill.PaidDate.Value.Month == month
                             && bi.Bill.PaidDate.Value.Year == year
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
                        SoLuong = sales.FirstOrDefault()?.SoLuong ?? 0
                    })
                .OrderBy(x => x.SoLuong)
                .Take(10)
                .ToList();

            return Ok(result);
        }

        [HttpGet]
        [Route("/Statistics/FoodRevenue")]
        public IActionResult ThongKeDoanhThuTheoMonAn(int branchID, int month, int year)
        {
            var allFoods = dbc.Foods
                .Select(f => new
                {
                    f.FoodId,
                    f.FoodName
                })
                .ToList();

            var foodRevenues = dbc.BillItems
                .Include(bi => bi.Bill)
                .Where(bi => bi.Bill.PaidDate != null
                             && bi.Bill.PaidDate.Value.Month == month
                             && bi.Bill.PaidDate.Value.Year == year
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
        public IActionResult ThongKeDoanhThuTheoDanhMuc(int branchID, int month, int year)
        {
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
                             && bi.Bill.PaidDate.Value.Month == month
                             && bi.Bill.PaidDate.Value.Year == year
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
        [Route("/Statistics/DailyRevenue")]
        public IActionResult ThongKeDoanhThuTheoNgay(int branchID, int month, int year)
        {
            var daysInMonth = Enumerable.Range(1, DateTime.DaysInMonth(year, month))
                .Select(day => new DateTime(year, month, day))
                .ToList();

            var doanhThuTheoNgay = dbc.BillItems
                .Include(bi => bi.Bill)
                .Include(bi => bi.Food)
                .Where(bi => bi.Bill.PaidDate != null
                             && bi.Bill.PaidDate.Value.Month == month
                             && bi.Bill.PaidDate.Value.Year == year
                             && bi.Bill.BranchId == branchID)
                .AsEnumerable()
                .GroupBy(bi => bi.Bill.PaidDate.Value.Date)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(x => x.Quantity * x.Food.Price)
                );

            var result = daysInMonth.Select(date => new
            {
                Ngay = date.ToString("yyyy-MM-dd"),
                TongDoanhThu = doanhThuTheoNgay.ContainsKey(date) ? doanhThuTheoNgay[date] : 0
            })
            .ToList();

            return Ok(result);
        }

        [HttpGet]
        [Route("/Statistics/MonthlyRevenue")]
        public IActionResult ThongKeDoanhThuTheoThang(int branchID, int year)
        {
            var doanhThuTheoThang = dbc.BillItems
                .Include(bi => bi.Bill)
                .Include(bi => bi.Food)
                .Where(bi => bi.Bill.PaidDate != null
                             && bi.Bill.PaidDate.Value.Year == year
                             && bi.Bill.BranchId == branchID)
                .AsEnumerable()
                .GroupBy(bi => bi.Bill.PaidDate.Value.Month)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(x => x.Quantity * x.Food.Price)
                );

            var result = Enumerable.Range(1, 12)
                .Select(month => new
                {
                    Thang = month,
                    TongDoanhThu = doanhThuTheoThang.ContainsKey(month) ? doanhThuTheoThang[month] : 0
                })
                .ToList();

            return Ok(result);
        }

        [HttpGet]
        [Route("/Statistics/RevenueByBranch")]
        public IActionResult ThongKeTongDoanhThuTheoChiNhanh(int month, int year)
        {
            var allBranches = dbc.Branches.ToList();

            var billItems = dbc.BillItems
                .Include(bi => bi.Bill)
                .Include(bi => bi.Food)
                .Where(bi => bi.Bill.PaidDate != null
                           && bi.Bill.PaidDate.Value.Month == month
                           && bi.Bill.PaidDate.Value.Year == year)
                .ToList(); // tránh Lazy Loading lỗi

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
                .OrderByDescending(r => r.TongDoanhThu)
                .ToList();

            return Ok(result);
        }
    }
}
