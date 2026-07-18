# DisplayLightのリリース手順

DisplayLight 0.1系は、Windows 11 x64向けの自己完結型ZIPをGitHub Releasesで配布します。
.NETランタイムを同梱するため、利用者が.NETを別途インストールする必要はありません。

インストーラー、コード署名、自動更新、自動起動は0.1系の配布対象に含めません。

## GitHubリポジトリの準備

GitHub上にリポジトリを作成し、ローカルリポジトリの`origin`へ登録します。

```powershell
git remote add origin https://github.com/<owner>/DisplayLight.git
git push -u origin HEAD
```

変更をレビューして既定ブランチへ取り込んだ後、GitHub Actionsが有効であることを確認します。
Releaseワークフローはリリース作成のために`contents: write`、成果物の来歴証明のために`id-token: write`と`attestations: write`を使用します。

公開リポジトリとして配布する場合は、初回公開前にライセンスを決定して`LICENSE`を追加します。
ライセンスがない状態では、ソースコードを閲覧できても第三者へ利用、変更、再配布の許諾を示せません。

## ローカル成果物の作成

リポジトリ直下で検証を実行し、リリース候補を生成します。

```powershell
pwsh -NoProfile -File ./scripts/verify.ps1
pwsh -NoProfile -File ./scripts/package.ps1 -Version 0.1.0 -NoRestore
```

成果物は`artifacts/release`へ生成されます。

- `DisplayLight-0.1.0-win-x64.zip`
- `DisplayLight-0.1.0-win-x64.zip.sha256`

ZIPを展開し、`DisplayLight.App.exe`から起動できることをWindows 11 x64の実機で確認します。
電源設定を変更する確認では、`docs/TESTING.md`の手順と安全規約に従います。

## GitHub Releaseの作成

リリース対象のコミットが既定ブランチへ取り込まれ、CIが成功した後に注釈付きタグを作成します。

```powershell
git switch main
git pull --ff-only
git tag -a v0.1.0 -m "DisplayLight 0.1.0"
git push origin v0.1.0
```

`vMAJOR.MINOR.PATCH`形式のタグをpushすると、Releaseワークフローが次を実行します。

1. 書式、Releaseビルド、自動テストを検証する。
2. タグのバージョンをアセンブリへ設定する。
3. Windows x64向けの自己完結型ZIPを生成する。
4. ZIPのSHA-256チェックサムと来歴証明を生成する。
5. GitHubの自動生成リリースノートを使ってReleaseを公開する。

失敗したワークフローからReleaseは作成されません。
原因を修正した場合は既存タグを移動せず、パッチバージョンを一つ上げた新しいタグを作成します。

## 公開後の確認

- ReleaseがDraftまたはPrereleaseではなく公開状態になっている。
- ZIPと`.sha256`が添付されている。
- ZIPのファイル名とアプリのバージョンがタグに一致している。
- ZIPを新しいフォルダーへ展開して起動できる。
- 通知領域アイコン、フライアウトの開閉、終了操作が動作する。
- `gh attestation verify <zip> --repo <owner>/DisplayLight`で来歴を検証できる。

コード署名を導入するまでは、Windows SmartScreenが発行元不明の警告を表示する可能性があります。
リリースノートには未署名であることと、SHA-256の確認方法を記載します。
