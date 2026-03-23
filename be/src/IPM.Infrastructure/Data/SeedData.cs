using IPM.Domain.Entities;
using IPM.Domain.Enums;
using IPM.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IPM.Infrastructure.Data;

public static class SeedData
{
    public static async Task SeedAsync(ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        // Seed Roles
        await SeedRolesAsync(roleManager);

        // Seed Users and Members
        var members = await SeedUsersAndMembersAsync(context, userManager);

        // Seed Resources
        var resources = await SeedResourcesAsync(context);

        // Seed Projects
        var projects = await SeedProjectsAsync(context, members);

        // Seed Tasks
        await SeedTasksAsync(context, projects, members);

        // Seed Bookings
        await SeedBookingsAsync(context, members, resources);

        // Seed News
        await SeedNewsAsync(context);

        // Seed Wallet Transactions
        await SeedWalletTransactionsAsync(context, members);

        await context.SaveChangesAsync();
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        string[] roles = { "Admin", "Accountant", "ProjectManager", "Subcontractor", "Client" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    private static async Task<Dictionary<string, Member>> SeedUsersAndMembersAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        var members = new Dictionary<string, Member>();

        if (await context.Members.AnyAsync())
            return await context.Members.ToDictionaryAsync(m => m.Email, m => m);

        var usersData = new[]
        {
            // Admin - main test account
            new { Email = "admin@ipm.vn", FullName = "Trần Minh Quân", Phone = "0901234567", Role = "Admin", Elo = 1500.0, Balance = 50000000m, Password = "Admin@123" },

            // Accountant - main test account
            new { Email = "accountant@ipm.vn", FullName = "Nguyễn Thị Hồng", Phone = "0909876543", Role = "Accountant", Elo = 1200.0, Balance = 10000000m, Password = "Accountant@123" },

            // Project Managers - main test account
            new { Email = "pm@ipm.vn", FullName = "Lê Văn Hoàng", Phone = "0912345678", Role = "ProjectManager", Elo = 1450.0, Balance = 25000000m, Password = "Pm@12345" },
            new { Email = "pm.thao@ipm.vn", FullName = "Nguyễn Thị Thảo", Phone = "0923456789", Role = "ProjectManager", Elo = 1420.0, Balance = 22000000m, Password = "Password@123" },

            // Subcontractors - main test account
            new { Email = "contractor@ipm.vn", FullName = "Phạm Đức Hùng", Phone = "0934567890", Role = "Subcontractor", Elo = 1380.0, Balance = 15000000m, Password = "Contractor@123" },
            new { Email = "contractor.minh@ipm.vn", FullName = "Võ Văn Minh", Phone = "0945678901", Role = "Subcontractor", Elo = 1350.0, Balance = 12000000m, Password = "Password@123" },
            new { Email = "contractor.long@ipm.vn", FullName = "Đỗ Thành Long", Phone = "0956789012", Role = "Subcontractor", Elo = 1320.0, Balance = 10000000m, Password = "Password@123" },
            new { Email = "contractor.tuan@ipm.vn", FullName = "Bùi Anh Tuấn", Phone = "0967890123", Role = "Subcontractor", Elo = 1290.0, Balance = 8000000m, Password = "Password@123" },
            new { Email = "contractor.duc@ipm.vn", FullName = "Hoàng Văn Đức", Phone = "0978901234", Role = "Subcontractor", Elo = 1260.0, Balance = 7000000m, Password = "Password@123" },

            // Clients - main test account
            new { Email = "client@ipm.vn", FullName = "Trương Thị Lan", Phone = "0989012345", Role = "Client", Elo = 1200.0, Balance = 100000000m, Password = "Client@123" },
            new { Email = "client.nam@gmail.com", FullName = "Lý Văn Nam", Phone = "0990123456", Role = "Client", Elo = 1200.0, Balance = 150000000m, Password = "Password@123" },
            new { Email = "client.mai@gmail.com", FullName = "Phan Thị Mai", Phone = "0911234567", Role = "Client", Elo = 1200.0, Balance = 80000000m, Password = "Password@123" },
        };

        foreach (var userData in usersData)
        {
            var user = new ApplicationUser
            {
                UserName = userData.Email,
                Email = userData.Email,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(user, userData.Password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, userData.Role);

                var member = new Member
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    FullName = userData.FullName,
                    Email = userData.Email,
                    PhoneNumber = userData.Phone,
                    RankELO = userData.Elo,
                    WalletBalance = userData.Balance,
                    IsActive = true
                };

                context.Members.Add(member);
                members[userData.Email] = member;
            }
        }

        await context.SaveChangesAsync();
        return members;
    }

