# Cleora

App dọn dung lượng miễn phí cho Windows 10/11: rác hệ thống, cache trình duyệt và app, file lớn, file trùng, gỡ app, tắt app khởi động. Do thaipro.store viết, mã nguồn mở MIT, giao diện tiếng Việt sáng/tối. Cùng họ với Recora (HoangThai18/recora): dùng chung bộ màu, nút, thẻ, chuyển động, CI, bộ cài.

## Lệnh
- Build (Mac hoặc Windows, .NET SDK 10): `dotnet build Cleora.slnx -c Release`. Trên Mac biên dịch được `Cleora.App` (net48) nhưng không chạy được.
- Test: `dotnet test tests/Cleora.Core.Tests` · một lớp: `--filter FullyQualifiedName~ScanAndCleanTests`
- Windows: `Cleora.exe --selftest <thư mục>` (dọn máy giả, quét máy thật không xoá, app, khởi động, Thùng rác); `Cleora.exe --ui-screenshot <png> [--page Clean] [--theme dark] [--ui-scale 1.5]`.
- CI: `.github/workflows/ci.yml` (build, test, selftest, ảnh, zip, bộ cài; tag `cleora-x.y.z` thì phát hành).
- Icon: `python3 tools/icon/make_icon.py` (Pillow).

## Cấu trúc
- `src/Cleora.Core/` (netstandard2.0, không đụng Windows): `Cleanup/` (danh mục chỗ dọn, `PathGuard`, quét, dọn), `Scanning/` (duyệt file không đi qua liên kết, file lớn, file trùng), `Apps/`, `Startup/`, `Settings/`.
- `src/Cleora.App/` (WinForms net48): `Cleaning/` (Thùng rác, Windows.old, registry app và khởi động, `CleanService`), `Platform/`, `UI/` (trang, điều khiển vẽ tay, `Theme` sáng/tối).
- `tests/Cleora.Core.Tests/` xUnit, dựng cây thư mục giả trong thư mục tạm.
- `docs/domain/DOMAIN.md` quy tắc `BR-xx` · `docs/plan/` · `docs/tasks/` · `docs/reviews/`.

## Quy tắc bắt buộc
- Mọi lệnh xoá đi qua `Cleaner` hoặc Thùng rác; không gọi `File.Delete`/`Directory.Delete` trên đường dẫn của người dùng ở chỗ khác. Không phá BR-01..BR-06 trong `docs/domain/DOMAIN.md`.
- Thêm chỗ dọn mới: chỉ thư mục cache/tạm có tài liệu hoặc tên rõ ràng; không bao giờ đụng mật khẩu, cookie, lịch sử, phiên, tin nhắn, file tải về. Kèm test chứng minh file người dùng bên cạnh còn nguyên.
- `Cleora.Core` không gọi API Windows để test được trên Mac.
- Người dùng chỉ thấy câu tiếng Việt dễ hiểu; message exception, mã lỗi Windows chỉ vào nhật ký.
- Tên class, file, biến tiếng Anh; chuỗi giao diện tiếng Việt; chú thích tiếng Anh, chỉ nói *tại sao*.
- Test: không làm xanh bằng cách xoá, bỏ qua, nới lỏng test.
- Release là việc công khai: chỉ đẩy tag `cleora-*` khi chủ sản phẩm yêu cầu; mỗi bản tăng `Version` trong `Directory.Build.props`.

## Quy ước
- Module mẫu: `src/Cleora.Core/Cleanup/` + `tests/Cleora.Core.Tests/Cleanup/`.
- Test: tên `Method_WhenCondition_Expected`, cây thư mục giả qua `Scratch`.
- Commit: `<TASK-ID>: <mô tả tiếng Việt>`.

## Quy trình
Bộ skill `vibe` (HoangThai18/vibe-code-pro): plan → tasks → build (một agent viết cả code lẫn test) → review → ship.
