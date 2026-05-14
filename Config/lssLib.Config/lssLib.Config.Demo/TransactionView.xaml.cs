// ══════════════════════════════════════════════════════════════════════════
//  lssLib.Config.Demo · Views/TransactionView.xaml.cs
//  탭⑥: ConfigTransaction Commit / Rollback / Undo / Redo 데모
// ══════════════════════════════════════════════════════════════════════════
using System.Text;
using System.Windows;
using System.Windows.Controls;
using lssLib.Config.Transaction;

namespace lssLib.Config.Demo.Views;

public partial class TransactionView : UserControl
{
    #region §1 ─ 필드

    private readonly ConfigManager _cfg = ConfigManager.CreateNew();
    private ConfigTransaction? _tx;

    #endregion

    #region §2 ─ 초기화

    public TransactionView()
    {
        InitializeComponent();

        // 트랜잭션 커밋 이벤트 구독
        _cfg.TransactionCommitted += changes =>
        {
            Dispatcher.InvokeAsync(() =>
            {
                Log($"📌 TransactionCommitted — {changes.Count}개 변경");
                foreach (var ch in changes)
                {
                    var arrow = ch.NewValue is null ? "삭제됨" : $"\"{ch.OldValue}\" → \"{ch.NewValue}\"";
                    Log($"   [{ch.Section}] {ch.Key}  {arrow}");
                }
                RefreshUndoRedoBtns();
                RefreshStoreView();
            });
        };

        // 초기 데이터
        _cfg.Set("Network", "Host", "192.168.1.100");
        _cfg.Set("Network", "Port", "502");
        _cfg.Set("Network", "Timeout", "5000");
        _cfg.Set("App", "Debug", "false");
        _cfg.Set("App", "Version", "1.0.0");
        RefreshStoreView();

        Log("TransactionView 초기화 완료.");
        Log("① [트랜잭션 시작] → ② [Set 추가] 반복 → ③ [Commit] 또는 [Rollback]");
    }

    #endregion

    #region §3 ─ 트랜잭션 제어

    private void BtnBegin_Click(object sender, RoutedEventArgs e)
    {
        if (_tx is not null) { Log("⚠ 이미 트랜잭션이 활성 상태입니다."); return; }
        _tx = _cfg.BeginTransaction();
        TbTxStatus.Text = "● 활성 (Commit 또는 Rollback 대기)";
        TbTxStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
        Log("⬤ 트랜잭션 시작 — Set/Remove 변경 항목을 추가하세요.");
    }

    private void BtnTxSet_Click(object sender, RoutedEventArgs e)
    {
        if (_tx is null) { Log("⚠ 먼저 [트랜잭션 시작]을 클릭하세요."); return; }
        var sec = TbTxSection.Text.Trim();
        var key = TbTxKey.Text.Trim();
        var val = TbTxValue.Text.Trim();
        _tx.Set(sec, key, val);
        Log($"   + Set [{sec}] {key} = \"{val}\"  (보류 {_tx.PendingCount}건)");
    }

    private void BtnTxRemove_Click(object sender, RoutedEventArgs e)
    {
        if (_tx is null) { Log("⚠ 먼저 [트랜잭션 시작]을 클릭하세요."); return; }
        var sec = TbTxSection.Text.Trim();
        var key = TbTxKey.Text.Trim();
        _tx.Remove(sec, key);
        Log($"   - Remove [{sec}] {key}  (보류 {_tx.PendingCount}건)");
    }

    private void BtnCommit_Click(object sender, RoutedEventArgs e)
    {
        if (_tx is null) { Log("⚠ 활성 트랜잭션이 없습니다."); return; }
        _tx.Commit();
        _tx = null;
        TbTxStatus.Text = "비활성";
        TbTxStatus.Foreground = System.Windows.Media.Brushes.SlateGray;
        Log("✅ Commit 완료 — 저장소에 반영됨.");
    }

    private void BtnRollback_Click(object sender, RoutedEventArgs e)
    {
        if (_tx is null) { Log("⚠ 활성 트랜잭션이 없습니다."); return; }
        _tx.Rollback();
        _tx = null;
        TbTxStatus.Text = "비활성";
        TbTxStatus.Foreground = System.Windows.Media.Brushes.SlateGray;
        Log("↩ Rollback — 변경 취소. 저장소는 이전 상태 유지.");
        RefreshStoreView();
    }

