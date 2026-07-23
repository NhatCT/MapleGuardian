# 🍁 Maple Guardian

**Kill Switch VPN chuyên nghiệp cho Maple Planet**

Maple Guardian là công cụ tự động bảo vệ kết nối game, đảm bảo game chỉ chạy khi VPN đang kết nối. Nếu VPN ngắt kết nối, ứng dụng sẽ tự động chặn game và thông báo ngay lập tức.

## ✨ Tính năng

- **Kill Switch tự động**: Chặn game ngay khi VPN ngắt, không cần can thiệp thủ công
- **Giám sát VPN thời gian thực**: Phát hiện trạng thái VPN dưới 1ms
- **Tự động kết nối lại**: Tự động reconnect VPN khi mất kết nối
- **Kiểm tra IP/DNS**: Hiển thị IP công cộng, quốc gia, loại IP (Residential/DataCenter)
- **Thông báo**: Thông báo desktop khi VPN mất/kết nối lại
- **Khởi chạy cùng Windows**: Tùy chọn tự động khởi động khi bật máy
- **Cập nhật tự động**: Tự động kiểm tra và cập nhật phiên bản mới

## 📋 Yêu cầu hệ thống

- Windows 10/11 (64-bit)
- SoftEther VPN Client đã cài đặt
- Quyền Administrator (để điều khiển firewall)

## 🚀 Cài đặt

### Cách 1: Sử dụng phiên bản Portable (Khuyên dùng)

1. Tải file `MapleGuardian_v2_Portable.zip` từ [Releases](https://github.com/NhatCT/MapleGuardian/releases)
2. Giải nén vào thư mục bất kỳ (ví dụ: `C:\MapleGuardian\`)
3. Chạy `MapleGuardian.exe` với quyền Administrator
4. Cấu hình tên adapter VPN và tên account SoftEther trong `appsettings.json` (nếu cần)

### Cách 2: Build từ source code

```bash
# Clone repository
git clone https://github.com/NhatCT/MapleGuardian.git
cd MapleGuardian

# Build
dotnet build src/MapleGuardian/MapleGuardian.csproj -c Release

# Chạy
src\MapleGuardian\bin\Release\net8.0-windows\win-x64\MapleGuardian.exe
```

## ⚙️ Cấu hình

Chỉnh sửa file `appsettings.json` trong thư mục chứa ứng dụng:

```json
{
  "VpnAdapterName": "VPN - VPN Client",           // Tên adapter VPN trong Network Connections
  "SoftEtherPath": "C:\\Program Files\\SoftEther VPN Client\\vpncmd.exe",
  "SoftEtherAccountName": "VPN",                  // Tên account SoftEther
  "FirewallRules": ["MSW Block", "NGM64 Block", "NexonLink Block"],
  "GameProcesses": ["MaplePlanet"],               // Tên process game cần giám sát
  "MaxReconnectAttempts": 10,                     // Số lần thử reconnect tối đa
  "ReconnectDelaySeconds": 5,                     // Thời gian chờ giữa các lần retry
  "EnableAutoUpdate": true                        // Tự động cập nhật
}
```

## 📖 Cách sử dụng

1. **Khởi động**: Chạy `MapleGuardian.exe` với quyền Administrator
2. **Kết nối VPN**: Kết nối VPN như bình thường
3. **Chơi game**: Mở game, Maple Guardian sẽ tự động giám sát
4. **Theo dõi**: Click icon trong system tray để xem trạng thái:
   - 🟢 **Connected**: VPN đang kết nối, game được phép chạy
   - 🔴 **LOST**: VPN ngắt, game bị chặn
   - 🟡 **Reconnecting**: Đang thử kết nối lại VPN

## 🔧 Cách hoạt động

### Khi VPN kết nối:
1. Flush DNS cache (ngăn DNS leak)
2. Tắt firewall rules (cho phép game chạy)
3. Thông báo "VPN connected"

### Khi VPN ngắt:
1. Bật firewall rules (chặn game)
2. Flush DNS cache
3. Thông báo "VPN LOST"
4. Tự động reconnect VPN
5. Khi VPN lên lại → tắt firewall → game chạy tiếp

## 🛡️ Bảo mật

- **Chỉ block game**: Firewall chỉ chặn process game, không ảnh hưởng internet của các ứng dụng khác
- **DNS leak protection**: Tự động flush DNS khi VPN kết nối
- **Zero-trust**: Nếu VPN mất, game bị chặn ngay lập tức

## 📝 Changelog

### v2.2.1 (2026-07-23)
- Sửa lỗi firewall COM API (chỉ block game, không block toàn bộ mạng)
- Sửa race condition khi VPN tự kết nối lại
- Tự động hủy SoftEther retry khi VPN đã lên
- Block traffic của MaplePlanet thay vì ping.exe

### v2.2.0
- Phản hồi Kill Switch siêu tốc dưới 0.2s
- Xem Log trực tiếp trong App
- Tự động dò tìm SoftEther VPN
- Win32 Kernel NDIS Driver Callbacks
- Icon khay hệ thống sắc nét

### v2.1.0
- Giao diện dashboard mới
- Hiển thị IP công cộng và quốc gia
- Kiểm tra loại IP (Residential/DataCenter)
- Ping monitor

## 🤝 Đóng góp

Mọi đóng góp đều được chào đón! Vui lòng tạo Issue hoặc Pull Request.

## 📄 License

MIT License - Xem file [LICENSE](LICENSE) để biết thêm chi tiết.

## 🔗 Liên kết

- **Repository**: https://github.com/NhatCT/MapleGuardian
- **Releases**: https://github.com/NhatCT/MapleGuardian/releases
- **Issues**: https://github.com/NhatCT/MapleGuardian/issues

---

**Lưu ý**: Ứng dụng này chỉ dùng cho mục đích giáo dục. Vui lòng tuân thủ điều khoản sử dụng của game và VPN provider.