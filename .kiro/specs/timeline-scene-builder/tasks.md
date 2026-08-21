# Implementation Plan

- [ ] 1. 基盤: Scene 構築情報モデルと結果契約の拡張
- [x] 1.1 (P) Scene 構築情報の型付き不変モデルを定義する
  - Scene 定義・Prefab 配置・バインド指定の各行を行番号付きの不変モデルとして表現し、行出現順を保持する Scene 構築計画として集約できるようにする
  - Scene 定義では Timeline 参照の「空欄 = 同一ビルド生成の TimelineAsset」を既存アセット明示参照と区別できる形で保持する
  - バインド指定は Track 名とバインド対象 GameObject 名の対応として保持する
  - 完了条件: パース層・Building 層から参照可能な Scene 構築計画モデルがコンパイル可能な状態で存在する
  - _Requirements: 1.1, 1.3, 3.2_
  - _Boundary: SceneRows_

- [x] 1.2 (P) 構築結果とエラーコードの契約を拡張する
  - 構築結果に生成 Scene のアセットパスを追加し、既存 4 引数コンストラクタは Scene パス null への委譲で従来どおり動作させる
  - エラーコード列挙に Scene 系コード(Timeline 解決不能・Prefab 不正・バインド系 5 種・保存失敗・キャンセル)を既存 10 値の順序・値を維持したまま末尾に追加する
  - 完了条件: 既存 4 引数コンストラクタの互換動作と 5 引数版の値保持を確認する EditMode 単体テストが成功する
  - _Requirements: 5.2, 5.4_
  - _Boundary: BuildResult, BuildErrorCode_

- [ ] 2. パーサー拡張: Scene 行の認識と検証
- [x] 2.1 Scene 行のルーティングとパースを実装する
  - 行種別キー(大文字小文字無視・trim 後比較)で Scene 系 3 行種を識別し、それ以外は既存のクリップ行パースへ委譲する。ヘッダー判定・列マッピング・固定 7 列フォールバックの既存ロジックは変更しない
  - Scene 系行を型付きモデルへ変換し、パース結果に Scene 構築計画として集約する(Scene 行なしの入力では計画は null で既存出力と完全一致)
  - 未使用列の値は警告なしで無視し、クリップ行が無く Scene 行のみの入力も許容する
  - 完了条件: Scene 行入り CSV/TSV のパースで Scene 構築計画が得られ、Scene 行がクリップ行の結果に混入しない
  - _Requirements: 1.1, 1.2, 1.4, 1.5_

- [x] 2.2 Scene 行のバリデーションを実装する
  - Scene 行の重複(2 行以上)、Scene 行なしでの Prefab / バインド行、行種別ごとの必須列欠落、ファイル名として不正な Scene 名、同一 Track 名への重複バインド指定を検出する
  - 全件を行番号付きの検証エラーとして収集し、既存の Phase A 一括報告に合流させる
  - 完了条件: 各不正入力で行番号と原因を特定できるエラーが返り、構築処理が中断される
  - _Requirements: 1.6_

- [x] 2.3 パーサー単体テストを追加する
  - ヘッダーあり / ヘッダーレス固定 7 列の両モードでの Scene 行集約と、クリップ行への非混入を検証する
  - 検証エラー系(重複 Scene 行・孤立 Prefab/バインド行・必須欠落・不正 Scene 名・重複バインド)の行番号付きエラーを検証する
  - Scene 行なしの既存 CSV で Scene 構築計画が null かつ既存パース結果と完全一致すること(後方互換)を検証する
  - 完了条件: 上記を網羅する EditMode 単体テストが全て成功する
  - _Requirements: 1.1, 1.2, 1.4, 1.5, 1.6_

- [ ] 3. Building 層: バインディング適用と Scene 生成
- [x] 3.1 (P) AnimationTrack バインディング適用コンポーネントを実装する
  - Scene の全階層(非アクティブ GameObject を含み、本ツールが生成した Director 用オブジェクトのルートは除外)から GameObject 名を Ordinal 完全一致で解決する
  - Track 名に一致する AnimationTrack の不在・複数一致、GameObject の不在・同名重複、Animator コンポーネント欠落を、行番号・対象名付きエラーとして全バインド行分収集する(最初の失敗で打ち切らない)
  - バインド指定のない Track のバインディング状態には一切触れない
  - 完了条件: 全バインド指定の検証・適用を行い、空のエラー一覧(成功)または全エラー一覧を返す
  - _Requirements: 4.1, 4.3, 4.4, 4.5, 4.6, 4.7_
  - _Depends: 1.1_
  - _Boundary: TrackBindingApplier_

