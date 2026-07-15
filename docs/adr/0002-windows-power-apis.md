# 0002 Windowsの電源APIを用途別に使用する

- 状態：Accepted
- 日付：2026-07-15

## 文脈

ディスプレイ消灯時間は現在のWindows電源プランへ残る設定であり、手動スリープ防止はプロセス終了時に消える一時的な要求です。
企画書の`powercfg`案では現在値の解析が表示言語に依存する可能性があり、変更後のAC値とDC値を同じ境界で検証できません。
スリープ防止ではディスプレイ消灯を許可し、利用者が明示したスリープを妨げない必要があります。

## 判断

ディスプレイ消灯時間の読み書きにはPowrProf APIを使用します。
現在の電源プランは[`PowerGetActiveScheme`](https://learn.microsoft.com/en-us/windows/win32/api/powersetting/nf-powersetting-powergetactivescheme)で取得し、返されたGUIDポインターは`LocalFree`で解放します。
ディスプレイ設定サブグループの`VIDEOIDLE`を、`PowerReadACValueIndex`、`PowerReadDCValueIndex`、`PowerWriteACValueIndex`、`PowerWriteDCValueIndex`で秒単位に読み書きします。
変更後は[`PowerSetActiveScheme`](https://learn.microsoft.com/en-us/windows/win32/api/powersetting/nf-powersetting-powersetactivescheme)で反映し、AC値とDC値を読み直します。
値`0`は無期限として扱います。

手動スリープ防止には[`PowerCreateRequest`](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-powercreaterequest)、[`PowerSetRequest`](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-powersetrequest)、[`PowerClearRequest`](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-powerclearrequest)を使用します。
要求種別は`SystemRequired`だけとし、ディスプレイ、Away Mode、実行継続は要求しません。
要求ハンドルはアプリプロセスが所有し、利用者の解除、エラー、終了時に閉じます。

## 結果

ディスプレイ設定はOSの表示言語に依存せず、対象値の読戻しが一致した場合だけ成功と表示できます。
AC値とDC値を実機で別々に変更し、読戻し後に変更前の値へ復旧できることを確認しました。

Power Requestは電源プランを書き換えず、ハンドルを閉じると要求が残りません。
利用者が明示したスリープ、管理ポリシー、Modern Standbyの制約はOS側が優先されます。
Modern StandbyのDC動作では、システムスリープタイムアウト後に要求の効力が制限されるため、長時間の維持を保証しません。

## 比較した案

`powercfg /change`と`powercfg /query`の組み合わせは、外部プロセス、出力解析、タイムアウト処理が必要になるため採用しませんでした。
`SetThreadExecutionState`は継続要求が呼び出しスレッドへ結びつき、理由文字列を持つ要求ハンドルとして管理できないため採用しませんでした。
