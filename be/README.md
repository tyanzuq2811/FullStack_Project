# IPM Pro Backend - Setup Instructions

## Bước 1: Cài đặt Dependencies

```bash
cd be
dotnet restore
```

## Bước 2: Cấu hình Database

1. Copy file template:
```bash
cd src/IPM.API
copy appsettings.template.json appsettings.Development.json
```

2. Chỉnh sửa `appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=IPM_DB_Dev;Trusted_Connection=True;...",
    "RedisConnection": "localhost:6379",
    "HangfireConnection": "Server=YOUR_SERVER;Database=IPM_Hangfire;..."
  },
  "Jwt": {
    "Key": "YOUR_SECRET_KEY_AT_LEAST_32_CHARS"
  },
  "Email": {
    "FromEmail": "your-email@gmail.com",
    "FromPassword": "your-app-password"
  }
}
```

## Bước 3: Tạo Database với Migrations

### Cài đặt EF Core Tools (nếu chưa có)
```bash
dotnet tool install --global dotnet-ef
```

### Tạo Migration
```bash
cd src/IPM.API
dotnet ef migrations add InitialCreate --project ../IPM.Infrastructure --startup-project .
```

### Apply Migration và Seed Data
```bash
dotnet ef database update --project ../IPM.Infrastructure --startup-project .
```

**Hoặc đơn giản chỉ cần chạy API**, nó sẽ tự động:
- Chạy migrations
- Seed dữ liệu mẫu

```bash
dotnet run
```

## Bước 4: Kiểm tra Database

Sau khi chạy, kiểm tra database đã có:
- **Tables**: 123_Members, 123_Projects, 123_Tasks, 123_Resources, 123_Bookings, 123_WalletTransactions, 123_News...
- **Sample Users**:
  - Admin: `admin@ipm.vn` / `Password@123`
  - PM: `pm.hoang@ipm.vn` / `Password@123`
  - Contractor: `contractor.hung@ipm.vn` / `Password@123`
  - Client: `client.lan@gmail.com` / `Password@123`

## Bước 5: Truy cập Swagger

```
https://localhost:7136/swagger
```

## Bước 6: Truy cập Hangfire Dashboard

```
https://localhost:7136/hangfire
```

## Các lệnh Migration hữu ích

### Xem danh sách migrations
```bash
dotnet ef migrations list --project ../IPM.Infrastructure --startup-project .
```

### Xóa database
```bash
dotnet ef database drop --project ../IPM.Infrastructure --startup-project .
```

### Xóa migration cuối cùng
```bash
dotnet ef migrations remove --project ../IPM.Infrastructure --startup-project .
```

### Tạo SQL script
```bash
dotnet ef migrations script --project ../IPM.Infrastructure --startup-project . --output migration.sql
```

## Troubleshooting

### Lỗi: "A connection was successfully established..."
- Kiểm tra SQL Server đang chạy
- Kiểm tra connection string trong appsettings.Development.json

### Lỗi: "Build failed"
- Chạy `dotnet clean` và `dotnet restore`
- Kiểm tra version .NET SDK: `dotnet --version` (cần >= 8.0)

### Lỗi: Redis connection
- Nếu không cần Redis, comment code trong `DependencyInjection.cs`
- Hoặc cài Redis: https://github.com/microsoftarchive/redis/releases

## Dữ liệu mẫu

Hệ thống tự động seed:
- **11 users** (1 Admin, 2 PM, 5 Contractors, 3 Clients)
- **5 projects** (Vinhomes, Phú Mỹ Hưng, Sunwah Tower...)
- **39 tasks** cho 3 dự án đang hoạt động
- **10 resources** (Đội thợ điện, mộc, sơn, xe tải, máy móc...)
- **4 bookings**
- **5 wallet transactions**
- **4 news items**

Tất cả dùng tên tiếng Việt thật, không có "Example" hay "Nguyễn Văn A".
