---
id: P0-T01
title: "Lõi dọn: danh mục, PathGuard, quét, dọn, file lớn, file trùng, app, khởi động"
phase: P0
lane: Core
status: done
deps: []
touches: [src/Cleora.Core/**, tests/Cleora.Core.Tests/**]
size: L
---

## Goal
Mọi quyết định xoá nằm trong Core và có test trên cây thư mục giả.

## Acceptance criteria
- [x] Test BR-01 (file ngoài gốc, gốc không an toàn, gốc không đệ quy), BR-02 (mật khẩu, lịch sử, cookie còn nguyên), BR-03 (symlink), BR-05 (luôn giữ một bản), BR-07 (StartupApproved đọc/ghi)
