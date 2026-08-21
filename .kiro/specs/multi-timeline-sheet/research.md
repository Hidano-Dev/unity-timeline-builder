# Research & Design Decisions

## Summary
- **Feature**: `multi-timeline-sheet`
- **Discovery Scope**: Extension（既存パイプラインの拡張。新規外部依存なし → Light Discovery を適用）
- **Key Findings**:
  - 現行 `TimelineBuilder.Build` はパース**前**にアセット名・出力パスを確定しており、`timeline` カラム由来の命名（D-2）とは制御フローが逆。パイプラインを「パース → 命名・パス計画 → 全グループ検証 → グループ逐次生成」へ再構成する必要がある（本機能最大の構造変更）
  - 出力ファイルは拡張子固定（`.playable` / `.prefab` / `.unity`）のため、衝突が起き得るのは「グループ間の Timeline 名同士（大文字小文字違い）」と「グループ間の Scene 名同士」のみ。同一グループ内のパス衝突は構造上発生しない
  - 変更対象の型のうち public なのは `BuildResult` / `BuildError` / `BuildRequest` / API / CLI のみ。`ParseOutcome` / `SceneBuildPlan` / 各 Factory は internal のため、後方互換の制約は public 境界に限定して設計できる
  - 非バッチ実行時の Scene 保存確認ダイアログ（`SaveCurrentModifiedScenesIfUserWantsTo`）は現在 `SceneFactory.TryCreate` 内にあり、複数グループではグループ数だけ表示され得る → パイプライン側へ 1 回に集約する

## Research Log

### 現行パイプラインの構造と命名タイミング
- **Context**: 「パース → 命名 → 検証 → 生成」への再構成可否の確認
- **Sources Consulted**: `Editor/Api/TimelineBuilder.cs`, `Editor/Parsing/BuildSheetParser.cs`
- **Findings**:
  - `TimelineBuilder.Build` は冒頭で `assetName`（`request.AssetName` またはシートファイル名）から `timelinePath` / `prefabPath` を確定し、その後に CSV 読み込み・パース・リソース解決・Scene 検証・生成を行う
  - `BuildSheetParser.Parse` は行単位でエラーを収集し、Scene 行 1 行制約・ScenePrefab/SceneBind の前提検証をシート全体スコープで実施している
  - リソース解決エラーがあると Scene 検証（`SceneBuildValidator`）を実行せずに失敗返却している（エラー集約が部分的）
- **Implications**:
  - 命名確定をパース後に移動し、`OutputPathPlanner`（純ロジック）で全グループの出力パスを一括計画する
  - Req 4.2（シート全体の検証完了後にまとめて報告）を満たすため、リソース解決と Scene 検証を全グループ分実行してからエラーを集約する（既存より報告が増える方向の変更であり、レガシー入力の互換を壊さない）

### 衝突判定の発生面の分析（D-8 の判定範囲）
- **Context**: validate-gap 申し送り「衝突判定はビルド内のみか、ディスク上の既存アセットも含むか。同一グループ内衝突の扱い」
- **Sources Consulted**: `TimelineAssetFactory.cs`（上書きログ）、`PrefabFactory.cs`、`SceneFactory.cs`、`SceneBuildValidator.ValidateSceneOutputPath`
- **Findings**:
  - Req 2.6 / 既存仕様は「出力先に既存アセットがあれば上書き + `Overwriting ...` ログ」。ディスク上の既存アセットを衝突扱いにすると上書き仕様と矛盾する
  - 出力名は「Timeline 名 + 固定拡張子」「Scene 名 + `.unity`」であり、拡張子が異なるため衝突は同種出力間（.playable 同士 / .prefab 同士 / .unity 同士）でのみ成立する。.playable と .prefab は常に同じ基底名（グループのアセット名）でペアになるため、同一グループ内の衝突は構造上不可能
  - 既存の `SceneBuildValidator.ValidateSceneOutputPath`（Scene パス vs Timeline/Prefab パスの防御的チェック）はグループ単位で呼べばそのまま維持できる