    private static async Task<List<Resource>> SeedResourcesAsync(ApplicationDbContext context)
    {
        if (await context.Resources.AnyAsync())
            return await context.Resources.ToListAsync();

        var resources = new List<Resource>
        {
            new Resource { Name = "Đội thợ điện chuyên nghiệp", Description = "Đội 5 người, chuyên lắp đặt hệ thống điện dân dụng và công nghiệp", HourlyRate = 500000m, IsActive = true },
            new Resource { Name = "Đội thợ nước Đại Phát", Description = "Đội 4 người, chuyên lắp đặt hệ thống cấp thoát nước", HourlyRate = 450000m, IsActive = true },
            new Resource { Name = "Đội thợ mộc Tân Phú", Description = "Đội 6 người, chuyên đóng tủ bếp, tủ quần áo cao cấp", HourlyRate = 600000m, IsActive = true },
            new Resource { Name = "Đội thợ sơn Hoàng Mai", Description = "Đội 8 người, chuyên sơn bả matit, sơn trang trí", HourlyRate = 400000m, IsActive = true },
            new Resource { Name = "Đội thi công thạch cao", Description = "Đội 5 người, chuyên làm trần và vách thạch cao", HourlyRate = 550000m, IsActive = true },
            new Resource { Name = "Xe tải Hyundai 2.5 tấn", Description = "Xe tải vận chuyển vật liệu xây dựng", HourlyRate = 800000m, IsActive = true },
            new Resource { Name = "Máy khoan Bosch Professional", Description = "Máy khoan đa năng, bao gồm mũi khoan các loại", HourlyRate = 200000m, IsActive = true },
            new Resource { Name = "Máy cắt gạch Makita", Description = "Máy cắt gạch ướt, đường kính dao 180mm", HourlyRate = 300000m, IsActive = true },
            new Resource { Name = "Giàn giáo bộ 50m2", Description = "Giàn giáo thép mạ kẽm, độ cao tối đa 10m", HourlyRate = 1500000m, IsActive = true },
            new Resource { Name = "Phòng họp dự án", Description = "Phòng họp 15 người, có màn chiếu và thiết bị họp trực tuyến", HourlyRate = 1000000m, IsActive = true },
        };

        context.Resources.AddRange(resources);
        await context.SaveChangesAsync();
        return resources;
    }

