# Domain: Cleora

## Thuật ngữ
- **Mục dọn (target)**: một nhóm chỗ chứa rác có tên ("File tạm của bạn", "Bộ nhớ đệm Google Chrome"), gồm một hay nhiều **gốc (root)**.
- **Gốc**: thư mục Cleora được phép xoá nội dung; hoặc lấy hết (đệ quy), hoặc chỉ các file khớp mẫu nằm ngay trong nó.
- **Quét**: liệt kê file sẽ bị xoá và tổng dung lượng, không xoá gì.
- **Dọn**: xoá đúng danh sách vừa quét, sau khi người dùng bấm và xác nhận.
- **Thùng rác**: nơi file lớn và file trùng đi vào (lấy lại được); rác và cache thì xoá hẳn.

## Quy tắc (BR)
- **BR-01 Không xoá ngoài gốc.** Mỗi file được kiểm lại ngay trước khi xoá: phải nằm trong gốc nó được tìm thấy (`PathGuard.IsInside`). Gốc lấy-hết không được là ổ đĩa, Windows, System32, WinSxS, hồ sơ người dùng, AppData, ProgramData, Program Files, và phải sâu ít nhất hai cấp.
- **BR-02 Chỉ bộ nhớ đệm.** Trình duyệt và app: chỉ thư mục Cache, Code Cache, GPUCache, Dawn*, ShaderCache, cache2. Không bao giờ mật khẩu, cookie, lịch sử, phiên, tin nhắn, file đã nhận.
- **BR-03 Không đi qua liên kết.** Junction và symbolic link không bao giờ được duyệt hay xoá theo; chúng có thể trỏ về Tài liệu hay C:\.
- **BR-04 Người dùng quyết.** Không có gì bị xoá khi chưa bấm Dọn/Xoá và xác nhận. Mục có rủi ro (Windows.old, bộ nhớ đệm shader) mặc định không chọn.
- **BR-05 File của người dùng vào Thùng rác.** File lớn và file trùng chỉ vào Thùng rác; mỗi nhóm file trùng luôn giữ ít nhất một bản.
- **BR-06 Tệp đang dùng thì để lại.** Không ép tắt app, không thử lại vòng lặp; đếm và báo "N tệp đang được dùng".
- **BR-07 Khởi động đảo ngược được.** Tắt app khởi động bằng StartupApproved (như Task Manager), không xoá mục Run.
- **BR-08 Không gửi dữ liệu.** Việc mạng duy nhất là hỏi GitHub có bản mới không.
