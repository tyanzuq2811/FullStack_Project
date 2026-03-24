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
        await SafeSeedStepAsync("roles", () => SeedRolesAsync(roleManager));

        var members = await SafeSeedStepAsync(
            "users-members",
            () => SeedUsersAndMembersAsync(context, userManager),
            async () => await context.Members.ToDictionaryAsync(m => m.Email, m => m, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, Member>(StringComparer.OrdinalIgnoreCase));

        var resources = await SafeSeedStepAsync(
            "resources",
            () => SeedResourcesAsync(context),
            async () => await context.Resources.ToListAsync(),
            new List<Resource>());

        var projects = await SafeSeedStepAsync(
            "projects",
            () => SeedProjectsAsync(context, members),
            async () => await context.Projects.ToListAsync(),
            new List<Project>());

        await SafeSeedStepAsync("tasks", () => SeedTasksAsync(context, projects, members));
        await SafeSeedStepAsync("bookings", () => SeedBookingsAsync(context, members, resources, projects));
        await SafeSeedStepAsync("news", () => SeedNewsAsync(context));
        await SafeSeedStepAsync("wallet-transactions", () => SeedWalletTransactionsAsync(context, members));

        await context.SaveChangesAsync();
    }

    private static async Task SafeSeedStepAsync(string stepName, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SeedData] Step '{stepName}' failed: {ex.Message}");
        }
    }

    private static async Task<T> SafeSeedStepAsync<T>(string stepName, Func<Task<T>> action, Func<Task<T>> fallback, T fallbackOnFailure)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SeedData] Step '{stepName}' failed: {ex.Message}");
            try
            {
                return await fallback();
            }
            catch (Exception fallbackEx)
            {
                Console.WriteLine($"[SeedData] Step '{stepName}' fallback failed: {fallbackEx.Message}");
                return fallbackOnFailure;
            }
        }
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

        var existingMembers = await context.Members.ToListAsync();
        var members = existingMembers.ToDictionary(m => m.Email, m => m, StringComparer.OrdinalIgnoreCase);

        foreach (var userData in usersData)
        {
            var user = await userManager.FindByEmailAsync(userData.Email);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = userData.Email,
                    Email = userData.Email,
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(user, userData.Password);
                if (!createResult.Succeeded)
                {
                    continue;
                }
            }

            if (!await userManager.IsInRoleAsync(user, userData.Role))
            {
                await userManager.AddToRoleAsync(user, userData.Role);
            }

            if (!members.TryGetValue(userData.Email, out var member))
            {
                member = new Member
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
        var templates = new[]
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

        var existingNames = await context.Resources
            .Select(r => r.Name)
            .ToListAsync();

        var existingNameSet = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
        var missingResources = templates
            .Where(t => !existingNameSet.Contains(t.Name))
            .ToList();

        if (missingResources.Count > 0)
        {
            context.Resources.AddRange(missingResources);
            await context.SaveChangesAsync();
        }

        return await context.Resources.ToListAsync();
    }

    private static async Task<List<Project>> SeedProjectsAsync(
        ApplicationDbContext context,
        Dictionary<string, Member> members)
    {
        if (!members.TryGetValue("pm@ipm.vn", out var pmHoang) ||
            !members.TryGetValue("pm.thao@ipm.vn", out var pmThao) ||
            !members.TryGetValue("client@ipm.vn", out var clientLan) ||
            !members.TryGetValue("client.nam@gmail.com", out var clientNam) ||
            !members.TryGetValue("client.mai@gmail.com", out var clientMai))
        {
            return await context.Projects.ToListAsync();
        }

        var templates = new[]
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

        var existingNames = await context.Projects
            .Select(p => p.Name)
            .ToListAsync();

        var existingNameSet = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
        var missingProjects = templates
            .Where(t => !existingNameSet.Contains(t.Name))
            .ToList();

        if (missingProjects.Count > 0)
        {
            context.Projects.AddRange(missingProjects);
            await context.SaveChangesAsync();
        }

        return await context.Projects.ToListAsync();
    }

    private static async Task SeedTasksAsync(
        ApplicationDbContext context,
        List<Project> projects,
        Dictionary<string, Member> members)
    {
        var contractors = members
            .Where(m => m.Key.Contains("contractor"))
            .Select(m => m.Value)
            .ToList();

        if (contractors.Count == 0)
        {
            return;
        }

        var targetProjectNames = new[]
        {
            "Căn hộ Vinhomes Grand Park - A1205",
            "Biệt thự Phú Mỹ Hưng - Khu E",
            "Văn phòng Sunwah Tower - Tầng 15"
        };

        var targetProjects = projects
            .Where(p => targetProjectNames.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (targetProjects.Count == 0)
        {
            targetProjects = projects.Take(3).ToList();
        }

        var tasks = new List<ProjectTask>();
        var random = new Random(42);

        foreach (var project in targetProjects)
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

            var existingTaskNames = await context.Tasks
                .Where(t => t.ProjectId == project.Id)
                .Select(t => t.Name)
                .ToListAsync();

            var existingTaskNameSet = new HashSet<string>(existingTaskNames, StringComparer.OrdinalIgnoreCase);

            var startDate = project.StartDate;
            foreach (var template in taskTemplates)
            {
                if (existingTaskNameSet.Contains(template.Name))
                {
                    startDate = startDate.AddDays(template.Days);
                    continue;
                }

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
        List<Resource> resources,
        List<Project> projects)
    {
        if (!members.TryGetValue("pm@ipm.vn", out var pmHoang) ||
            !members.TryGetValue("pm.thao@ipm.vn", out var pmThao))
        {
            return;
        }

        var projectVinhomes = projects.FirstOrDefault(p => p.Name.Contains("Vinhomes Grand Park", StringComparison.OrdinalIgnoreCase));
        var projectSunwah = projects.FirstOrDefault(p => p.Name.Contains("Sunwah Tower", StringComparison.OrdinalIgnoreCase));
        var projectNhaPho = projects.FirstOrDefault(p => p.Name.Contains("Nhà phố Thảo Điền", StringComparison.OrdinalIgnoreCase));

        var resourceThoDien = resources.FirstOrDefault(r => r.Name.Contains("Đội thợ điện", StringComparison.OrdinalIgnoreCase));
        var resourceThoMoc = resources.FirstOrDefault(r => r.Name.Contains("Đội thợ mộc", StringComparison.OrdinalIgnoreCase));
        var resourceXeTai = resources.FirstOrDefault(r => r.Name.Contains("Xe tải Hyundai", StringComparison.OrdinalIgnoreCase));
        var resourcePhongHop = resources.FirstOrDefault(r => r.Name.Contains("Phòng họp dự án", StringComparison.OrdinalIgnoreCase));

        if (projectVinhomes == null || projectSunwah == null || projectNhaPho == null ||
            resourceThoDien == null || resourceThoMoc == null || resourceXeTai == null || resourcePhongHop == null)
        {
            return;
        }

        var bookingTemplates = new List<Booking>
        {
            new Booking
            {
                Id = Guid.NewGuid(),
                ResourceId = resourceThoDien.Id,
                MemberId = pmHoang.Id,
                ProjectId = projectVinhomes.Id,
                StartTime = DateTime.UtcNow.AddDays(1).Date.AddHours(8),
                EndTime = DateTime.UtcNow.AddDays(1).Date.AddHours(17),
                Price = 4500000m,
                Status = BookingStatus.Confirmed,
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new Booking
            {
                Id = Guid.NewGuid(),
                ResourceId = resourceThoMoc.Id,
                MemberId = pmThao.Id,
                ProjectId = projectSunwah.Id,
                StartTime = DateTime.UtcNow.AddDays(2).Date.AddHours(8),
                EndTime = DateTime.UtcNow.AddDays(4).Date.AddHours(17),
                Price = 14400000m,
                Status = BookingStatus.Confirmed,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new Booking
            {
                Id = Guid.NewGuid(),
                ResourceId = resourceXeTai.Id,
                MemberId = pmHoang.Id,
                ProjectId = projectVinhomes.Id,
                StartTime = DateTime.UtcNow.AddDays(3).Date.AddHours(7),
                EndTime = DateTime.UtcNow.AddDays(3).Date.AddHours(12),
                Price = 4000000m,
                Status = BookingStatus.PendingPayment,
                CreatedAt = DateTime.UtcNow
            },
            new Booking
            {
                Id = Guid.NewGuid(),
                ResourceId = resourcePhongHop.Id,
                MemberId = pmThao.Id,
                ProjectId = projectNhaPho.Id,
                StartTime = DateTime.UtcNow.AddDays(1).Date.AddHours(14),
                EndTime = DateTime.UtcNow.AddDays(1).Date.AddHours(16),
                Price = 2000000m,
                Status = BookingStatus.Confirmed,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
        };

        var existingSignatures = await context.Bookings
            .Select(b => new { b.ProjectId, b.ResourceId, b.MemberId, b.StartTime, b.EndTime })
            .ToListAsync();

        var missingBookings = bookingTemplates
            .Where(template => !existingSignatures.Any(existing =>
                existing.ProjectId == template.ProjectId &&
                existing.ResourceId == template.ResourceId &&
                existing.MemberId == template.MemberId &&
                existing.StartTime == template.StartTime &&
                existing.EndTime == template.EndTime))
            .ToList();

        if (missingBookings.Count > 0)
        {
            context.Bookings.AddRange(missingBookings);
            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedNewsAsync(ApplicationDbContext context)
    {
        var templates = new List<News>
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

        var existingTitles = await context.News
            .Select(n => n.Title)
            .ToListAsync();

        var existingTitleSet = new HashSet<string>(existingTitles, StringComparer.OrdinalIgnoreCase);
        var missingNews = templates
            .Where(t => !existingTitleSet.Contains(t.Title))
            .ToList();

        if (missingNews.Count > 0)
        {
            context.News.AddRange(missingNews);
            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedWalletTransactionsAsync(
        ApplicationDbContext context,
        Dictionary<string, Member> members)
    {
        if (!members.TryGetValue("pm@ipm.vn", out var pmHoang) ||
            !members.TryGetValue("contractor@ipm.vn", out var contractorHung) ||
            !members.TryGetValue("client@ipm.vn", out var clientLan))
        {
            return;
        }

        var templates = new List<WalletTransaction>
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

        var existingDescriptions = await context.WalletTransactions
            .Select(t => t.Description)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .ToListAsync();

        var existingDescriptionSet = new HashSet<string>(existingDescriptions!, StringComparer.OrdinalIgnoreCase);
        var missingTransactions = templates
            .Where(t => string.IsNullOrWhiteSpace(t.Description) || !existingDescriptionSet.Contains(t.Description))
            .ToList();

        if (missingTransactions.Count > 0)
        {
            context.WalletTransactions.AddRange(missingTransactions);
            await context.SaveChangesAsync();
        }
    }
}
