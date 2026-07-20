# DisplayLight

DisplayLightは、Windowsのディスプレイ消灯時間と、一時的なスリープ防止を通知領域から管理するWPFアプリです。
MVPはCodex連携の手前までを対象とし、電源操作は利用者が画面または通知領域から明示的に実行します。

## MVPでできること

- 現在の電源プランにあるAC時とバッテリー時のディスプレイ消灯時間を表示する。
- `1分`、`5分`、`10分`、`30分`、`60分`、`無期限`を離散スライダーから適用する。
- AC値とバッテリー値を別々に変更し、変更後の実値を読み直す。
- ディスプレイ消灯を許可したまま、手動でシステムスリープ防止を開始または解除する。
- AC電源接続時だけスリープ防止を有効にする。
- 3秒のキャンセル可能なカウントダウン後に、接続中のディスプレイをオフにする。
- 通知領域から小型コントロールセンターを開き、再読込みと終了を行う。
- 二回目の起動で既存ウィンドウを表示する。
- インストール版では安定版の更新を確認し、承認後に更新して再起動する。
- インストール版ではWindowsサインイン時にフライアウトを開かず通知領域へ常駐する。

ディスプレイ消灯時間はWindowsの通常設定として残り、アプリ終了時には戻りません。
手動スリープ防止はプロセス内だけで保持し、解除、エラー、アプリ終了時に解放します。
Codex Hook、複数セッション、自動スリープはAgent Preview以降の対象です。

## ダウンロードと実行

GitHub Releasesから`DisplayLight-win-Setup.exe`をダウンロードして実行します。
インストール後は、起動から20秒後に安定版の更新を確認し、更新がある場合だけフライアウトへ通知します。
右上の「その他の操作」から手動で「更新を確認」することもできます。
更新のダウンロードと再起動は「更新して再起動」を押すまで始まりません。
インストール版はWindowsのスタートアップ アプリへ既定で登録されます。
自動起動の有無は、タスクマネージャーの「スタートアップ アプリ」にあるDisplayLightから切り替えます。
自動起動時はフライアウトを開かず、通知領域アイコンだけを表示します。

インストールせず試す場合は`DisplayLight-win-Portable.zip`を展開して実行できます。
Portable版ではアプリ内更新を利用できません。

配布ZIPは.NETランタイムを含むため、利用者が.NET SDKまたはランタイムを別途インストールする必要はありません。
対象環境はWindows 11 x64です。

0.1系にはコード署名がないため、Windows SmartScreenが発行元不明の警告を表示する可能性があります。
GitHubのArtifact Attestationを使って配布物の来歴を確認できます。

```powershell
gh attestation verify ./DisplayLight-win-Setup.exe --repo zwnj/DisplayLight
```

## 開発環境

- Windows 11のx64環境
- .NET 10 SDKの`global.json`に記載したFeature Band
- PowerShell 7
- Git

WPFとWindows電源APIを使用するため、ビルドと実機確認はWindowsで行います。

## セットアップ

```powershell
git clone <repository-url>
cd DisplayLight
pwsh -NoProfile -File ./scripts/setup.ps1
```

## 起動

```powershell
dotnet run --project ./src/DisplayLight.App/DisplayLight.App.csproj
```

起動時は通知アイコンの近くに小型コントロールセンターを表示します。
通知アイコンの再クリック、外側クリック、Escapeで閉じますが、プロセスは終了しません。
プロセスを止める場合は、右上の補助メニューまたは通知アイコンの右クリックメニューから「終了」を選びます。

利用者設定は`%LOCALAPPDATA%\DisplayLight\settings.json`へ保存します。
壊れた設定は同じフォルダーへ`settings.corrupt.<UTC時刻>.json`として残し、安全な既定値で起動します。

## ビルドとテスト

```powershell
pwsh -NoProfile -File ./scripts/verify.ps1
```

個別に実行する場合は次のコマンドを使用します。

```powershell
dotnet restore DisplayLight.slnx --locked-mode
dotnet format DisplayLight.slnx --verify-no-changes --no-restore
dotnet build DisplayLight.slnx --configuration Release --no-restore
dotnet test DisplayLight.slnx --configuration Release --no-build --no-restore
```

自動テストは実際の電源設定を変更せず、Power Requestの実ハンドルも取得しません。
実機確認済みの項目と未確認の項目は[`docs/TESTING.md`](./docs/TESTING.md)に分けて記録しています。

## 現時点の制約

- Modern Standbyのバッテリー動作では、OSの制約によりスリープ防止を長時間保証できません。
- 利用者が電源ボタン、蓋、スタートメニューから開始したスリープは妨げません。
- タスクバー四辺、複数DPI、ダークテーマ、高コントラスト、Explorer再起動後の通知領域表示は実機確認が残っています。
- コード署名は未実装です。
- v0.1.1以前のZIP利用者は、自動更新を利用するために一度Setupを手動で導入する必要があります。

## 設計資料

- [`DESIGN.md`](./DESIGN.md)：MVP境界、アーキテクチャ、安全性、状態モデル。
- [`docs/OPEN_QUESTIONS.md`](./docs/OPEN_QUESTIONS.md)：確定した判断と後続フェーズの未決事項。
- [`docs/TESTING.md`](./docs/TESTING.md)：自動検証と実機確認の分離。
- [`docs/RELEASING.md`](./docs/RELEASING.md)：VelopackのSetupとGitHub Releaseの作成手順。
- [`docs/adr`](./docs/adr)：採用した技術判断の履歴。
- [`docs/plans/2026-07-15-mvp.md`](./docs/plans/2026-07-15-mvp.md)：MVP実装の実行記録。
- [`PLANS.md`](./PLANS.md)：長い実装作業で使う実行計画の書式。

## Codexでの作業

Codexはリポジトリ直下の`AGENTS.md`を作業規約として読みます。
Codexデスクトップアプリのローカル環境では、次のアクションを登録すると日常作業を短縮できます。

| アクション | コマンド |
|---|---|
| Setup | `pwsh -NoProfile -File ./scripts/setup.ps1` |
| Verify | `pwsh -NoProfile -File ./scripts/verify.ps1` |
| Run | `dotnet run --project ./src/DisplayLight.App/DisplayLight.App.csproj` |

リポジトリ固有のローカル設定はCodexデスクトップアプリの設定画面から登録し、秘密情報をリポジトリへ保存しません。