- **Implications**: 衝突判定スコープは「同一ビルド内の全グループの計画出力パス間」のみとする（下記 Decision 参照）

### 非バッチ実行時の Scene 保存確認ダイアログ
- **Context**: グループ数だけダイアログが出る問題の集約可否
- **Sources Consulted**: `SceneFactory.cs` 64-69 行
- **Findings**: プロンプトは `TryCreate` 冒頭にあり、キャンセル時は `SceneBuildCanceled` を返す。現行では Timeline/Prefab 生成**後**・Scene 生成**前**に 1 回表示される
- **Implications**: プロンプトを `SceneFactory` から Api 層へ移し、「ビルド内で最初の Scene 生成の直前に 1 回だけ」実行する。レガシー入力（グループ 1 + Scene 行 1）では表示タイミング・キャンセル時の成果物状態（Timeline/Prefab は生成済み、Scene 未生成）が現行と同一に保たれる

### テンプレートと E2E テストへの影響
- **Context**: 同梱 `timeline-template.csv` を改変するか、別テンプレートを追加するか
- **Sources Consulted**: `Documentation~/timeline-template.csv`, `Tests/Editor/BundledTemplateE2ETests.cs`
- **Findings**: `BundledTemplateE2ETests` は同梱テンプレートをそのまま入力し、`BundledTemplate.playable` / `.prefab`（シートファイル名由来）を期待する。既存テンプレートに `timeline` カラムを追加すると出力名が Timeline 名由来に変わり、テストと既存ユーザーの出力が変化する
- **Implications**: 既存テンプレートは不変とし、複数 Timeline 用テンプレートを別ファイルとして追加する（下記 Decision 参照）

### 新規外部依存の有無
- **Context**: Light Discovery の技術検証要否
- **Findings**: 本機能は既存の Unity Editor API（`AssetDatabase` / `EditorSceneManager` / `PrefabUtility`）と `com.unity.timeline` 1.8.13 の範囲で完結し、新規ライブラリ・新規 Unity API の導入はない
- **Implications**: Web 調査による新規依存検証は不要。既存コードの統合点分析を discovery の中心とした

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| ハイブリッド拡張（採用） | パーサーをグループ化対応に拡張し、パイプラインを「パース → 計画 → 検証 → 逐次生成」に再構成。Factory / Resolver / Reader は無変更で再利用 | 単一 Timeline 分の生成仕様（Out of scope）に触れない。純ロジックの Planner を単体テスト可能 | Api 層 `Build` の内部再構成が大きい | validate-gap の推奨案 |
| ラッパー方式 | 既存 `Build` を温存し、グループごとに CSV を仮想分割して既存パイプラインを N 回呼ぶ | 既存コード変更最小 | エラー集約（4.2）・行番号保持・衝突計画・BuildResult 統合が困難。AssetDatabase Refresh が N 回走り非効率 | 不採用 |
| 全面書き換え | パイプラインをグループ第一級の新設計に置換 | 最も整った内部構造 | public 互換（5.4, 6.3）維持コストが過大。回帰リスク大 | 不採用 |

## Design Decisions

### Decision: D-8 の衝突判定スコープは「同一ビルド内の計画出力パス間」のみ
- **Context**: validate-gap 申し送り。ディスク上の既存アセットを含むか、同一グループ内衝突の扱い
- **Alternatives Considered**:
  1. ビルド内のみ — 既存アセットは Req 2.6 の上書き仕様に委ねる
  2. ディスク上の既存アセットも衝突扱い — 上書き仕様（2.6）と矛盾し、再実行の冪等性（同名上書き）を壊す
- **Selected Approach**: 案 1。`OutputPathPlanner` が全グループの計画出力パス（OrdinalIgnoreCase）を突き合わせ、後出グループにサフィックスを付加。既存アセットへの上書きは従来どおり `Overwriting ...` ログで継続
- **Rationale**: 上書きは意図されたワークフロー（冪等な再実行）であり、衝突扱いにすると再実行のたびにサフィックスが増殖する
- **Trade-offs**: ビルド跨ぎの意図しない上書きは検出しない（既存仕様と同等）
- **Follow-up**: 同一グループ内衝突は拡張子固定により構造上発生しないことをテストで裏付ける（Scene 名 = Timeline 名のケース）

