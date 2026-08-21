# Research & Design Decisions

## Summary
- **Feature**: `timeline-scene-builder`
- **Discovery Scope**: Extension(既存 UPM パッケージ `com.hidano.unity-timeline-builder` の拡張。Scene 生成系コードは全面新規)
- **Key Findings**:
  - CSV 拡張は trackType 列の行種別キー(`Scene` / `ScenePrefab` / `SceneBind`)方式が、既存のヘッダーレス固定 7 列フォールバックと Google スプレッドシートエクスポート互換(1 ファイル 1 シート)を両立できる唯一の低リスク案
  - `PlayableDirector.SetGenericBinding` によるバインディングは PlayableDirector と共に Scene 側へシリアライズされるため、`MarkSceneDirty` → `SaveScene` の順で永続化できる(公式ドキュメントで確認)
  - `EditorSceneManager.SaveScene` は明示パス指定時に dirty 状態に関わらず保存し、既存 `.unity` を上書きする(`.meta` / GUID は維持)。batchmode でも同一挙動
  - 既存 `TimelineAssetFactory` はトラックを `(trackType, trackName)` キーで一意に集約しているため、Track 名によるバインド指定(Req 1.3)は同一ビルド生成タイムラインに対して Phase A(パース直後)で事前検証できる

## Research Log

### CSV フォーマット拡張方式(Research Needed #1)
- **Context**: Scene 構築情報を既存のクリップ行と同一ファイル内で区別する必要がある(Req 1.2)。既存パーサーはヘッダー行(`trackType` 列名を含む行)の有無を判定し、ヘッダーレス時は固定 7 列順で解釈する。この挙動と Google スプレッドシートの File > Download エクスポート(1 ファイル 1 シート、セクション構造なし)を壊せない。
- **Sources Consulted**: 既存実装 `BuildSheetParser.cs`(ヘッダー判定・既定列順)、`TrackBuilderRegistry.cs`(trackType キーの登録制)、`Documentation~/column-definitions.md`
- **Findings**:
  - trackType 列は既に「登録済みキーによる行の種別分岐」の役割を持つ。行種別キーを追加してもパース規約(RFC 4180 / ヘッダー判定 / 列マッピング)は無変更で済む
  - セクションマーカー方式(`[Scene]` 等の区切り行)は、ヘッダーレス固定列順との整合が崩れ、スプレッドシート上での行ソートにも弱い
  - 列を追加する方式(8 列目以降)は、ヘッダーレス固定 7 列フォールバックの既定順と衝突し、既存ファイルとの互換リスクがある
- **Implications**: trackType 列に予約行種別キー `Scene` / `ScenePrefab` / `SceneBind` を導入し、既存 7 列の意味を行種別ごとに再定義する(Design Decision 参照)。`TrackBuilderRegistry.Register` は予約キーと衝突する登録を拒否する必要がある。

