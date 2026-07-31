# ChatGPTデスクトップ版Codex連携の設計整理

## 目的

Windows版ChatGPTデスクトップアプリ内のCodexをDisplayLightの最初のエージェント連携対象として明確にし、観測可能なHookイベントから安全にスリープ防止状態を組み立てる方法を定めます。

## 完了条件

- `DESIGN.md`が旧Codex単体アプリやCLIを最初の対象として扱っていない。
- Hook、連携ブリッジ、名前付きパイプ、Coreの状態集約という推奨データフローが記載されている。
- 公式仕様で確認できた事実と、実機観測が必要な事項が区別されている。
- `docs/OPEN_QUESTIONS.md`と`docs/TESTING.md`が新しい対象範囲と一致している。
- 文書差分を確認し、`pwsh -NoProfile -File ./scripts/verify.ps1`を実行する。

## 対象外

- Hook連携ブリッジ、IPC、セッション集約の実装
- 利用者のCodex設定またはプラグインの変更
- 自動スリープの実行
- Codex CLI、IDE拡張、クラウドタスクへの対応

## 現在の設計

`DESIGN.md`はCodex AppとCLIを同列の検証対象としていました。
2026年7月9日にCodex単体アプリはChatGPTデスクトップアプリへ統合され、現在はChatGPT内の専用Codexビューとして提供されています。
CodexのHook設定はChatGPTデスクトップアプリ、CLI、IDE拡張で共通ですが、公開されている共通Hook payloadには起動元サーフェスを直接示すフィールドがありません。

Q8からQ12およびQ16はAgent Previewの動作へ影響します。
起動元サーフェスの識別方法とHookの導入方式は新しい未決事項として扱います。

## 実装手順

1. 現行の公式Codexマニュアルから、ChatGPT統合、設定レイヤー、Hookイベントとpayloadを確認する。
2. 対象範囲とイベントの意味を`DESIGN.md`へ反映する。
3. 未決事項と実機確認項目を更新する。
4. 差分と文書間の表記を確認し、標準検証を実行する。

## 進捗

- [x] 2026-07-28 ChatGPTデスクトップアプリへのCodex統合を公式マニュアルで確認した。
- [x] 2026-07-28 Hookの共通フィールドと`SessionStart`、`SessionEnd`、`UserPromptSubmit`、`PermissionRequest`、`Stop`の意味を確認した。
- [x] 2026-07-28 設計、未決事項、検証項目を更新した。
- [x] 2026-07-28 差分と標準検証の結果を記録した。

## 発見と判断

- 第一対象はWindows版ChatGPTデスクトップアプリ内のCodexとし、CLIとIDE拡張は初期対象外とします。
- Codex Hookから標準入力でJSONを受ける短命な連携ブリッジと、現在ユーザーに限定した名前付きパイプを推奨構成とします。
- `UserPromptSubmit`はターン開始、`PermissionRequest`は権限入力待ち、`Stop`はターン停止として扱えますが、いずれもスレッド全体の完了を保証しません。
- `SessionEnd`はアーカイブ、削除、Codexの正常終了、または開かれていない状態で30分アイドルになった場合に発火するため、即時の作業完了判定には使いません。
- Hookに含まれるプロンプト、回答、transcriptパスはDisplayLightへ転送または保存しません。
- 公開Hook payloadだけではChatGPTデスクトップアプリとCLIまたはIDE拡張を区別できないため、実機観測前に識別方式を確定しません。

## 検証結果

- `git diff --check`：成功。
- `pwsh -NoProfile -File ./scripts/verify.ps1`：成功。
- Releaseビルド：警告0件、エラー0件。
- Core単体テスト：22件成功。
- App単体テスト：71件成功。
- ChatGPTデスクトップアプリでのHook実機観測：未実施。

## 残作業

- ChatGPTデスクトップアプリ上でHook payload、親プロセス、環境変数、同時セッション時の発火順を診断用ビルドで観測する。
- Hookの導入を利用者設定への追記、または信頼確認を伴うCodexプラグインのどちらにするか決める。
- アプリ未起動時、欠落イベント、有効期限、完了判定を実機観測後に決める。