### Decision: 生成順序は「グループごとに Timeline → Prefab → Scene」の逐次実行
- **Context**: validate-gap 申し送り。「全 Timeline → 全 Prefab → 全 Scene」との比較
- **Selected Approach**: グループ初出順に、グループ単位で Timeline → Prefab → Scene を完結させる。失敗時はその時点で fail-fast（D-7）
- **Rationale**: (1) `SceneFactory` の `NewScene(Single)` は旧シーンアンロード時にマネージド参照のみのアセットを破棄し得るため、Scene 生成を跨いで参照を保持する「全 Timeline → … → 全 Scene」方式はパスからの再ロード防御を全面的に必要とする。グループ単位なら生成直後のアセットを直近で使い切れる。(2) fail-fast 時に「完結したグループ / 失敗したグループ」の境界が明確になり、Req 4.3 の報告が単純になる
- **Trade-offs**: `AssetDatabase.SaveAssets` / 同期 Import がグループ数だけ走る（Editor ツールの規模では許容）
- **Follow-up**: グループ間で Scene 生成 → 次グループ Timeline 生成の遷移が安全であることを統合テストで確認

### Decision: 同梱テンプレートは不変とし、複数 Timeline 用テンプレートを別ファイルで追加
- **Context**: validate-gap 申し送り。`BundledTemplateE2ETests` と既存ユーザーの出力への影響
- **Alternatives Considered**:
  1. 既存 `timeline-template.csv` に `timeline` カラムを追加 — 出力名がシートファイル名由来から Timeline 名由来に変わり、既存テスト・既存ユーザーの成果物名が変化する
  2. 別テンプレート `multi-timeline-template.csv` を追加 — 既存は完全温存
- **Selected Approach**: 案 2。`Documentation~/multi-timeline-template.csv` を追加し、E2E テストも新テンプレート用を追加する
- **Rationale**: Req 6.1（レガシー入力の完全温存）と Req 7.1（記入例の追加）を両立する最小手段
- **Trade-offs**: テンプレートが 2 ファイルになる（列定義ドキュメントで役割を明記して緩和）

### Decision: BuildResult は加法的拡張（`Outputs` コレクション追加 + レガシープロパティは先頭グループへ写像）
- **Context**: Req 4.1 / 5.1（複数出力の判別可能な返却）と Req 6.3 / 5.4（レガシー呼び出し元の互換）の両立
- **Alternatives Considered**:
  1. 新結果型 `MultiBuildResult` を別 API で返す — API 分裂、CLI 二重化
  2. `BuildResult` に `IReadOnlyList<BuildOutput> Outputs` を追加し、既存プロパティ（`TimelineAssetPath` / `PrefabPath` / `ScenePath`）は先頭グループ（初出順）の値へ写像
- **Selected Approach**: 案 2。既存 public コンストラクタ 2 種は維持し、単一 `BuildOutput` を合成する。新コンストラクタで `Outputs` を受け取る
- **Rationale**: レガシー入力ではグループが 1 つのため既存プロパティの値は従来と完全一致（6.3）。複数グループ利用者は `Outputs` を参照する
- **Trade-offs**: 複数グループ時にレガシープロパティを読む旧コードは先頭グループしか見えない（新フォーマット利用は新規呼び出し元のため許容）
- **Follow-up**: `BuildError` にも `TimelineName`（nullable）を加法的に追加し、Req 4.4 の対象 Timeline 特定を可能にする

### Decision: Timeline 名の妥当性検証は Scene 名と同一規則のエラー中断（サニタイズしない）
- **Context**: requirements の申し送り「ファイル名に使えない文字を含む Timeline 名の扱い」
- **Selected Approach**: `BuildSheetParser.IsValidSceneName` と同一の判定（制御文字・`<>:"/\|?*`・末尾ピリオド/空白・`.`/`..` を拒否）を共通ヘルパー化し、Timeline 名にも適用。違反は行番号付き `RowValidationError` で中断（1.5）
- **Rationale**: D-2「Timeline 名が常にアセット名」の下でサニタイズすると CSV 記載と出力名が乖離し、D-8 のサフィックスと二重の名前変換になる。Scene 名の既存挙動（エラー中断）と規則を揃える
- **Trade-offs**: 制作者は不正文字を修正する必要がある（エラーメッセージで行番号と名前を明示して緩和）