### EditorSceneManager の新規作成・保存・上書き挙動(Research Needed #3)
- **Context**: batchmode / EditMode テストの両方で Scene ファイル(.unity)を生成・上書きする必要がある(Req 2.1, 2.5)。
- **Sources Consulted**:
  - [EditorSceneManager.NewScene](https://docs.unity3d.com/ScriptReference/SceneManagement.EditorSceneManager.NewScene.html)
  - [EditorSceneManager.SaveScene](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/SceneManagement.EditorSceneManager.SaveScene.html)
  - [EditorSceneManager クラス](https://docs.unity3d.com/ScriptReference/SceneManagement.EditorSceneManager.html)
- **Findings**:
  - `NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)` は既存の開いている Scene を閉じ、Untitled の空 Scene をメモリ上に作る。EditMode(非再生時)専用 API であり batchmode でも動作する
  - `NewSceneMode.Additive` は「Untitled Scene が既に開いていると追加できない」制約がある。batchmode 起動直後は Untitled Scene が開いているため、Additive 方式は batchmode で失敗し得る → Single 方式を採用
  - `SaveScene(scene, path)` は dirty 状態に関係なく保存し、既存パスへは無確認で上書きする。既存 `.meta`(GUID)は維持される
  - Single モードは呼び出し元の開いている Scene(未保存変更を含む)を破棄する。対話モードでは保存確認が必要
- **Implications**: SceneFactory は `NewScene(EmptyScene, Single)` → 構築 → 全バインド成功後に 1 回だけ `SaveScene` する(失敗時は未保存のまま中断し、既存 `.unity` を破壊しない)。対話モード(`Application.isBatchMode == false`)では事前に `EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()` を呼び、キャンセル時は中断エラーを返す。EditMode テストは「テスト前に開いていた Scene に依存しない」前提で書く。

### SetGenericBinding の永続化手順(Research Needed #4)
- **Context**: Req 4.2「保存後の Scene ファイルを再度開いた際にもバインディングが保持される」を保証する手順の確定。
- **Sources Consulted**:
  - [PlayableDirector.SetGenericBinding](https://docs.unity3d.com/ScriptReference/Playables.PlayableDirector.SetGenericBinding.html)
  - [PlayableDirector.GetGenericBinding](https://docs.unity3d.com/ScriptReference/Playables.PlayableDirector.GetGenericBinding.html)
  - [Assign Animator to AnimationTrack (Unity Discussions)](https://discussions.unity.com/t/assign-animator-component-to-an-animationtrack-in-script/789180)
- **Findings**:
  - バインディングテーブルは「キー = TrackAsset、値 = Scene 内オブジェクト」の対応として PlayableDirector コンポーネント側(= Scene 側)にシリアライズされる
  - AnimationTrack のバインド値は `Animator` コンポーネント参照を渡す
  - エディタスクリプトから変更した場合、`EditorSceneManager.MarkSceneDirty(scene)` を経て `SaveScene` すれば .unity に永続化される
- **Implications**: 手順は「全 `SetGenericBinding` 適用 → `MarkSceneDirty` → `SaveScene`」で確定。検証は統合テストで「保存 → `OpenScene` で再オープン → `GetGenericBinding` が対象 Animator を返す」ことを確認する。

### 既存アーキテクチャとの統合ポイント
- **Context**: validate-gap 分析の結果を踏まえた統合方針の確定。
- **Sources Consulted**: `TimelineBuilder.cs`(2 フェーズコミット)、`BuildSheetParser.cs`、`TimelineAssetFactory.cs`(TrackKey 集約)、`PrefabFactory.cs`、`BuildResult.cs` / `BuildError.cs`、`.kiro/specs/timeline-prefab-builder/design.md`
- **Findings**:
  - 既存の 2 フェーズコミット(Phase A: 検証・収集 / Phase B: 生成)と `BuildError` 収集パターンは Scene 構築にそのまま適用できる
  - `BuildResult` は public 4 引数コンストラクタを持つ(呼び出し互換維持が必要 → オーバーロード追加)。`BuildRequest` は可変プロパティクラス(今回は追加不要)。`BuildErrorCode` への enum 追加はソース互換
  - `TimelineAssetFactory` の `(trackType, trackName)` 一意集約により、同一ビルド生成タイムラインの AnimationTrack 存在検証(Req 4.7)はクリップ行から Phase A で判定できる
  - Scene 関連 API(`EditorSceneManager` / `SetGenericBinding` / `PrefabUtility.InstantiatePrefab`)の使用箇所はゼロ。新規コンポーネントとして追加する
- **Implications**: Option C ハイブリッド構成(新規: SceneRows モデル + SceneFactory + TrackBindingApplier、小規模拡張: Parser / TimelineBuilder / Cli / BuildResult / BuildErrorCode / ドキュメント)を採用。新規パッケージ依存なし(Unity 6000.0 / com.unity.timeline 1.8.13 のまま)。

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| A: パーサー全面改修(セクション形式) | CSV をセクション区切りで Scene 部とクリップ部に分割 | Scene 情報の表現自由度が高い | ヘッダーレス固定列順と非互換。既存パース規約の変更が必要 | 却下 |
| B: 別ファイル方式 | Scene 構築情報を別 CSV で受け取る | 既存パーサー無変更 | Req 1.2(同一ファイル内で区別)に違反。CLI 引数も増える | 却下 |
| C: 行種別キー + 新規コンポーネント(採用) | trackType 列の予約キーで行を分岐し、Scene 構築は新規 Factory/Applier に分離 | 既存パース規約・レイヤー構造・2 フェーズコミットを維持。変更範囲が最小 | 7 列への意味の重ね合わせ(列定義ドキュメントで明示が必要) | validate-gap 推奨案 |

## Design Decisions

### Decision: CSV 拡張は trackType 列の行種別キー方式(Research Needed #1)
- **Context**: Scene 構築情報を既存フォーマットと同一ファイルで区別しつつ、ヘッダーレス固定 7 列フォールバックと Google スプレッドシートエクスポート互換を維持する。
- **Alternatives Considered**:
  1. セクションマーカー方式 — 固定列順・行ソートと非互換
  2. 列追加方式(8 列目以降) — ヘッダーレス既定順と衝突
- **Selected Approach**: trackType 列の予約キー `Scene` / `ScenePrefab` / `SceneBind`(大文字小文字無視)で行を分岐し、既存 7 列の意味を行種別ごとに再定義する(列対応表は design.md の列仕様を正とする)。
- **Rationale**: パース規約(RFC 4180・ヘッダー判定・列マッピング・固定列順フォールバック)を一切変更せず、行単位の後方互換(Req 1.5)が構造的に保証される。
- **Trade-offs**: 列名(`trackName` / `resourcePath`)と行種別ごとの意味に乖離が生じる → 列定義ドキュメントとテンプレートで行種別別の対応表を明示して補う。
- **Follow-up**: `TrackBuilderRegistry.Register` に予約キー衝突ガードを追加。ヘッダーレス CSV に Scene 行を混在させた E2E テストで確認。

### Decision: 1 シート = 1 Scene(Research Needed #2)
- **Context**: 1 ファイルで複数 Scene を定義可能にするか。
- **Alternatives Considered**:
  1. 複数 Scene 対応(Scene 行ごとにグルーピング) — 行の帰属規則(どの ScenePrefab / SceneBind がどの Scene か)が必要になり、フォーマットが複雑化
- **Selected Approach**: `Scene` 行は 1 ファイルに高々 1 行。2 行以上は行番号付き `RowValidationError`。
- **Rationale**: 既存の「1 シート = 1 TimelineAsset = 1 Prefab」の対称性を維持し、`ScenePrefab` / `SceneBind` 行の帰属が自明になる。複数 Scene は複数シートの複数回実行で実現できる。
- **Trade-offs**: 1 回の実行で複数 Scene を作れない(CI 側でのループ実行で代替)。
- **Follow-up**: なし。

### Decision: Scene 生成は Single モード + 保存 1 回のアトミック方式(Research Needed #3)
- **Context**: batchmode / EditMode テスト / 対話モードの全てで安全に Scene を生成・上書きする。
- **Alternatives Considered**:
  1. `NewSceneMode.Additive` で現在の Scene を保持 — batchmode 起動直後の Untitled Scene と競合し失敗し得る
  2. 生成途中で逐次保存 — 失敗時に部分生成の .unity が残る
- **Selected Approach**: `NewScene(EmptyScene, Single)` で空 Scene を作り、全構築・全バインドが成功した場合のみ `MarkSceneDirty` → `SaveScene(scene, path)` を 1 回実行。対話モードでは事前に `SaveCurrentModifiedScenesIfUserWantsTo()` を呼び、キャンセルは `SceneBuildCanceled` で中断。
- **Rationale**: batchmode で確実に動作し、失敗時に既存 .unity を破壊しない(未保存のまま中断)。`SaveScene` の上書きは `.meta` / GUID を維持する。
- **Trade-offs**: 対話モードでは実行後に生成 Scene が開いた状態になる(利用上はむしろ確認に便利なため許容)。
- **Follow-up**: EditMode テストは開いている Scene に依存しない前提で記述。上書き再実行テストで GUID 維持を検証。

### Decision: バインディング永続化手順(Research Needed #4)
- **Selected Approach**: `director.SetGenericBinding(animationTrack, animator)` を全 `SceneBind` 行に適用 → `EditorSceneManager.MarkSceneDirty(scene)` → `SaveScene`。統合テストで再オープン後の `GetGenericBinding` を検証(Req 4.2)。
- **Rationale**: バインディングは PlayableDirector(Scene 側)にシリアライズされるため、この順序で必要十分。Research Log 参照。

### Decision: 同一ビルド生成 TimelineAsset の参照規約(Research Needed #5)
- **Context**: `Scene` 行から「同一の構築処理で生成した TimelineAsset」(Req 2.3)をどう指定するか。
- **Alternatives Considered**:
  1. アセット名で指定 — 1 シート 1 タイムラインなので名前指定は冗長かつ綴りミスの温床
  2. 出力パスで指定 — 生成前のパスを利用者が組み立てる必要があり脆い
- **Selected Approach**: `Scene` 行の resourcePath 列が**空欄なら同一ビルド生成の TimelineAsset**(暗黙参照)、**`Assets/` 始まりのパスなら既存 TimelineAsset** を明示参照。それ以外の値は `SceneTimelineNotFound` エラー。
- **Rationale**: 1 シート = 1 TimelineAsset の不変条件により暗黙参照が一意に定まる。既存アセット参照も同じ列で表現でき、resourcePath 列の「参照先」セマンティクスとも一致する。
- **Trade-offs**: 空欄の意味が「未指定」ではなく「暗黙参照」になる → 列定義ドキュメントで明示。

### Decision: GameObject 名探索仕様(Research Needed #6)
- **Context**: Req 4.1 / 4.5 のバインド対象探索範囲・一致規則・重複判定単位の確定。
- **Selected Approach**:
  - 探索範囲: 生成 Scene の全ルート配下の全階層(Prefab インスタンス内部を含む)。**非アクティブ GameObject も含む**(`GetComponentsInChildren<Transform>(true)` 相当の走査)
  - ただし本ツールが自動生成した PlayableDirector 用 GameObject(名前 = アセット名)は探索対象から除外する(利用者が名前を制御できないオブジェクトとの偶発的な重複エラーを防ぐ。Animator を持たないため除外しても Req 4.1 と矛盾しない)
  - 一致規則: GameObject 名の完全一致(Ordinal、大文字小文字区別)
  - 重複判定: Scene 全体(全 Prefab インスタンス横断)で同名が 2 個以上見つかった場合に `BindTargetDuplicated`。バインド行単位ではなく名前単位で判定
- **Rationale**: 演出用オブジェクトは初期非アクティブのことが多く、非アクティブ除外は実用上の罠になる。Ordinal 完全一致は Unity のオブジェクト名運用(大文字小文字区別)と一致し、偶発一致を防ぐ。
- **Trade-offs**: 階層パス指定(`Root/Child`)は非対応(将来拡張)。同名オブジェクトが必要な構成では利用者側のリネームが必要。

### Decision: PlayableDirector は Prefab 参照ではなく素の GameObject として配置
- **Context**: 既存パイプラインは PlayableDirector 付き Prefab を生成しており、それを Scene に配置する案もあった。
- **Alternatives Considered**:
  1. 生成 Prefab をインスタンス配置して Director として使う — バインディング設定が Prefab インスタンスのオーバーライドになり、Prefab 側更新との整合管理が複雑化
- **Selected Approach**: Scene 直下に素の GameObject(名前 = アセット名)を新規作成し `PlayableDirector` を追加、`playableAsset` に TimelineAsset を割り当てる。既存 Prefab 生成はそのまま継続する(後方互換)。
- **Rationale**: バインディングが Scene 完結のプレーンなシリアライズになり、オーバーライド起因の不具合を避けられる。Req 2.2 は「PlayableDirector を持つ GameObject の配置」であり Prefab 経由を要求しない。

### Decision: Track 名照合の厳格性と重複時の扱い(validate-design レビュー反映)
- **Context**: (1) Phase A の Track 名事前検証がカスタム TrackBuilder 登録(レジストリ拡張性)と衝突し得る。(2) 既存 TimelineAsset を明示参照した場合、同名 AnimationTrack が複数存在するケースの挙動が未定義だった。
- **Selected Approach**:
  1. Phase A の事前検証は組み込みキー `Animation` のクリップ行のみ厳格照合し、組み込み以外のキーのクリップ行が存在する構成(カスタム TrackBuilder)では Phase B の実 TimelineAsset 照合に委ねる
  2. Track 名に一致する AnimationTrack が複数存在する場合は、GameObject 名重複(Req 4.5)と対称に専用エラー `BindTrackDuplicated`(重複 Track 名と件数をメッセージに含む)で構築を中断する
- **Rationale**: カスタムトラックが AnimationTrack を生成する可能性を Phase A では否定できないため誤検出を避ける。同一ビルド生成 Timeline は `(trackType, trackName)` 集約で同名重複が構造的に発生しないため、重複エラーは既存 Timeline 参照時のみ実質的に発生する。`BindTrackNotFound` の使い回しは原因特定(Req 4 系の意図)を損なうため新コードとした。
- **Follow-up**: カスタム TrackBuilder の外部公開シナリオが生じたら Phase A 検証規則を再検討(design.md の Revalidation Triggers に登録済み)。

## Risks & Mitigations
- Phase B(Scene 生成中)の失敗で中途半端な状態が残る — 保存を最後の 1 回に集約し、失敗時は未保存のまま中断(既存 .unity は無傷)。エラーはすべて `BuildError` に集約して報告
- 対話モードで利用者の未保存 Scene が失われる — `SaveCurrentModifiedScenesIfUserWantsTo()` を事前に呼び、キャンセル時は `SceneBuildCanceled` で中断。batchmode では確認なしで続行(CI 用途)
- 予約行種別キーと将来のトラック種別キーの衝突 — `TrackBuilderRegistry.Register` で予約キーを拒否し、単体テストで担保
- テンプレート・列定義ドキュメント・パーサーの三者不一致 — 既存方針を踏襲し、同梱テンプレートをそのまま入力にする E2E テストで自動担保(Req 7.3)
- EditMode テストが Scene 状態を変更し他テストへ波及 — Scene を使うテストは SetUp で自前の空 Scene を作る規約とし、既存テストとの独立性を保つ

## References
- [EditorSceneManager.NewScene](https://docs.unity3d.com/ScriptReference/SceneManagement.EditorSceneManager.NewScene.html) — 新規 Scene 作成とモードの挙動
- [EditorSceneManager.SaveScene](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/SceneManagement.EditorSceneManager.SaveScene.html) — 明示パス保存・上書き挙動
- [PlayableDirector.SetGenericBinding](https://docs.unity3d.com/ScriptReference/Playables.PlayableDirector.SetGenericBinding.html) — バインディングの設定とシリアライズ先
- [PlayableDirector.GetGenericBinding](https://docs.unity3d.com/ScriptReference/Playables.PlayableDirector.GetGenericBinding.html) — 永続化検証に使用
- [Assign Animator to AnimationTrack (Unity Discussions)](https://discussions.unity.com/t/assign-animator-component-to-an-animationtrack-in-script/789180) — AnimationTrack へ Animator を渡す実例
- `.kiro/specs/timeline-prefab-builder/design.md` — 既存アーキテクチャ(パイプライン + Strategy レジストリ、2 フェーズコミット)の設計根拠
