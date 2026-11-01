# PRD: Cleora

**Vấn đề:** ổ C: của người dùng Windows (game thủ, dân văn phòng) đầy dần vì file tạm, bản tải Windows Update, cache trình duyệt và app (Chrome, Cốc Cốc, Zalo, Discord, Steam), file lớn quên xoá, file trùng. Các app dọn quen thuộc có quảng cáo, gói trả phí, hoặc xoá quá tay.

**Người dùng:** một người trên một máy Windows 10/11, có hoặc không có quyền quản trị.

**Mục tiêu đo được:**
- Quét xong danh mục dọn trên máy thường dưới 30 giây; dọn không bao giờ xoá file ngoài gốc (test + selftest có junction).
- Bản cài dưới 5 MB, mở app dưới 2 giây.

**Luồng chính:**
1. Dọn rác: Quét → xem từng mục và dung lượng → bỏ chọn mục không muốn → Dọn → xác nhận → thấy dung lượng đã giải phóng.
2. File lớn: chọn ổ → quét → xem thư mục và file lớn nhất → mở thư mục chứa hoặc chuyển vào Thùng rác.
3. File trùng: chọn thư mục, cỡ tối thiểu, bản giữ lại → tìm → bản thừa được chọn sẵn → chuyển vào Thùng rác.
4. Gỡ app: xem app theo dung lượng, tìm, mở bộ gỡ của app.
5. Khởi động: bật/tắt app chạy cùng Windows.

**Ngoài phạm vi:** dọn registry, "tăng tốc RAM", diệt virus, tự dọn theo lịch (để bản sau), Microsoft Store app.