### Decision: グループ集約キーは Ordinal 比較、パス衝突判定は OrdinalIgnoreCase
- **Context**: `Title` と `title` のような大文字小文字違いの Timeline 名の扱い
- **Selected Approach**: グループの同一性は Ordinal（完全一致）で判定し、大文字小文字のみ異なる名前は別グループとする。その出力パスは OrdinalIgnoreCase の衝突判定に掛かり、後出グループが D-8 のサフィックスでリネームされ警告される
- **Rationale**: SceneBind の Track 名照合など既存の名前比較は Ordinal で統一されている。IgnoreCase で暗黙にグループを統合すると、制作者の意図しない行の混在が黙って起きる。D-8 の「衝突 → サフィックス + 警告」がそのまま適用でき、追加規則が不要
- **Trade-offs**: タイポ由来の別グループ化は警告付きリネームとして顕在化する（黙殺されない）

### Decision: サフィックス書式は `{名前} (n)`（半角スペース + 丸括弧、n は 1 起点）
- **Context**: Req 2.5 / 3.7 の例示 `(1)` の具体化
- **Selected Approach**: 衝突した出力単位の基底名に ` (1)` を付加し、なお衝突する場合は n を増分。衝突単位は「グループのアセット名（.playable と .prefab のペア）」と「Scene 名（.unity）」の 2 種類で、ペアは常に同名を維持する
- **Rationale**: Windows エクスプローラー慣行と同じ書式で説明が不要。ペア単位のリネームにより Prefab と Timeline の対応が保たれる

### Decision: Scene 保存確認ダイアログはビルドにつき 1 回へ集約
- **Context**: validate-gap 申し送り
- **Selected Approach**: `SaveCurrentModifiedScenesIfUserWantsTo` の呼び出しを `SceneFactory` から Api 層へ移動し、「非バッチ かつ Scene 行を持つグループが 1 つ以上ある」場合に、最初の Scene 生成の直前に 1 回だけ実行。キャンセル時は `SceneBuildCanceled` で fail-fast
- **Rationale**: レガシー入力ではタイミング（Timeline/Prefab 生成後・Scene 生成前）とキャンセル時の成果物状態が現行と同一。複数グループでもダイアログは 1 回
- **Trade-offs**: `SceneFactory` の契約からダイアログ責務が外れる（internal のため影響はパッケージ内とテストのみ）

## Risks & Mitigations
- `NewScene(Single)` によるマネージド参照破棄がグループ間生成へ波及 — グループ単位生成順序 + 既存のパスからの再ロード防御を維持し、複数グループ統合テストで検証
- リネーム後の暗黙参照ずれ（Req 3.3）— `SceneBuildContext` を計画済み（サフィックス適用後）のパスから構築することで構造的に一致を保証し、リネーム発生ケースの統合テストを追加
- `ParseOutcome` 再構成による既存テストの広範な修正 — internal 型のため公開互換に影響なし。`BuildSheetParserTests` をグループ化前提へ更新
- エラー集約強化（検証を最後まで実行）によるレガシー入力の報告件数増 — 成否・エラーコード・メッセージ形式は不変であり互換とみなす。統合テストで確認

## References
- `.kiro/specs/timeline-scene-builder/design.md` — 既存レイヤー規約（Models → Parsing → Resources → Building → Api → Cli）と 2 フェーズコミットパターンの基準
- `.kiro/specs/timeline-scene-builder/research.md` — 「1 シート = 1 Timeline」制約の原設計判断
- [Unity Manual: EditorSceneManager](https://docs.unity3d.com/ScriptReference/SceneManagement.EditorSceneManager.html) — Scene 保存 API（既存使用の再確認のみ）