    private static async Task<List<Project>> SeedProjectsAsync(
        ApplicationDbContext context,
        Dictionary<string, Member> members)
    {
        if (await context.Projects.AnyAsync())
            return await context.Projects.ToListAsync();

        var pmHoang = members["pm@ipm.vn"];
        var pmThao = members["pm.thao@ipm.vn"];
        var clientLan = members["client@ipm.vn"];
        var clientNam = members["client.nam@gmail.com"];
        var clientMai = members["client.mai@gmail.com"];

        var projects = new List<Project>
        {
            new Project
            {
                Id = Guid.NewGuid(),
                Name = "Căn hộ Vinhomes Grand Park - A1205",
                ClientId = clientLan.Id,
                ManagerId = pmHoang.Id,
                StartDate = DateTime.UtcNow.AddDays(-30),
                TargetDate = DateTime.UtcNow.AddDays(60),
                TotalBudget = 450000000m,
                Status = ProjectStatus.Ongoing
            },
            new Project
            {
                Id = Guid.NewGuid(),
                Name = "Biệt thự Phú Mỹ Hưng - Khu E",
                ClientId = clientNam.Id,
                ManagerId = pmHoang.Id,
                StartDate = DateTime.UtcNow.AddDays(-60),
                TargetDate = DateTime.UtcNow.AddDays(30),
                TotalBudget = 1200000000m,
                Status = ProjectStatus.Ongoing
            },
            new Project
            {
                Id = Guid.NewGuid(),
                Name = "Văn phòng Sunwah Tower - Tầng 15",
                ClientId = clientMai.Id,
                ManagerId = pmThao.Id,
                StartDate = DateTime.UtcNow.AddDays(-15),
                TargetDate = DateTime.UtcNow.AddDays(75),
                TotalBudget = 800000000m,
                Status = ProjectStatus.Planning
            },
            new Project
            {
                Id = Guid.NewGuid(),
                Name = "Nhà phố Thảo Điền - 52 Nguyễn Văn Hưởng",
                ClientId = clientLan.Id,
                ManagerId = pmThao.Id,
                StartDate = DateTime.UtcNow.AddDays(-90),
                TargetDate = DateTime.UtcNow.AddDays(-5),
                TotalBudget = 650000000m,
                Status = ProjectStatus.Handover
            },
            new Project
            {
                Id = Guid.NewGuid(),
                Name = "Căn hộ Masteri Thảo Điền - T3-2808",
                ClientId = clientNam.Id,
                ManagerId = pmHoang.Id,
                StartDate = DateTime.UtcNow.AddDays(-120),
                TargetDate = DateTime.UtcNow.AddDays(-30),
                TotalBudget = 380000000m,
                Status = ProjectStatus.Completed
            },
        };

        context.Projects.AddRange(projects);
        await context.SaveChangesAsync();
        return projects;
    }

    private static async Task SeedTasksAsync(
        ApplicationDbContext context,
        List<Project> projects,
        Dictionary<string, Member> members)
    {
        if (await context.Tasks.AnyAsync())
            return;

        var contractors = members
            .Where(m => m.Key.Contains("contractor"))
            .Select(m => m.Value)
            .ToList();

        var tasks = new List<ProjectTask>();
        var random = new Random(42);

        foreach (var project in projects.Take(3))
        {
            var taskTemplates = new[]
            {
                new { Name = "Tháo dỡ tường cũ", Phase = PhaseType.Demolition, Days = 3, Cost = 15000000m },
                new { Name = "Dọn dẹp mặt bằng", Phase = PhaseType.Demolition, Days = 2, Cost = 8000000m },
                new { Name = "Lắp đặt hệ thống điện âm tường", Phase = PhaseType.MEP, Days = 5, Cost = 35000000m },
                new { Name = "Lắp đặt hệ thống nước", Phase = PhaseType.MEP, Days = 4, Cost = 28000000m },
                new { Name = "Lắp điều hòa và thông gió", Phase = PhaseType.MEP, Days = 3, Cost = 45000000m },
                new { Name = "Làm trần thạch cao phòng khách", Phase = PhaseType.Drywall, Days = 4, Cost = 25000000m },
                new { Name = "Làm vách ngăn thạch cao", Phase = PhaseType.Drywall, Days = 3, Cost = 18000000m },
                new { Name = "Bả matit toàn bộ tường", Phase = PhaseType.Painting, Days = 5, Cost = 20000000m },
                new { Name = "Sơn lót chống thấm", Phase = PhaseType.Painting, Days = 2, Cost = 12000000m },
                new { Name = "Sơn phủ 2 lớp", Phase = PhaseType.Painting, Days = 4, Cost = 22000000m },
                new { Name = "Lắp đặt tủ bếp", Phase = PhaseType.Woodwork, Days = 5, Cost = 85000000m },
                new { Name = "Lắp đặt tủ quần áo", Phase = PhaseType.Woodwork, Days = 4, Cost = 65000000m },
                new { Name = "Lắp đặt cửa gỗ", Phase = PhaseType.Woodwork, Days = 3, Cost = 42000000m },
            };

            var startDate = project.StartDate;
            foreach (var template in taskTemplates)
            {
                var status = project.Status switch
                {
                    ProjectStatus.Completed => ProjectTaskStatus.Approved,
                    ProjectStatus.Handover => random.Next(10) < 8 ? ProjectTaskStatus.Approved : ProjectTaskStatus.Review,
                    ProjectStatus.Ongoing => (ProjectTaskStatus)random.Next(0, 4),
                    _ => ProjectTaskStatus.ToDo
                };

                var progress = status switch
                {
                    ProjectTaskStatus.Approved => 100,
                    ProjectTaskStatus.Review => random.Next(80, 100),
                    ProjectTaskStatus.InProgress => random.Next(20, 80),
                    _ => 0
                };

                tasks.Add(new ProjectTask
                {
                    ProjectId = project.Id,
                    Name = template.Name,
                    PhaseType = template.Phase,
                    SubcontractorId = contractors[random.Next(contractors.Count)].Id,
                    StartTime = startDate,
                    EndTime = startDate.AddDays(template.Days),
                    TargetDate = startDate.AddDays(template.Days),
                    Status = status,
                    ProgressPct = progress,
                    EstimatedCost = template.Cost
                });

                startDate = startDate.AddDays(template.Days);
            }
        }

        context.Tasks.AddRange(tasks);
        await context.SaveChangesAsync();
    }