- [x] 3.2 Scene 生成・配置・保存コンポーネントを実装する
  - 対話モードでは処理前に未保存 Scene の保存確認を行い(キャンセル時は何も変更せず中断)、batchmode では確認なしで空 Scene(EmptyScene / Single)を作成する
  - Scene 直下に Director 用 GameObject(名前 = TimelineAsset のアセット名)を配置して TimelineAsset を割り当て、Prefab を行出現順に Prefab インスタンスとして配置する(Transform・インスタンス名は既定値のまま)
  - バインディング適用でエラーが 1 件以上返された場合は保存せずに失敗を返し、全成功時のみ MarkSceneDirty → SaveScene を 1 回だけ実行する。既存 .unity がある場合は保存前に上書きログを出力し GUID を維持する
  - 完了条件: 成功時に指定パスへ .unity が保存され、失敗時は上書き対象の既存ファイルが実行前の状態のまま残る
  - 3.1 のバインディング適用コンポーネントを呼び出すため、3.1 完了までは並列実行不可
  - _Requirements: 2.1, 2.2, 2.5, 3.1, 3.2, 4.2_
  - _Depends: 3.1_

- [x] 4. (P) TrackBuilderRegistry に予約行種別キーの登録拒否ガードを追加する
  - Scene 系予約キーと衝突するカスタム TrackBuilder の登録を拒否する
  - 完了条件: 予約キー(大文字小文字無視)での登録が拒否されることを確認する単体テストが成功する
  - _Requirements: 1.2_
  - _Boundary: TrackBuilderRegistry_

- [ ] 5. API 統合: パイプラインへの Scene フェーズ組み込み
- [x] 5.1 Phase A の Scene 静的検証を実装する
  - Timeline 参照の解決: 空欄は同一ビルド生成の TimelineAsset、Assets/ 始まりは既存アセットとして解決し、不在・型不一致・それ以外の値はエラーとして収集する
  - Prefab 行を Assets/ 配下のアセットパスとして解決し、Assets/ 外・不在・型不一致は行番号・参照パス付きエラーとして収集する
  - Track 名の事前検証: 標準構成(組み込み Animation キーのみ)ではクリップ行の Track 名集合と Ordinal 照合し、カスタム TrackBuilder 構成では Phase B の実 TimelineAsset 照合に委ねる。既存 Timeline 明示参照時は実トラック名と照合する
  - Scene 出力パスが TimelineAsset / Prefab の出力パスと衝突しないことを確認する
  - 完了条件: 解決不能な指定を含む入力で Phase B(Timeline 生成含む)に進まず、全エラーが一括報告される
  - _Requirements: 2.3, 2.4, 3.3, 3.4, 4.7_
  - _Depends: 2.2_

- [x] 5.2 Phase B への Scene 生成統合と結果返却を実装する
  - TimelineAsset・Prefab 生成後に検証済み Scene 構築コンテキストを組み立てて Scene 生成を実行し、失敗時は「Timeline / Prefab は生成済み、Scene は未生成」であることをログで明示して失敗結果を返す
  - 成功時は Scene パスを含む構築結果を返却し、Scene パスを Info ログに出力する
  - 公開メソッドのシグネチャ・引数検証は無変更とし、Scene 行なし入力では処理経路・成果物・ログ・返却値(Scene パス null)が従来と完全に一致する
  - 完了条件: Scene 行入り CSV での公開 API 呼び出しが Scene パス入りの成功結果を返す
  - _Requirements: 1.5, 2.3, 5.1, 5.2, 5.3, 5.4_
  - _Depends: 3.2, 5.1_

