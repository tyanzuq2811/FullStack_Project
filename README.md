# IPM Pro Edition - Interior Project Manager

Hệ thống quản lý dự án nội thất chuyên nghiệp với đầy đủ tính năng Fintech, AI Progress Tracking và Smart Booking.

![Status](https://img.shields.io/badge/status-completed-brightgreen)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![Vue.js](https://img.shields.io/badge/Vue.js-3.x-4FC08D)
![Docker](https://img.shields.io/badge/Docker-ready-2496ED)

## 🎯 Tính năng đầy đủ

✅ **14/14 Views hoàn chỉnh**
- Authentication: Login, Register, ForgotPassword, ResetPassword
- Dashboard với thống kê real-time
- Projects: Quản lý dự án với filters
- Kanban Board: Drag & drop tasks
- Bookings: Đặt lịch tài nguyên với optimistic locking
- Wallet: Quản lý ví điện tử
- Leaderboard: Bảng xếp hạng ELO với podium
- Settings: Cài đặt đa tab (Profile, Security, Appearance, Notifications)

✅ **Backend Architecture hoàn chỉnh**
- Clean Architecture (Domain, Application, Infrastructure, API)

## Tech Stack

### Backend
- **Framework:** ASP.NET Core 8.0 Web API
- **Architecture:** Clean Architecture (4 layers)
- **Database:** SQL Server 2022 + Entity Framework Core
- **Authentication:** JWT + Refresh Token + Argon2id Password Hashing
- **Caching:** Redis với Sorted Sets cho ELO Leaderboard
- **Real-time:** SignalR WebSocket
- **Background Jobs:** Hangfire
- **AI Integration:** Google Gemini (Progress Analysis + Invoice OCR)

### Frontend
- **Framework:** Vue.js 3 + TypeScript
- **UI:** Tailwind CSS + Motion (animations)
- **State Management:** Pinia
- **HTTP Client:** Axios với interceptors
- **Real-time:** SignalR Client

### DevOps
- **Containerization:** Docker + Docker Compose
- **Containers:** db (SQL Server), redis, api, web

## Cấu trúc dự án

```
FullStackProject/
├── be/                          # Backend
│   ├── src/
│   │   ├── IPM.Domain/          # Entities, Interfaces, Enums
│   │   ├── IPM.Application/     # Business Logic, Services, DTOs
│   │   ├── IPM.Infrastructure/  # EF Core, Redis, SignalR, Hangfire
│   │   └── IPM.API/             # Controllers, Program.cs
│   ├── Dockerfile
│   └── IPM.sln
├── fe/                          # Frontend
│   ├── src/
│   │   ├── components/          # Vue components
│   │   ├── views/               # Pages
│   │   ├── stores/              # Pinia stores
│   │   ├── services/            # API services
│   │   ├── composables/         # Vue composables
│   │   └── types/               # TypeScript interfaces
│   ├── Dockerfile
│   └── nginx.conf
├── docker-compose.yml
└── README.md
```

## Tính năng chính

### 1. Fintech/Wallet
- Ví điện tử cho mỗi Member
- Nạp/rút tiền với mã xác nhận SHA256
- Lịch sử giao dịch chi tiết
- Chống gian lận với EncryptedSignature

### 2. AI Progress Tracking
- Upload hình ảnh tiến độ công trình
- Google Gemini phân tích tự động
- Ước tính % hoàn thành
- Trích xuất thông tin từ hóa đơn (OCR)

### 3. Smart Booking
- Đặt lịch tài nguyên/nhân công
- Optimistic Locking với RowVersion
- Xử lý DbUpdateConcurrencyException
- Real-time cập nhật qua SignalR

### 4. ELO Rating System
- Đánh giá nhà thầu sau dự án
- Redis Sorted Sets cho Leaderboard
- Cập nhật rank tự động

### 5. Kanban Board
- Quản lý task theo WBS
- Drag & drop thay đổi trạng thái
- Real-time sync qua SignalR

## Cài đặt & Chạy

### Yêu cầu
- Docker Desktop
- .NET 8 SDK (khi chạy backend local)
- Node.js 20+ (khi chạy frontend local)

### 1) Chạy full bằng Docker (khuyên dùng khi demo)

#### Bước 1: Clone mã nguồn

```bash
git clone <repo-url>
cd FullStackProject
```

#### Bước 2: Chuẩn bị biến môi trường

- Nếu bạn đang dùng compose theo biến môi trường, tạo file env runtime từ file mẫu:

PowerShell:

```powershell
Copy-Item .env.example .env
```

Linux/macOS:

```bash
cp .env.example .env
```

- Sau đó cập nhật các giá trị quan trọng trong file env: JWT, Gemini API key, SMTP, password DB.

#### Bước 3: Build và chạy

```bash
docker compose up -d --build
```

#### Bước 4: Truy cập

- Frontend: http://localhost
- API: http://localhost:8080
- Hangfire: http://localhost:8080/hangfire

#### Lệnh quản trị nhanh

```bash
docker compose ps
docker compose logs -f api
docker compose down
```

### 2) Chạy FE và BE local đồng thời (2 terminal)

Phù hợp khi phát triển hằng ngày để hot reload nhanh.

Lưu ý:
- Cần có SQL Server + Redis. Cách gọn nhất là chạy riêng 2 service này bằng Docker.
- Sau đó chạy backend và frontend local ở 2 terminal khác nhau.

#### Bước 1: Chạy database/cache

```bash
docker compose up -d db redis
```

#### Bước 2: Chạy backend local (Terminal 1)

```bash
cd be
dotnet restore
dotnet run --project src/IPM.API
```

Backend mặc định chạy theo cấu hình trong appsettings và biến môi trường máy.

#### Bước 3: Chạy frontend local (Terminal 2)

```bash
cd fe
npm install
npm run dev
```

Frontend dev server thường chạy tại http://localhost:5173.

### 3) Chạy hybrid: BE Docker + FE local

Phù hợp khi muốn backend ổn định theo container, frontend vẫn hot reload.

#### Bước 1: Chạy backend stack bằng Docker

```bash
docker compose up -d db redis api
```

#### Bước 2: Chạy frontend local

```bash
cd fe
npm install
npm run dev
```

#### Bước 3: Kiểm tra biến API của frontend

- Đảm bảo frontend trỏ đúng API backend Docker.
- Ví dụ biến VITE_API_URL nên trỏ về địa chỉ API đang mở cổng trên máy local.

### Troubleshooting nhanh

- Port 80/8080/1433/6379 bị chiếm:
  - đổi port trong docker-compose hoặc dừng dịch vụ đang chiếm.
- Frontend gọi sai API:
  - kiểm tra VITE_API_URL và CORS backend.
- Lỗi đăng nhập JWT:
  - kiểm tra JWT secret đồng nhất giữa môi trường chạy.
- Lỗi AI/OCR:
  - kiểm tra GEMINI_API_KEY đã được cấu hình.

## API Endpoints

### Authentication
- `POST /api/auth/register` - Đăng ký
- `POST /api/auth/login` - Đăng nhập
- `POST /api/auth/refresh` - Refresh token
- `POST /api/auth/logout` - Đăng xuất

### Wallets
- `GET /api/wallets/balance` - Lấy số dư
- `GET /api/wallets/transactions` - Lịch sử giao dịch
- `POST /api/wallets/deposit` - Nạp tiền
- `POST /api/wallets/withdraw` - Rút tiền

### Bookings
- `GET /api/bookings` - Danh sách đặt lịch
- `POST /api/bookings` - Tạo đặt lịch mới
- `PUT /api/bookings/{id}` - Cập nhật (với RowVersion)
- `DELETE /api/bookings/{id}` - Hủy đặt lịch

### AI
- `POST /api/ai/analyze-progress` - Phân tích tiến độ từ ảnh
- `POST /api/ai/extract-invoice` - Trích xuất thông tin hóa đơn

### Leaderboard
- `GET /api/leaderboard` - Top contractors theo ELO

## Bảo mật

### Password Hashing
Sử dụng **Argon2id** theo chuẩn OWASP:
- Memory: 47104 KB (46 MiB)
- Iterations: 1
- Parallelism: 1
- Salt: 16 bytes random
- Hash: 32 bytes

### JWT Authentication
- Access Token: 60 phút
- Refresh Token: 7 ngày
- Blacklist token khi logout

### Transaction Security
- SHA256 signature cho mỗi giao dịch
- Verify signature trước khi xử lý

## Cấu hình

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `DB_PASSWORD` | SQL Server SA password | YourStrong@Passw0rd |
| `REDIS_PASSWORD` | Redis password | redis123 |
| `JWT_SECRET_KEY` | JWT signing key | (32+ chars required) |
| `GEMINI_API_KEY` | Google Gemini API key | (required for AI features) |

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=IPMProDB;...",
    "Redis": "localhost:6379"
  },
  "JwtSettings": {
    "SecretKey": "...",
    "ExpiryMinutes": 60,
    "RefreshTokenExpiryDays": 7
  },
  "GeminiSettings": {
    "ApiKey": "...",
    "Model": "gemini-1.5-flash"
  }
}
```

## Database Schema

Tất cả bảng có prefix theo mã sinh viên (ví dụ: `001_Members`, `001_Projects`).

### Core Entities
- **Members** - Người dùng (Admin, Accountant, PM, Subcontractor, Client)
- **Projects** - Dự án nội thất
- **ProjectTasks** - Công việc theo WBS
- **Bookings** - Đặt lịch tài nguyên
- **WalletTransactions** - Giao dịch ví
- **ContractorRatings** - Đánh giá nhà thầu

## Roles & Permissions

| Role | Permissions |
|------|-------------|
| Admin | Quản lý người dùng, cấu hình hệ thống, xem báo cáo tổng thể |
| Accountant | Quản lý ví điện tử, phê duyệt nạp tiền, kiểm soát tài chính |
| Project Manager | Quản lý dự án, tasks, bookings, nghiệm thu công việc |
| Subcontractor | Xem tasks được giao, cập nhật tiến độ, nhận giải ngân |
| Client | Xem tiến độ dự án, nạp tiền, nhận thông báo |

### Bảng phân quyền chi tiết theo chức năng

| Chức năng | Admin | Accountant | Project Manager | Subcontractor | Client |
|-----------|-------|------------|-----------------|---------------|--------|
| Xem dashboard theo vai trò | Có | Có | Có | Có | Có |
| Quản lý người dùng (xem/tạo/sửa/khóa) | Có | Không | Không | Không | Không |
| Xem danh sách dự án | Có | Không | Có | Có | Có |
| Tạo dự án mới | Không | Không | Có | Không | Không |
| Xem chi tiết dự án | Có | Có | Có | Có | Có |
| Tạo task | Không | Không | Có | Không | Không |
| Cập nhật trạng thái task | Có | Không | Có | Có (task được giao) | Không |
| Nghiệm thu task (approve) | Không | Không | Có | Không | Không |
| Dùng AI phân tích tiến độ | Không | Không | Có | Có (task được giao) | Không |
| Dùng AI OCR hóa đơn | Không | Có | Có | Không | Không |
| Xem Kanban | Có | Không | Có | Có | Có (chỉ xem) |
| Đặt lịch tài nguyên (tạo booking) | Không | Không | Có | Có | Không |
| Xem booking | Có | Không | Có | Có | Không |
| Thanh toán booking pending | Không | Không | Có | Có | Không |
| Xem ví cá nhân | Không | Không | Không | Có | Có |
| Nạp tiền vào dự án | Không | Không | Không | Không | Có |
| Phê duyệt/từ chối yêu cầu nạp tiền | Không | Có | Không | Không | Không |
| Xem ví dự án tổng hợp | Không | Có | Có | Không | Không |
| Xem leaderboard ELO | Có | Không | Có | Có | Có |
| Xem tin tức hệ thống | Có | Có | Có | Có | Có |
| Cấu hình/cài đặt tài khoản cá nhân | Có | Có | Có | Có | Có |

Ghi chú:
- "Có" = có quyền thao tác chức năng.
- "Không" = không có quyền thao tác chức năng.
- Một số quyền có điều kiện theo phạm vi dữ liệu (ví dụ: Subcontractor chỉ thao tác task được giao).

## Tài khoản Mẫu (Sample Accounts)

Hệ thống được seed sẵn các tài khoản sau để test:

| Vai trò | Email | Mật khẩu | Mô tả |
|---------|-------|----------|-------|
| Admin | admin@ipm.vn | Admin@123 | Quản trị viên hệ thống |
| Accountant | accountant@ipm.vn | Accountant@123 | Kế toán |
| Project Manager | pm@ipm.vn | Pm@12345 | Quản lý dự án |
| Subcontractor | contractor@ipm.vn | Contractor@123 | Nhà thầu phụ |
| Client | client@ipm.vn | Client@123 | Khách hàng/Chủ đầu tư |

## Background Jobs (Hangfire)

- **BookingCleanupJob** - Dọn dẹp booking hết hạn (hourly)
- **DailySummaryJob** - Gửi báo cáo tổng hợp (daily)
- **EloUpdateJob** - Cập nhật ELO ranking (daily)

## SignalR Hubs

- `/hubs/notification` - Real-time notifications
  - `OnTaskUpdated` - Task status changed
  - `OnBookingChanged` - Booking updated
  - `OnWalletTransaction` - New transaction

## License

MIT License - Dự án dành cho mục đích học tập.
