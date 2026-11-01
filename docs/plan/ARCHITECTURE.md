# Kiến trúc: Cleora

- **Stack:** C# WinForms .NET Framework 4.8 (có sẵn trên Windows 10/11, exe nhỏ), lõi netstandard2.0 test bằng xUnit trên Mac/CI. Giống Recora để dùng chung giao diện và CI.
- **Quyền:** manifest `highestAvailable`: tài khoản quản trị được hỏi UAC và dọn được cả mục hệ thống; tài khoản thường vẫn chạy, mục hệ thống ghi "Cần quyền quản trị". Bộ cài per-user (không cần quyền).
- **Lõi (Core):** `TargetCatalog` dựng danh mục từ `KnownFolders` (tests trỏ vào cây giả) → `CleanupScanner` → `Cleaner` (kiểm `PathGuard` từng file) · `FileWalker` không đi qua liên kết · `LargeFileFinder` một lượt, giữ top N · `DuplicateFinder` cỡ → 64 KB đầu/cuối → SHA-256.
- **App:** `CleanService` thêm Thùng rác (shell API) và Windows.old (takeown/icacls/rd đúng thư mục `X:\Windows.old`); `InstalledApps`, `StartupApps` đọc registry; trang vẽ tay theo `Theme` sáng/tối của Recora.

| ADR | Quyết định | Lý do | Bỏ |
|---|---|---|---|
| 1 | Xoá hẳn rác, file người dùng vào Thùng rác | Rác tạo lại được; file người dùng phải lấy lại được | Xoá hẳn mọi thứ |
| 2 | Tắt khởi động bằng StartupApproved | Đảo ngược được, khớp Task Manager | Xoá mục Run |
| 3 | Không dọn registry | Lợi ích gần bằng 0, rủi ro hỏng app | — |
