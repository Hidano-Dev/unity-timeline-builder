# Requirements Document

## Project Description (Input)
1 つの CSV/TSV シートに複数の Timeline を記述し、1 回のビルドで複数の TimelineAsset / Prefab / Scene を同時生成できるようにする。現状は 1 シート = 1 TimelineAsset = 1 Prefab = 1 Scene の制約があり（.kiro/specs/timeline-scene-builder/research.md の設計判断）、複数 Timeline を作るには CSV を分割して複数回ビルドする必要がある。この制約を緩和し、CSV 内で Timeline 単位のグルーピング（例: timelineName カラム追加、または Scene 行ごとのセクション区切り）を導入する。影響範囲: BuildSheetParser の列定義・行検証（Scene 行 1 行制約の見直し）、TimelineBuilder のビルドパイプライン（複数アセット出力）、TimelineAssetFactory / PrefabFactory / SceneFactory、BuildRequest / BuildResult モデル（複数出力パス対応）、TimelineBuilderCli、column-definitions.md などのドキュメント。後方互換性: 既存の単一 Timeline CSV（timelineName 無し）はそのまま動作すること。

## Requirements
<!-- Will be generated in /kiro-spec-requirements phase -->
