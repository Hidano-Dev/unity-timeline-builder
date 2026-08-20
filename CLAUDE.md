# 開発メモ

## SDD ワークフロー

@.claude/rules/sdd-workflow.md

上記は [orchestration-development-template](https://github.com/Hidano-Dev/orchestration-development-template) から初期化時 (Template Init) / 同期時 (Orchestration Sync) に取り込まれる SDD ワークフローメモへの参照。テンプレートリポジトリ自体には実体が無いため、取り込み前は単に読み込まれない。

## Unity Editor をコマンドラインで起動するとき

CLI から Unity Editor を起動して自動化処理（ビルド、テスト、エージェント操作など）を行う場合は、**`-automated` フラグを付ける**。

```
"C:\Program Files\Unity\Hub\Editor\<バージョン>\Editor\Unity.exe" -projectPath "<プロジェクトディレクトリ>" -automated
```

- 未保存シーンの確認などの**ブロッキングダイアログが表示されなくなり**、各ダイアログの既定アクションが自動選択されるため、処理が途中で停止しない。
- `-batchmode` と違い GUI ありの起動でも使える（Unity 6 系で確認）。
- 注意: 「すべて OK が押される」のではなく**各ダイアログの既定動作**に従う。意図しない変更が起きる可能性があるため、自動化専用の起動にのみ付け、実行前に git がクリーンな状態であることを確認する。普段の手作業用 Editor には付けない。