    private static async Task SeedBookingsAsync(
        ApplicationDbContext context,
        Dictionary<string, Member> members,
        List<Resource> resources)
    {
        if (await context.Bookings.AnyAsync())
            return;

        var pmHoang = members["pm@ipm.vn"];
        var pmThao = members["pm.thao@ipm.vn"];

        var bookings = new List<Booking>
        {
            new Booking
            {
                Id = Guid.NewGuid(),
                ResourceId = resources[0].Id,
                MemberId = pmHoang.Id,
                StartTime = DateTime.UtcNow.AddDays(1).Date.AddHours(8),
                EndTime = DateTime.UtcNow.AddDays(1).Date.AddHours(17),
                Price = 4500000m,
                Status = BookingStatus.Confirmed,
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new Booking
            {
                Id = Guid.NewGuid(),
                ResourceId = resources[2].Id,
                MemberId = pmThao.Id,
                StartTime = DateTime.UtcNow.AddDays(2).Date.AddHours(8),
                EndTime = DateTime.UtcNow.AddDays(4).Date.AddHours(17),
                Price = 14400000m,
                Status = BookingStatus.Confirmed,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new Booking
            {
                Id = Guid.NewGuid(),
                ResourceId = resources[5].Id,
                MemberId = pmHoang.Id,
                StartTime = DateTime.UtcNow.AddDays(3).Date.AddHours(7),
                EndTime = DateTime.UtcNow.AddDays(3).Date.AddHours(12),
                Price = 4000000m,
                Status = BookingStatus.PendingPayment,
                CreatedAt = DateTime.UtcNow
            },
            new Booking
            {
                Id = Guid.NewGuid(),
                ResourceId = resources[9].Id,
                MemberId = pmThao.Id,
                StartTime = DateTime.UtcNow.AddDays(1).Date.AddHours(14),
                EndTime = DateTime.UtcNow.AddDays(1).Date.AddHours(16),
                Price = 2000000m,
                Status = BookingStatus.Confirmed,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
        };

        context.Bookings.AddRange(bookings);
        await context.SaveChangesAsync();
    }

    private static async Task SeedNewsAsync(ApplicationDbContext context)
    {
        if (await context.News.AnyAsync())
            return;

        var news = new List<News>
        {
            new News
            {
                Title = "Ra mắt hệ thống quản lý dự án IPM Pro",
                Content = "IPM Pro chính thức đi vào hoạt động với đầy đủ tính năng quản lý dự án nội thất, theo dõi tiến độ bằng AI và hệ thống ví điện tử tích hợp.",
                CreatedDate = DateTime.UtcNow.AddDays(-10),
                IsPinned = true
            },
            new News
            {
                Title = "Cập nhật tính năng đặt lịch thông minh",
                Content = "Hệ thống đặt lịch mới với cơ chế khóa lạc quan (Optimistic Locking) giúp tránh xung đột khi nhiều người đặt cùng tài nguyên.",
                CreatedDate = DateTime.UtcNow.AddDays(-5),
                IsPinned = false
            },
            new News
            {
                Title = "Hướng dẫn sử dụng AI phân tích tiến độ",
                Content = "Tải ảnh công trình lên hệ thống, AI Google Gemini sẽ tự động phân tích và ước tính phần trăm hoàn thành công việc.",
                CreatedDate = DateTime.UtcNow.AddDays(-2),
                IsPinned = false
            },
            new News
            {
                Title = "Bảng xếp hạng ELO nhà thầu",
                Content = "Hệ thống đánh giá ELO giúp khách hàng dễ dàng lựa chọn nhà thầu uy tín dựa trên lịch sử hoàn thành dự án.",
                CreatedDate = DateTime.UtcNow.AddDays(-1),
                IsPinned = true
            },
        };

        context.News.AddRange(news);
        await context.SaveChangesAsync();
    }

    private static async Task SeedWalletTransactionsAsync(
        ApplicationDbContext context,
        Dictionary<string, Member> members)
    {
        if (await context.WalletTransactions.AnyAsync())
            return;

        var pmHoang = members["pm@ipm.vn"];
        var contractorHung = members["contractor@ipm.vn"];
        var clientLan = members["client@ipm.vn"];

        var transactions = new List<WalletTransaction>
        {
            new WalletTransaction
            {
                Id = Guid.NewGuid(),
                MemberId = clientLan.Id,
                Amount = 50000000m,
                TransType = TransactionType.Credit,
                Category = TransactionCategory.Deposit,
                Status = TransactionStatus.Success,
                Description = "Nạp tiền đặt cọc dự án Vinhomes Grand Park",
                CreatedAt = DateTime.UtcNow.AddDays(-25),
                EncryptedSignature = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            },
            new WalletTransaction
            {
                Id = Guid.NewGuid(),
                MemberId = clientLan.Id,
                Amount = 100000000m,
                TransType = TransactionType.Credit,
                Category = TransactionCategory.Deposit,
                Status = TransactionStatus.Success,
                Description = "Thanh toán đợt 1 dự án Vinhomes Grand Park",
                CreatedAt = DateTime.UtcNow.AddDays(-15),
                EncryptedSignature = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            },
            new WalletTransaction
            {
                Id = Guid.NewGuid(),
                MemberId = contractorHung.Id,
                Amount = 15000000m,
                TransType = TransactionType.Credit,
                Category = TransactionCategory.SubcontractorPayment,
                Status = TransactionStatus.Success,
                Description = "Thanh toán công việc tháo dỡ tường cũ căn hộ Vinhomes",
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                EncryptedSignature = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            },
            new WalletTransaction
            {
                Id = Guid.NewGuid(),
                MemberId = contractorHung.Id,
                Amount = 5000000m,
                TransType = TransactionType.Debit,
                Category = TransactionCategory.Refund,
                Status = TransactionStatus.Success,
                Description = "Rút tiền về tài khoản Vietcombank",
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                EncryptedSignature = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            },
            new WalletTransaction
            {
                Id = Guid.NewGuid(),
                MemberId = pmHoang.Id,
                Amount = 8000000m,
                TransType = TransactionType.Credit,
                Category = TransactionCategory.Deposit,
                Status = TransactionStatus.Success,
                Description = "Thưởng hoàn thành dự án Masteri Thảo Điền sớm hạn",
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                EncryptedSignature = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            },
        };

        context.WalletTransactions.AddRange(transactions);
        await context.SaveChangesAsync();
    }
}
