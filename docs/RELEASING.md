# DisplayLightのリリース手順

DisplayLightは、Windows 11 x64向けの自己完結型SetupとPortable ZIPをVelopackで生成します。
通常利用にはSetupを使用し、インストール後の安定版はアプリ内から更新できます。
Portable ZIPは試用向けであり、アプリ内更新を利用できません。

NuGetの`Velopack`とローカルツールの`vpk`は同じバージョンへ固定します。
どちらかを更新する場合は、両方を同じ変更で更新してください。

## ローカル成果物の作成

リポジトリ直下で検証し、リリース候補を生成します。

```powershell
pwsh -NoProfile -File ./scripts/verify.ps1
pwsh -NoProfile -File ./scripts/package.ps1 -Version 0.1.2 -NoRestore
```

`scripts/package.ps1`は自己完結・単一ファイルで発行した後、`artifacts/release`へ次のVelopack成果物を生成します。

- `DisplayLight-win-Setup.exe`
- `DisplayLight-win-Portable.zip`
- `DisplayLight-<version>-full.nupkg`
- `releases.win.json`

Setupで新規インストールし、通知領域アイコン、フライアウト、終了、手動更新確認をWindows 11 x64の実機で確認します。
Portable ZIPでは「更新を確認」がインストール版を必要とする案内を表示することも確認します。
電源状態を変える確認では`docs/TESTING.md`の安全規約に従います。

## GitHub Releaseの作成

リリース対象のコミットを既定ブランチへ取り込んでCIの成功を確認した後、注釈付きタグを作成します。

```powershell
git switch main
git pull --ff-only
git tag -a v0.1.2 -m "DisplayLight 0.1.2"
git push origin v0.1.2
```

`vMAJOR.MINOR.PATCH`形式のタグをpushすると、Releaseワークフローが次の処理を行います。

1. 書式、Releaseビルド、自動テストを検証します。
2. 前回のVelopack Releaseを取得し、更新フィードを引き継ぎます。
3. タグのバージョンで自己完結型のSetup、Portable ZIP、更新パッケージを生成します。
4. SetupとPortable ZIPのArtifact Attestationを生成します。
5. Velopack成果物をGitHub Releaseの安定版として公開します。

最初のVelopack Releaseでは前回成果物が存在しないため、前回Releaseの取得失敗を許容します。
それ以降の取得失敗はワークフロー上では継続しますが、公開後に更新フィードの内容を必ず確認します。
失敗したタグを移動せず、修正後はパッチバージョンを上げた新しいタグを使用します。

## 公開後の確認

- ReleaseがDraftまたはPrereleaseではなく公開状態になっている。
- Setup、Portable ZIP、full NuGet package、`releases.win.json`が添付されている。
- 成果物とアプリのバージョンがタグに一致している。
- Setupから新規インストールして起動できる。
- 一つ前のインストール版から更新を検出し、承認後に再起動できる。
- スリープ防止中の更新でPower Requestが残らない。
- `gh attestation verify DisplayLight-win-Setup.exe --repo zwnj/DisplayLight`が成功する。

コード署名を導入するまでは、Windows SmartScreenが発行元不明の警告を表示する可能性があります。
リリースノートには未署名であること、Setupが通常利用向けであること、Portable版では自動更新できないことを明記します。

## 既存ZIP利用者の移行

v0.1.1以前のZIPにはVelopackのインストール情報がないため、アプリ内更新は開始できません。
既存利用者は一度だけ新しいSetupを手動で実行します。
設定は従来と同じ`%LOCALAPPDATA%\DisplayLight\settings.json`を使用します。