- [x] 6. CLI 拡張: 成功時に Scene パスをログ出力する
  - 引数仕様・exit code 体系(0/1/2)・エラーログフォーマットは無変更とし、Scene 系エラーコードも既存フォーマットでそのまま出力されることを確認する
  - 完了条件: Scene 行入り CSV の成功実行で、既存の TimelineAsset / Prefab パスログに続けて Scene パスが出力される
  - _Requirements: 6.1, 6.2, 6.3, 6.4_
  - _Depends: 5.2_

- [x] 7. (P) CSV テンプレートと列定義ドキュメントを Scene 行仕様に同期する
  - 同梱 CSV テンプレートに Scene / ScenePrefab / SceneBind 各行のサンプルを追加する
  - 列定義ドキュメントに行種別ごとの列対応表(名称・意味・データ型・必須/任意・記入例)、Timeline 参照規約(空欄 = 同一ビルド)、GameObject 名探索規約(非アクティブ含む・Ordinal 完全一致・重複エラー)を追記する
  - 完了条件: 設計の Scene 行列仕様表とテンプレート・列定義の記載が一致し、8.3 の E2E テストの入力として使用できる
  - _Requirements: 7.1, 7.2, 7.3_
  - _Boundary: Documentation~_

- [ ] 8. 検証: 統合テストと E2E
- [x] 8.1 (P) Scene 生成・保存・永続化の統合テストを追加する
  - Director GameObject の存在、playableAsset が生成 TimelineAsset を参照、Prefab インスタンス判定が真、複数 Prefab の全配置を検証する
  - 保存後に Scene を再オープンしてバインディングが保持され、バインド指定のない Track が未設定のままであることを検証する
  - 同一出力先への 2 回実行で上書きログ出力と .unity の GUID 維持、バインド失敗時に .unity が保存されないことを検証する
  - 完了条件: 実 AssetDatabase・EditorSceneManager を使った EditMode 統合テストが全て成功する
  - _Requirements: 2.1, 2.2, 2.3, 2.5, 3.1, 3.2, 4.2, 4.3_
  - _Boundary: SceneFactoryTests_

- [x] 8.2 (P) バインディング適用・エラー系の統合テストを追加する
  - バインド適用成功時に対象 AnimationTrack のバインディングが指定 GameObject の Animator を返すことを検証する
  - GameObject 不在・同名重複(非アクティブ含む)・Animator 欠落・Track 名不一致・既存 Timeline 内の同名 AnimationTrack 重複で各エラーコードが返ることを検証する
  - 複数の不備が 1 回の実行で全件報告されることを検証する
  - 完了条件: 各エラーコードと全件収集を網羅する EditMode 統合テストが全て成功する
  - _Requirements: 4.1, 4.4, 4.5, 4.6, 4.7_
  - _Boundary: TrackBindingApplierTests_

- [x] 8.3 (P) E2E テスト(CSV → .unity・テンプレート・後方互換)を追加する
  - 更新後の同梱テンプレート(+ フィクスチャ Prefab)をそのまま入力とし、Scene 構築込みで成功することでドキュメントとパーサー仕様の一致を自動担保する
  - 既存フォーマットのみの CSV で従来成果物が生成され、Scene パスが null であること(後方互換)を検証する
  - Prefab パス不在 / Assets/ 外・既存 Timeline パス不在で Phase A エラーとなり Timeline 生成にも進まないことを検証する
  - 完了条件: CSV 入力から .unity 生成までの E2E テストが全て成功する
  - _Requirements: 1.5, 2.4, 3.4, 7.1, 7.2, 7.3_
  - _Depends: 7_
  - _Boundary: SceneBuilderIntegrationTests_

- [x]* 8.4 CLI バッチ実行の受け入れ検証スクリプトを整備する
  - -batchmode / -executeMethod で Scene 行入り CSV を実行し、Scene パスログと exit code 0(受け入れ基準 6.1–6.3)を確認する
  - 失敗系入力で原因を特定できるログと exit code 1(受け入れ基準 6.4)を確認する
  - CLI 実装自体は既存テストとタスク 6 で担保されるため、実プロセス起動での確認は MVP 後に実施可能な補助的検証として延期可
  - 完了条件: スクリプト実行で成功・失敗両系の exit code とログが確認できる
  - _Requirements: 6.1, 6.2, 6.3, 6.4_
  - _Depends: 6_
