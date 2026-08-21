# Changelog

## [Unreleased]

- Samples~/BuildMenu を追加しました (TimelineBuilder.Build() をメニューから実行するサンプル。メニューは Tools > Hidano > Unity Timeline Builder)。
- 出力先ディレクトリが存在しない場合に自動作成するよう修正しました (Parent directory must exist エラーの解消)。
- duration を任意にしました。空欄の場合はアセットの長さを使用します。
- clipName を任意にしました。空欄の場合はアセット名を使用します。
- resourcePath のダブルクォート囲み (エクスプローラーの「パスのコピー」形式) を解釈するようにしました。

## [0.1.0] - 2026-08-20

- パッケージの初期構成と UPM マニフェストを追加しました。
