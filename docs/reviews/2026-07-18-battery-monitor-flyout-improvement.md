# BatteryMonitorの領域外クリック改善提案

- 作成日：2026年7月18日
- 対象：通知領域から開く既存のWPF `Popup`
- 目的：領域外クリック時にも既存の閉じるアニメーションを最後まで表示する

## 対象範囲

BatteryMonitorの表示方式とモーションデザインは変更しません。
Hardcodet.Wpf.TaskbarNotificationのWPF `Popup`を維持し、領域外クリックから既存の閉じるアニメーションを開始します。

今回の変更には、次の項目を含めません。

- ボーダーレス`Window`への置き換え
- タスクバーの裏へ隠れる開閉モーション
- 通知アイコン基準の再配置
- タスクバー方向ごとのモーション
- 動的な高さ変更
- 既存の開くアニメーションの変更
- 既存の閉じるアニメーションの時間と速度曲線の変更

## 現在の問題

領域外クリック時は、既存の閉じるアニメーションを通る前に`Popup`が非表示になっています。
そのため、通知アイコンの再クリックなどではアニメーションする一方、別の場所をクリックした場合は即座に閉じたように見えます。

修正対象はアニメーション自体ではありません。
領域外クリックを検出した処理が、`IsOpen = false`、`CloseBalloon()`、`Popup.IsOpen = false`などの即時非表示を直接実行している経路です。

WPFまたはHardcodet側の自動クローズが先に`Popup`を閉じている可能性もあります。
その場合は自動クローズへ任せず、BatteryMonitorが閉じる完了時刻を管理できる設定へ変更します。

## 閉じる処理の統合

領域外クリックを含むすべての閉じる要求を、既存アニメーションを開始する一つの処理へ渡します。
`Popup`を実際に閉じるのは、アニメーションの完了後だけです。

```csharp
private bool isClosing;

private async Task RequestCloseAsync(CloseReason reason)
{
    if (!IsPopupOpen || isClosing)
    {
        return;
    }

    if (IsPinned && reason == CloseReason.OutsideClick)
    {
        return;
    }

    isClosing = true;
    StopCloseWatchdog();
    FlyoutContent.IsHitTestVisible = false;

    try
    {
        await RunExistingCloseAnimationAsync();
        ClosePopupImmediately();
    }
    finally
    {
        FlyoutContent.IsHitTestVisible = true;
        isClosing = false;
    }
}
```

`ClosePopupImmediately()`は既存アニメーションの完了後に限って呼び出します。
領域外クリックのハンドラー、watchdog、通知アイコンの再クリックから直接呼び出しません。

## 領域外クリックの扱い

領域外クリックを検出した時点では、`Popup`を閉じずに閉じる要求だけを発行します。

```csharp
private void OnOutsideClickDetected()
{
    _ = RequestCloseAsync(CloseReason.OutsideClick);
}
```

現行のwatchdogは200ミリ秒周期であり、閉じるアニメーションは300ミリ秒です。
watchdogを動かしたままにすると、条件が継続している間に閉じる要求を繰り返す可能性があります。

最初の要求でwatchdogを停止し、`isClosing`によって後続要求を無視します。
アニメーションをStoryboardで実装している場合は、`Completed`を待ってから`Popup`を閉じます。

## WPF Popupの自動クローズ

`Popup.StaysOpen`が`false`の場合、WPFは領域外のマウス操作を受けて`Popup`を自動的に閉じます。
この経路では、BatteryMonitorが300ミリ秒の閉じるアニメーションを完了させる時間がありません。

既存構成でこの自動クローズを使用している場合は、`StaysOpen="True"`相当へ変更します。
領域外クリックの判定は既存のwatchdogまたは既存のマウス監視へ任せ、判定結果を`RequestCloseAsync`へ渡します。

Hardcodet側が別の自動非表示機能を提供している場合も、同じ理由で無効化または延期が必要です。
実際にどの設定が`Popup`を閉じているかは、BatteryMonitorのイベントとプロパティを確認して決めます。

