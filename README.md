# 🍁 Maple Guardian

**Kill Switch VPN cho Maple Planet**

Tự động chặn game khi VPN ngắt kết nối. Chỉ block game, không ảnh hưởng internet khác.

## Cài đặt

1. Tải `MapleGuardian_v2_Portable.zip` từ [Releases](https://github.com/NhatCT/MapleGuardian/releases)
2. Giải nén, chạy `MapleGuardian.exe` với quyền Administrator
3. Chỉnh `appsettings.json` nếu cần:
   - `VpnAdapterName`: tên adapter VPN
   - `SoftEtherAccountName`: tên account SoftEther

## Sử dụng

- **Icon xanh lá**: VPN connected, game chạy bình thường
- **Icon đỏ**: VPN lost, game bị chặn
- **Icon vàng**: Đang reconnect VPN
- Click icon tray để xem chi tiết IP, ping, firewall

## Cập nhật

App tự động kiểm tra update khi khởi động. Hoặc tải bản mới từ [Releases](https://github.com/NhatCT/MapleGuardian/releases).

## Yêu cầu

- Windows 10/11
- SoftEther VPN Client
- Quyền Administrator