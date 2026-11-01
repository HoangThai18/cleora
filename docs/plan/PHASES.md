# Phases: Cleora

| Phase | Mục tiêu | Trạng thái |
|---|---|---|
| P0 | Nền móng + bản 0.1.0: dọn rác, file lớn, file trùng, gỡ app, khởi động, CI, bộ cài, trang tải | doing |

## P0: Bản đầu đủ năm luồng
**Tiêu chí ra:**
- [ ] `dotnet test` xanh; test chứng minh BR-01, BR-02, BR-03, BR-05, BR-07
- [ ] CI Windows: selftest OK (dọn máy giả có junction, quét máy thật không xoá, startup bật/tắt, Thùng rác), ảnh mọi trang sáng/tối, zip và bộ cài dưới 5 MB
- [ ] Review độc lập PASS; Release `cleora-0.1.0`; trang thaipro.store/phan-mem/cleora