    private void BtnTxSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "config", "transaction_demo.json");
            _cfg.Save(path, ConfigFormat.Json);
            Log($"💾 저장: {System.IO.Path.GetFileName(path)}");
        }
        catch (Exception ex) { Log($"❌ {ex.Message}"); }
    }

    #endregion

    #region §4 ─ Undo / Redo

    private void BtnUndo_Click(object sender, RoutedEventArgs e)
    {
        var changes = _cfg.Undo();
        if (changes is null) { Log("⚠ Undo 할 항목이 없습니다."); return; }
        Log($"↩ Undo — {changes.Count}개 변경 복원:");
        foreach (var ch in changes)
        {
            var restored = ch.OldValue is null ? "키 제거됨" : $"\"{ch.NewValue}\" → \"{ch.OldValue}\"";
            Log($"   [{ch.Section}] {ch.Key}  {restored}");
        }
        RefreshUndoRedoBtns();
        RefreshStoreView();
    }

    private void BtnRedo_Click(object sender, RoutedEventArgs e)
    {
        var changes = _cfg.Redo();
        if (changes is null) { Log("⚠ Redo 할 항목이 없습니다."); return; }
        Log($"↪ Redo — {changes.Count}개 변경 재적용:");
        foreach (var ch in changes)
        {
            var reapplied = ch.NewValue is null ? "키 삭제됨" : $"\"{ch.OldValue}\" → \"{ch.NewValue}\"";
            Log($"   [{ch.Section}] {ch.Key}  {reapplied}");
        }
        RefreshUndoRedoBtns();
        RefreshStoreView();
    }

    #endregion

    #region §5 ─ 샘플 시나리오

    private void BtnScenario1_Click(object sender, RoutedEventArgs e)
    {
        Log("─── 시나리오 1: 네트워크 설정 변경 + Commit ──────────");
        using var tx = _cfg.BeginTransaction();
        tx.Set("Network", "Host", "10.0.0.50");
        tx.Set("Network", "Port", "1502");
        tx.Set("Network", "Timeout", "3000");
        tx.Commit();
        Log("✅ 시나리오 1 완료.");
    }

    private async void BtnScenario2_Click(object sender, RoutedEventArgs e)
    {
        Log("─── 시나리오 2: 변경 후 Rollback ─────────────────────");
        var tx = _cfg.BeginTransaction();
        tx.Set("Network", "Host", "INVALID-HOST");
        tx.Set("App", "Debug", "true");
        Log("   (변경 보류 중 — 검증 실패 가정 → Rollback)");

        await Task.Delay(600);

        tx.Rollback();
        Log("↩ Rollback 완료. 저장소는 이전 상태 유지.");
        RefreshStoreView();
    }

    private async void BtnScenario3_Click(object sender, RoutedEventArgs e)
    {
        Log("─── 시나리오 3: Commit × 3 → Undo × 2 → Redo ─────────");

        // Commit 1
        using (var tx1 = _cfg.BeginTransaction())
        {
            tx1.Set("App", "Version", "2.0.0");
            tx1.Commit();
        }
        Log("   Commit 1: Version → 2.0.0");
        await Task.Delay(300);

        // Commit 2
        using (var tx2 = _cfg.BeginTransaction())
        {
            tx2.Set("App", "Version", "2.1.0");
            tx2.Set("App", "Debug", "true");
            tx2.Commit();
        }
        Log("   Commit 2: Version → 2.1.0  Debug → true");
        await Task.Delay(300);

        // Commit 3
        using (var tx3 = _cfg.BeginTransaction())
        {
            tx3.Set("Network", "Host", "10.0.0.99");
            tx3.Commit();
        }
        Log("   Commit 3: Host → 10.0.0.99");
        RefreshStoreView();
        await Task.Delay(500);

        // Undo × 2
        Log("   Undo 1 →"); BtnUndo_Click(this, new RoutedEventArgs()); await Task.Delay(400);
        Log("   Undo 2 →"); BtnUndo_Click(this, new RoutedEventArgs()); await Task.Delay(400);

        // Redo × 1
        Log("   Redo 1 →"); BtnRedo_Click(this, new RoutedEventArgs());
        Log("─── 시나리오 3 완료 ────────────────────────────────────");
    }

    #endregion

    #region §6 ─ 내부 헬퍼

    private void RefreshStoreView()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"  총 {_cfg.Store.Count}개 항목  |  Undo={_cfg.UndoDepth}  CanRedo={_cfg.CanRedo}");
        sb.AppendLine();
        foreach (var section in _cfg.Store.GetSections())
        {
            sb.AppendLine($"  [{section}]");
            foreach (var entry in _cfg.Store.GetSection(section))
                sb.AppendLine($"    {entry.Key,-16} = {entry.Value}");
        }
        TbStoreState.Text = sb.ToString();
    }

    private void RefreshUndoRedoBtns()
    {
        BtnUndo.IsEnabled = _cfg.CanUndo;
        BtnRedo.IsEnabled = _cfg.CanRedo;
        TbUndoDepth.Text = _cfg.UndoDepth.ToString();
        TbRedoDepth.Text = _cfg.CanRedo ? "?" : "0";  // Redo depth 는 내부 스택에서 추적
    }

    private void Log(string msg)
    {
        TbLog.AppendText($"[{DateTime.Now:HH:mm:ss.fff}]  {msg}\n");
        TbLog.ScrollToEnd();
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e) => TbLog.Clear();

    #endregion
}