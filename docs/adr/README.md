# Architecture Decision Records

ADRは、後から変更理由を追跡する必要がある設計判断を保存します。

## 状態

- `Proposed`：候補であり、まだ実装の前提にしません。
- `Accepted`：現在の実装が従う判断です。
- `Superseded`：新しいADRに置き換えられました。
- `Rejected`：検討したが採用しませんでした。

## ファイル名

`NNNN-short-title.md`の形式で連番を付けます。

## テンプレート

```markdown
# NNNN 判断の題名

- 状態：Proposed
- 日付：YYYY-MM-DD

## 文脈

判断が必要になった条件を書く。

## 判断

選んだ案を書く。

## 結果

得られる性質、制約、移行方法を書く。

## 比較した案

採用しなかった案と理由を書く。
```
