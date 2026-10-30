# Handoff — continue dvx on the Windows laptop

State at handoff (2026-10-30): branch `claude/laughing-bohr-37djeu`, head `581b2c4` + this handoff commit.
Done: M0, S-A, S-F, M1, M2. Next milestone: **M3 — offline verify + analyzers (plan Tasks 9, 10, 11)**.
SDD ledger (rulings, deferred minors, per-task history): `docs/superpowers/sdd/2026-10-19-dvx-sp0-sp1-ledger.md`.

## Before the first session on Windows

1. Install: .NET 8 SDK (8.0.400+), Visual Studio 2026 with ".NET desktop development"
   (gives the .NET Framework 4.6.2 targeting pack), Git, Claude Code with the superpowers plugin.
2. Get the code:
   ```powershell
   git clone https://github.com/truongdxn/dvx.git
   cd dvx
   git checkout claude/laughing-bohr-37djeu
   dotnet build
   dotnet test
   ```
   Expected: 0 warnings; 5 `Passed!` lines incl. `Dvx.Plugin.Sdk.Tests (net462)` 41 tests,
   `Dvx.Core.Tests` 65, `Dvx.Cli.Tests` 10. On Windows no mono and no `-p:IsTestProject=true`.
3. Open questions still waiting on you (SPEC §15 / Appendix B "To ask"): identity key for
   global-message steps; confirm diagnostic codes DVX090, DVX180–DVX185, DVX190.

## Prompt to paste into the new session

```
/superpowers:subagent-driven-development Tiếp tục thực thi plan
docs/superpowers/plans/2026-10-19-dvx-sp0-sp1.md, scope session này: M3 (Task 9, 10, 11) — offline
verify + analyzers. Không làm sang M4.

Bối cảnh:
- Repo dvx, branch claude/laughing-bohr-37djeu. Đã xong M0, S-A, S-F, M1, M2 (xem CLAUDE.md Status).
- Đọc trước: CLAUDE.md, SPEC.md (§6, §7, §10, §11, §12, §15, Appendix B D22–D33),
  docs/superpowers/HANDOFF.md, và ledger docs/superpowers/sdd/2026-10-19-dvx-sp0-sp1-ledger.md.
- Ledger là bản copy từ session cloud: khởi tạo workspace SDD mới của plan này rồi copy ledger vào
  <workspace>/progress.md, append "## Session 5 — milestone M3 (Tasks 9, 10, 11)". Các task có dòng
  "Task N: complete" là ĐÃ XONG, không dispatch lại.
- Máy này là Windows: chạy `dotnet build` và `dotnet test` ở root; test net462 chạy native, không cần
  mono hay -p:IsTestProject=true.

Ghi nhớ từ các milestone trước (đã có trong plan/ledger, nhắc lại để không sót):
- Task 9: mở rộng src/Dvx.Core/DiagnosticCodes.cs đã có (KHÔNG tạo Verify/DiagnosticCodes.cs thứ
  hai); DesiredStateBuilder phải báo trùng cùng key là diagnostic người dùng, chỉ throw DVX190 khi
  khác key mà trùng ID; thêm rule stage 10/20/40 và mode 0/1; thêm test determinism với input xáo trộn.
- Task 10: dùng AnalyzerReleases.Shipped.md / Unshipped.md (SPEC D31), không NoWarn RS2008;
  Microsoft.CodeAnalysis.CSharp 4.8.x; analyzer test compile snippet bằng CSharpCompilation.
- Identity (SPEC §7, D33) đã đóng băng: không đổi key format/namespace/normalization; golden test
  và tests/Dvx.Cli.Tests/Snapshots/describe.json không được sửa để test pass.
- Mỗi rule verify/analyzer có 1 test pass + 1 test fail; mỗi lệnh CLI có snapshot JSON + exit code.
- Dependency chỉ trong SPEC §11; cần thêm gì thì hỏi tôi trước.

Quy tắc session:
- Làm trong isolated worktree (superpowers:using-git-worktrees).
- SPEC và plan mâu thuẫn ở điểm ảnh hưởng hành vi/ID thì dừng hỏi tôi; còn lại tự ruling và ghi
  ledger như các session trước.
- Kết thúc: dotnet build + dotnet test xanh trên Windows, final review, cập nhật CLAUDE.md Status,
  copy ledger mới đè lên docs/superpowers/sdd/2026-10-19-dvx-sp0-sp1-ledger.md, commit, push lên
  claude/laughing-bohr-37djeu, tóm tắt kèm "Rulings I made", rồi dừng chờ tôi.
```
