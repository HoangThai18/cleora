---
id: P0-T03
title: "CI Windows, selftest, bộ cài, phát hành 0.1.0, trang tải"
phase: P0
lane: QA
status: doing
deps: [P0-T02]
touches: [.github/workflows/**, tools/**, docs/release-notes/**, "thaidev: config/software.php", "thaidev: public/assets/phan-mem/cleora*"]
size: M
---

## Acceptance criteria
- [ ] CI xanh: selftest OK, zip và bộ cài dưới 5 MB, cài/chạy/gỡ im lặng
- [ ] Release `cleora-0.1.0` đủ file; trang tải hiện 0.1.0