## Zオーダーの確認

今回の目的は既存アニメーションを領域外クリックでも実行することであり、DisplayLightの`Window.Topmost`方式は移植しません。
WPF `Popup`は独立したネイティブウィンドウを使用するため、領域外クリック後も閉じるアニメーションが手前に見えるかは実機で確認します。

アニメーションが実行されてもクリック先のウィンドウに隠れる場合は、即時非表示とは別のZオーダー問題です。
その場合に限り、閉じる300ミリ秒の間だけ`Popup`のHWNDを手前に維持する方法を追加で検証します。

## 表示モードごとの規則

- **ホバー表示**：既存の閉じる条件を維持し、条件成立時は共通の閉じる処理を使う。
- **明示表示**：領域外クリックと通知アイコンの再クリックから共通の閉じる処理を使う。
- **ピン固定表示**：領域外クリックを無視し、既存の固定解除または終了操作だけを受け付ける。
- **ショートカット表示**：既存の閉じる条件を維持し、閉じる場合は共通の閉じる処理を使う。

今回の修正では表示モード全体を新しい状態機械へ置き換えません。
閉じる処理の多重起動を防ぐ最小限の`isClosing`だけを追加します。

## 例外とキャンセル

閉じるアニメーションが失敗または中断した場合でも、`isClosing`と入力禁止を残しません。
例外後に`Popup`を開いたまま戻すか即時非表示にするかは、既存のエラー方針へ合わせます。

通知アイコンの再クリックによる反転が現行仕様にない場合は、今回追加しません。
開閉途中の反転まで扱うと、既存モーションの進捗取得とキャンセル処理が別途必要になるためです。

## 実装順序

1. 領域外クリック時に`Popup`を直接閉じている箇所を特定する。
2. `Popup.StaysOpen`とHardcodet側の自動非表示設定を確認する。
3. 既存の閉じるアニメーションを待機可能な一つの処理へまとめる。
4. アニメーション完了後だけ`Popup`を非表示にする。
5. watchdogを閉じる開始時に停止し、多重起動を防ぐ。
6. 領域外クリック、再クリック、既存の自動クローズ条件を共通処理へ接続する。
7. 実機録画でアニメーションとZオーダーを確認する。

## 検証項目

- [ ] 領域外クリック時に既存の閉じるアニメーションが開始される。
- [ ] アニメーション完了前に`Popup`が非表示にならない。
- [ ] 既存の300ミリ秒の時間と速度曲線が変わらない。
- [ ] 領域外クリックを続けても閉じるアニメーションが再開始されない。
- [ ] watchdogの次周期で閉じるアニメーションが再開始されない。
- [ ] 通知アイコンの再クリックによる既存動作が変わらない。
- [ ] ホバー表示の既存動作が変わらない。
- [ ] ピン固定中は領域外クリックで閉じない。
- [ ] 閉じる途中はフライアウト内の操作を受け付けない。
- [ ] 完了、中断、例外の後に入力禁止と`isClosing`が残らない。
- [ ] クリック先のウィンドウに隠れず、閉じるアニメーションが目視できる。

## 未確認事項

BatteryMonitorで即時非表示を発生させている具体的なコード経路は、この文書では未確認です。
`Popup.StaysOpen`、Hardcodetの自動クローズ、watchdogのいずれが直接閉じているかを実装前に特定します。

領域外クリック後のZオーダーも実機確認が必要です。
既存の`Popup`がアニメーション中も手前に残る場合、追加のZオーダー制御は行いません。

## 参照

- DisplayLightの実装：`src/DisplayLight.App/MainWindow.xaml.cs`
- BatteryMonitorのデザイン参照レビュー：`docs/reviews/2026-07-15-battery-monitor-reference.md`
- 参照元：[zwnj/BatteryMonitor](https://github.com/zwnj/BatteryMonitor)
